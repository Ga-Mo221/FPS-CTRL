using System;
using System.Collections;
using Backend.Interfaces;
using Backend.Models;
using UnityEngine;
using PlayFab;
using PlayFab.MultiplayerModels;

namespace Backend.PlayfabImpl
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  QUY ƯỚC CHIỀU DỮ LIỆU:
    //   ↑ GỬI LÊN  = tạo/hủy vé tìm trận trên PlayFab (Create/Cancel...Ticket)
    //   ↓ LẤY VỀ   = hỏi trạng thái vé/ trận (Get...Ticket) để cập nhật UI
    //
    //  Vòng đời 1 lần tìm trận:
    //   ↑ CreateMatchmakingTicket  (vào hàng chờ)
    //   ↓ GetMatchmakingTicket * N (poll mỗi ~6s tới khi Matched / Canceled)
    //   ↑ CancelMatchmakingTicket  (khi người chơi bấm hủy)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ghép trận bằng PlayFab Matchmaking. Thuần PlayFab, không đụng Fusion.
    /// </summary>
    public class PlayFabMatchmakingService : IMatchmakingService
    {
        private readonly ICoroutineRunner _runner;
        private readonly MatchmakingConfig _config;

        private string _ticketId;
        private string _queueName;
        private Coroutine _pollRoutine;
        private float _startTime;
        private bool _userCancelled;

        public bool IsSearching { get; private set; }

        public PlayFabMatchmakingService(ICoroutineRunner runner, MatchmakingConfig config)
        {
            _runner = runner;
            _config = config ?? new MatchmakingConfig();
        }

        public void StartMatchmaking(
            MatchmakingRequest request,
            Action<MatchFoundResult> onMatchFound,
            Action<MatchmakingProgress> onProgress,
            Action<AuthError> onError,
            Action onTimedOut,
            Action onCancelled)
        {
            if (IsSearching)
            {
                onError?.Invoke(new AuthError { ErrorCode = "AlreadySearching", ErrorMessage = "Đang tìm trận rồi." });
                return;
            }

            // Bắt buộc đã đăng nhập (matchmaking dùng Entity token của player).
            if (!PlayFabSettings.staticPlayer.IsEntityLoggedIn())
            {
                onError?.Invoke(new AuthError { ErrorCode = "NotLoggedIn", ErrorMessage = "Cần đăng nhập (có Entity token) trước khi tìm trận." });
                return;
            }

            _userCancelled = false;
            _ticketId = null; // [MM-BACKFILL] khong de ma ve cu lot vao lan huy sau
            _queueName = _config.QueueFor(request.Mode);   // chọn queue theo mode (FFA/TDM)
            IsSearching = true;

            // Entity của player (lấy từ phiên đã đăng nhập) để đưa vào vé.
            var entity = new EntityKey
            {
                Id = PlayFabSettings.staticPlayer.EntityId,
                Type = PlayFabSettings.staticPlayer.EntityType
            };

            // [UI-INTEGRATION 2026-09-13] nhet ten + avatar vao ticket -> lobby (GetMatch) hien thong tin nguoi choi that.
            string myName = "Player";
            string myAvatar = "";
            var _prof = Backend.Bootstrap.ProfileCache.Cached;
            if (_prof != null)
            {
                if (!string.IsNullOrEmpty(_prof.DisplayName)) myName = _prof.DisplayName;
                if (!string.IsNullOrEmpty(_prof.AvatarUrl)) myAvatar = _prof.AvatarUrl;
            }

            var createReq = new CreateMatchmakingTicketRequest
            {
                Creator = new MatchmakingPlayer
                {
                    Entity = entity,
                    Attributes = new MatchmakingPlayerAttributes { DataObject = new { name = myName, avatar = myAvatar } } // [UI-INTEGRATION]
                },
                GiveUpAfterSeconds = _config.GiveUpAfterSeconds,   // hết giờ này -> PlayFab tự hủy vé
                QueueName = _queueName
            };

            // ↑ GỬI LÊN: tạo vé, vào hàng chờ. Trả VỀ TicketId để poll.
            PlayFabMultiplayerAPI.CreateMatchmakingTicket(
                createReq,
                result =>
                {
                    // Nguoi choi da huy (vd chuyen sang vao tran dang dien ra) TRUOC khi
                    // ve tao xong -> huy luon ve nay, tranh ve "ma" bi ghep voi nguoi khac roi khong ai vao.
                    if (!IsSearching)
                    {
                        PlayFabMultiplayerAPI.CancelMatchmakingTicket(
                            new CancelMatchmakingTicketRequest { QueueName = _queueName, TicketId = result.TicketId },
                            _ => Debug.Log("<color=#22d3ee>[Matchmaking]</color> Huy ve vua tao (nguoi choi da huy)."),
                            e => Debug.LogWarning("<color=#22d3ee>[Matchmaking]</color> Huy ve vua tao loi: " + e.ErrorMessage));
                        return;
                    }
                    _ticketId = result.TicketId;               // ↓ nhận VỀ mã vé
                    _startTime = Time.realtimeSinceStartup;
                    Debug.Log($"<color=#22d3ee>[Matchmaking]</color> Ticket tạo xong: {_ticketId} (queue {_queueName})");
                    _pollRoutine = _runner.Run(PollRoutine(request.Mode, onMatchFound, onProgress, onError, onTimedOut, onCancelled));
                },
                error =>
                {
                    IsSearching = false;
                    onError?.Invoke(MapError(error));
                });
        }

        // Vòng lặp ↓ LẤY VỀ trạng thái vé mỗi PollIntervalSeconds.
        private IEnumerator PollRoutine(
            MatchMode mode,
            Action<MatchFoundResult> onMatchFound,
            Action<MatchmakingProgress> onProgress,
            Action<AuthError> onError,
            Action onTimedOut,
            Action onCancelled)
        {
            while (IsSearching)
            {
                yield return new WaitForSeconds(_config.PollIntervalSeconds);   // chờ tới nhịp poll
                if (!IsSearching) yield break;

                bool callbackDone = false;

                // ↓ LẤY VỀ: hỏi trạng thái vé hiện tại.
                PlayFabMultiplayerAPI.GetMatchmakingTicket(
                    new GetMatchmakingTicketRequest { QueueName = _queueName, TicketId = _ticketId },
                    res =>
                    {
                        float elapsed = Time.realtimeSinceStartup - _startTime;
                        onProgress?.Invoke(new MatchmakingProgress   // đẩy tiến trình cho UI
                        {
                            ElapsedSeconds = elapsed,
                            MaxSeconds = _config.GiveUpAfterSeconds,
                            Status = res.Status
                        });

                        switch (res.Status)
                        {
                            case "Matched":   // ↓ đã ghép được -> có MatchId (dùng làm SessionName Fusion)
                                IsSearching = false;
                                Debug.Log($"<color=#22d3ee>[Matchmaking]</color> MATCHED! MatchId={res.MatchId}");
                                onMatchFound?.Invoke(new MatchFoundResult
                                {
                                    MatchId = res.MatchId,
                                    Mode = mode,
                                    QueueName = _queueName
                                });
                                break;

                            case "Canceled":  // hết giveUp (2 phút) hoặc do người dùng hủy
                                IsSearching = false;
                                if (_userCancelled) onCancelled?.Invoke();
                                else onTimedOut?.Invoke();
                                break;

                            // "WaitingForPlayers" / "WaitingForMatch" -> tiếp tục poll vòng sau.
                        }

                        callbackDone = true;
                    },
                    err =>
                    {
                        IsSearching = false;
                        onError?.Invoke(MapError(err));
                        callbackDone = true;
                    });

                // Chờ callback xử lý xong mới sang vòng poll kế.
                while (!callbackDone) yield return null;
                if (!IsSearching) yield break;
            }
        }

        // ↑ GỬI LÊN: hủy vé tìm trận.
        public void CancelMatchmaking(Action onDone = null)
        {
            if (!IsSearching)
            {
                onDone?.Invoke();
                return;
            }

            _userCancelled = true;    // đánh dấu để phân biệt với hết-giờ
            IsSearching = false;
            if (_pollRoutine != null) _runner.Stop(_pollRoutine);

            if (string.IsNullOrEmpty(_ticketId) || string.IsNullOrEmpty(_queueName))
            {
                onDone?.Invoke();
                return;
            }

            // ↑ GỬI LÊN: báo PlayFab hủy vé.
            PlayFabMultiplayerAPI.CancelMatchmakingTicket(
                new CancelMatchmakingTicketRequest { QueueName = _queueName, TicketId = _ticketId },
                res =>
                {
                    Debug.Log("<color=#22d3ee>[Matchmaking]</color> Đã hủy ticket.");
                    onDone?.Invoke();
                },
                err =>
                {
                    Debug.LogWarning($"<color=#22d3ee>[Matchmaking]</color> Hủy ticket lỗi (bỏ qua): {err.ErrorMessage}");
                    onDone?.Invoke();
                });
        }

        // [UI-INTEGRATION 2026-09-13] Lay danh sach nguoi choi da ghep (ten/avatar/doi) tu PlayFab GetMatch -> hien lobby.
        public void GetMatchMembers(string matchId, string queueName,
            System.Action<System.Collections.Generic.List<Backend.Models.LobbyMember>> onDone,
            System.Action<AuthError> onError)
        {
            PlayFabMultiplayerAPI.GetMatch(
                new GetMatchRequest { MatchId = matchId, QueueName = queueName, ReturnMemberAttributes = true },
                res =>
                {
                    var list = new System.Collections.Generic.List<Backend.Models.LobbyMember>();
                    string myId = PlayFabSettings.staticPlayer.EntityId;
                    if (res.Members != null)
                    {
                        foreach (var m in res.Members)
                        {
                            var lm = new Backend.Models.LobbyMember();
                            lm.TeamId = m.TeamId;
                            lm.IsLocal = (m.Entity != null && m.Entity.Id == myId);
                            string nm, av; ParseNameAvatar(m.Attributes, out nm, out av);
                            lm.Name = nm; lm.AvatarUrl = av;
                            list.Add(lm);
                        }
                    }
                    onDone?.Invoke(list);
                },
                err => onError?.Invoke(MapError(err)));
        }

        // Doc name/avatar tu DataObject (JsonObject implements IDictionary) cua member attributes.
        private static void ParseNameAvatar(MatchmakingPlayerAttributes attr, out string name, out string avatar)
        {
            name = "Player"; avatar = "";
            if (attr == null || attr.DataObject == null) return;
            var dict = attr.DataObject as System.Collections.Generic.IDictionary<string, object>;
            if (dict == null) return;
            object v;
            if (dict.TryGetValue("name", out v) && v != null && !string.IsNullOrEmpty(v.ToString())) name = v.ToString();
            if (dict.TryGetValue("avatar", out v) && v != null) avatar = v.ToString();
        }

        private static AuthError MapError(PlayFabError error) => new AuthError
        {
            ErrorCode = error.Error.ToString(),
            ErrorMessage = error.ErrorMessage
        };
    }
}
