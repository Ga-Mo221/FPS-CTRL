using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPlayerScore : MonoBehaviour
{
    [TabGroup("UI", "Background")]
    [SerializeField] private Image _bg;
    [TabGroup("UI", "Background")]
    [SerializeField] private Sprite _topRankSprite;
    [TabGroup("UI", "Background")]
    [SerializeField] private Sprite _yourSprite;
    [TabGroup("UI", "Background")]
    [SerializeField] private Sprite _opponentSprite;

    [TabGroup("UI", "Player")] 
    [SerializeField] private UIPlayerScoreData _rank_text;
    [TabGroup("UI", "Player")] 
    [SerializeField] private UIPlayerScoreData _name_text;
    [TabGroup("UI", "Player")] 
    [SerializeField] private UIPlayerScoreData _kills_text;
    [TabGroup("UI", "Player")] 
    [SerializeField] private UIPlayerScoreData _deadcount_text;
    [TabGroup("UI", "Player")] 
    [SerializeField] private UIPlayerScoreData _assist_text;

    [Button]
    public void SetPlayer(int rank, string name, int kills, int deadcount, int assist, bool isLocalPlayer)
    {
        if (rank == 0)
        {
            _bg.sprite = _opponentSprite;
            _rank_text.Text.text = "";
            _name_text.Text.text = "";
            _kills_text.Text.text = "";
            _deadcount_text.Text.text = "";
            _assist_text.Text.text = "";
            return;
        }
        _rank_text.Text.text = rank.ToString();
        _name_text.Text.text = name;
        _kills_text.Text.text = kills.ToString();
        _deadcount_text.Text.text = deadcount.ToString();
        _assist_text.Text.text = assist.ToString();
        
        if (GameManager.Instance != null && GameManager.Instance.GetGamePlayMode() == GamePlayMode.Solo)
        {
            if (isLocalPlayer)
            {
                _bg.sprite = _yourSprite;
                SetColor(2);
            }
            else if (rank == 1)
            {
                _bg.sprite = _topRankSprite;
                SetColor(1);
            }
            else
            {
                _bg.sprite = _opponentSprite;
                SetColor(0);
            }
        }
        else
        {
            if (isLocalPlayer)
            {
                _bg.sprite = _yourSprite;
                SetColor(2);
            }
            else
            {
                _bg.sprite = _opponentSprite;
                SetColor(0);
            }
        }
    }

    private void SetColor(int tag)
    {
        _rank_text.SetColor(tag);
        _name_text.SetColor(tag);
        _kills_text.SetColor(tag);
        _deadcount_text.SetColor(tag);
        _assist_text.SetColor(tag);
    }
}

[System.Serializable]
public class UIPlayerScoreData
{
    public TextMeshProUGUI Text;
    [SerializeField] private Color DefaultColor;
    [SerializeField] private Color TopRankColor;
    [SerializeField] private Color TargetColor;

    
    /// <summary>
    /// Set mau sac cho text
    /// </summary>
    /// <param name="tag"> = 0 là màu mặc định \n = 1 là màu top Rank \n = 2 là màu của bạn</param>
    public void SetColor(int tag = 0)
    {
        switch (tag)
        {
            case 0:
                Text.color = DefaultColor;
                break;
            case 1:
                Text.color = TopRankColor;
                break;
            case 2:
                Text.color = TargetColor;
                break;
            default:
                Text.color = DefaultColor;
                break;
        }
    }
}
