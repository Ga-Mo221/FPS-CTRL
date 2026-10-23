using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
#if PHOTON_VOICE_R5
using StreamTransport = Photon.Voice.Realtime5Transport;
#else
using StreamTransport = Photon.Voice.LoadBalancingTransport;
#endif

namespace Photon.Voice
{
    /// <summary>
    /// Base class for stream senders/receivers which rely on a IRegisterableStreamTransportManager to provide the underlying LoadBalancingTransport upon which the stream run
    /// Automatically tries to find _streamTransportManager IRegisterableStreamTransportManager, through StreamTransportObserver.FindStreamTransportManager
    /// 
    /// OnStreamTransportManagerFound is called when the IRegisterableStreamTransportManager is found
    /// </summary>
    public abstract class StreamTransportManagerListener : MonoBehaviour, IStreamTransportManagerListener
    {
        [Header("Voice connection detection (automatic)")]
        [Tooltip("GameObject storing the component handling the PhotonVoice transport (either a IStreamTransportManager or a VoiceConnection like FusionVoiceClient). If not set, the current game object will be used. Will look otherwise for a VoiceConnection in the scene")]
        public GameObject streamTransportGameObject = null; 
        
        protected IStreamTransportManager _streamTransportManager = null;
        protected Photon.Voice.Unity.Logger _logger = new Photon.Voice.Unity.Logger(LogLevel.Info);

        #region IStreamTransportManagerListener
        public GameObject StreamTransportGameObject
        {
            get => streamTransportGameObject;
            set => streamTransportGameObject = value;
        }
        public IStreamTransportManager StreamTransportManager { get => _streamTransportManager; set => _streamTransportManager = value; }
        #endregion

        public StreamTransport Transport => _streamTransportManager.Transport;

        protected virtual void Awake()
        {
            StartCoroutine(FindStreamTransportManagerCoroutine());
        }

        protected virtual void OnDestroy()
        {
            if (_streamTransportManager is StreamTransportObserver disposable)
                disposable.Dispose();
        }

        /// <summary>
        /// Try to find a IRegisterableStreamTransportManager, using StreamTransportObserver.FindStreamTransportManager, that will fill the _streamTransportManager field if found
        /// Retries a few times (around 10s) if not found, before giving up
        /// </summary>
        /// <returns></returns>
        protected virtual IEnumerator FindStreamTransportManagerCoroutine() 
        { 
            if (streamTransportGameObject == null)
                streamTransportGameObject = gameObject;
            var defaultStreamTransportGameObject = streamTransportGameObject;
            int watchdog = 100;
            while(_streamTransportManager == null && watchdog > 0)
            {
                // In case of failure, FindStreamTransportManager will reset the streamTransportGameObject property (as we do not change the changeStreamTransportGameObjectWithResult param here),
                //  so we need to restore it between attempts
                streamTransportGameObject = defaultStreamTransportGameObject;
                yield return StreamTransportObserver.FindStreamTransportManager(this, logFailure: false);
                if (_streamTransportManager == null)
                {
                    yield return new WaitForSeconds(0.1f);
                }
                watchdog--;
            }
            if (_streamTransportManager != null)
            {
                _logger = new Photon.Voice.Unity.Logger(_streamTransportManager.StreamLogLevel());
                OnStreamTransportManagerFound();
            }
            else
            {
                Debug.LogError($"[{GetType().Name}] Unable to find a suitable transport provider with StreamTransportObserver.FindStreamTransportManager");
            }
        }

        /// <summary>
        /// Called when a _streamTransportManager is set, and so a trnasport can be used
        /// </summary>
        protected abstract void OnStreamTransportManagerFound();
    }

}
