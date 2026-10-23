// [Aemy 2026-10-19] FILE MOI. Bao "Submit" (A tay cam / Enter) ma khong dung onClick -> chuot trai khong bi anh huong.
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public class SubmitRelay : MonoBehaviour, ISubmitHandler
    {
        public Action Submitted;
        public void OnSubmit(BaseEventData eventData) { if (Submitted != null) Submitted(); }
    }
}
