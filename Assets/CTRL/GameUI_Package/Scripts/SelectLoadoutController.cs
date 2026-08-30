using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI
{
    /// <summary>
    /// Controller cho panel "CHON CAU HINH HOI SINH" (Select Loadout).
    /// Chon 1 trong cac cot (ALPHA/BRAVO/CHARLIE) kieu radio-button:
    ///  - Ban dau KHONG cot nao duoc chon: tat ca hien trang thai "chua chon"
    ///    (card xam + nut CHON).
    ///  - Click 1 cot -> cot do thanh "dang chon" (card teal + nhan DANG CHON
    ///    + nut XAC NHAN & HOI SINH), cac cot con lai quay ve "chua chon".
    /// Chi lo phan trinh bay; nut XAC NHAN ban su kien ConfirmRequested de noi logic hoi sinh sau.
    /// </summary>
    public class SelectLoadoutController : MonoBehaviour
    {
        /// <summary>Mot cot loadout voi cac phan tu doi theo trang thai chon.</summary>
        [Serializable]
        public class Column
        {
            [Tooltip("Card teal - hien khi DANG CHON.")]
            public GameObject tealCard;
            [Tooltip("Card xam - hien khi CHUA CHON.")]
            public GameObject grayCard;
            [Tooltip("Nhan 'DANG CHON' - hien khi dang chon.")]
            public GameObject badge;
            [Tooltip("Nut 'XAC NHAN & HOI SINH' - hien khi dang chon.")]
            public GameObject confirm;
            [Tooltip("Nut 'CHON' - hien khi chua chon.")]
            public GameObject chon;
            [Tooltip("Vung click ca cot de chon.")]
            public Button selectArea;
            [Tooltip("Nut CHON (click cung chon cot nay).")]
            public Button chonButton;
            [Tooltip("Nut XAC NHAN (click -> ban ConfirmRequested).")]
            public Button confirmButton;
            [Tooltip("Nen cac o vu khi (Main+Sub) - doi sprite theo trang thai chon.")]
            public Image[] weaponBgs;
            [Tooltip("2 o sung: [0]=sung 1 (Main), [1]=sung 2 (Sub).")]
            public WeaponSlot[] weapons;
            [Tooltip("2 o utility: [0]=slot 1, [1]=slot 2.")]
            public UtilitySlot[] utilities;
            [Tooltip("Nhan ten cot / ten cau hinh (ALPHA...).")]
            public TMP_Text titleLabel;
        }

        /// <summary>Mot o sung: nhan ten + nhan loai sung.</summary>
        [Serializable]
        public class WeaponSlot
        {
            public TMP_Text nameLabel;
            public TMP_Text typeLabel;
        }

        /// <summary>Mot o utility: nhan ten + icon.</summary>
        [Serializable]
        public class UtilitySlot
        {
            public TMP_Text nameLabel;
            public Image icon;
        }

        // ============================================================
        //  PLAYFAB Cau truc du lieu loadout nhan tu PlayFab.
        //  (Dung de truyen vao cac ham do du lieu ben duoi.)
        // ============================================================
        /// <summary>Du lieu 1 o sung de do vao UI.</summary>
        [Serializable]
        public class WeaponSlotData
        {
            public string name;   // Ten sung hien thi, vd "Umbra".
            public string type;   // Loai sung hien thi, vd "RIFLE" / "SMG".
        }

        /// <summary>Du lieu 1 o utility de do vao UI (icon co the null).</summary>
        [Serializable]
        public class UtilitySlotData
        {
            public string name;   // Ten utility hien thi, vd "Flash".
            public Sprite icon;   // Icon utility (Sprite). Truyen null = giu nguyen icon cu.
        }

        /// <summary>Du lieu ca 1 cot loadout: ten cot + 2 sung + 2 utility.</summary>
        [Serializable]
        public class ColumnLoadout
        {
            public string title;              // Ten cot / ten cau hinh (vd "ALPHA"). Rong = giu nguyen.
            public WeaponSlotData weapon1;    // Sung 1 (o Main).
            public WeaponSlotData weapon2;    // Sung 2 (o Sub).
            public UtilitySlotData utility1;  // Utility o slot 1.
            public UtilitySlotData utility2;  // Utility o slot 2.
        }

        [SerializeField] private Column[] _columns = new Column[0];

        [Tooltip("Cot chon san khi mo panel. -1 = khong chon cot nao.")]
        [SerializeField] private int _defaultSelected = -1;

        [Header("Sprite nen o vu khi theo trang thai")]
        [Tooltip("Sprite khi CHUA chon (gun_bg).")]
        [SerializeField] private Sprite _weaponNormalSprite;
        [Tooltip("Sprite khi DANG chon (gun_selected_bg).")]
        [SerializeField] private Sprite _weaponSelectedSprite;

        /// <summary>Ban vua chon cot i (i = -1 neu bo chon).</summary>
        public event Action<int> ColumnSelected;
        /// <summary>Ban nhan XAC NHAN o cot i.</summary>
        public event Action<int> ConfirmRequested;

        /// <summary>Cot dang chon (-1 = khong co).</summary>
        public int SelectedIndex { get; private set; }

        public int ColumnCount { get { return _columns != null ? _columns.Length : 0; } }

        private void Awake()
        {
            if (_columns == null) return;
            for (int i = 0; i < _columns.Length; i++)
            {
                int idx = i;
                var c = _columns[i];
                if (c == null) continue;
                if (c.selectArea != null) c.selectArea.onClick.AddListener(delegate { Select(idx); });
                if (c.chonButton != null) c.chonButton.onClick.AddListener(delegate { Select(idx); });
                if (c.confirmButton != null) c.confirmButton.onClick.AddListener(delegate { if (ConfirmRequested != null) ConfirmRequested(idx); });
            }
            SetSelected(_defaultSelected);
        }

        /// <summary>Chon cot index (ban su kien ColumnSelected). -1 = bo chon het.</summary>
        public void Select(int index)
        {
            SetSelected(index);
            if (ColumnSelected != null) ColumnSelected(index);
        }

        /// <summary>Ap trang thai chon len cac cot (khong ban su kien).</summary>
        public void SetSelected(int index)
        {
            SelectedIndex = index;
            if (_columns == null) return;
            for (int i = 0; i < _columns.Length; i++)
            {
                var c = _columns[i];
                if (c == null) continue;
                bool sel = (i == index);
                if (c.tealCard != null) c.tealCard.SetActive(sel);
                if (c.grayCard != null) c.grayCard.SetActive(!sel);
                if (c.badge != null) c.badge.SetActive(sel);
                if (c.confirm != null) c.confirm.SetActive(sel);
                if (c.chon != null) c.chon.SetActive(!sel);
                // Doi nen o vu khi: chon -> gun_selected_bg, chua chon -> gun_bg.
                if (c.weaponBgs != null)
                {
                    Sprite spr = sel ? _weaponSelectedSprite : _weaponNormalSprite;
                    if (spr != null)
                        for (int k = 0; k < c.weaponBgs.Length; k++)
                            if (c.weaponBgs[k] != null) c.weaponBgs[k].sprite = spr;
                }
            }
        }

        // ============================================================
        //  PLAYFAB API do du lieu loadout vao UI.
        //  Sau khi lay preset loadout tu PlayFab, goi cac ham duoi day
        //  (SetWeapon / SetUtility / SetColumnLoadout / SetAllLoadouts) de hien len UI.
        // ============================================================
        /// <summary>Lay cot theo chi so (null neu ngoai pham vi).</summary>
        private Column GetColumn(int i)
        {
            if (_columns == null || i < 0 || i >= _columns.Length) return null;
            return _columns[i];
        }

        /// <summary>
        /// Dat ten + loai sung cho 1 o vu khi.
        /// - column: 0=ALPHA, 1=BRAVO, 2=CHARLIE.
        /// - slot: 0=sung 1 (o Main), 1=sung 2 (o Sub).
        /// - Truyen name/type = null neu muon giu nguyen gia tri cu.
        /// Vi du: SetWeapon(0, 0, "Umbra", "RIFLE"); // cot ALPHA, sung 1
        /// </summary>
        public void SetWeapon(int column, int slot, string name, string type)
        {
            var c = GetColumn(column);                                    // Lay cot theo chi so.
            // Bo qua neu cot khong hop le, chua co mang o sung, hoac slot vuot pham vi.
            if (c == null || c.weapons == null || slot < 0 || slot >= c.weapons.Length) return;
            var w = c.weapons[slot];                                      // O sung can cap nhat.
            if (w == null) return;
            if (w.nameLabel != null && name != null) w.nameLabel.text = name;   // Cap nhat ten sung.
            if (w.typeLabel != null && type != null) w.typeLabel.text = type;   // Cap nhat loai sung.
        }

        /// <summary>
        /// Dat ten + icon cho 1 o utility.
        /// - column: 0=ALPHA, 1=BRAVO, 2=CHARLIE. slot: 0 hoac 1.
        /// - icon = null neu chi doi ten (giu nguyen icon cu).
        /// Vi du: SetUtility(1, 0, "Flash", flashSprite); // cot BRAVO, utility slot 1
        /// </summary>
        public void SetUtility(int column, int slot, string name, Sprite icon)
        {
            var c = GetColumn(column);                                       // Lay cot theo chi so.
            // Bo qua neu cot khong hop le, chua co mang utility, hoac slot vuot pham vi.
            if (c == null || c.utilities == null || slot < 0 || slot >= c.utilities.Length) return;
            var u = c.utilities[slot];                                       // O utility can cap nhat.
            if (u == null) return;
            if (u.nameLabel != null && name != null) u.nameLabel.text = name; // Cap nhat ten utility.
            if (u.icon != null && icon != null) u.icon.sprite = icon;         // Cap nhat icon (neu co).
        }

        /// <summary>
        /// Do TOAN BO 1 cot loadout trong 1 lan goi: ten cot + 2 sung + 2 utility.
        /// Truong nao trong 'data' de null thi bo qua (giu nguyen tren UI).
        /// Vi du:
        ///   var d = new ColumnLoadout { title = "ALPHA",
        ///       weapon1 = new WeaponSlotData { name = "Umbra", type = "RIFLE" },
        ///       utility1 = new UtilitySlotData { name = "Flash", icon = flashSprite } };
        ///   SetColumnLoadout(0, d);
        /// </summary>
        public void SetColumnLoadout(int column, ColumnLoadout data)
        {
            if (data == null) return;                                        // Khong co du lieu -> bo qua.
            var c = GetColumn(column);
            if (c == null) return;                                           // Cot khong hop le -> bo qua.
            // Doi ten cot neu co (title rong thi giu nguyen).
            if (c.titleLabel != null && !string.IsNullOrEmpty(data.title)) c.titleLabel.text = data.title;
            // Do tung o (bo qua o nao null trong data).
            if (data.weapon1 != null) SetWeapon(column, 0, data.weapon1.name, data.weapon1.type);
            if (data.weapon2 != null) SetWeapon(column, 1, data.weapon2.name, data.weapon2.type);
            if (data.utility1 != null) SetUtility(column, 0, data.utility1.name, data.utility1.icon);
            if (data.utility2 != null) SetUtility(column, 1, data.utility2.name, data.utility2.icon);
        }

        /// <summary>
        /// Do nhieu cot cung luc. Phan tu thu i cua mang ung voi cot thu i
        /// (0=ALPHA, 1=BRAVO, 2=CHARLIE). Vuot so cot thi bo qua phan du.
        /// Vi du: SetAllLoadouts(new [] { alphaData, bravoData, charlieData });
        /// </summary>
        public void SetAllLoadouts(ColumnLoadout[] datas)
        {
            if (datas == null) return;
            // Lap qua tung phan tu, khong vuot so cot hien co (ColumnCount).
            for (int i = 0; i < datas.Length && i < ColumnCount; i++)
                SetColumnLoadout(i, datas[i]);
        }

        // ============================================================
        //  HET PHAN PLAYFAB
        // ============================================================
    }
}
