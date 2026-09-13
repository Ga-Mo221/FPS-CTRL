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
            SetBaloOne(WeaponName._M4, WeaponName._9MM_DEMON);
            SetBaloTwo(WeaponName._REMINGTON, WeaponName._KNIFE);
            SetBaloThree(WeaponName._9MM_DEMON, WeaponName._KNIFE);
            
            if (GameManager.Instance != null && GameManager.Instance.GetStartHostMigration())
                gameObject.SetActive(false);
        }

        public void Out()
        {
            gameObject.SetActive(false);
            
            if (GameManager.Instance != null)
                GameManager.Instance.SetOnuiLoadout(false);
        }

        public void SetBaloOne(string weaponOne, string weaponTwo)
            => SetBalo(_balo1, weaponOne, weaponTwo);
        
        public void SetBaloTwo(string weaponOne, string weaponTwo)
            => SetBalo(_balo2, weaponOne, weaponTwo);
        
        public void SetBaloThree(string weaponOne, string weaponTwo)
            => SetBalo(_balo3, weaponOne, weaponTwo);

        private void SetBalo(UISelectedWeaponButton balo, string weaponOne, string weaponTwo)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            WeaponData _data1 = null;
            WeaponData _data2 = null;

            if (!weaponOne.IsNullOrEmpty())
                _data1 = gm.GetWeaponData(weaponOne);
            
            if (!weaponTwo.IsNullOrEmpty())
                _data2 = gm.GetWeaponData(weaponTwo);
            
            balo.SetWeapon(_data1, _data2);
        }
    }
}
