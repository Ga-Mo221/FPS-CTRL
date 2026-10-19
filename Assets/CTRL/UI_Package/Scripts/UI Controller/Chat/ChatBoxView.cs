using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Backend.Models;

namespace Game.UI
{
    /// <summary>
    /// Chat box nho o goc duoi phai tab CHOI (duoi muc SUNG CHU LUC), thu gon duoc.
    /// Chi lo hien thi + nhap lieu; logic/poll nam o ChatController (SRP).
    ///  - Tab TO DOI: kenh chung cua to doi.
    ///  - Tab BAN BE: danh sach ban (cham online + so tin chua doc) -> bam 1 ban de nhan rieng, ◄ de quay lai.
    ///  - Thu gon: chi con thanh tab + so tin chua doc; bam tab bat ky de mo.
    /// Bo cuc duoc dung san trong scene bang BuildLayout() (chuot phai component > Build Layout) -> chinh tay duoc.
    /// </summary>
    public class ChatBoxView : MonoBehaviour
    {
        [Header("Logic")]
        [SerializeField] private ChatController _chat;

        [Header("Font")]
        [SerializeField] private TMP_FontAsset _headFont;
        [SerializeField] private TMP_FontAsset _bodyFont;

        [Header("Kich thuoc")]
        [SerializeField] private float _expandedHeight = 330f;
        [SerializeField] private float _headerHeight = 36f;

        [Header("Refs (BuildLayout tu gan)")]
        [SerializeField] private RectTransform _root;
        [SerializeField] private GameObject _body;
        [SerializeField] private Button _tabParty;
        [SerializeField] private Button _tabFriends;
        [SerializeField] private TMP_Text _tabPartyLabel;
        [SerializeField] private TMP_Text _tabFriendsLabel;
        [SerializeField] private GameObject _tabPartyLine;
        [SerializeField] private GameObject _tabFriendsLine;
        [SerializeField] private GameObject _badgeParty;
        [SerializeField] private TMP_Text _badgePartyText;
        [SerializeField] private GameObject _badgeFriends;
        [SerializeField] private TMP_Text _badgeFriendsText;
        [SerializeField] private Button _toggle;
        [SerializeField] private TMP_Text _toggleLabel;
        [SerializeField] private GameObject _convHeader;
        [SerializeField] private Button _convBack;
        [SerializeField] private TMP_Text _convTitle;
        [SerializeField] private ScrollRect _messageScroll;
        [SerializeField] private RectTransform _messageContent;
        [SerializeField] private ScrollRect _friendScroll;
        [SerializeField] private RectTransform _friendContent;
        [SerializeField] private TMP_Text _hint;
        [SerializeField] private GameObject _inputRow;
        [SerializeField] private TMP_InputField _input;
        [SerializeField] private Button _send;

        // mau theo theme menu
        private static readonly Color Teal = new Color32(0x4F, 0xD6, 0xC9, 0xFF);
        private static readonly Color Gold = new Color32(0xF2, 0xC1, 0x4E, 0xFF);
        private static readonly Color TextDim = new Color32(0x8A, 0x96, 0x9A, 0xFF);
        private static readonly Color PanelBg = new Color32(0x0C, 0x10, 0x12, 0xE6);
        private static readonly Color HeaderBg = new Color32(0x14, 0x1B, 0x1E, 0xFF);
        private static readonly Color RowBg = new Color32(0xFF, 0xFF, 0xFF, 0x0A);
        private static readonly Color BadgeBg = new Color32(0xFF, 0x4D, 0x4D, 0xFF);

        private const string PrefCollapsed = "chatbox_collapsed";
        private bool _collapsed;
        private bool _friendsTab;
        private string _openFriend;   // PlayFabId dang nhan rieng (null = dang xem danh sach)
        private bool _wired;

        private string CurrentKey
        {
            get
            {
                if (!_friendsTab) return ChatController.PartyKey;
                return string.IsNullOrEmpty(_openFriend) ? null : ChatController.DmKey(_openFriend);
            }
        }

        // ------------------------------------------------------------------ lifecycle
        private void Awake()
        {
            if (_chat == null) _chat = GetComponent<ChatController>();
            if (_root == null) _root = (RectTransform)transform;
            _collapsed = PlayerPrefs.GetInt(PrefCollapsed, 0) == 1;
            Wire();
        }

