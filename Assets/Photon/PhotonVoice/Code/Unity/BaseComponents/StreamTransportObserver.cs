using Photon.Realtime;
using Photon.Voice.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if PHOTON_VOICE_R5
using StreamTransport = Photon.Voice.Realtime5Transport;
#else
using StreamTransport = Photon.Voice.LoadBalancingTransport;
#endif

namespace Photon.Voice
{
    public interface IStreamTransportManagerListener
    {
        public GameObject gameObject { get; }
        public GameObject StreamTransportGameObject { get; set;  }
        public IStreamTransportManager StreamTransportManager { get; set; }
    }

    /// <summary>
    /// Wrapper handling LoadBalancingTransport callbacks for a given transport,
    ///  to be used as a wrapper around component handling a transport but not compatible with the IRegisterableStreamTransportManager interface
    ///  
    /// Provide the StreamTransportObserver.FindStreamTransportManager helper, that will, for a given game object:
    /// 1. find a IRegisterableStreamTransportManager
    /// 2. or wrap a IStreamTransportManager in its children to enhance it with callback handling
    /// 3. or, wrap a VoiceConnection (like FusionVoiceClient), if any in the children
    /// </summary>
    public class StreamTransportObserver : IStreamTransportManager, IDisposable, IConnectionCallbacks, IMatchmakingCallbacks
    {
        protected List<IStreamSender> _registeredStreamSenders = new List<IStreamSender>();
        protected List<IStreamReceiver> _registeredStreamReceivers = new List<IStreamReceiver>();

        public bool isRoomJoined = false;

        #region IStreamTransportManager
        public StreamTransport Transport { get; set; } = null;
        protected LogLevel _logLevel = LogLevel.Info;
        public LogLevel StreamLogLevel() => _logLevel;

        public StreamTransportObserver(StreamTransport transport, LogLevel logLevel = LogLevel.Info)
        {
            Transport = transport;
            _logLevel = logLevel;
            RegisterOnTransport();
        }

        public void RegisterOnTransport()
        {
            Transport.AddCallbackTarget(this);
            Transport.VoiceClient.OnRemoteVoiceInfoAction += OnRemoteVoiceAdd;
        }

        protected virtual void OnRemoteVoiceAdd(int channelId, int playerId, byte voiceId, VoiceInfo voiceInfo, ref RemoteVoiceOptions options)
        {
            NotifyRemoteVoiceAdd(channelId, playerId, voiceId, voiceInfo, ref options);
        }

        public void RegisterStreamSender(IStreamSender streamSender)
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

        public void RegisterStreamReceiver(IStreamReceiver streamReceiver)
        {
            if (_registeredStreamReceivers.Contains(streamReceiver) == false)
            {
                _registeredStreamReceivers.Add(streamReceiver);
            }
        }
#endregion

        protected void NotifiyJoinedRoom()
        {
            isRoomJoined = true;
            foreach (var streamSender in _registeredStreamSenders)
            {
                streamSender.OnRoomJoined();
            }
        }

        protected void NotifyLeftRoom()
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

        #region IDisposable
        public void Dispose()
        {
            if(Transport != null)
            {
                Transport.RemoveCallbackTarget(this);
            }
        }
        #endregion

        #region Realtime API/transport Callbacks 

        #region IConnectionCallbacks

        public void OnDisconnected(DisconnectCause cause)
        {
            NotifyLeftRoom();
        }
        #endregion

        #region IConnectionCallbacks (unused here)
        public void OnConnectedToMaster() { }
        public virtual void OnConnected() { }
        public virtual void OnRegionListReceived(RegionHandler regionHandler) { }
        public virtual void OnCustomAuthenticationFailed(string debugMessage) { }
        public virtual void OnCustomAuthenticationResponse(Dictionary<string, object> data) { }
        #endregion

        #region IMatchmakingCallbacks

        public virtual void OnJoinedRoom()
        {
            NotifiyJoinedRoom();
        }

        public virtual void OnLeftRoom()
        {
            NotifyLeftRoom();
        }
        #endregion

        #region IMatchmakingCallbacks (unsed here)
        public virtual void OnFriendListUpdate(List<FriendInfo> friendList) { }
        public virtual void OnCreatedRoom() { }
        public virtual void OnCreateRoomFailed(short returnCode, string message) { }
        public virtual void OnJoinRoomFailed(short returnCode, string message) { }
        public virtual void OnJoinRandomFailed(short returnCode, string message) {}


        #endregion

