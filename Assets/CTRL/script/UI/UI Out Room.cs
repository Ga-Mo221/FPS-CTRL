using System;
using UnityEngine;

public class UIOutRoom : MonoBehaviour
{
    [SerializeField] private GameObject _outRoom;
    private bool _lastOnUI;
    private void Update()
    {
        if (_lastOnUI != GameManager.Instance.OnUI)
        {
            _lastOnUI = GameManager.Instance.OnUI;
            _outRoom.SetActive(_lastOnUI);
        }
    }

    public void LeaveRoom()
    {
        GameManager.Instance.NetworkRunnerHandler.LeaveRoom();
    }
}
