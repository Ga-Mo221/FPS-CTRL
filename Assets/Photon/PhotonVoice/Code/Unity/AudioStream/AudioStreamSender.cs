using System;
using System.Collections;
using UnityEngine;

namespace Photon.Voice
{
    [System.Serializable]
    public class AudioSenderSettings
    {
        public const POpusCodec.Enums.SamplingRate AudioEncoderSamplingRate = POpusCodec.Enums.SamplingRate.Sampling24000;
        public const int AudioMicSamplingRate = 24000;
        public int AudioBitrate = 30000;
        public bool AudioEcho = false;
    }

    /// <summary>
    /// Basic audio stream sender
    /// Here for demo/pedagogical purposes: prefer using FusionVoiceClient, UnityVoiceClient, ... aka the main VoiceConnection subclasses that directly manage the audio stream (they manage recorders, offer a deeper app life cycle management, ...)
    /// </summary>
    public class AudioStreamSender : StreamTransportManagerListener, IStreamSender
    {
        public AudioSenderSettings audioSenderSettings = new AudioSenderSettings();
        public bool sendOnJoinRoom = true;

        public enum Status
        {
            NotSending,
            WaitingForRoomJoin,
            WaitingForRecordingToBePossible,
            Sending,
            FailedToSend,
        }

        [Header("Sender status")]
        public Status status = Status.NotSending;

        LocalVoice _localVoice = null;
        protected bool _shouldSend = false;
        protected bool _roomJoined = false;

        // Default implementaiton will use a single channel for the audio for the source and the stream. Most microphones are mono and for a webcam chat this is fine.
        protected virtual int AudioChannels => 1;

        // Separate media in channels for better Photon transport performance: 1 for audio, 2 for video
        protected int AudioTransportChannel => 1;

        protected Photon.Voice.DeviceInfo _micDevice = Photon.Voice.DeviceInfo.Default;

        protected IAudioDesc _audioSource;
        protected bool _permissionGranted = false;
        public virtual bool IsRecordingPossible() => _permissionGranted;
        public virtual float MaxWaitTimeforCollectingAvailability => 0f;

        #region StreamTransportManagerListener
        protected override void OnStreamTransportManagerFound()
        {
            _streamTransportManager.RegisterStreamSender(this);
        }
        #endregion

        protected virtual void Update()
        {
            if (_localVoice != null)
            {
                _localVoice.DebugEchoMode = this.audioSenderSettings.AudioEcho;

                // it is typically fine to keep the following settings at their default. shown here, as this is a place where this can be set for testing
                //this.LocalAudioStream.localVoice.TransmitEnabled = true;
                //this.LocalAudioStream.localVoice.Reliable = false;
                //this.LocalAudioStream.localVoice.Encrypt = false;
                //this.LocalAudioStream.localVoice.Fragment = false;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            RemoveLocalStreamAudio();
        }

        [ContextMenu("StartSending")]
        public virtual void StartSending()
        {
            if (_localVoice != null)
            {
                RemoveLocalStreamAudio();
            }

            status = Status.WaitingForRoomJoin;
            _shouldSend = true;
            
            if (_roomJoined)
            {
                CreateLocalStreamAudio();
            }
        }

        [ContextMenu("StopSending")]
        public virtual void StopSending()
        {
            _shouldSend = false;
            RemoveLocalStreamAudio();
        }

        [ContextMenu("ToggleSending")]
        public virtual void ToggleSending()
        {
            if (_shouldSend)
            {
                StopSending();
            }
            else
            {
                StartSending();
            }
        }

        public virtual void OnRoomJoined()
        {
            if (isActiveAndEnabled == false)
            {
                return;
            }

            _roomJoined = true;
            if (sendOnJoinRoom || _shouldSend)
            {
                CreateLocalStreamAudio();
            }
        }

        protected void CreateLocalStreamAudio()
        {
            RemoveLocalStreamAudio();
            StartCoroutine(CreateStreamCoroutine());
        }

        protected void RemoveLocalStreamAudio()
        {
            if (_localVoice != null)
            {
                _localVoice?.RemoveSelf();
                _audioSource?.Dispose();
                _localVoice = null;
                _audioSource = null;
            }
            status = Status.NotSending;
        }

        protected void CreateStream()
        {
            VoiceInfo voiceInfo = VoiceInfo.CreateAudioOpus(AudioSenderSettings.AudioEncoderSamplingRate, AudioChannels, OpusCodec.FrameDuration.Frame60ms, audioSenderSettings.AudioBitrate);

            _audioSource = Platform.CreateDefaultAudioSource(_logger, _micDevice, AudioSenderSettings.AudioMicSamplingRate, AudioChannels);

            _localVoice = Transport.VoiceClient.CreateLocalVoiceAudioFromSource(voiceInfo, _audioSource, AudioSampleType.Source, AudioTransportChannel);
            status = Status.Sending;

            Debug.LogFormat("[AudioStreamSender] Audio source created: {0}", _audioSource.GetType());
        }

        public virtual void OnLeftRoom()
        {
            RemoveLocalStreamAudio();
        }

        #region Recording capability check
        /// <summary>
        /// Check if collecting is possible (by testing IsRecordingPossible()) before starting the recording
        /// If the recording is not possible for MaxWaitTimeforCollectingAvailability seconds, displays an error and gives up
        /// </summary>
        protected virtual IEnumerator CreateStreamCoroutine()
        {
            status = Status.WaitingForRecordingToBePossible;
            WillTrytoRecord();
            if (IsRecordingPossible() == false)
            {
                yield return WaitForRecordingBeingPossible();
            }

            if (IsRecordingPossible())
            {
                CreateStream();
            }
            else
            {
                Debug.LogError($"Impossible to create audio stream: {GetType().Name} is not currently capable of collecting.");
                status = Status.FailedToSend;
            }
        }

        protected virtual IEnumerator WaitForRecordingBeingPossible()
        {
            float waitingStep = 0.5f;
            int watchDog = (int)(MaxWaitTimeforCollectingAvailability / waitingStep);
            while (IsRecordingPossible() == false && (MaxWaitTimeforCollectingAvailability == 0 || watchDog > 0))
            {
                Debug.Log($"Waiting for {GetType().Name} recording to be possible ...");
                yield return new WaitForSeconds(waitingStep);
                if(MaxWaitTimeforCollectingAvailability > 0)
                {
                    watchDog--;
                }
            }
        }

        protected virtual void WillTrytoRecord()
        {
            AskForPermissions();
        }
        #endregion

        #region Permissions
        protected virtual void AskForPermissions()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            PermissionsRequester.SharedInstance.AddPermissionRequest(UnityEngine.Android.Permission.Microphone, permissionCallback: OnPermissionGranted);
#elif UNITY_WEBGL
            // No need to ask for permission: the Photon Voice SDK handles it automatically on webGL
            OnPermissionGranted(true);
#else
            PermissionsRequester.SharedInstance.AddAuthorizationRequest(UserAuthorization.Microphone, permissionCallback: OnPermissionGranted);
#endif
        }
        protected virtual void OnPermissionGranted(bool granted)
        {
            Debug.Log($"[{GetType().Name}] OnPermissiongranted: {granted}");
            _permissionGranted = granted;
        }
        #endregion
    }

}
