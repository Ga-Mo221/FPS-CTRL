using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

#if (UNITY_IOS || UNITY_VISIONOS) && !UNITY_EDITOR
using UnityEngine.iOS;
#endif

namespace Photon.Voice
{
    /// <summary>
    /// Launch successively permission requests (Android only for now)
    /// </summary>
    [DefaultExecutionOrder(PermissionsRequester.EXECUTION_ORDER)]
    public class PermissionsRequester : MonoBehaviour
    {
        public const int EXECUTION_ORDER = -20_000;
        public static PermissionsRequester _sharedInstance;

        public static PermissionsRequester SharedInstance
        {
            get
            {
                if (_sharedInstance == null)
                {
                    var permissionRequesterGameObject = new GameObject("PermissionsRequester");
                    permissionRequesterGameObject.AddComponent<PermissionsRequester>();
                }
                return _sharedInstance;
            }
        }

        // To store a UserAuthorization as a string, we use their android equivalent (this way, the same permission name can be passed to the PermissionsRequester for Android or iOS)
        public const string MicrophoneAuthorizationPermissionName = MicrophoneAndroidPermissionName;
        public const string WebcamAuthorizationPermissionName = WebcamAndroidPermissionName;
        public const string MicrophoneAndroidPermissionName = "android.permission.RECORD_AUDIO";
        public const string WebcamAndroidPermissionName = "android.permission.CAMERA";

        /// <summary>
        /// Convert a string permission to a UserAuthorization (using MicrophoneAuthorizationPermissionName and WebcamAuthorizationPermissionName values)
        /// </summary>
        public static bool TryConvertToUserAuthorization(string permissionName, out UserAuthorization userAuthoriation)
        {
            userAuthoriation = default;
            bool isUnityMicrophoneApplicationAuthorization = permissionName == MicrophoneAuthorizationPermissionName;
            bool isUnityWebcamApplicationAuthorization = permissionName == WebcamAuthorizationPermissionName;
            bool isUnityApplicationAuthorization = isUnityMicrophoneApplicationAuthorization || isUnityWebcamApplicationAuthorization;

            if (isUnityApplicationAuthorization)
            {
                userAuthoriation = isUnityMicrophoneApplicationAuthorization ? UserAuthorization.Microphone : UserAuthorization.WebCam;
            }
            return isUnityApplicationAuthorization;
        }

        public string PermissionNameForUserAuthoriation(UserAuthorization userAuthoriation)
        {
            if (userAuthoriation == UserAuthorization.Microphone)
                return MicrophoneAuthorizationPermissionName;
            if (userAuthoriation == UserAuthorization.WebCam)
                return WebcamAuthorizationPermissionName;
            return null;
        }

        /// <summary>
        /// Launch a permission request
        /// </summary>
        [System.Serializable]
        public class PermissionRequest
        {
            public const float DefaultDelayBeforeRequest = 0.05f;
            public string permissionName = "";
            /// <summary>
            /// Tells if the permission has received an answer. Set by callbacks on Android, and by the HasPermission setter on other platforms
            /// </summary>
            public bool hasBeenChecked = false;
            public bool isRequesting = false;
            public float delayBeforeRequest = DefaultDelayBeforeRequest;
            public float delayBeforeCallback = 0;
            [Tooltip("Has the permission been received (do not edit in the inspector)")]
            [SerializeField] private bool _hasPermission = false;
            public UnityEvent<bool> permissionCallback = new UnityEvent<bool>();
            public UnityEvent permissionGrantedCallback = new UnityEvent();
            public UnityEvent permissionCheckStartedCallback = new UnityEvent();
            public bool isUnityApplicationAuthorization = false;
            UserAuthorization unityApplicationAuthorization;

            /// <summary>
            /// Modify hasPermission, and when setting values, ensure to set hasBeenChecked/isRequesting, to consider the request as handled
            /// </summary>
            public bool HasPermission
            {
                get
                {
                    return this._hasPermission;
                }
                private set
                {
                    Debug.Log($"[PermissionsRequester] {permissionName} permission Granted: {value}");
                    this.hasBeenChecked = true;
                    this.isRequesting = false;
                    if (this._hasPermission != value)
                    {
                        this._hasPermission = value;
                    }
                    NotifyPermissionChange(value);
                }
            }

            async void NotifyPermissionChange(bool value)
            {
                if(delayBeforeCallback > 0)
                {
                    await Delay((int)(1000 * delayBeforeCallback));
                }
                permissionCallback?.Invoke(value);
                if (value)
                {
                    permissionGrantedCallback?.Invoke();
                }
            }

            public PermissionRequest(string permissionName, float delayBeforeRequest = DefaultDelayBeforeRequest)
            {
                this.permissionName = permissionName;
                this.delayBeforeRequest = delayBeforeRequest;
            }

