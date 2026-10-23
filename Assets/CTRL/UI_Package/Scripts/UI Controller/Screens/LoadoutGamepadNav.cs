// [Aemy 2026-10-12] FILE MOI. Tay cam o man Trang bi: di ngang giua 3 cot  SUNG | ◄ SKIN ► | UTILITIES.
using System.Collections.Generic;
using UnityEngine;
using GaMo.TrueInputSystem;

namespace Game.UI
{
    /// <summary>
    /// Gan len LoadoutPanel. Chi chinh navLeft/navRight cua TISGamepadUIButton (len/xuong giu nhu scene, chuot khong anh huong).
    ///   O sung  --phai-->  ◄ (skin truoc)  --phai-->  ► (skin sau)  --phai-->  o utility cung hang
    ///   O utility --trai--> ►  --trai--> ◄  --trai--> o sung vua dung (khong nhay ve dau danh sach)
    /// Sung khong co skin (◄ ► an) -> sung <-> utility noi thang. Nut "DEN CUA HANG" (skin chua mua): trai ve sung, phai sang utility.
    /// Cap nhat moi frame vi ◄ ► / DEN CUA HANG an hien theo sung dang xem.
    /// </summary>
    [DisallowMultipleComponent]
    public class LoadoutGamepadNav : MonoBehaviour
    {
        [SerializeField] private LoadoutController _loadout;
        [SerializeField] private TISGamepadUIButton _skinPrev;   // de trong = tim "SkinPrev"
        [SerializeField] private TISGamepadUIButton _skinNext;   // de trong = tim "SkinNext"
        [SerializeField] private TISGamepadUIButton _goShop;     // de trong = tim "GoShop"

        private readonly List<TISGamepadUIButton> _weapons = new List<TISGamepadUIButton>();
        private readonly List<TISGamepadUIButton> _utils = new List<TISGamepadUIButton>();
        private TISGamepadUIButton _lastWeapon;
        private TISGamepadUIButton _lastUtil;
        private bool _collected;

        private void Awake()
        {
            if (_loadout == null) _loadout = GetComponentInChildren<LoadoutController>(true);
            foreach (var b in GetComponentsInChildren<TISGamepadUIButton>(true))
            {
                if (_skinPrev == null && b.name == "SkinPrev") _skinPrev = b;
                else if (_skinNext == null && b.name == "SkinNext") _skinNext = b;
                else if (_goShop == null && b.name == "GoShop") _goShop = b;
            }
        }

        private void OnEnable() { Collect(); }

        private void Collect()
        {
            if (_collected || _loadout == null) return;
            for (int i = 0; i < _loadout.WeaponCount; i++) Add(_weapons, _loadout.GetWeaponItem(i));
            for (int i = 0; i < _loadout.UtilityCount; i++) Add(_utils, _loadout.GetUtilityItem(i));
            if (_weapons.Count == 0 || _utils.Count == 0) return;
            foreach (var w in _weapons) { var b = w; b.onSelect.AddListener(() => _lastWeapon = b); }
            foreach (var u in _utils) { var b = u; b.onSelect.AddListener(() => _lastUtil = b); }
            _lastWeapon = _weapons[0];
            _collected = true;
        }

        private void LateUpdate()
        {
            if (!_collected) { Collect(); if (!_collected) return; }

            bool prevOn = Visible(_skinPrev), nextOn = Visible(_skinNext), shopOn = Visible(_goShop);
            var backWeapon = _lastWeapon != null ? _lastWeapon : _weapons[0];

            // cot sung -> phai
            for (int i = 0; i < _weapons.Count; i++)
            {
                var row = _utils[Mathf.Min(i, _utils.Count - 1)];
                _weapons[i].navRight = prevOn ? _skinPrev : (nextOn ? _skinNext : row);
            }
            var utilTarget = _lastUtil != null ? _lastUtil : _utils[0];

            // cum skin o giua
            if (prevOn) { _skinPrev.navLeft = backWeapon; _skinPrev.navRight = nextOn ? _skinNext : utilTarget; }
            if (nextOn) { _skinNext.navLeft = prevOn ? _skinPrev : backWeapon; _skinNext.navRight = utilTarget; }
            if (shopOn) { _goShop.navLeft = backWeapon; _goShop.navRight = utilTarget; }

            // cot utility -> trai
            var leftOfUtils = nextOn ? _skinNext : (prevOn ? _skinPrev : backWeapon);
            foreach (var u in _utils) u.navLeft = leftOfUtils;
        }

        private static bool Visible(TISGamepadUIButton b) { return b != null && b.gameObject.activeInHierarchy && b.interactable; }

        private static void Add(List<TISGamepadUIButton> list, Transform t)
        {
            if (t == null) return;
            var b = t.GetComponent<TISGamepadUIButton>();
            if (b != null) list.Add(b);
        }
    }
}
