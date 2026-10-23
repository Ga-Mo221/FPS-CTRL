using Photon.Realtime;
using System.Collections.Generic;
using UnityEngine;

#if PHOTON_VOICE_R5
using Photon.Client;
using PhotonJoinRandomRoomArgs = Photon.Realtime.JoinRandomRoomArgs;
using PhotonEnterRoomArgs = Photon.Realtime.EnterRoomArgs;
#else
using PhotonJoinRandomRoomArgs = Photon.Realtime.OpJoinRandomRoomParams;
using PhotonEnterRoomArgs = Photon.Realtime.EnterRoomParams;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
#endif


namespace Photon.Voice
{
    [System.Serializable]
    public struct StringSessionProperty
    {
        public string propertyName;
        public string value;
    }

    [System.Serializable]
    public class StreamMatchmakingSettings
    {
        public int maxPlayers = 20;
        [Tooltip("If not empty, the following properties will be required in the matchmaking (they are added to OpJoinRandomRoomParams.ExpectedCustomRoomProperties, EnterRoomParams.CustomRoomPropertiesForLobby and EnterRoomParams.CustomRoomProperties)")]
        public List<StringSessionProperty> matchmakingSessionProperties = new List<StringSessionProperty>();

        
        public virtual PhotonJoinRandomRoomArgs RandomRoomParams()
        {
            PhotonJoinRandomRoomArgs randomRoomParams = null;
            if (matchmakingSessionProperties != null && matchmakingSessionProperties.Count > 0)
            {
                var expectedRoomProperties = new PhotonHashtable();
                foreach (var property in matchmakingSessionProperties)
                {
                    expectedRoomProperties.Add(property.propertyName, property.value);
                }
                if (randomRoomParams == null)
                    randomRoomParams = new PhotonJoinRandomRoomArgs();
                randomRoomParams.ExpectedCustomRoomProperties = expectedRoomProperties;
            }
            return randomRoomParams;
        }

        public virtual PhotonEnterRoomArgs EnterRoomParams()
        {
            var roomParams = new PhotonEnterRoomArgs()
            {
                RoomOptions = new RoomOptions() { MaxPlayers = maxPlayers }
            };
            if (matchmakingSessionProperties != null && matchmakingSessionProperties.Count > 0)
            {
                var customRoomProperties = new PhotonHashtable();
                var customRoomPropertiesForLobby = new List<string>();
                foreach (var property in matchmakingSessionProperties)
                {
                    customRoomProperties.Add(property.propertyName, property.value);
                    customRoomPropertiesForLobby.Add(property.propertyName);
                }
                roomParams.RoomOptions.CustomRoomProperties = customRoomProperties;
                roomParams.RoomOptions.CustomRoomPropertiesForLobby = customRoomPropertiesForLobby.ToArray();
            }
            return roomParams;
        }
    }
}
