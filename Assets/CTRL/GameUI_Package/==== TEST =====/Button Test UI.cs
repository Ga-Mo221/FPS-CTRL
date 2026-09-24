using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ButtonTestUI : MonoBehaviour
{
    private Button _button;
    public TextMeshProUGUI _name;
    
    public string Name;
    public string ID;
    public SetupManagerTestUI Setup;
    public ButtonType Type;
    public PanelSelectSetupTestUI PanelSelect;

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (_button == null) return;
        
        _button.onClick.AddListener(Add);
    }

    private void Start()
    {
        _name.text = Name;
    }

    private void Add()
    {
        // su ly du lieu dua tren button type
        switch (Type)
        {
            // ----- Balo 1 -----
            case ButtonType.BL1_W1:
                Setup._data.Balo_One.Weapon_One = ID;
                break;
            case ButtonType.BL1_W2:
                Setup._data.Balo_One.Weapon_Two = ID;
                break;
            case ButtonType.BL1_Q:
                Setup._data.Balo_One.Grenade_Q = ID;
                break;
            case ButtonType.BL1_E:
                Setup._data.Balo_One.Grenade_E = ID;
                break;

            // ----- Balo 2 -----
            case ButtonType.BL2_W1:
                Setup._data.Balo_Two.Weapon_One = ID;
                break;
            case ButtonType.BL2_W2:
                Setup._data.Balo_Two.Weapon_Two = ID;
                break;
            case ButtonType.BL2_Q:
                Setup._data.Balo_Two.Grenade_Q = ID;
                break;
            case ButtonType.BL2_E:
                Setup._data.Balo_Two.Grenade_E = ID;
                break;

            // ----- Balo 3 -----
            case ButtonType.BL3_W1:
                Setup._data.Balo_Three.Weapon_One = ID;
                break;
            case ButtonType.BL3_W2:
                Setup._data.Balo_Three.Weapon_Two = ID;
                break;
            case ButtonType.BL3_Q:
                Setup._data.Balo_Three.Grenade_Q = ID;
                break;
            case ButtonType.BL3_E:
                Setup._data.Balo_Three.Grenade_E = ID;
                break;

            // ----- Character -----
            case ButtonType.CHARACTER:
                Setup._data.CharacterName = ID;
                break;

            default:
                Debug.LogWarning($"<color=yellow>[ButtonTestUI]</color> ButtonType {Type} chua duoc xu ly.");
                break;
        }

        Setup.SaveSetup();
        PanelSelect.Off();
    }

    private void OnDestroy()
    {
        _button.onClick.RemoveAllListeners();
    }
}