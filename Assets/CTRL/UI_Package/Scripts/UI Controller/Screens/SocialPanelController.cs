using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Backend.Bootstrap;
using Backend.Models;

namespace Game.UI
{
    // [Aemy 2026-09-21] Panel ban be overlay trong MenuShell: list ban (online/away/offline) + them/xoa.
    // Mo tu nut '+' canh SAN SANG; dong bang Back hoac ESC. Data tu FriendsService (co presence).
    public class SocialPanelController : MonoBehaviour
    {
        [SerializeField] private GameObject _panelRoot;        // overlay bat/tat
        [SerializeField] private Button _backButton;
        [SerializeField] private TMP_InputField _addInput;
        [SerializeField] private Button _addButton;
        [SerializeField] private Transform _listContent;       // parent cac row (Vertical Layout)
        [SerializeField] private FriendRowUI _rowTemplate;      // template an de clone
        [SerializeField] private TMP_Text _onlineCountLabel;
        [SerializeField] private TMP_Text _statusLabel;         // 'Dang tai...' / trong
        [SerializeField] private bool _loadAvatars = true;
        [SerializeField] private PartyController _partyController; // [Aemy] moi ban vao party
        [SerializeField] private GameObject _invitePopup;   // [Aemy] popup loi moi
        [SerializeField] private TMP_Text _inviteText;
        [SerializeField] private UnityEngine.UI.Button _inviteAccept;
        [SerializeField] private UnityEngine.UI.Button _inviteDecline;
        private Backend.Models.PartyInvite _pendingInvite;

        // [Aemy 2026-09-24] Popup loi moi KET BAN den (khac popup PARTY o tren).
        [Header("Lời mời KẾT BẠN đến (Aemy)")]
        [SerializeField] private GameObject _friendReqPopup;
        [SerializeField] private TMP_Text _friendReqText;
        [SerializeField] private UnityEngine.UI.Button _friendReqAccept;
        [SerializeField] private UnityEngine.UI.Button _friendReqDecline;
        [SerializeField] private float _reqPollSeconds = 20f;
        private readonly List<FriendRequestInfo> _incomingReqs = new List<FriendRequestInfo>();
        private FriendRequestInfo _shownReq;
        private Coroutine _reqPollCo;

        private readonly List<FriendRowUI> _rows = new List<FriendRowUI>();

        private void Awake()
        {
            if (_panelRoot != null) _panelRoot.SetActive(false);
            if (_rowTemplate != null) _rowTemplate.gameObject.SetActive(false);
            if (_backButton != null) _backButton.onClick.AddListener(Close);
            if (_addButton != null) _addButton.onClick.AddListener(OnAddClicked);
            if (_invitePopup != null) _invitePopup.SetActive(false);
            if (_partyController != null) _partyController.OnInviteReceived += ShowInvite;
            if (_inviteAccept != null) _inviteAccept.onClick.AddListener(AcceptInvite);
            if (_inviteDecline != null) _inviteDecline.onClick.AddListener(DeclineInvite);
            // [Aemy] popup loi moi ket ban
            if (_friendReqPopup != null) _friendReqPopup.SetActive(false);
            if (_friendReqAccept != null) _friendReqAccept.onClick.AddListener(AcceptFriendReq);
            if (_friendReqDecline != null) _friendReqDecline.onClick.AddListener(DeclineFriendReq);
        }

        private void OnDestroy() { if (_partyController != null) _partyController.OnInviteReceived -= ShowInvite; }

        private void ShowInvite(Backend.Models.PartyInvite inv)
        {
            _pendingInvite = inv;
            if (_inviteText != null) _inviteText.text = (string.IsNullOrEmpty(inv.FromName) ? "Một người" : inv.FromName) + " mời bạn vào party";
            if (_invitePopup != null) _invitePopup.SetActive(true);
        }
        private void AcceptInvite()
        {
            if (_partyController != null && _pendingInvite != null) _partyController.AcceptInvite(_pendingInvite.Code);
            if (_invitePopup != null) _invitePopup.SetActive(false);
        }
        private void DeclineInvite()
        {
            if (_partyController != null) _partyController.DeclineInvite();
            if (_invitePopup != null) _invitePopup.SetActive(false);
        }

        private void Update()
        {
            if (_panelRoot != null && _panelRoot.activeSelf
                && UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) // [Aemy 2026-10-01] Input System
                Close();
        }

        public void Toggle() { if (_panelRoot != null && _panelRoot.activeSelf) Close(); else Open(); }

        public void Open()
        {
            if (_panelRoot != null) _panelRoot.SetActive(true);
            var svc = BackendBootstrap.FriendsService;
            if (svc != null) svc.IndexMyName();   // [Aemy] dang ky ten minh de nguoi khac tim duoc
            Refresh();
            RefreshRequests();                    // [Aemy] keo loi moi ket ban den
            if (_reqPollCo == null) _reqPollCo = StartCoroutine(RequestPollLoop());
        }

        public void Close()
        {
            if (_panelRoot != null) _panelRoot.SetActive(false);
            if (_reqPollCo != null) { StopCoroutine(_reqPollCo); _reqPollCo = null; }
        }

        public void Refresh()
        {
            var svc = BackendBootstrap.FriendsService;
            if (svc == null) { Populate(null); return; }
            if (_statusLabel != null) _statusLabel.text = "Đang tải...";
            svc.GetFriendsWithPresence(Populate,
                err => { if (_statusLabel != null) _statusLabel.text = "Lỗi: " + err.ErrorMessage; });
        }

