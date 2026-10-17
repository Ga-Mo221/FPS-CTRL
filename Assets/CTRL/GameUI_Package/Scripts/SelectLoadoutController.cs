using UnityEngine;
using WebSocketSharp;

namespace Game.UI
{
    public class SelectLoadoutController : MonoBehaviour
    {
        [SerializeField] private UISelectedWeaponButton _balo1;
        [SerializeField] private UISelectedWeaponButton _balo2;
        [SerializeField] private UISelectedWeaponButton _balo3;

        // test
        private void Start()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            
            SetBaloOne(gm.GetSetupData().Balo_One);
            SetBaloTwo(gm.GetSetupData().Balo_Two);
            SetBaloThree(gm.GetSetupData().Balo_Three);
            // [Aemy 2026-10-11] Balo 1/2/3 = ALPHA/BRAVO/CHARLIE o menu Trang bi -> ghi ro ten de khoi nham.
            if (_balo1 != null) _balo1.SetTitle("BALO 1 · ALPHA");
            if (_balo2 != null) _balo2.SetTitle("BALO 2 · BRAVO");
            if (_balo3 != null) _balo3.SetTitle("BALO 3 · CHARLIE");
            
            gm.setCurrentBalo(gm.GetSetupData().Balo_One);
            gm.SetCurrentWeapon(1,gm.GetSetupData().Balo_One.Weapon_One);
            gm.SetCurrentWeapon(2,gm.GetSetupData().Balo_One.Weapon_Two);
            
            if (gm.GetStartHostMigration())
                gameObject.SetActive(false);
        }

        public void Out()
        {
            gameObject.SetActive(false);
            
            if (GameManager.Instance != null)
                GameManager.Instance.SetOnuiLoadout(false);
        }

        public void SetBaloOne(BaloData data)
            => SetBalo(_balo1, data);
        
        public void SetBaloTwo(BaloData data)
            => SetBalo(_balo2, data);
        
        public void SetBaloThree(BaloData data)
            => SetBalo(_balo3, data);

        private void SetBalo(UISelectedWeaponButton balo, BaloData data)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            WeaponData _data1 = null;
            WeaponData _data2 = null;
            GrenadeHandler _Q = null;
            GrenadeHandler _E = null;

            if (!data.Weapon_One.IsNullOrEmpty())
                _data1 = gm.GetWeaponData(data.Weapon_One);
            
            if (!data.Weapon_Two.IsNullOrEmpty())
                _data2 = gm.GetWeaponData(data.Weapon_Two);
            
            if (!data.Grenade_Q.IsNullOrEmpty())
                _Q = gm.GetGrenade(data.Grenade_Q);
            
            if (!data.Grenade_E.IsNullOrEmpty())
                _E = gm.GetGrenade(data.Grenade_E);
            
            balo.SetWeapon(_data1, _data2, _Q, _E);
        }
    }

    [System.Serializable]
    public struct BaloData
    {
        public string Weapon_One;
        public string Weapon_Two;
        public string Grenade_Q;
        public string Grenade_E;

        public BaloData(string w1, string w2, string G_Q, string G_E)
        {
            Weapon_One = w1;
            Weapon_Two = w2;
            Grenade_Q = G_Q;
            Grenade_E = G_E;
        }
    }
}
