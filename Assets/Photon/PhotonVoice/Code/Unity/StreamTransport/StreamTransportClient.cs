using Photon.Realtime;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Events;

#if PHOTON_VOICE_R5
using Photon.Client;
using StreamTransport = Photon.Voice.Realtime5Transport;
#else
using ExitGames.Client.Photon;
using StreamTransport = Photon.Voice.LoadBalancingTransport;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
#endif


namespace Photon.Voice
{
    [System.Serializable]
    public class StreamTransportClientSettings
    {
        public string appId;
        public string appVersion = "1";
        public string region = "";
        public ConnectionProtocol protocol = ConnectionProtocol.Udp;
    }

    /// <summary>
    /// Basic implementation of a IRegisterableStreamTransportManager, initiating a LoadBalancingTransport and forwarding its callbacks to the sender/receiver listeners
    /// Here for demo/pedagogical purposes: prefer using FusionVoiceClient, UnityVoiceClient, ... aka the main VoiceConnection subclasses (that manage recorders, offer a deeper app life cycle management, ...)
    /// </summary>
    public class StreamTransportClient : MonoBehaviour, IStreamTransportManager, IConnectionCallbacks, IMatchmakingCallbacks
    {
        public bool autoStartWithAppSettings = true;

        [Tooltip("Use App Settings from the global VoiceAppSettings (PhotonAppSettings) instead of the local Transport Client Settings")]
        [FormerlySerializedAs("UseVoiceAppSettings")]
        public bool UseSharedAppSettings = false;

        public StreamTransportClientSettings transportClientSettings = new StreamTransportClientSettings();
        public StreamMatchmakingSettings matchmakingSettings = new StreamMatchmakingSettings();
        public LogLevel logLevel = LogLevel.Info;
        protected Photon.Voice.Unity.Logger _logger;

        protected StreamTransport _client = new StreamTransport();
        protected List<IStreamSender> _registeredStreamSenders = new List<IStreamSender>();
        protected List<IStreamReceiver> _registeredStreamReceivers = new List<IStreamReceiver>();

        public UnityEvent onStartConnection = new UnityEvent();

        [Header("Status")]
        public bool isRoomJoined = false;
        public string joinedRoomName = "";
        [Tooltip("String representation of property of the joined room. String values only: should be used for editor debugging: access Transport.CurrentRoom directly for actual values")]
        public List<StringSessionProperty> roomCustomPropertiesPreview = new List<StringSessionProperty>();

#if PHOTON_VOICE_R5
        public ConnectionProtocol Protocol => Transport.RealtimePeer?.TransportProtocol ?? ConnectionProtocol.Udp;
        public ConnectionProtocol UsedProtocol => Transport.RealtimePeer?.UsedProtocol ?? ConnectionProtocol.Udp;

#else
        public ConnectionProtocol Protocol => Transport.LoadBalancingPeer?.TransportProtocol ?? ConnectionProtocol.Udp;
        public ConnectionProtocol UsedProtocol => Transport.LoadBalancingPeer?.UsedProtocol ?? ConnectionProtocol.Udp;

#endif



        #region Monobehaviour
        protected virtual void Awake()
        {
            _logger = new Photon.Voice.Unity.Logger(logLevel);
        }

        protected virtual void Start()
        {
            if (autoStartWithAppSettings)
            {
                StartConnectionWithDefaultSettings();
            }
        }

        protected virtual void Update()
        {
            Service();
        }

        protected virtual void OnApplicationQuit()
        {
            NotifyLeftRoom();
            Disconnect();
        }

        protected virtual void OnDestroy()
        {
            if (Transport != null)
            {
                Transport.RemoveCallbackTarget(this);
            }
        }
        #endregion

        [ContextMenu("Disconnect")]
        public virtual void Disconnect()
        {
            if (Transport != null)
            {
                Transport.Disconnect();
            }
        }

        [ContextMenu("StartConnectionWithDefaultSettings")]
        public virtual void StartConnectionWithDefaultSettings()
        {
            StartConnection(transportClientSettings);
        }

        #region Transport logic
        public virtual void StartConnection(StreamTransportClientSettings startVideoClientSettings)
        {
            _logger.Log(LogLevel.Info, "[{0}] StartConnection", GetType().Name);
            transportClientSettings = startVideoClientSettings;

            // Find the AppSettings from either the local settings (priority) or the shared PhotonAppSettings asset.
            AppSettings appSettings = FindAppSettings(startVideoClientSettings);
            StartConnection(appSettings);
        }

