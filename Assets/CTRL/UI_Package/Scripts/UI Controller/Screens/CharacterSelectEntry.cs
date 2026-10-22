using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// [Aemy 2026-10-05] Gan len 1 nut (co Button) de mo man CHON NHAN VAT.
    /// Tu noi Button.onClick -> CharacterSelectController.Open() luc Awake, khong can wire Inspector.
    /// </summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public class CharacterSelectEntry : MonoBehaviour
    {
        private void Awake()
        {
            var btn = GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(CharacterSelectController.Open);
        }
    }
}