        private void OnEnable()
        {
            if (_chat != null)
            {
                _chat.ConversationChanged += OnConversationChanged;
                _chat.UnreadChanged += RefreshBadges;
                _chat.FriendsChanged += OnFriendsChanged;
                _chat.PartyChanged += OnPartyChanged;
            }
            ApplyState();
        }

        private void OnDisable()
        {
            if (_chat != null)
            {
                _chat.ConversationChanged -= OnConversationChanged;
                _chat.UnreadChanged -= RefreshBadges;
                _chat.FriendsChanged -= OnFriendsChanged;
                _chat.PartyChanged -= OnPartyChanged;
                _chat.SetVisible(null);
            }
        }

        private void Wire()
        {
            if (_wired || _tabParty == null) return;
            _wired = true;
            // Tat dieu huong UI (WASD/mui ten/Space) tren chat box: go chu "a", "d", Space trong o nhap
            // khong duoc nhay focus sang nut khac / bam nham tab.
            var none = new Navigation { mode = Navigation.Mode.None };
            foreach (var sel in GetComponentsInChildren<Selectable>(true)) sel.navigation = none;
            _tabParty.onClick.AddListener(() => SelectTab(false));
            _tabFriends.onClick.AddListener(() => SelectTab(true));
            _toggle.onClick.AddListener(() => SetCollapsed(!_collapsed));
            _convBack.onClick.AddListener(() => { _openFriend = null; ApplyState(); });
            _send.onClick.AddListener(SendCurrent);
            _input.onSubmit.AddListener(_ => SendCurrent());
        }

        // ------------------------------------------------------------------ trang thai
        private void SelectTab(bool friends)
        {
            if (_collapsed) SetCollapsed(false);
            if (_friendsTab == friends && friends) _openFriend = null; // bam lai tab BAN BE -> ve danh sach
            _friendsTab = friends;
            ApplyState();
        }

        private void SetCollapsed(bool c)
        {
            _collapsed = c;
            PlayerPrefs.SetInt(PrefCollapsed, c ? 1 : 0);
            ApplyState();
        }

        private void ApplyState()
        {
            if (_root == null || _body == null) return;
            _root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _collapsed ? _headerHeight : _expandedHeight);
            _body.SetActive(!_collapsed);
            if (_toggleLabel != null) _toggleLabel.text = _collapsed ? "▲" : "▼";

            _tabPartyLabel.color = !_friendsTab ? Teal : TextDim;
            _tabFriendsLabel.color = _friendsTab ? Teal : TextDim;
            _tabPartyLine.SetActive(!_collapsed && !_friendsTab);
            _tabFriendsLine.SetActive(!_collapsed && _friendsTab);

            bool listMode = _friendsTab && string.IsNullOrEmpty(_openFriend);
            _convHeader.SetActive(_friendsTab && !listMode);
            _friendScroll.gameObject.SetActive(listMode);
            _messageScroll.gameObject.SetActive(!listMode);
            // dang nhan rieng -> danh cho dong "◄ Ten ban" o tren
            var mrt = (RectTransform)_messageScroll.transform;
            mrt.offsetMax = new Vector2(mrt.offsetMax.x, _convHeader.activeSelf ? -((RectTransform)_convHeader.transform).sizeDelta.y : 0f);

            bool canType = _friendsTab ? !listMode : (_chat != null && _chat.PartyAvailable);
            _inputRow.SetActive(canType);
            if (_convTitle != null && !listMode && _friendsTab) _convTitle.text = _chat != null ? _chat.FriendName(_openFriend) : _openFriend;

            if (_chat != null) _chat.SetVisible(_collapsed ? null : CurrentKey);
            if (listMode) RebuildFriends(); else RebuildMessages();
            RefreshBadges();
        }

        // ------------------------------------------------------------------ su kien tu controller
        private void OnConversationChanged(string key)
        {
            if (!_collapsed && key == CurrentKey) RebuildMessages();
            else if (!_collapsed && _friendsTab && string.IsNullOrEmpty(_openFriend)) RebuildFriends(); // cap nhat badge tung ban
        }

