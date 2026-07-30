using System.Collections;
using TMPro;
using UnityEngine;

public class UIInGameMessagesHandler : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI[] _messageText;
    
    private Queue _messageQueue = new Queue();

    public void OnGameMessageReceived(string msg)
    {
        Debug.Log($"<color=cyan>[UIInGame Messages]</color>: {msg}");
        
        _messageQueue.Enqueue(msg);
        
        if (_messageQueue.Count > 3)
            _messageQueue.Dequeue();

        int queueIndex = 0;

        foreach (string msgInQueue in _messageQueue)
        {
            _messageText[queueIndex].text = msgInQueue;
            queueIndex++;
        }
    }
}
