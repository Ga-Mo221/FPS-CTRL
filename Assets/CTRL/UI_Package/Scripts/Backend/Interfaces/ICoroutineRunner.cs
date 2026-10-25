using System.Collections;
using UnityEngine;

namespace Backend.Interfaces
{
    /// <summary>
    /// Cho phép service thuần (không phải MonoBehaviour) chạy coroutine để poll.
    /// BackendBootstrap (MonoBehaviour, DontDestroyOnLoad) cài đặt interface này.
    /// </summary>
    public interface ICoroutineRunner
    {
        Coroutine Run(IEnumerator routine);
        void Stop(Coroutine routine);
    }
}
