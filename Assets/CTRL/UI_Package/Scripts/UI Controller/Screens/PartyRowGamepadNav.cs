// [Aemy 2026-10-19] FILE MOI. Tay cam: hang "NHOM CUA BAN" (5 o party + ROI DOI + "+" ban be + SAN SANG).
using System.Collections.Generic;
using UnityEngine;
using GaMo.TrueInputSystem;

namespace Game.UI
{
    /// <summary>
    /// Gan len PartyPanelController (tu them boi PartySlotContextMenu). Moi frame noi trai / phai cac nut TIS dang bat
    /// trong hang theo vi tri x that (o party nam trong HorizontalLayoutGroup, ROI DOI / SAN SANG bat tat theo trang thai).
    /// Xuong: o party dung chung navDown cua nut khac trong hang (vd BAT DAU). SRP: chi lo dieu huong.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public class PartyRowGamepadNav : MonoBehaviour
    {
        private readonly List<TISGamepadUIButton> _items = new List<TISGamepadUIButton>();

        private void LateUpdate()
        {
            _items.Clear();
            TISGamepadUIButton down = null;
            foreach (var b in GetComponentsInChildren<TISGamepadUIButton>(false))
            {
                if (!b.isActiveAndEnabled || b.layer != TISGamepadUIButton.NavigationLayer.Content) continue;
                _items.Add(b);
                if (down == null && b.GetComponent<SubmitRelay>() == null && b.navDown != null) down = b.navDown;
            }
            if (_items.Count == 0) return;
            _items.Sort((a, c) => a.transform.position.x.CompareTo(c.transform.position.x));
            for (int i = 0; i < _items.Count; i++)
            {
                var b = _items[i];
                b.navLeft = i > 0 ? _items[i - 1] : null;
                b.navRight = i < _items.Count - 1 ? _items[i + 1] : null;
                if (b.GetComponent<SubmitRelay>() != null) { b.navUp = null; b.navDown = down; } // o party
            }
        }
    }
}