            public async void CheckPermission()
            {
                if (delayBeforeRequest > 0)
                {
                    Debug.Log($"[PermissionsRequester] Awaiting {delayBeforeRequest} before checking {permissionName} ...");
                    await Delay((int)(1000 * delayBeforeRequest));
                }
                if(permissionCallback != null)
                {
                    permissionCheckStartedCallback.Invoke();
                }
                Debug.Log($"[PermissionsRequester] Checking permission {permissionName} (RequestUserPermission) ...");
#if UNITY_ANDROID && !UNITY_EDITOR
                AndroidCheckPermission();
#else
                PermissionsRequester.SharedInstance.StartCoroutine(UnityCheckAuthorization());
#endif
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            protected void AndroidCheckPermission()
            {
                if (Permission.HasUserAuthorizedPermission(permissionName))
                {
                    this.HasPermission = true;
                }
                else
                {
                    Debug.Log($"[PermissionsRequester] Permission {permissionName} Request");
                    var callbacks = new PermissionCallbacks();
                    callbacks.PermissionDenied += PermissionCallbacks_PermissionDenied;
                    callbacks.PermissionGranted += PermissionCallbacks_PermissionGranted;
                    Permission.RequestUserPermission(permissionName, callbacks);

                    this.isRequesting = true;
                }
            }
#endif

            /// <summary>
            /// UserAuthorization based authorization, for iOS and webGL
            /// </summary>
            /// <returns></returns>
            protected IEnumerator UnityCheckAuthorization()
            {
                if (TryConvertToUserAuthorization(permissionName, out var unityApplicationAuthorization))
                {
                    if (Application.HasUserAuthorization(unityApplicationAuthorization))
                    {
                        Debug.Log($"[PermissionsRequester] Permission {unityApplicationAuthorization} already available");
                        this.HasPermission = true;
                    }
                    else
                    {
                        Debug.Log($"[PermissionsRequester] Permission {unityApplicationAuthorization} Request");
                        this.isRequesting = true;
                        yield return Application.RequestUserAuthorization(unityApplicationAuthorization);
                        this.isRequesting = false;
                        if (Application.HasUserAuthorization(unityApplicationAuthorization))
                        {
                            Debug.Log($"[PermissionsRequester] Permission {permissionName} granted");
                            this.HasPermission = true;
                        }
                        else
                        {
                            Debug.Log($"[PermissionsRequester] Permission {permissionName} rejected");
                            this.HasPermission = false;
                        }
                    }
                } 
                else
                {
#if !UNITY_EDITOR
                    Debug.LogError("Unsupported authorization request: "+permissionName);
#endif 
                    this.HasPermission = true;
                }
            }

            public void UpdatePermission()
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (hasBeenChecked && HasPermission == false && Permission.HasUserAuthorizedPermission(permissionName)){
                    Debug.LogError($"[PermissionsRequester] Permission {permissionName} granted, after first rejection (several conflicting request were initially pending probably)");
                    this.HasPermission = true;
                }
#endif
            }

            internal void PermissionCallbacks_PermissionGranted(string permissionName)
            {
                this.HasPermission = true;
                Debug.Log($"[PermissionsRequester] {permissionName} PermissionGranted");
            }

            internal void PermissionCallbacks_PermissionDenied(string permissionName)
            {
                this.HasPermission = false;
                Debug.Log($"[PermissionsRequester] {permissionName} PermissionDenied");
            }
        }


        public List<PermissionRequest> permissionRequests = new List<PermissionRequest>();
        public UnityEvent allPermissionCallback = new UnityEvent();
        public List<GameObject> objectsToActivateAfterPermissionChecks = new List<GameObject>();

        public bool requestOnStart = true;
        public bool isLaunchingRequests = false;
        bool launchingRequestAlreadyStarted = false;
        bool shouldLaunchPermissionRequests = false;

        void Awake()
        {
            _sharedInstance = this;
        }

        private void Start()
        {
            if (requestOnStart)
            {
                shouldLaunchPermissionRequests = true;
            }
        }

        async Task WaitForRequestLaunchFinished()
        {
            while (isLaunchingRequests)
            {
                await Delay(50);
            }
        }

        public async Task LaunchPermissionRequests()
        {
            await WaitForRequestLaunchFinished();
            isLaunchingRequests = true;
            launchingRequestAlreadyStarted = true;
            Debug.Log("[PermissionsRequester] LaunchPermissionRequests");
            int i = 1;
            foreach (var requester in permissionRequests) {
                Debug.Log($"[PermissionsRequester] LaunchPermissionRequests: {i}/{permissionRequests.Count} ({requester.permissionName}/hasBeenChecked={requester.hasBeenChecked}/ isRequesting={requester.isRequesting})");
                if (requester.hasBeenChecked == false && requester.isRequesting == false)
                {
                    requester.CheckPermission();
                }
                while (requester.hasBeenChecked == false)
                {
                    await Delay(50);
                }
                i++;
            }
            Debug.Log("[PermissionsRequester] All permissions checked");
            foreach(var objectToActivateAfterPermissionChecks in objectsToActivateAfterPermissionChecks)
            {
                objectToActivateAfterPermissionChecks.SetActive(true);
            }
            allPermissionCallback?.Invoke();
            isLaunchingRequests = false;
        }

        private void Update()
        {
            if (shouldLaunchPermissionRequests)
            {
                shouldLaunchPermissionRequests = false;
                StartPermissionRequests();
            }

            foreach (var requester in permissionRequests)
            {
                requester.UpdatePermission();
            }
        }

