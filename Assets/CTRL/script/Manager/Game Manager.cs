using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance {set; get;}

    private byte[] connectionToken;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (connectionToken == null)
        {
            connectionToken = NConnectionTokenUtils.NewToken();
            Debug.Log($"<color=yellow>[GameManager]</color> Player Connection token {NConnectionTokenUtils.HashToken(connectionToken)}");
        }
    }

    public void SetConnectionToken(byte[] token)
    {
        connectionToken = token;
    }
    
    public byte[] GetConnectionToken() => connectionToken;
}