        private void Populate(List<FriendInfo> list)
        {
            UnityEngine.Debug.Log("[SocialPanel-DIAG] Populate friend count=" + (list != null ? list.Count.ToString() : "null")); // [2026-09-28 Aemy]
            for (int i = 0; i < _rows.Count; i++) if (_rows[i] != null) Destroy(_rows[i].gameObject);
            _rows.Clear();

            if (list == null)
            {
                if (_statusLabel != null) _statusLabel.text = "Chưa có bạn bè";
                if (_onlineCountLabel != null) _onlineCountLabel.text = "0 đang online";
                return;
            }

            list.Sort((a, b) =>
            {
                int c = ((int)b.Presence).CompareTo((int)a.Presence);
                if (c != 0) return c;
                return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.OrdinalIgnoreCase);
            });

            int online = 0;
            foreach (var fr in list)
            {
                if (fr.Presence == FriendPresence.Online) online++;
                if (_rowTemplate == null || _listContent == null) continue;
                var rowGo = Instantiate(_rowTemplate, _listContent);
                rowGo.gameObject.SetActive(true);
                rowGo.Set(fr, OnInvite, OnRemove);
                if (_loadAvatars && !string.IsNullOrEmpty(fr.AvatarUrl)) StartCoroutine(LoadAvatar(fr.AvatarUrl, rowGo));
                _rows.Add(rowGo);
            }
            if (_onlineCountLabel != null) _onlineCountLabel.text = online + " đang online";
            if (_statusLabel != null) _statusLabel.text = list.Count == 0 ? "Chưa có bạn bè" : "";
        }

        private IEnumerator LoadAvatar(string url, FriendRowUI row)
        {
            using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success) yield break;
                var tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req); if (tex != null) tex = Game.UI.ImageScale.Fit(tex, 64); // [#5] thu nho 64px
                if (tex == null || row == null) yield break;
                row.SetAvatar(Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f)));
            }
        }

        private void OnAddClicked()
        {
            if (_addInput == null) return;
            string q = _addInput.text != null ? _addInput.text.Trim() : "";
            if (string.IsNullOrEmpty(q)) return;
            var svc = BackendBootstrap.FriendsService;
            if (svc == null) return;
            // GUI LOI MOI (khong con them thang 1 chieu). Ben nhan se thay popup chap nhan.
            if (_statusLabel != null) _statusLabel.text = "Đang gửi lời mời...";
            svc.SendFriendRequest(q,
                msg => { _addInput.text = ""; if (_statusLabel != null) _statusLabel.text = string.IsNullOrEmpty(msg) ? "Đã gửi lời mời." : msg; },
                err => { if (_statusLabel != null) _statusLabel.text = "Lỗi: " + err.ErrorMessage; });
        }

        // ================= LOI MOI KET BAN DEN (request -> accept) =================
        // Keo danh sach loi moi den tu CloudScript, hien popup lan luot.
        public void RefreshRequests()
        {
            var svc = BackendBootstrap.FriendsService;
            if (svc == null) return;
            svc.GetIncomingRequests(reqs =>
            {
                _incomingReqs.Clear();
                if (reqs != null) _incomingReqs.AddRange(reqs);
                ShowNextRequest();
            },
            err => { /* im lang, khong pha UI list ban */ });
        }

        private IEnumerator RequestPollLoop()
        {
            var wait = new WaitForSeconds(_reqPollSeconds > 1f ? _reqPollSeconds : 20f);
            while (_panelRoot != null && _panelRoot.activeSelf)
            {
                yield return wait;
                RefreshRequests();
            }
            _reqPollCo = null;
        }

        private void ShowNextRequest()
        {
            if (_friendReqPopup == null) return;
            if (_incomingReqs.Count == 0) { _shownReq = null; _friendReqPopup.SetActive(false); return; }
            _shownReq = _incomingReqs[0];
            if (_friendReqText != null)
                _friendReqText.text = (string.IsNullOrEmpty(_shownReq.FromName) ? "Một người" : _shownReq.FromName) + " muốn kết bạn với bạn";
            _friendReqPopup.SetActive(true);
        }

        private void AcceptFriendReq()
        {
            var svc = BackendBootstrap.FriendsService;
            if (svc == null || _shownReq == null) return;
            string fromId = _shownReq.FromId;
            if (_incomingReqs.Count > 0) _incomingReqs.RemoveAt(0);
            svc.AcceptFriendRequest(fromId,
                () => Refresh(),
                err => { if (_statusLabel != null) _statusLabel.text = "Lỗi: " + err.ErrorMessage; });
            ShowNextRequest();
        }

        private void DeclineFriendReq()
        {
            var svc = BackendBootstrap.FriendsService;
            if (svc == null || _shownReq == null) return;
            string fromId = _shownReq.FromId;
            if (_incomingReqs.Count > 0) _incomingReqs.RemoveAt(0);
            svc.DeclineFriendRequest(fromId, null,
                err => { if (_statusLabel != null) _statusLabel.text = "Lỗi: " + err.ErrorMessage; });
            ShowNextRequest();
        }

        // [Aemy] Moi vao party (top bar) - noi party session o pha sau.
        private void OnInvite(FriendInfo fr)
        {
            if (_partyController != null && fr != null) { _partyController.InviteFriend(fr.PlayFabId); if (_statusLabel != null) _statusLabel.text = "Đã mời " + fr.DisplayName; }
            else Debug.Log("[SocialPanel] Moi " + (fr != null ? fr.DisplayName : "?") + " (chua gan PartyController).");
        }

        private void OnRemove(FriendInfo fr)
        {
            var svc = BackendBootstrap.FriendsService;
            if (svc == null || fr == null) return;
            svc.RemoveFriend(fr.PlayFabId, Refresh,
                err => { if (_statusLabel != null) _statusLabel.text = "Xoá bạn lỗi: " + err.ErrorMessage; });
        }
    }
}