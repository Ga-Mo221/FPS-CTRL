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
            }
            _hooked = true;
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
