using System;
using UnityEngine;

public class PanelSelectSetupTestUI : MonoBehaviour
{
    public SetupManagerTestUI SetupManagerTestUI;
    public ButtonTestUI ButtonTestUI;
    public Transform parrent;
    private ButtonType _buttonType;
    
    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Load(bool weaponbool, bool grenadebool, bool characterbool, ButtonType type)
    {
        _canvasGroup.alpha = 1;
        _canvasGroup.blocksRaycasts = true;
        
        // clear parrent truoc
        ClearParent();
        
        if (weaponbool)
        {
            foreach (var weapon in GameManager.Instance.GetWeaponDataList())
            {
                var bt = Instantiate(ButtonTestUI, parrent);
                var script = bt.GetComponent<ButtonTestUI>();
                script.Name = weapon.Name();
                script.ID = weapon.ID();
                script.Setup = SetupManagerTestUI;
                script.Type = type;
                script.PanelSelect = this;
            }
        }

        if (grenadebool)
        {
            foreach (var grenade in GameManager.Instance.GetGrenadeList())
            {
                var bt = Instantiate(ButtonTestUI, parrent);
                var script = bt.GetComponent<ButtonTestUI>();
                script.Name = grenade.Name;
                script.ID = grenade.ID;
                script.Setup = SetupManagerTestUI;
                script.Type = type;
                script.PanelSelect = this;
            }
        }

        if (characterbool)
        {
            foreach (var chara in GameManager.Instance.GetCharacterList())
            {
                var bt = Instantiate(ButtonTestUI, parrent);
                var script = bt.GetComponent<ButtonTestUI>();
                script.Name = chara.Name;
                script.ID = chara.Name;
                script.Setup = SetupManagerTestUI;
                script.Type = type;
                script.PanelSelect = this;
            }
        }
    }
    
    public void ClearParent()
    {
        if (parrent == null) return;

        // Duyệt ngược từ cuối về đầu để tránh lỗi thay đổi chỉ số (index) khi xóa
        for (int i = parrent.childCount - 1; i >= 0; i--)
        {
            Destroy(parrent.GetChild(i).gameObject);
        }
    }
    
    public void Off()
    {
        _canvasGroup.alpha = 0;
        _canvasGroup.blocksRaycasts = false;
    }
}

public enum ButtonType
{
    BL1_W1,
    BL1_W2,
    BL1_Q,
    BL1_E,
    BL2_W1,
    BL2_W2,
    BL2_Q,
    BL2_E,
    BL3_W1,
    BL3_W2,
    BL3_Q,
    BL3_E,
    CHARACTER
}
