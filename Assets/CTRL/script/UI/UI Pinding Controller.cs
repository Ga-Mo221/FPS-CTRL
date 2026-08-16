using TMPro;
using TrueInputSystem;
using UnityEngine;

public class UIPindingController : MonoBehaviour
{
    private TISInputBridge _input;
    [SerializeField] private TextMeshProUGUI _fragPinding_text;
    [SerializeField] private TextMeshProUGUI _flashPinding_text;
    

    private void Awake()
    {
        _input = TISInputBridge.Instance;
    }

    private void Start()
    {
        string path_Frag = TISRebindingSystem.GetActivePath(_input, "Q", RebindTarget.PC);
        _fragPinding_text.text = TISRebindingSystem.GetDisplayName(path_Frag);
        
        string path_Flash = TISRebindingSystem.GetActivePath(_input, "E", RebindTarget.PC);
        _flashPinding_text.text = TISRebindingSystem.GetDisplayName(path_Flash);
    }
}
