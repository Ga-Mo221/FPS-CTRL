using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using GaMo.TrueInputSystem;

namespace Game.UI
{
    /// <summary>
    /// [Aemy 2026-10-05] Bam O/B (buttonEast tay cam) khi dang focus TRONG 1 section
    /// (vd Đồ họa/Âm thanh/Chuột...) -> quay ve "mục chọn chức năng" (category menu / tab row).
    /// Nho lai category/tab vua bam (onClick) de tra ve dung cho.
    /// Gan len panel Settings (menu) voi _categoryRoot = "Chức năng";
    /// hoac len Pause Settings voi _categoryRoot = "Nav" (hang tab).
    /// </summary>
    [DisallowMultipleComponent]
    public class TISSectionBackNav : MonoBehaviour
    {
        [Tooltip("Vung chua cac nut CHON CHUC NANG (category menu / tab row). Vd 'Chức năng' hoac 'Nav'.")]
        [SerializeField] private Transform _categoryRoot;

        private TISGamepadUIButton _lastCategory;
        private bool _wired;

        private void OnEnable() { WireCategories(); }

        private void WireCategories()
        {
            if (_wired || _categoryRoot == null) return;
            foreach (var cat in _categoryRoot.GetComponentsInChildren<TISGamepadUIButton>(true))
            {
                var c = cat;
                cat.onClick.AddListener(() => _lastCategory = c);
            }
            _wired = true;
        }

        private void Update()
        {
            if (TISGamepadUIButton.ExternalSuppress > 0) return; // co overlay -> de overlay xu ly B
            var gp = Gamepad.current;
            if (gp == null || !gp.buttonEast.wasPressedThisFrame) return;

            var es = EventSystem.current;
            if (es == null) return;
            var cur = es.currentSelectedGameObject;
            if (cur == null) return;

            // chi xu ly khi focus thuoc panel nay
            if (!cur.transform.IsChildOf(transform)) return;
            // dang o trong category menu roi thi thoi
            if (_categoryRoot != null && cur.transform.IsChildOf(_categoryRoot)) return;
            // focus phai la 1 nut TIS (dang o section)
            if (cur.GetComponent<TISGamepadUIButton>() == null) return;

            var target = (_lastCategory != null && _lastCategory.isActiveAndEnabled) ? _lastCategory : PickCategory();
            if (target != null) es.SetSelectedGameObject(target.gameObject);
        }

        private TISGamepadUIButton PickCategory()
        {
            if (_categoryRoot == null) return null;
            foreach (var cat in _categoryRoot.GetComponentsInChildren<TISGamepadUIButton>(false))
                if (cat.isActiveAndEnabled) return cat;
            return null;
        }
    }
}
