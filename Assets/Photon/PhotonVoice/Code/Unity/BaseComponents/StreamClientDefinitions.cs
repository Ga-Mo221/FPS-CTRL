using Photon.Realtime;
using UnityEngine;

#if PHOTON_VOICE_R5
using StreamTransport = Photon.Voice.Realtime5Transport;
#else
using StreamTransport = Photon.Voice.LoadBalancingTransport;
#endif

namespace Photon.Voice
{
    /// <summary>
    /// Interface for components capable of providing a LoadBalancingTransport for an IStreamSender and/or IStreamReceiver
    /// Those components should forward the stream callbacks to their registered listeners:
    /// - OnRoomJoined / OnLeftRoom to IStreamSender
    /// - OnRemoteVoiceAdd to IStreamReceiver
    public interface IStreamTransportManager
    {
        public StreamTransport Transport { get; }
        public LogLevel StreamLogLevel() => Voice.LogLevel.Info;

        public void RegisterStreamSender(IStreamSender streamSender);
        public void RegisterStreamReceiver(IStreamReceiver streamReceiver);

        #region Statistic
#if PHOTON_VOICE_R5
        /// <summary>
        /// Time until a reliable command is acknowledged by the server.
        /// </summary>
        public long RoundTripTime => Transport?.RealtimePeer?.Stats?.RoundtripTime ?? 0;

        /// <summary>
        /// Count of sent bytes (excluding all transport layer headers).
        /// </summary>
        public long BytesOut => Transport?.RealtimePeer?.Stats?.BytesOut ?? 0;

        /// <summary>
        /// Count of sent packages / datagrams.
        /// </summary>
        public long PacketsOut => Transport?.RealtimePeer?.Stats?.PackagesOut ?? 0;

        /// <summary>
        /// Count of received bytes (excluding all transport layer headers).
        /// </summary>
        public long BytesIn => Transport?.RealtimePeer?.Stats?.BytesIn ?? 0;

        /// <summary>
        /// Count of received packages / datagrams
        /// </summary>
        public long PacketsIn => Transport?.RealtimePeer?.Stats?.PackagesIn ?? 0;
#else
        public long RoundTripTime => (long)(Transport?.LoadBalancingPeer?.RoundTripTime ?? 0);
        public long BytesOut => Transport?.LoadBalancingPeer?.BytesOut ?? 0;
        public long PacketsOut => Transport?.LoadBalancingPeer?.TrafficStatsOutgoing?.TotalPacketCount ?? 0;
        public long BytesIn => Transport?.LoadBalancingPeer?.BytesIn ?? 0;
        public long PacketsIn => Transport?.LoadBalancingPeer?.TrafficStatsIncoming?.TotalPacketCount ?? 0;
#endif
        #endregion
    }

    /// <summary>
    /// Interface for component relying on a LoadBalancingTransport to send a stream of data (audio, vidoe, ...)
    /// </summary>
    public interface IStreamSender
    {
        /// <summary>
        /// Called when the Photon Voice room is joined, and creating streams (voice) is possible
        /// </summary>
        public void OnRoomJoined();
        public void OnLeftRoom();
    }

    /// <summary>
    /// Interface for component relying on a LoadBalancingTransport to receive a stream of data (audio, vidoe, ...)
    /// </summary>
    public interface IStreamReceiver
    {
        /// <summary>
        /// Called when a remote stream (voice) has been detected
        /// </summary>
        public void OnRemoteVoiceAdd(int channelId, int playerId, byte voiceId, VoiceInfo i, ref RemoteVoiceOptions options);
    }

    public enum WebcamSelectionMode
    {
        WebcamIndex,
        Front,
        Back,
    }
}