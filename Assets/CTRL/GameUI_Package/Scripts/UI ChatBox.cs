using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

public class UIChatBox : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _messageText;
    [SerializeField] private TMP_InputField _inputField;
    private StringBuilder chatTextBuilder = new StringBuilder();

    private GameObject _messageParrent;
    private GameObject _inputParrent;

    private Coroutine _hideMessageBox;

    private void Awake()
    {
        _messageParrent = _messageText.transform.parent.parent.gameObject;
        _inputParrent = _inputField.transform.parent.gameObject;
        
        _inputField.text = "";
        _messageText.text = "";
        //gioi han o nhap de tin khong vuot payload RPC
        _inputField.characterLimit = NInGameMessagesHandler.MAX_CHAT_CHARS;
    }
    
    public string GetMessage() => _inputField.text;

    public void SendChatMessage(string message)
    {
        chatTextBuilder.Append(message).AppendLine();
        _messageParrent.SetActive(true);
        _messageText.SetText(chatTextBuilder.ToString()); // ← thêm .ToString()
    
        if (_hideMessageBox != null)
            StopCoroutine(_hideMessageBox);
    
        _hideMessageBox = StartCoroutine(HideMessageBox());
    }

    public bool SendMessageVisible(bool visible)
    {
        // Trả về trạng thái Ô NHẬP (đang gõ hay không). Trước đây trả về khung lịch sử chat,
        // mà khung đó tự ẩn sau 5s → gõ lâu hơn 5s rồi Enter thì tin bị bỏ âm thầm.
        bool lastvisible = _inputParrent.activeSelf;
        
        if (visible)
        {
            _inputParrent.SetActive(true);
            _messageParrent.SetActive(true);
            _inputField.text = "";
            _inputField.ActivateInputField();
            
            if (_hideMessageBox != null)
                StopCoroutine(_hideMessageBox);
        
            _hideMessageBox = StartCoroutine(HideMessageBox());
        }
        else _inputParrent.SetActive(false);
        
        return lastvisible;
    }

    private IEnumerator HideMessageBox()
    {
        yield return new WaitForSeconds(5f);
        // Đang gõ thì giữ khung lịch sử, chỉ ẩn sau khi đóng ô nhập.
        while (_inputParrent.activeSelf)
            yield return null;
        _messageParrent.SetActive(false);
        _hideMessageBox = null;
    }
}
