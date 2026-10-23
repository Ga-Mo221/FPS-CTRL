using UnityEngine;
using UnityEngine.Events;

namespace Photon.Voice
{
    /// <summary>
    /// Wait for a permission to be granted.
    /// 
    /// Should be used alongside a PermissionsRequester, to align permissions on the desired timing (to avoid conflicting requests)
    /// Can create one if missing
    /// 
    /// Subclass should have a [DefaultExecutionOrder(PermissionWaiter.EXECUTION_ORDER)]
    ///   (PermissionWaiter.EXECUTION_ORDER or any higher value - to wait for the Permissionsrequester Awake, so that the singleton is detected) 
    ///   tag to be sure to trigger early, if needed
    /// </summary>
    [DefaultExecutionOrder(PermissionWaiter.EXECUTION_ORDER)]
    public abstract class PermissionWaiter : MonoBehaviour
    {
        public const int EXECUTION_ORDER = PermissionsRequester.EXECUTION_ORDER + 1;

        protected bool waitingForPermission = false;

        [Tooltip("If true, OnPermissionGranted will be automatically called when the requested permission is granted. Otherwise, it has to be called manually")]
        public bool checkPermisisonGrantAutomatically = true;

        [Tooltip("Warn of permission grant status changes")]
        public UnityEvent<bool> onPermissionChanged = new UnityEvent<bool>();
        [Tooltip("Warn of permission grant reception")]
        public UnityEvent onPermissionGranted = new UnityEvent();
        public bool IsWaitingForPermission => waitingForPermission;

        protected virtual void Awake()
        {
#if (UNITY_ANDROID && !UNITY_EDITOR) || UNITY_IOS || UNITY_VISIONOS || UNITY_WEBGL
            OnPermissionRequired();
            waitingForPermission = true;
#endif
            PermissionsRequester.SharedInstance.AddPermissionRequest(PermissionName);

        }

        #region Override methods (base method, when available, should be called in subclasses implementations)
        public virtual string PermissionName { get; }

        /// <summary>
        /// Called when the permission is required 
        /// </summary>
        protected virtual void OnPermissionRequired()
        {
            Debug.Log($"[PermissionsRequester-{GetType().Name}] OnPermissionRequired " + PermissionName);
        }

        protected virtual void OnPermissionChanged(bool isGranted)
        {
            Debug.Log($"[PermissionsRequester-{GetType().Name}] OnPermissionChanged(isGranted: {isGranted}) {PermissionName}");
            if (onPermissionChanged != null) onPermissionChanged.Invoke(isGranted);
        }


        /// <summary>
        /// Should be called manually (for instance in a PermissionsRequester callback) if checkPermisisonGrantAutomatically is not checked
        /// Otherwise, automatically called when relevant in Update
        /// </summary>
        public virtual void OnPermissionGranted()
        {
            Debug.Log($"[PermissionsRequester-{GetType().Name}] OnPermissionGranted " + PermissionName);
            waitingForPermission = false;
            if (onPermissionGranted != null) onPermissionGranted.Invoke();
        }
        #endregion 

        protected virtual void Update()
        {
            if (checkPermisisonGrantAutomatically && waitingForPermission && IsPermissionGranted)
            {
                waitingForPermission = false;
                OnPermissionGranted();
            }
        }

        public bool IsPermissionGranted
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR


                if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(PermissionName))
                {
                    return true;
                }
                else
                {
                    return false;
                }
#elif UNITY_IOS || UNITY_VISIONOS || UNITY_WEBGL
                if (PermissionsRequester.TryConvertToUserAuthorization(PermissionName, out var userAuthorization) && Application.HasUserAuthorization(userAuthorization))
                {
                    return true;
                }
                else
                { 
                    return false;
                }
#else
                return true;
#endif
            }
        }
    }
}