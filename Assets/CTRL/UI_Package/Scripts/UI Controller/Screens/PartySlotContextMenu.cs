// [Aemy 2026-10-17] FILE MOI. Chuot phai vao o thanh vien trong "PHONG CUA BAN" -> menu: ket ban, da khoi doi...
using System;
using System.Collections.Generic;
using UnityEngine;
using Backend.Bootstrap;
using Backend.Models;

namespace Game.UI
{
    /// <summary>
    /// Gan RightClickRelay vao tung o party; mo UIContextMenu voi lua chon theo vai tro:
    ///  - Nguoi khac: KET BAN (neu chua la ban), [truong nhom] CHUYEN TRUONG NHOM, DA KHOI DOI.
    ///  - Chinh minh (dang trong doi): ROI DOI.
    /// Logic party nam o PartyController; o day chi dung menu + xac nhan (SRP).
    /// [Aemy 2026-10-19] Tay cam: moi o la 1 nut TIS (vien chon). A tren o CO NGUOI = mo menu nay (ket ban / chuyen truong
    /// nhom / da khoi doi / roi doi); A tren o TRONG = nut "+ MOI". Chuot trai van nhu cu (chi chuot phai mo menu).
    /// Dieu huong trai / phai trong hang: PartyRowGamepadNav.
    /// </summary>
    public class PartySlotContextMenu : MonoBehaviour
    {
        private PartyPanelController _panel;
        private PartyController _party;
        private Func<List<FriendInfo>> _friends;
        private Action _afterLeave;
        private bool _hooked;

        public static PartySlotContextMenu Attach(PartyPanelController panel, PartyController party, Func<List<FriendInfo>> friends, Action afterLeave)
        {
            if (panel == null || party == null) return null;
            var m = panel.GetComponent<PartySlotContextMenu>();
            if (m == null) m = panel.gameObject.AddComponent<PartySlotContextMenu>();
            m._panel = panel; m._party = party; m._friends = friends; m._afterLeave = afterLeave;
            m.Hook();
            return m;
        }

        private void Hook()
        {
            if (_hooked || _panel == null) return;
            for (int i = 0; i < _panel.SlotCount; i++)
            {
                var root = _panel.GetSlotRoot(i);
                if (root == null) continue;
                // O can Graphic nhan raycast (o co Image) -> relay nhan duoc chuot phai.
                int idx = i;
                RightClickRelay.On(root).RightClicked += pos => Open(idx, pos);
                MakeGamepadFocusable(root, idx);
            }
            if (_panel.GetComponent<PartyRowGamepadNav>() == null) _panel.gameObject.AddComponent<PartyRowGamepadNav>();
            _hooked = true;
        }

        // O party -> nut TIS (chi nhan focus tay cam + Submit; onClick rong nen chuot trai khong doi hanh vi).
        private void MakeGamepadFocusable(GameObject root, int idx)
        {
            var btn = root.GetComponent<GaMo.TrueInputSystem.TISGamepadUIButton>();
            if (btn == null) btn = root.AddComponent<GaMo.TrueInputSystem.TISGamepadUIButton>();
            btn.transition = UnityEngine.UI.Selectable.Transition.None;
            if (btn.context == null)
            {
                var near = _panel.GetComponentInChildren<GaMo.TrueInputSystem.TISGamepadUIButton>(true);
                foreach (var b in _panel.GetComponentsInChildren<GaMo.TrueInputSystem.TISGamepadUIButton>(true))
                    if (b != btn && b.context != null) { near = b; break; }
                if (near != null && near.context != null) btn.SetContext(near.context);
            }
            if (root.GetComponent<TISSelectHighlight>() == null) root.AddComponent<TISSelectHighlight>();
            var relay = root.GetComponent<SubmitRelay>();
            if (relay == null) relay = root.AddComponent<SubmitRelay>();
            relay.Submitted = () => OnSlotSubmit(idx, root);
        }

