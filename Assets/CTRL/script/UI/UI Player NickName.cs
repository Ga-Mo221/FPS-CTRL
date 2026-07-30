using TMPro;
using UnityEngine;

public class UIPlayerNickName : MonoBehaviour
{
    [SerializeField] private GameObject _blue;
    [SerializeField] private GameObject _red;
    
    [SerializeField] private TextMeshProUGUI _nickName;

    public void SetNickName(string nickName)
    {
        _nickName.text = nickName;
    }

    public void SetActive(PlayerType type)
    {
        if (type == PlayerType.Blue)
        {
            _blue.SetActive(true);
            _red.SetActive(false);
        }
        else
        {
            _blue.SetActive(false);
            _red.SetActive(true);
        }
    }
}
