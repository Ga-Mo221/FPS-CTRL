//TODO Replace by an automatic define set by Voice
#define PHOTON_VOICE_AVAILABLE

#if PHOTON_VOICE_AVAILABLE

using Photon.Voice.Unity;
#endif
using UnityEngine;
using System.Reflection;

namespace Photon.Voice
{
    /// <summary>
    /// Wait for Microphone access permissions to be available before letting VoiceConnection connect
    /// Note: recordWhenJoined should be disabled on the recorder
    /// 
    /// Should be used alongside a PermissionsRequester, to align permissions on the desired timing (to avoid conflicting requests)
    /// </summary>
#if PHOTON_VOICE_AVAILABLE
    [RequireComponent(typeof(VoiceConnection))]
#endif
    [DefaultExecutionOrder(VoiceConnectionPermissionWaiter.LOWPRIORITY_EXECUTION_ORDER)]
    public class VoiceConnectionPermissionWaiter : PermissionWaiter
    {
        public const int LOWPRIORITY_EXECUTION_ORDER = PermissionWaiter.EXECUTION_ORDER + 50;

#if PHOTON_VOICE_AVAILABLE
        VoiceConnection voiceConnection;
        bool shouldRestartRecording = false;
        bool isPaused = false;
        bool isFocused = true;

        #region PermissionWaiter override 
        // Note: this permission name will be converted for iOS to UserAuthorization by the PermissionRequester
        public override string PermissionName => PermissionsRequester.MicrophoneAuthorizationPermissionName;

        protected override void Awake()
        {
#if UNITY_WEBGL
            // No need to ask for permission: the Photon Voice SDK handles it automatically on webGL
            Destroy(this);
            return;
#else
            voiceConnection = GetComponent<VoiceConnection>();
            base.Awake();
#endif
        }

        protected override void OnPermissionRequired()
        {
            base.OnPermissionRequired();

            // Disable recording, to restart it only when microphone access is granted
            PreventRecordersToStartRecording();
        }

        /// <summary>
        /// Should be called manually (for instance in a PermissionsRequester callback) if checkPermisisonGrantAutomatically is not checked
        /// </summary>
        public override void OnPermissionGranted()
        {
            base.OnPermissionGranted();

            // Reenable recording
            TryRestartRecording();
        }
        #endregion

        void TryRestartRecording()
        {
            if (isPaused == false && isFocused == true)
            {
                RestartRecording();
            }
            else
            {
                // We avoid restart the voice recording while the application is paused, as it may cause issues
                Debug.Log($"[PermissionsRequester-{GetType().Name}] We avoid restarting the voice recording while the application is paused, as it may cause issues");
                shouldRestartRecording = true;
            }
        }

        protected override void Update()
        {
            base.Update();
            if (shouldRestartRecording)
            {
                TryRestartRecording();
            }
        }

        private void OnApplicationPause(bool pause)
        {
            isPaused = pause;
            Debug.Log($"[PermissionsRequester-{GetType().Name}] OnApplicationPause isPaused:{isPaused} / isFocused:{isFocused} / shouldRestartRecording:{shouldRestartRecording}");
        }

        private void OnApplicationFocus(bool focus)
        {
            isFocused = focus;
            Debug.Log($"[PermissionsRequester-{GetType().Name}] OnApplicationFocus isPaused:{isPaused} / isFocused:{isFocused} / shouldRestartRecording:{shouldRestartRecording}");
        }

        void RestartRecording()
        {
            Debug.Log($"[PermissionsRequester-{GetType().Name}] RestartRecording");
            if (voiceConnection.PrimaryRecorder) RestartRecordingForRecorder(voiceConnection.PrimaryRecorder);
            var recorder = voiceConnection.GetComponentInChildren<Recorder>();
            if (recorder != null)
            {
                RestartRecordingForRecorder(recorder);
            }
            shouldRestartRecording = false;
        }

        void PreventRecordersToStartRecording()
        {
            if (voiceConnection.PrimaryRecorder) PreventRecorderToStartRecording(voiceConnection.PrimaryRecorder);
            var recorder = voiceConnection.GetComponentInChildren<Recorder>();
            if (recorder != null)
            {
                PreventRecorderToStartRecording(recorder);
            }
        }

        void RestartRecordingForRecorder(Recorder recorder)
        {
            ChangeRecorderRecording(recorder, isRecording: true);
        }

        void PreventRecorderToStartRecording(Recorder recorder)
        {
            ChangeRecorderRecording(recorder, isRecording: false);
        }

        void ChangeRecorderRecording(Recorder recorder, bool isRecording)
        {
            if (recorder == null) return;
            recorder.RecordingEnabled = isRecording;
            recorder.RecordWhenJoined = isRecording;
        }
#endif
    }
}