        public virtual void StartConnection(AppSettings appSettings)
        { 
            if (appSettings == null)
            {
                // No valid app id available: abort instead of failing later during the connection.
                return;
            }

            #if PHOTON_VOICE_R4 
            Transport.LoadBalancingPeer.TrafficStatsEnabled = true;
            #endif

            Transport.AddCallbackTarget(this);
            var authValues = AuthValues;
            if (authValues != null)
            {
                Transport.AuthValues = AuthValues;
            }

            Transport.VoiceClient.OnRemoteVoiceInfoAction += OnRemoteVoiceAdd;

            Transport.ConnectUsingSettings(appSettings);

            if (onStartConnection != null)
            {
                onStartConnection.Invoke();
            }
        }

        /// <summary>
        /// Find the AppSettings used to connect. 
        /// Behaviour:
        /// - if UseSharedAppSettings == true  => use the global VoiceAppSettings (PhotonAppSettings). If its AppId is empty, log an error and return null.
        /// - if UseSharedAppSettings == false => use transportClientSettings. If its appId is empty, log an error and return null.
        /// </summary>
        /// <returns></returns>
        public virtual AppSettings FindAppSettings()
        {
            return FindAppSettings(transportClientSettings);
        }

        /// <summary>
        /// Find the AppSettings used to connect. 
        /// Behaviour:
        /// - if UseSharedAppSettings == true  => use the global VoiceAppSettings (PhotonAppSettings). If its AppId is empty, log an error and return null.
        /// - if UseSharedAppSettings == false => use the local startVideoClientSettings. If its appId is empty, log an error and return null.
        /// </summary>
        public virtual AppSettings FindAppSettings(StreamTransportClientSettings startVideoClientSettings)
        {
            if (UseSharedAppSettings)
            {
                // The getter auto-creates the asset in the editor if it is missing.
                PhotonAppSettings photonAppSettings = PhotonAppSettings.Instance;
                bool photonAppSettingsConfigured = photonAppSettings != null && photonAppSettings.AppSettings != null && string.IsNullOrEmpty(photonAppSettings.AppSettings.AppIdVoiceOrVideo) == false;

                if (photonAppSettingsConfigured == false)
                {
                    Debug.LogError($"[{GetType().Name}] Use Voice App Settings is enabled but the global VoiceAppSettings (PhotonAppSettings) is not configured. Set a valid Photon Voice app id in the VoiceAppSettings asset.");
                    return null;
                }

                _logger.Log(LogLevel.Debug, "[{0}] Connecting with the global VoiceAppSettings (appVersion={1}, region='{2}').", GetType().Name, photonAppSettings.AppSettings.AppVersion, photonAppSettings.AppSettings.FixedRegion);
                return photonAppSettings.AppSettings;
            }

            // Use the local transport client settings.
            if (string.IsNullOrEmpty(startVideoClientSettings.appId))
            {
                Debug.LogError($"[{GetType().Name}] Missing app id in the local Transport Client Settings. Set a valid Photon Voice app id, or enable Use Voice App Settings to use the global VoiceAppSettings.");
                return null;
            }

            _logger.Log(LogLevel.Debug, "[{0}] Connecting with the local Transport Client Settings (appVersion={1}, region='{2}').", GetType().Name, startVideoClientSettings.appVersion, startVideoClientSettings.region);
            return new AppSettings()
            {
                AppIdVoice = startVideoClientSettings.appId,
                AppIdVideo = startVideoClientSettings.appId,
                AppVersion = startVideoClientSettings.appVersion,
                FixedRegion = startVideoClientSettings.region,
                Protocol = startVideoClientSettings.protocol
            };
        }

        protected virtual AuthenticationValues AuthValues => null;

        protected virtual void Service()
        {
            // Important to call regularly to keep network communication up and drive encoding / decoding of video
            if (Transport != null)
            {
                Transport.Service();
            }
        }
#endregion

        protected virtual void NotifiyJoinedRoom()
        {
            isRoomJoined = true;
            foreach (var streamSender in _registeredStreamSenders)
            {
                streamSender.OnRoomJoined();
            }
        }

        protected virtual void NotifyLeftRoom()
        {
            if (isRoomJoined)
            {
                isRoomJoined = false;
                foreach (var streamSender in _registeredStreamSenders)
                {
                    streamSender.OnLeftRoom();
                }
            }
        }

