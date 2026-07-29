using UnityEngine;
using UnityEngine.UI;

public class UIADSPlayer : MonoBehaviour
{
    [SerializeField] private Image _aimImage;
    
    private CharComponentManager _componentManager;
    private NCharMovementHandler _nCharMovementHandler;

    private void Awake()
    {
        _componentManager = GetComponentInParent<CharComponentManager>();
        _nCharMovementHandler = _componentManager.NCharMovementHandler;
    }

    private void Update()
    {
        var _state = _nCharMovementHandler._state;
        Color defaultColor = _aimImage.color;
        
        if (_state.IsADS)
            defaultColor.a = 0;
        else defaultColor.a = 1;
        
        _aimImage.color = defaultColor;
    }
}
