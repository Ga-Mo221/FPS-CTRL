using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Voice.Unity;

using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

namespace Photon.Voice
{

    /// <summary>
    /// Use matchmaking to connect and join a VoiceConnection
    /// </summary>
    [RequireComponent(typeof(VoiceConnection))]
    public class VoiceConnectionMatchmaking : MonoBehaviour, IConnectionCallbacks, IMatchmakingCallbacks
    {
        protected VoiceConnection voiceConnection;

        public bool autoConnect = true;

        public StreamMatchmakingSettings matchmakingSettings = new StreamMatchmakingSettings();

        [Header("Status")]
        [Tooltip("String representation of property of the joined room. String values only: should be used for editor debugging: access voiceConnection.Client.CurrentRoom directly for actual values")]
        public List<StringSessionProperty> roomCustomPropertiesPreview = new List<StringSessionProperty>();

        #region Monobehaviour

        protected virtual void Start()
        {
            this.voiceConnection = this.GetComponent<VoiceConnection>();
            this.voiceConnection.Client.AddCallbackTarget(this);
            if (autoConnect)
            {
                ConnectNow();
            }
        }

        public void ConnectNow()
        {
            this.voiceConnection.ConnectUsingSettings();
        }

        private void OnDestroy()
        {
            this.voiceConnection.Client.RemoveCallbackTarget(this);
        }
        #endregion

        protected virtual AuthenticationValues AuthValues => null;


        #region Room selection logic
        /// <summary>
        /// Join the room once connected to the server. The default implementation relies on random matchmaking, using EnterRoomParams parameters
        /// </summary>
        public virtual void JoinRoom()
        {
            voiceConnection.Client.OpJoinRandomOrCreateRoom(matchmakingSettings.RandomRoomParams(), matchmakingSettings.EnterRoomParams());
        }
        #endregion

        #region Realtime API/transport Callbacks

        #region IConnectionCallbacks

        public virtual void OnConnectedToMaster()
        {
            voiceConnection.Logger.Log(LogLevel.Info, "[{0}] OnConnectedToMaster", GetType().Name);
            JoinRoom();
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
            voiceConnection.Logger.Log(LogLevel.Info, "[{0}] OnJoinedRoom", GetType().Name);
            roomCustomPropertiesPreview.Clear();
            if (voiceConnection.Client.CurrentRoom.CustomProperties != null)
            {
                foreach (var propertyEntry in voiceConnection.Client.CurrentRoom.CustomProperties)
                {
                    roomCustomPropertiesPreview.Add(new StringSessionProperty { propertyName = propertyEntry.Key.ToString(), value = propertyEntry.Value.ToString() });
                }
            }
        }

        public virtual void OnLeftRoom()
        {
            roomCustomPropertiesPreview.Clear();
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

        public void OnDisconnected(DisconnectCause cause)
        {
        }
        #endregion

        #endregion
    }
}