        protected virtual void NotifyRemoteVoiceAdd(int channelId, int playerId, byte voiceId, VoiceInfo i, ref RemoteVoiceOptions options)
        {
            foreach (var receiver in _registeredStreamReceivers)
            {
                receiver.OnRemoteVoiceAdd(channelId, playerId, voiceId, i, ref options);
            }
        }

        protected virtual void CheckJoinStatus()
        {
            if (isRoomJoined == false && Transport != null && Transport.State == Realtime.ClientState.Joined)
            {
                isRoomJoined = true;
            }
        }

        #region IRegisterableStreamTransportManager
        public StreamTransport Transport { get { return this._client; } }

        public LogLevel StreamLogLevel() => logLevel;

        public virtual void RegisterStreamSender(IStreamSender streamSender)
        {
            if (_registeredStreamSenders.Contains(streamSender) == false)
            {
                _registeredStreamSenders.Add(streamSender);
                CheckJoinStatus();
                if (isRoomJoined)
                {
                    streamSender.OnRoomJoined();
                }
            }
        }

        public virtual void RegisterStreamReceiver(IStreamReceiver streamReceiver)
        {
            if (_registeredStreamReceivers.Contains(streamReceiver) == false)
            {
                _registeredStreamReceivers.Add(streamReceiver);
            }
        }
        #endregion

        // Called by VoiceClient for every new audio or video stream
        protected virtual void OnRemoteVoiceAdd(int channelId, int playerId, byte voiceId, VoiceInfo i, ref RemoteVoiceOptions options)
        {
            NotifyRemoteVoiceAdd(channelId, playerId, voiceId, i, ref options);
        }

        #region Room selection logic
        /// <summary>
        /// Join the room once connected to the server. The default implementation relies on random matchmaking, using EnterRoomParams parameters
        /// </summary>
        public virtual void JoinRoom()
        {
            Transport.OpJoinRandomOrCreateRoom(matchmakingSettings.RandomRoomParams(), matchmakingSettings.EnterRoomParams());
        }

        #endregion

        #region Realtime API/transport Callbacks

        #region IConnectionCallbacks

        public virtual void OnConnectedToMaster()
        {
            _logger.Log(LogLevel.Info, "[{0}] OnConnectedToMaster", GetType().Name);
            JoinRoom();
        }

        public virtual void OnDisconnected(DisconnectCause cause)
        {
            NotifyLeftRoom();
        }
        #endregion

        #region IConnectionCallbacks (unused here)
        public virtual void OnConnected()
        {
        }

        public virtual void OnRegionListReceived(RegionHandler regionHandler)
        {
        }

        public virtual void OnCustomAuthenticationFailed(string debugMessage)
        {
        }
        public virtual void OnCustomAuthenticationResponse(Dictionary<string, object> data)
        {
        }
        #endregion

        #region IMatchmakingCallbacks

        public virtual void OnJoinedRoom()
        {
            _logger.Log(LogLevel.Info, "[{0}] OnJoinedRoom", GetType().Name);
            joinedRoomName = Transport.CurrentRoom.Name;
            roomCustomPropertiesPreview.Clear();
            if (Transport.CurrentRoom.CustomProperties != null)
            {
                foreach (var propertyEntry in Transport.CurrentRoom.CustomProperties)
                {
                    roomCustomPropertiesPreview.Add(new StringSessionProperty { propertyName = propertyEntry.Key.ToString(), value = propertyEntry.Value.ToString() });
                }
            }
            NotifiyJoinedRoom();
        }

        public virtual void OnLeftRoom()
        {
            joinedRoomName = "";
            roomCustomPropertiesPreview.Clear();
            NotifyLeftRoom();
        }
        #endregion

        #region IMatchmakingCallbacks (unsed here)
        public virtual void OnFriendListUpdate(List<FriendInfo> friendList)
        {
        }

        public virtual void OnCreatedRoom()
        {
        }

        public virtual void OnCreateRoomFailed(short returnCode, string message)
        {
            Debug.LogError($"[{GetType().Name}] OnCreateRoomFailed {returnCode}: {message}");
        }

        public virtual void OnJoinRoomFailed(short returnCode, string message)
        {
            Debug.LogError($"[{GetType().Name}] OnJoinRoomFailed {returnCode}: {message}");
        }

        public virtual void OnJoinRandomFailed(short returnCode, string message)
        {
            Debug.LogError($"[{GetType().Name}] OnJoinRandomFailed {returnCode}: {message}");
        }
        #endregion

        #endregion
    }

}