        private void OnSlotSubmit(int slot, GameObject root)
        {
            // Menu / hop thoai dang mo (hoac vua dong trong frame nay): nut A thuoc ve no, khong mo lai menu.
            if (GaMo.TrueInputSystem.TISGamepadUIButton.ExternalSuppress > 0 || UIContextMenu.IsOpen) return;
            if (_panel.IsSlotFilled(slot))
            {
                var rt = root.transform as RectTransform;
                var canvas = root.GetComponentInParent<Canvas>();
                Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
                Vector2 pos = rt != null ? RectTransformUtility.WorldToScreenPoint(cam, rt.position) : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                Open(slot, pos);
                return;
            }
            var invite = _panel.GetSlotInviteButton(slot);
            if (invite != null && invite.interactable) invite.onClick.Invoke();
        }

        private void Open(int slot, Vector2 screenPos)
        {
            if (_party == null || !_party.InParty || _party.State == null || _party.State.Members == null) return;
            if (slot < 0 || slot >= _party.State.Members.Count) return;
            var m = _party.State.Members[slot];
            if (m == null || string.IsNullOrEmpty(m.PlayFabId)) return;
            string me = _party.MyId;
            bool self = m.PlayFabId == me;
            var items = new List<UIContextMenu.Item>();

            if (self)
            {
                items.Add(new UIContextMenu.Item("RỜI ĐỘI", ConfirmLeave, true));
            }
            else
            {
                if (!IsFriend(m.PlayFabId)) items.Add(new UIContextMenu.Item("KẾT BẠN", () => AddFriend(m)));
                if (_party.IsLeader)
                {
                    items.Add(new UIContextMenu.Item("CHUYỂN TRƯỞNG NHÓM", () => ConfirmTransfer(m)));
                    items.Add(new UIContextMenu.Item("ĐÁ KHỎI ĐỘI", () => ConfirmKick(m), true));
                }
            }
            if (items.Count == 0) items.Add(new UIContextMenu.Item("ĐÃ LÀ BẠN BÈ", null));
            UIContextMenu.Show(screenPos, items, string.IsNullOrEmpty(m.Name) ? "THÀNH VIÊN" : m.Name.ToUpperInvariant());
        }

        private bool IsFriend(string id)
        {
            var list = _friends != null ? _friends() : null;
            if (list == null) return false;
            foreach (var f in list) if (f != null && f.PlayFabId == id) return true;
            return false;
        }

        private void AddFriend(PartyMember m)
        {
            var svc = BackendBootstrap.FriendsService;
            if (svc == null) { Info("Kết bạn", "Chưa kết nối dịch vụ bạn bè."); return; }
            svc.SendFriendRequest(m.PlayFabId,
                msg => Info("Kết bạn", string.IsNullOrEmpty(msg) ? ("Đã gửi lời mời kết bạn tới " + m.Name + ".") : msg),
                e => Info("Kết bạn", "Không gửi được lời mời: " + (e != null ? e.ErrorMessage : "?")));
        }

        private void ConfirmKick(PartyMember m)
        {
            if (ModalDialog.Instance == null) { _party.KickMember(m.PlayFabId); return; }
            ModalDialog.Instance.Confirm("Đá khỏi đội", "Mời " + m.Name + " ra khỏi đội?", () => _party.KickMember(m.PlayFabId), "ĐÁ", "HỦY");
        }

        private void ConfirmTransfer(PartyMember m)
        {
            if (ModalDialog.Instance == null) { _party.TransferLeader(m.PlayFabId); return; }
            ModalDialog.Instance.Confirm("Chuyển trưởng nhóm", m.Name + " sẽ là trưởng nhóm và chọn chế độ, bản đồ.", () => _party.TransferLeader(m.PlayFabId), "CHUYỂN", "HỦY");
        }

        private void ConfirmLeave()
        {
            System.Action leave = () => { _party.LeaveParty(); if (_afterLeave != null) _afterLeave(); };
            if (ModalDialog.Instance == null) { leave(); return; }
            ModalDialog.Instance.Confirm("Rời đội", "Bạn muốn rời đội hiện tại?", leave, "RỜI", "HỦY");
        }

        private static void Info(string title, string msg)
        {
            if (ModalDialog.Instance != null) ModalDialog.Instance.Info(title, msg); else Debug.Log("[PartyMenu] " + title + ": " + msg);
        }
    }
}