        async void StartPermissionRequests()
        {
            await LaunchPermissionRequests();
        }

        private void OnDestroy()
        {
            if(SharedInstance == this)
            {
                _sharedInstance = null;
            }
        }

        public bool TryFindConfiguredPermissionRequest(string permission, out PermissionRequest requester)
        {
            bool found = false;
            requester = null;
            foreach (var r in permissionRequests)
            {
                if(r.permissionName == permission)
                {
                    requester = r;
                    found = true;
                    break;
                }
            }
            return found;
        }

        public static bool HasMicrophonePermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return Permission.HasUserAuthorizedPermission(MicrophoneAndroidPermissionName);
#else
            return Application.HasUserAuthorization(UserAuthorization.Microphone);
#endif
        }

        public static bool HasWebcamPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return Permission.HasUserAuthorizedPermission(WebcamAndroidPermissionName);
#else
            return Application.HasUserAuthorization(UserAuthorization.WebCam);
#endif
        }

        public void AddMicrophoneRequest(UnityAction<bool> permissionCallback = null, UnityAction permissionCheckStartedCallback = null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AddPermissionRequest(MicrophoneAndroidPermissionName, permissionCallback, permissionCheckStartedCallback);            
#else
            AddAuthorizationRequest(UserAuthorization.Microphone, permissionCallback, permissionCheckStartedCallback);
#endif
        }

        public void AddWebcamRequest(UnityAction<bool> permissionCallback = null, UnityAction permissionCheckStartedCallback = null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AddPermissionRequest(WebcamAndroidPermissionName, permissionCallback, permissionCheckStartedCallback);            
#else
            AddAuthorizationRequest(UserAuthorization.WebCam, permissionCallback, permissionCheckStartedCallback);
#endif
        }

        /// <summary>
        /// Queue a permission request for authorization UserAuthorization (for iOS or webGL)
        /// Will be converted to a string (WebcamAuthorizationPermissionName or MicrophoneAuthorizationPermissionName), and passed to AddPermissionRequest
        /// </summary>
        public void AddAuthorizationRequest(UserAuthorization authorization, UnityAction<bool> permissionCallback = null, UnityAction permissionCheckStartedCallback = null)
        {
            var permissionName = "";
            switch (authorization)
            {
                case UserAuthorization.WebCam:
                    permissionName = WebcamAuthorizationPermissionName;
                    break;
                case UserAuthorization.Microphone:
                    permissionName = MicrophoneAuthorizationPermissionName;
                    break;
            }
            if (string.IsNullOrEmpty(permissionName) == false)
            {
                AddPermissionRequest(permissionName, permissionCallback, permissionCheckStartedCallback);
            }
        }

        /// <summary>
        /// Queue a permission request for permission string (can be either an Android request, or, for iOS and webGL WebcamAuthorizationPermissionName or MicrophoneAuthorizationPermissionName values)
        /// </summary>
        public async void AddPermissionRequest(string permission, UnityAction<bool> permissionCallback = null, UnityAction permissionCheckStartedCallback = null)
        {
            PermissionRequest request = null;
            if (launchingRequestAlreadyStarted)
            {
                // Permisison checks had already started: making sure it is finished before adding a new one (to avoid changing the list while read)");
                await WaitForRequestLaunchFinished();
            }
            if (TryFindConfiguredPermissionRequest(permission, out request) == false)
            {
                Debug.Log($"[PermissionsRequester] Adding permission request for {permission}");
                request = new PermissionRequest(permission);

                if (permissionCheckStartedCallback != null) 
                    request.permissionCheckStartedCallback.AddListener(permissionCheckStartedCallback);
                if (permissionCallback != null) 
                    request.permissionCallback.AddListener(permissionCallback);
                permissionRequests.Add(request);

                if (launchingRequestAlreadyStarted)
                {
                    // Permission checks had already started: we have to run the permissions again for this one to be checked
                    Debug.Log("[PermissionsRequester] Launching permissions requests due to late AddPermissionRequest");
                    await LaunchPermissionRequests();
                }
            }
            else
            {
                // The request already exists. Set the callback, if any
                if (permissionCallback != null)
                {
                    if(permissionCallback != null)
                        request.permissionCallback.AddListener(permissionCallback);

                    if (permissionCheckStartedCallback != null)
                        request.permissionCheckStartedCallback.AddListener(permissionCheckStartedCallback);

                    if (request.hasBeenChecked)
                    {
                        // The request has already been requested, and a permission result has been set. Manually call the callback
                        permissionCallback(request.HasPermission);
                    }
                }
            }
        }

        public static async Task Delay(int milliseconds)
        {
#if !UNITY_WEBGL
            await Task.Delay(milliseconds);
#else
            // Unity 2021 do NOT support Task.Delay() in WebGL
            float startTime = Time.realtimeSinceStartup;
            float delay = (float)milliseconds / 1000f;

            while (Time.realtimeSinceStartup - startTime < delay)
            {
                // Wait for the delay time to pass
                await Task.Yield();
            }
#endif
        }
    }
}
