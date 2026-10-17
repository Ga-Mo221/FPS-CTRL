using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Backend.Models;

namespace Game.UI
{
    // [Aemy 2026-09-21] 1 dong ban trong SocialPanel: avatar, ten, trang thai, cham online, nut moi/xoa.
    public class FriendRowUI : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
    {
        [SerializeField] private Image _avatar;
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private Image _dot;
        [SerializeField] private Button _inviteButton;
        [SerializeField] private Button _removeButton;

        [SerializeField] private Color _onColor  = new Color(0.22f, 0.77f, 0.42f, 1f);
        [SerializeField] private Color _awayColor = new Color(0.90f, 0.70f, 0.30f, 1f);
        [SerializeField] private Color _offColor  = new Color(0.36f, 0.43f, 0.42f, 1f);

        private FriendInfo _info;
        private Action<FriendInfo> _onInvite, _onRemove;

        public void Set(FriendInfo info, Action<FriendInfo> onInvite, Action<FriendInfo> onRemove)
        {
            _info = info; _onInvite = onInvite; _onRemove = onRemove;
            if (_removeButton != null) _removeButton.gameObject.SetActive(false); // [CTX 2026-09-29 Aemy] xoa qua chuot phai
            if (_nameLabel != null) _nameLabel.text = string.IsNullOrEmpty(info.DisplayName) ? "PLAYER" : info.DisplayName;
            if (_statusLabel != null) _statusLabel.text = StatusText(info);
            if (_dot != null) _dot.color = info.Presence == FriendPresence.Online ? _onColor
                                          : info.Presence == FriendPresence.Away ? _awayColor : _offColor;
            if (_inviteButton != null)
            {
                _inviteButton.gameObject.SetActive(info.Presence == FriendPresence.Online);
                _inviteButton.onClick.RemoveAllListeners();
                _inviteButton.onClick.AddListener(() => { if (_onInvite != null) _onInvite(_info); });
            }
            if (_removeButton != null)
            {
                _removeButton.onClick.RemoveAllListeners();
                _removeButton.onClick.AddListener(() => { if (_onRemove != null) _onRemove(_info); });
            }
        }

        public void SetAvatar(Sprite s) { if (_avatar != null && s != null) _avatar.sprite = s; }

        // [CTX 2026-09-29 Aemy] Chuot phai -> menu hanh dong (copy id/ten, moi party, xoa ban, lich su).
        private static UnityEngine.GameObject _ctxMenu;
        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e)
        {
            if (e.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right) ShowContextMenu(e.position);
        }
        private void ShowContextMenu(Vector2 screenPos)
        {
            CloseContextMenu();
            var canvas = GetComponentInParent<Canvas>(); if (canvas == null) return;
            var rootCanvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
            var panel = new GameObject("FriendCtxMenu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.VerticalLayoutGroup), typeof(UnityEngine.UI.ContentSizeFitter));
            panel.transform.SetParent(rootCanvas.transform, false);
            _ctxMenu = panel;
            var prt = panel.GetComponent<RectTransform>();
            prt.pivot = new Vector2(0f, 1f); prt.anchorMin = prt.anchorMax = new Vector2(0f, 0f);
            var img = panel.GetComponent<Image>(); img.color = new Color(0.10f, 0.12f, 0.16f, 0.98f);
            var vlg = panel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>(); vlg.padding = new RectOffset(6,6,6,6); vlg.spacing = 2; vlg.childForceExpandWidth = true; vlg.childControlWidth = true; vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
            var fit = panel.GetComponent<UnityEngine.UI.ContentSizeFitter>(); fit.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.MinSize; fit.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var src = _nameLabel;
            AddCtxButton(panel.transform, src, "Copy PlayFabId", () => { GUIUtility.systemCopyBuffer = _info != null ? _info.PlayFabId : ""; });
            AddCtxButton(panel.transform, src, "Copy tên", () => { GUIUtility.systemCopyBuffer = _info != null ? _info.DisplayName : ""; });
            AddCtxButton(panel.transform, src, "Mời vào party", () => { if (_onInvite != null && _info != null) _onInvite(_info); });
            AddCtxButton(panel.transform, src, "Xem lịch sử", () => { if (ModalDialog.Instance != null) ModalDialog.Instance.Info("LỊCH SỬ ĐẤU", "Tính năng đang phát triển."); });
            AddCtxButton(panel.transform, src, "Xóa bạn", () => {
                if (ModalDialog.Instance != null) ModalDialog.Instance.Confirm("XÓA BẠN", "Xóa " + (_info != null ? _info.DisplayName : "") + " khỏi danh sách bạn?", () => { if (_onRemove != null && _info != null) _onRemove(_info); }, "XÓA", "HỦY");
                else if (_onRemove != null && _info != null) _onRemove(_info);
            });
            prt.anchoredPosition = new Vector2(screenPos.x / rootCanvas.scaleFactor, screenPos.y / rootCanvas.scaleFactor);
            // backdrop dong menu khi bam ra ngoai
            var back = new GameObject("CtxBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            back.transform.SetParent(rootCanvas.transform, false); back.transform.SetSiblingIndex(panel.transform.GetSiblingIndex());
            var brt = back.GetComponent<RectTransform>(); brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            back.GetComponent<Image>().color = new Color(0,0,0,0.001f);
            back.GetComponent<Button>().onClick.AddListener(CloseContextMenu);
        }
        private void AddCtxButton(Transform parent, TMP_Text fontSrc, string label, System.Action onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(UnityEngine.UI.LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(1f,1f,1f,0.06f);
            go.GetComponent<UnityEngine.UI.LayoutElement>().minHeight = 34; go.GetComponent<UnityEngine.UI.LayoutElement>().minWidth = 190;
            go.GetComponent<Button>().onClick.AddListener(() => { onClick(); CloseContextMenu(); });
            var tgo = new GameObject("T", typeof(RectTransform)); tgo.transform.SetParent(go.transform, false);
            var trt = tgo.GetComponent<RectTransform>(); trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(12,0); trt.offsetMax = new Vector2(-12,0);
            var t = tgo.AddComponent<TextMeshProUGUI>(); t.text = label; t.fontSize = 20; t.alignment = TextAlignmentOptions.Left; t.color = Color.white; t.raycastTarget = false;
            if (fontSrc != null && fontSrc.font != null) t.font = fontSrc.font;
        }
        private static void CloseContextMenu()
        {
            if (_ctxMenu != null) { var back = _ctxMenu.transform.parent != null ? _ctxMenu.transform.parent.Find("CtxBackdrop") : null; if (back != null) Destroy(back.gameObject); Destroy(_ctxMenu); _ctxMenu = null; }
        }

        private static string StatusText(FriendInfo info)
        {
            if (info.Presence == FriendPresence.Online) return "Đang online";
            if (info.Presence == FriendPresence.Away)   return "Vắng mặt";
            return "Ngoại tuyến";
        }
    }
}
