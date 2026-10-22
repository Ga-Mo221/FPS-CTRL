using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Backend.Models;
using Backend.Bootstrap;

namespace Backend.UI
{
    /// <summary>
    /// UI test ghép trận (Phase 2) + cơ chế KHÓA THEO VERIFIED trước khi vào trận.
    /// </summary>
    public class MatchmakingTestUI : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button findFfaButton;
        [SerializeField] private Button findTdmButton;
        [SerializeField] private Button cancelButton;

        [Header("Gate")]
        [Tooltip("Bật: bắt buộc email Confirmed mới được vào trận.")]
        [SerializeField] private bool requireVerifiedEmail = true;
        [Tooltip("Bật: khách (chưa có email) vẫn được vào trận (warm-up).")]
        [SerializeField] private bool allowGuestWithoutEmail = true;

        [Header("Output")]
        [SerializeField] private TMP_Text statusText;

        private void Start()
        {
            findFfaButton?.onClick.AddListener(() => FindMatch(MatchMode.FFA));
            findTdmButton?.onClick.AddListener(() => FindMatch(MatchMode.TDM));
            cancelButton?.onClick.AddListener(OnCancel);
            SetStatus("San sang. Bam FFA hoac TDM de tim tran.");
        }

        private void FindMatch(MatchMode mode)
        {
            if (!BackendBootstrap.AuthService.IsLoggedIn)
            {
                SetStatus("Chua login -> dang login khach...");
                BackendBootstrap.AuthService.LoginAsGuest(
                    null,
                    _ => Gate(mode),
                    err => SetStatus($"<color=#f87171>[LOI login] {err.ErrorCode}</color>\n{err.ErrorMessage}"));
            }
            else Gate(mode);
        }

        // Cửa khóa: kiểm tra verified trước khi cho tìm trận.
        private void Gate(MatchMode mode)
        {
            if (!requireVerifiedEmail) { StartSearch(mode); return; }

            SetStatus("Dang kiem tra trang thai xac thuc email...");
            BackendBootstrap.EmailVerificationStatusService.GetEmailVerificationStatus(
                status =>
                {
                    bool allowed = status == EmailVerifyStatus.Confirmed
                                   || (allowGuestWithoutEmail && status == EmailVerifyStatus.NoContactEmail);
                    if (allowed) StartSearch(mode);
                    else SetStatus($"<color=#fbbf24>[BI KHOA] Email chua xac thuc (trang thai {status}).\nHay xac thuc email truoc khi vao tran.</color>");
                },
                err => SetStatus($"<color=#f87171>[LOI check verify] {err.ErrorMessage}</color>"));
        }

        private void StartSearch(MatchMode mode)
        {
            SetStatus($"Bat dau tim tran {mode}...");
            BackendBootstrap.MatchmakingService.StartMatchmaking(
                new MatchmakingRequest { Mode = mode },
                onMatchFound: match =>
                {
                    SetStatus($"<color=#4ade80>[MATCH FOUND] {match.Mode}</color>\nMatchId: {match.MatchId}");
                    BackendBootstrap.MatchHandoff.HandOff(match);
                },
                onProgress: p =>
                {
                    int e = Mathf.FloorToInt(p.ElapsedSeconds);
                    SetStatus($"Dang tim {mode}... {e/60:00}:{e%60:00} / {p.MaxSeconds}s\nTrang thai: {p.Status}");
                },
                onError: err => SetStatus($"<color=#f87171>[LOI] {err.ErrorCode}</color>\n{err.ErrorMessage}"),
                onTimedOut: () =>
                {
                    SetStatus($"<color=#fbbf24>[HET 2 PHUT] Ep bat dau {mode} (solo/warm-up).</color>");
                    BackendBootstrap.MatchHandoff.HandOffSolo(mode);
                },
                onCancelled: () => SetStatus("Da huy tim tran."));
        }

        private void OnCancel()
        {
            SetStatus("Dang huy...");
            BackendBootstrap.MatchmakingService.CancelMatchmaking(() => SetStatus("Da huy tim tran."));
        }

        private void SetStatus(string msg)
        {
            Debug.Log("[MatchmakingTestUI] " + msg);
            if (statusText != null) statusText.text = msg;
        }
    }
}