        #endregion

        #region Lookup helper
        /// <summary>
        /// Stream Transport Manager finder function, that will, for a given game object:
        /// 1. find a IRegisterableStreamTransportManager
        /// 2. or wrap a IStreamTransportManager in its children to enhance it with callback handling
        /// 3. or, wrap a VoiceConnection (like FusionVoiceClient), if any in the children or in the scene
        /// 4. will look for a StreamTransportClient in the scene
        /// 
        /// For the found manager, if the Transport is not yet set, wait for it to be set before returning
        /// </summary>
        public static IEnumerator FindStreamTransportManager(IStreamTransportManagerListener listener, bool changeStreamTransportGameObjectWithResult = true, bool logFailure = true)
        {
            listener.StreamTransportManager = null;
            if (listener.StreamTransportGameObject == null)
            {
                listener.StreamTransportGameObject = listener.gameObject;
            }
            var streamTransportManager = listener.StreamTransportGameObject.GetComponentInChildren<IStreamTransportManager>();

            // Look for VoiceConnection alternative to wrap
            if (streamTransportManager == null)
            {
                var voiceConnection = listener.StreamTransportGameObject.GetComponentInChildren<VoiceConnection>();

                bool shouldLookForSceneVoiceConnection = false;


                if (voiceConnection == null)
                {
                    shouldLookForSceneVoiceConnection = true;
                }
                while (shouldLookForSceneVoiceConnection)
                {
                    var voiceConnections = GameObject.FindObjectsByType<VoiceConnection>(FindObjectsSortMode.None);
                    if (voiceConnections.Length == 0)
                    {
                        // No VoiceConnection in the scene
                        shouldLookForSceneVoiceConnection = false;
                        break;
                    }

                    // In some case, the voice connection is not really used (for instance FusionVoiceClient on a template NetworkRunner).
                    // We look for a FusionVoiceClient which went further than the PeerCreated ClientState
                    foreach(var sceneVoiceConnection in voiceConnections)
                    {
                        if(sceneVoiceConnection.Client == null || sceneVoiceConnection.ClientState == ClientState.PeerCreated)
                        {
                            // Ignoring for now, still no Client or in PeerCreated  state...
                            continue;
                        }
                        if (sceneVoiceConnection != null)
                        {
                            // We found a proper voice connection
                            voiceConnection = sceneVoiceConnection;
                            shouldLookForSceneVoiceConnection = false;
                            var logLevel = LogLevel.Info;
                            var logger = VoiceLogger.FindLogger(voiceConnection.gameObject);
                            if (logger != null)
                            {
                                logLevel = logger.LogLevel;
                            }
                            // Check if FusionVoiceClient transport is ready before affecting it to a wrapping StreamTransportObserver
                            if (voiceConnection.Client == null)
                            {
                                while (voiceConnection.Client == null)
                                {
                                    yield return new WaitForEndOfFrame();
                                }
                            }
                            streamTransportManager = new StreamTransportObserver(voiceConnection.Client, logLevel);
                            break;
                        }
                    }
                    if (voiceConnection == null)
                    {
                        // Some Voiceconnection were found, but still in PeerCreated state. We wait a bit to check again if any of them progressed during the next iteration
                        yield return new WaitForSeconds(0.1f);
                    }
                    else
                    {
                        shouldLookForSceneVoiceConnection = false;
                        if (changeStreamTransportGameObjectWithResult)
                        {
                            listener.StreamTransportGameObject = voiceConnection.gameObject;
                        }
                        break;
                    }
                }
            }

            if(streamTransportManager == null)
            {
                streamTransportManager = GameObject.FindAnyObjectByType<StreamTransportClient>();
            }

            if (streamTransportManager == null && logFailure)
            {
                Debug.LogError("Should be placed next to a IStreamTransportManager component, or a VoiceConnection should be enabled in the scene");
            }

            // Check if streamTransportManager transport is ready
            if (streamTransportManager != null && streamTransportManager.Transport == null)
            {
                while(streamTransportManager.Transport == null)
                {
                    yield return new WaitForEndOfFrame();
                }
            }

            if (streamTransportManager is IStreamTransportManager registerableManager)
            {
                listener.StreamTransportManager = registerableManager;
            }
            else
            {
                if (changeStreamTransportGameObjectWithResult)
                {
                    // We set the gameobject to null, to make clear that no stream transport connection was found
                    listener.StreamTransportGameObject = null;
                }
            }
        }
        #endregion
    }
}
