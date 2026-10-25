// [Aemy 2026-10-17] FILE MOI. Bat click CHUOT PHAI tren 1 o UI (Button cua Unity chi nhan chuot trai).
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>Gan vao o UI can menu/hanh dong chuot phai. Chi bao su kien, khong tu xu ly (SRP).</summary>
    [DisallowMultipleComponent]
    public class RightClickRelay : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>Ban khi bam chuot phai; tham so = vi tri man hinh (de mo menu ngu canh tai cho con tro).</summary>
        public event Action<Vector2> RightClicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Right) return;
            if (RightClicked != null) RightClicked(eventData.position);
        }

        /// <summary>Lay (hoac them) relay tren go.</summary>
        public static RightClickRelay On(GameObject go)
        {
            if (go == null) return null;
            var r = go.GetComponent<RightClickRelay>();
            return r != null ? r : go.AddComponent<RightClickRelay>();
        }
    }
}
