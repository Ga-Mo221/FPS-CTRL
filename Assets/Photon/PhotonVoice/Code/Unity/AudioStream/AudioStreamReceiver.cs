using System;
using System.Collections.Generic;
using UnityEngine;

namespace Photon.Voice
{
    [System.Serializable]
    public class AudioReceptionSettings
    {
        public int AudioJitterBufferMs = 200;
        public int AudioJitterBufferMsHigh = 400;
        public int AudioJitterBufferMsMax = 1000;
    }

    /// <summary>
    /// Basic audio stream receiver
    /// Here for demo/pedagogical purposes: prefer using FusionVoiceClient, UnityVoiceClient, ... aka the main VoiceConnection subclasses that directly manage the audio stream (they manage recorders, offer a deeper app life cycle management, ...)
    /// </summary>
    public class AudioStreamReceiver : StreamTransportManagerListener, IStreamReceiver
    {
        public AudioReceptionSettings audioReceptionSettings = new AudioReceptionSettings();
        public IEnumerable<IAudioOut<float>> AudioPlayers { get { return audioPlayers; } }
        protected List<IAudioOut<float>> audioPlayers = new List<IAudioOut<float>>();

        #region StreamTransportManagerListener
        protected override void OnStreamTransportManagerFound()
        {
            _streamTransportManager.RegisterStreamReceiver(this);
        }
        #endregion

        #region Monobehaviour
        protected virtual void Update()
        {
            StreamService();
        }
        #endregion

        protected virtual void StreamService()
        {
            // Important to call regularly to drive encoding / decoding of audio
            foreach (var audioOut in AudioPlayers)
            {
                audioOut.Service();
            }
        }

        public virtual void OnRemoteVoiceAdd(int channelId, int playerId, byte voiceId, VoiceInfo i, ref RemoteVoiceOptions options)
        {
            if (isActiveAndEnabled == false)
            {
                return;
            }

            if (i.Codec.IsAudio())
            {
                var audioSourceGameObject = new GameObject($"RemoteAudio-{playerId}");
                var audioSource = audioSourceGameObject.AddComponent<AudioSource>();
                var pdc = Photon.Voice.Unity.UnityAudioOut.PlayDelayConfig.Default;
                pdc.Delay = this.audioReceptionSettings.AudioJitterBufferMs;

                var audioPlayer = Platform.CreateUnityAudioOut(audioSource, pdc, _logger, "[AudioStreamReceiver] PhotonVoiceSpeaker:", true);

                audioPlayer.Start(i.SamplingRate, i.Channels, i.FrameDurationSamples);
                this.audioPlayers.Add(audioPlayer);
                options.SetOutput(frame => audioPlayer.Push(frame.Buf));
                options.OnRemoteVoiceRemoveAction = () =>
                {
                    this.audioPlayers.Remove(audioPlayer);
                    audioPlayer.Stop();
                    Destroy(audioSourceGameObject);
                };
            }
        }
    }
}
