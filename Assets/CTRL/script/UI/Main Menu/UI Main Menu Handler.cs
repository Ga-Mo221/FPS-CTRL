using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIMainMenuHandler : MonoBehaviour
{
    [SerializeField] private TMP_InputField _inputField;

    private void Start()
    {
        if (PlayerPrefs.HasKey("PlayerNickName"))
        {
            _inputField.text = PlayerPrefs.GetString("PlayerNickName");
        }
    }

    public void OnJoinGameClicked()
    {
        PlayerPrefs.SetString("PlayerNickName", _inputField.text);
        PlayerPrefs.Save();

        SceneManager.LoadScene("SampleScene");
    }
}