        private void OnFriendsChanged()
        {
            if (!_collapsed && _friendsTab && string.IsNullOrEmpty(_openFriend)) RebuildFriends();
        }

        private void OnPartyChanged()
        {
            if (!_friendsTab) ApplyState();
        }

        private void RefreshBadges()
        {
            if (_chat == null) return;
            SetBadge(_badgeParty, _badgePartyText, _chat.UnreadOf(ChatController.PartyKey));
            SetBadge(_badgeFriends, _badgeFriendsText, _chat.UnreadDirect);
        }

        private static void SetBadge(GameObject badge, TMP_Text label, int n)
        {
            if (badge == null) return;
            badge.SetActive(n > 0);
            if (label != null) label.text = n > 9 ? "9+" : n.ToString();
        }

        // ------------------------------------------------------------------ gui tin
        private void SendCurrent()
        {
            string key = CurrentKey;
            if (_chat == null || string.IsNullOrEmpty(key) || _input == null) return;
            string text = _input.text;
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0) return;
            string err = _chat.Send(key, text);
            if (err != null) { ShowHint(err, true); return; }
            _input.text = "";
            _input.ActivateInputField(); // giu con tro de go tiep
        }

        // ------------------------------------------------------------------ ve danh sach
        private void RebuildMessages()
        {
            if (_messageContent == null) return;
            Clear(_messageContent);
            string key = CurrentKey;
            if (_chat == null || key == null) { ShowHint(null, false); return; }

            if (key == ChatController.PartyKey && !_chat.PartyAvailable)
            {
                ShowHint("Mời bạn vào tổ đội để chat cùng nhau.", false);
                _inputRow.SetActive(false);
                return;
            }
            var msgs = _chat.Messages(key);
            if (msgs.Count == 0) { ShowHint("Chưa có tin nhắn nào. Chào mọi người đi!", false); }
            else ShowHint(null, false);

            string self = _chat.SelfId;
            foreach (var m in msgs) MakeMessageRow(m, m.f == self);
            ScrollToBottom();
        }

        private void RebuildFriends()
        {
            if (_friendContent == null) return;
            Clear(_friendContent);
            if (_chat == null || _chat.Friends.Count == 0)
            {
                ShowHint("Chưa có bạn bè. Kết bạn ở mục BẠN BÈ phía trên nhé.", false);
                return;
            }
            ShowHint(null, false);
            foreach (var f in _chat.Friends) MakeFriendRow(f);
        }

        private void ShowHint(string text, bool warn)
        {
            if (_hint == null) return;
            bool show = !string.IsNullOrEmpty(text);
            _hint.gameObject.SetActive(show);
            if (show) { _hint.text = text; _hint.color = warn ? Gold : TextDim; }
        }

        private void ScrollToBottom()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_messageContent);
            if (_messageScroll != null) _messageScroll.verticalNormalizedPosition = 0f;
        }

        private static void Clear(RectTransform content)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var c = content.GetChild(i).gameObject;
                c.SetActive(false);
                Destroy(c);
            }
        }

        private void MakeMessageRow(ChatMessage m, bool mine)
        {
            var t = NewText("Msg", _messageContent, _bodyFont, 14f, Color.white, TextAlignmentOptions.TopLeft);
            t.enableWordWrapping = true;
            t.richText = true;
            string time = DateTimeOffset.FromUnixTimeMilliseconds(m.t).ToLocalTime().ToString("HH:mm");
            string nameCol = ColorUtility.ToHtmlStringRGB(mine ? Gold : Teal);
            t.text = "<color=#6E7B80><size=85%>" + time + "</size></color> <color=#" + nameCol + ">" + Safe(m.n) + "</color>: " + Safe(m.x);
        }

        private void MakeFriendRow(FriendInfo f)
        {
            var row = new GameObject("Friend", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(_friendContent, false);
            row.GetComponent<Image>().color = RowBg;
            row.GetComponent<LayoutElement>().preferredHeight = 32f;
            string id = f.PlayFabId;
            var rb = row.GetComponent<Button>();
            rb.navigation = new Navigation { mode = Navigation.Mode.None };
            rb.onClick.AddListener(() => { _openFriend = id; ApplyState(); });

            var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(row.transform, false);
            var drt = (RectTransform)dot.transform;
            drt.anchorMin = drt.anchorMax = new Vector2(0f, 0.5f); drt.pivot = new Vector2(0f, 0.5f);
            drt.anchoredPosition = new Vector2(10f, 0f); drt.sizeDelta = new Vector2(8f, 8f);
            dot.GetComponent<Image>().color = f.Presence == FriendPresence.Online ? new Color32(0x4D, 0xFF, 0x88, 0xFF)
                                             : f.Presence == FriendPresence.Away ? Gold : new Color32(0x55, 0x5F, 0x63, 0xFF);

            var name = NewText("Name", row.transform, _bodyFont, 15f, f.Presence == FriendPresence.Offline ? TextDim : Color.white, TextAlignmentOptions.MidlineLeft);
            Stretch((RectTransform)name.transform, 26f, 44f);
            name.enableWordWrapping = false; name.overflowMode = TextOverflowModes.Ellipsis;
            name.text = Safe(string.IsNullOrEmpty(f.DisplayName) ? f.PlayFabId : f.DisplayName);

            int unread = _chat.UnreadOf(ChatController.DmKey(id));
            if (unread > 0)
            {
                TMP_Text bt;
                var badge = NewBadge(row.transform, out bt);
                var brt = (RectTransform)badge.transform;
                brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f); brt.pivot = new Vector2(1f, 0.5f);
                brt.anchoredPosition = new Vector2(-10f, 0f);
                bt.text = unread > 9 ? "9+" : unread.ToString();
            }
        }

        /// <summary>Chan rich-text cua nguoi choi (khong cho chen the mau / size vao chat).</summary>
        private static string Safe(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("</noparse>", "").Replace("</NOPARSE>", "");
            return "<noparse>" + s + "</noparse>";
        }

        // ------------------------------------------------------------------ dung bo cuc (edit mode)
        private TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, float size, Color col, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size; t.color = col; t.alignment = align; t.raycastTarget = false;
            return t;
        }

        private GameObject NewBadge(Transform parent, out TMP_Text label)
        {
            var b = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            b.transform.SetParent(parent, false);
            ((RectTransform)b.transform).sizeDelta = new Vector2(20f, 18f);
            var img = b.GetComponent<Image>(); img.color = BadgeBg; img.raycastTarget = false;
            label = NewText("Count", b.transform, _headFont, 11f, Color.white, TextAlignmentOptions.Center);
            Stretch((RectTransform)label.transform, 0f, 0f);
            label.gameObject.AddComponent<LocIgnore>();
            return b;
        }

        private static void Stretch(RectTransform rt, float left, float right, float top = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
        }

        private Button NewTab(string name, Transform parent, string label, float x, float w, out TMP_Text text, out GameObject line, out GameObject badge, out TMP_Text badgeText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f); rt.sizeDelta = new Vector2(w, 0f);
            go.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            text = NewText("Label", go.transform, _headFont, 15f, TextDim, TextAlignmentOptions.MidlineLeft);
            Stretch((RectTransform)text.transform, 12f, 26f);
            text.text = label;
            line = new GameObject("Line", typeof(RectTransform), typeof(Image));
            line.transform.SetParent(go.transform, false);
            var lrt = (RectTransform)line.transform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 0f); lrt.pivot = new Vector2(0.5f, 0f);
            lrt.offsetMin = new Vector2(8f, 0f); lrt.offsetMax = new Vector2(-8f, 2f);
            line.GetComponent<Image>().color = Teal;
            badge = NewBadge(go.transform, out badgeText);
            var brt = (RectTransform)badge.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f); brt.pivot = new Vector2(1f, 0.5f);
            brt.anchoredPosition = new Vector2(-4f, 0f);
            badge.SetActive(false);
            return go.GetComponent<Button>();
        }

        private ScrollRect NewScroll(string name, Transform parent, float top, float bottom, out RectTransform content, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(ScrollRect));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, 0f, 0f, top, bottom);
            var vp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            vp.transform.SetParent(go.transform, false);
            Stretch((RectTransform)vp.transform, 0f, 0f);
            vp.GetComponent<Image>().color = new Color(0, 0, 0, 0); // nhan cuon chuot
            var c = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LocIgnore));
            c.transform.SetParent(vp.transform, false);
            content = (RectTransform)c.transform;
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            var vlg = c.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 6, 6); vlg.spacing = spacing;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            c.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = go.GetComponent<ScrollRect>();
            sr.viewport = (RectTransform)vp.transform; sr.content = content;
            sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 20f;
            return sr;
        }

        /// <summary>
        /// Dung bo cuc mac dinh (goi 1 lan trong edit mode roi save scene). Xoa con cu neu co.
        /// Kich thuoc rong 300; mo = 330 cao, thu gon = 36 (pivot day -> thanh tab nam o day khi thu gon).
        /// </summary>
        [ContextMenu("Build Layout")]
        public void BuildLayout()
        {
            for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject);
            _root = (RectTransform)transform;
            var bg = GetComponent<Image>(); if (bg == null) bg = gameObject.AddComponent<Image>();
            bg.color = PanelBg;
            if (_chat == null) _chat = GetComponent<ChatController>();

            // ---- header (thanh tab) - luon o tren cung
            var header = new GameObject("Header", typeof(RectTransform), typeof(Image));
            header.transform.SetParent(transform, false);
            var hrt = (RectTransform)header.transform;
            hrt.anchorMin = new Vector2(0f, 1f); hrt.anchorMax = new Vector2(1f, 1f); hrt.pivot = new Vector2(0.5f, 1f);
            hrt.anchoredPosition = Vector2.zero; hrt.sizeDelta = new Vector2(0f, _headerHeight);
            header.GetComponent<Image>().color = HeaderBg;
            _tabParty = NewTab("TabParty", header.transform, "TỔ ĐỘI", 0f, 122f, out _tabPartyLabel, out _tabPartyLine, out _badgeParty, out _badgePartyText);
            _tabFriends = NewTab("TabFriends", header.transform, "BẠN BÈ", 122f, 122f, out _tabFriendsLabel, out _tabFriendsLine, out _badgeFriends, out _badgeFriendsText);

            var tg = new GameObject("Toggle", typeof(RectTransform), typeof(Image), typeof(Button));
            tg.transform.SetParent(header.transform, false);
            var trt = (RectTransform)tg.transform;
            trt.anchorMin = new Vector2(1f, 0f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(1f, 0.5f);
            trt.anchoredPosition = Vector2.zero; trt.sizeDelta = new Vector2(_headerHeight, 0f);
            tg.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            _toggle = tg.GetComponent<Button>();
            _toggleLabel = NewText("Label", tg.transform, _headFont, 12f, TextDim, TextAlignmentOptions.Center);
            Stretch((RectTransform)_toggleLabel.transform, 0f, 0f);
            _toggleLabel.text = "▼";
            _toggleLabel.gameObject.AddComponent<LocIgnore>();

            // ---- body
            _body = new GameObject("Body", typeof(RectTransform));
            _body.transform.SetParent(transform, false);
            Stretch((RectTransform)_body.transform, 0f, 0f, _headerHeight, 0f);
            const float inputH = 38f, convH = 28f;

            // dong tieu de cuoc tro chuyen rieng: ◄  Ten ban
            _convHeader = new GameObject("ConvHeader", typeof(RectTransform), typeof(Image));
            _convHeader.transform.SetParent(_body.transform, false);
            var crt = (RectTransform)_convHeader.transform;
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f); crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = Vector2.zero; crt.sizeDelta = new Vector2(0f, convH);
            _convHeader.GetComponent<Image>().color = RowBg;
            var back = new GameObject("Back", typeof(RectTransform), typeof(Image), typeof(Button));
            back.transform.SetParent(_convHeader.transform, false);
            var brt = (RectTransform)back.transform;
            brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(0f, 1f); brt.pivot = new Vector2(0f, 0.5f);
            brt.anchoredPosition = Vector2.zero; brt.sizeDelta = new Vector2(32f, 0f);
            back.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            _convBack = back.GetComponent<Button>();
            var backLabel = NewText("Label", back.transform, _headFont, 12f, Teal, TextAlignmentOptions.Center);
            Stretch((RectTransform)backLabel.transform, 0f, 0f); backLabel.text = "◄";
            backLabel.gameObject.AddComponent<LocIgnore>();
            _convTitle = NewText("Title", _convHeader.transform, _headFont, 14f, Color.white, TextAlignmentOptions.MidlineLeft);
            Stretch((RectTransform)_convTitle.transform, 34f, 8f);
            _convTitle.enableWordWrapping = false; _convTitle.overflowMode = TextOverflowModes.Ellipsis;
            _convTitle.gameObject.AddComponent<LocIgnore>();

            _messageScroll = NewScroll("Messages", _body.transform, 0f, inputH, out _messageContent, 4f);
            _friendScroll = NewScroll("Friends", _body.transform, 0f, 0f, out _friendContent, 4f);

            _hint = NewText("Hint", _body.transform, _bodyFont, 13f, TextDim, TextAlignmentOptions.Center);
            Stretch((RectTransform)_hint.transform, 16f, 16f, 40f, inputH + 10f);
            _hint.enableWordWrapping = true;
            _hint.text = "Chưa có tin nhắn nào. Chào mọi người đi!";

            // ---- o nhap + nut GUI
            _inputRow = new GameObject("InputRow", typeof(RectTransform), typeof(Image));
            _inputRow.transform.SetParent(_body.transform, false);
            var irt = (RectTransform)_inputRow.transform;
            irt.anchorMin = new Vector2(0f, 0f); irt.anchorMax = new Vector2(1f, 0f); irt.pivot = new Vector2(0.5f, 0f);
            irt.anchoredPosition = Vector2.zero; irt.sizeDelta = new Vector2(0f, inputH);
            _inputRow.GetComponent<Image>().color = HeaderBg;

            var inGo = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            inGo.transform.SetParent(_inputRow.transform, false);
            Stretch((RectTransform)inGo.transform, 6f, 70f, 5f, 5f);
            inGo.GetComponent<Image>().color = new Color32(0x0F, 0x17, 0x1A, 0xFF);
            var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(inGo.transform, false);
            Stretch((RectTransform)area.transform, 8f, 8f, 2f, 2f);
            var ph = NewText("Placeholder", area.transform, _bodyFont, 14f, TextDim, TextAlignmentOptions.MidlineLeft);
            Stretch((RectTransform)ph.transform, 0f, 0f);
            ph.text = "Nhập tin nhắn…"; ph.enableWordWrapping = false; ph.fontStyle = FontStyles.Italic;
            var tx = NewText("Text", area.transform, _bodyFont, 14f, Color.white, TextAlignmentOptions.MidlineLeft);
            Stretch((RectTransform)tx.transform, 0f, 0f);
            tx.enableWordWrapping = false; tx.richText = false;
            _input = inGo.GetComponent<TMP_InputField>();
            _input.textViewport = (RectTransform)area.transform;
            _input.textComponent = tx;
            _input.placeholder = ph;
            if (_bodyFont != null) _input.fontAsset = _bodyFont;
            _input.pointSize = 14f;
            _input.characterLimit = ChatController.MaxTextLength;
            _input.lineType = TMP_InputField.LineType.SingleLine;
            _input.richText = false;
            _input.caretColor = Teal; _input.customCaretColor = true;

            var sendGo = new GameObject("Send", typeof(RectTransform), typeof(Image), typeof(Button));
            sendGo.transform.SetParent(_inputRow.transform, false);
            var srt = (RectTransform)sendGo.transform;
            srt.anchorMin = new Vector2(1f, 0f); srt.anchorMax = new Vector2(1f, 1f); srt.pivot = new Vector2(1f, 0.5f);
            srt.anchoredPosition = new Vector2(-6f, 0f); srt.sizeDelta = new Vector2(58f, -10f);
            sendGo.GetComponent<Image>().color = Teal;
            _send = sendGo.GetComponent<Button>();
            var sendLabel = NewText("Label", sendGo.transform, _headFont, 14f, new Color32(0x0A, 0x0E, 0x10, 0xFF), TextAlignmentOptions.Center);
            Stretch((RectTransform)sendLabel.transform, 0f, 0f); sendLabel.text = "GỬI";

            _convHeader.SetActive(false);
            _friendScroll.gameObject.SetActive(false);
            _root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _expandedHeight);
        }
    }
}
