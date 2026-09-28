// ============================================================
//  TrueInputSystem — TISActionMapData.cs
//  Một nhóm action (Movement, Camera, Skill, Custom...).
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GaMo.TrueInputSystem
{
    /// <summary>
    /// Nhóm các TISInputActionDefinition lại thành một Action Map.
    /// Tương đương với một ActionMap trong Unity Input System.
    /// </summary>
    [Serializable]
    public class TISActionMapData
    {
        // ── Identity ──────────────────────────────────────────
        [Tooltip("Tên của Action Map này.\n" +
                 "Tên mặc định: Movement | Camera | Skill\n" +
                 "Có thể thêm map tuỳ ý: Vehicle, UI, Cutscene...")]
        public string mapName = "NewMap";

        [Tooltip("Khi false, toàn bộ action trong map này bị disable.\n" +
                 "Dùng để tắt input khi vào cutscene, menu, v.v.\n" +
                 "Có thể thay đổi lúc runtime qua TISInputBridge.SetActionMapActive().")]
        public bool enabledByDefault = true;

        // ── Actions ───────────────────────────────────────────
        [Tooltip("Danh sách action trong map này.")]
        public List<TISInputActionDefinition> actions = new();

        // ── Helpers ───────────────────────────────────────────

        /// <summary>
        /// Tìm action theo tên (case-insensitive).
        /// Trả về null nếu không tìm thấy.
        /// </summary>
        public TISInputActionDefinition FindAction(string name)
        {
            return actions.FirstOrDefault(
                a => string.Equals(a.actionName, name,
                                   StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Trả về true nếu map này chứa action có tên đã cho.
        /// </summary>
        public bool ContainsAction(string name) => FindAction(name) != null;

        /// <summary>
        /// Lấy danh sách tên của tất cả action trong map này.
        /// Dùng để validate tên trùng trong Editor Window.
        /// </summary>
        public List<string> GetActionNames()
        {
            return actions.Select(a => a.actionName).ToList();
        }

        /// <summary>
        /// Deep copy toàn bộ map — dùng khi SwapProfile.
        /// </summary>
        public TISActionMapData Clone()
        {
            var clone = new TISActionMapData
            {
                mapName          = this.mapName,
                enabledByDefault = this.enabledByDefault,
                actions          = new List<TISInputActionDefinition>()
            };
            foreach (var action in actions)
                clone.actions.Add(action.Clone());
            return clone;
        }
    }
}
