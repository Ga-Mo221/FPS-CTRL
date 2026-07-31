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
        if (_nCharMovementHandler == null || _nCharMovementHandler.Object == null || !_nCharMovementHandler.Object.IsValid)
            return;
        
        var _state = _nCharMovementHandler.StateFl;
        Color defaultColor = _aimImage.color;
        
        if (_state.IsADS)
            defaultColor.a = 0;
        else defaultColor.a = 1;
        
        _aimImage.color = defaultColor;
    }
}
