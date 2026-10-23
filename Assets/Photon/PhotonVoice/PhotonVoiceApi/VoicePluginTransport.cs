#if PHOTON_VOICE_PLUGIN_ENABLE

using ExitGames.Client.Photon;
using System.Collections.Generic;
using System.Linq;

namespace Photon.Voice
{
    class VoicePluginEvent : VoiceEvent
    {
        public const byte CommandCode = 220;
    }


    enum CommandEventSubcode : byte
    {
        UpdateMute = 2,
        UpdateProximity = 3,
    }
    enum CommandEventMethod : byte
    {
        Set = 0,
        Remove = 1,
        Add = 2
    }

    public class VoicePluginTransport : LoadBalancingTransport2
    {
        public VoicePluginTransport(ILogger logger = null,
            ConnectionProtocol connectionProtocol = ConnectionProtocol.Udp, bool cppCompatibilityMode = false) : base(
            logger, connectionProtocol, cppCompatibilityMode)
        {
        }

        public void AddToProximity(IEnumerable<int> playersInProximity)
        {
            this.UpdateProximity(playersInProximity, CommandEventMethod.Add);
        }

        public void RemoveFromProximity(IEnumerable<int> playersInProximity)
        {
            this.UpdateProximity(playersInProximity, CommandEventMethod.Remove);
        }

        public void SetProximity(IEnumerable<int> playersInProximity)
        {
            this.UpdateProximity(playersInProximity, CommandEventMethod.Set);
        }

        private void UpdateProximity(IEnumerable<int> playersInProximity, CommandEventMethod method)
        {
            PluginCommand(CommandEventSubcode.UpdateProximity, playersInProximity, method);
        }


        public void AddMuted(IEnumerable<int> playersMuted)
        {
            this.UpdateMuted(playersMuted, CommandEventMethod.Add);
        }

        public void RemoveMuted(IEnumerable<int> playersMuted)
        {
            this.UpdateMuted(playersMuted, CommandEventMethod.Remove);
        }

        public void SetMuted(IEnumerable<int> playersMuted)
        {
            this.UpdateMuted(playersMuted, CommandEventMethod.Set);
        }
        private void UpdateMuted(IEnumerable<int> playersMuted, CommandEventMethod method)

        {
            PluginCommand(CommandEventSubcode.UpdateMute, playersMuted, method);
        }

        private void PluginCommand(CommandEventSubcode subCode, IEnumerable<int> players, CommandEventMethod method)
        {
            var data = new object[3];
            data[0] = (byte)subCode;
            data[1] = players.ToArray();
            data[2] = (byte)method;

            this.OpRaiseEvent(VoicePluginEvent.CommandCode, data, Realtime.RaiseEventOptions.Default, SendOptions.SendReliable);
        }
    }
}
#endif
