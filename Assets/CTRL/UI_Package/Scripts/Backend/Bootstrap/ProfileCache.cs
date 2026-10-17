using System;
using System.Collections.Generic;
using UnityEngine;
using Backend.Models;

namespace Backend.Bootstrap
{
    /// <summary>
    /// Cache ho so nguoi choi (ten / level / avatar) dung chung cho toan bo menu.
    /// Muc dich: nhieu noi (TopBar, khoa sung theo level, ...) chi goi GetMyProfile MOT lan.
    ///  - Get(): co cache thi tra ngay; chua co thi tai 1 lan, cac caller goi cung luc dung chung 1 request.
    ///  - Refresh(): buoc tai lai tu PlayFab (vd sau tran dau len cap).
    ///  - Invalidate() / Set(): xoa / gan cache thu cong.
    /// </summary>
    public static class ProfileCache
    {
        private static PlayerProfileInfo _cached;
        private static bool _fetching;
        /// <summary>[Aemy 2026-09-28] Ban tin khi ho so bi doi thu cong (Set/Invalidate) -> UI (top bar) tu refresh.</summary>
        public static event System.Action Changed;
        private static readonly List<Action<PlayerProfileInfo>> _okWaiters = new List<Action<PlayerProfileInfo>>();
        private static readonly List<Action<AuthError>> _failWaiters = new List<Action<AuthError>>();

        /// <summary>Ho so dang cache (null neu chua tai).</summary>
        public static PlayerProfileInfo Cached { get { return _cached; } }
        /// <summary>Da co ho so trong cache chua.</summary>
        public static bool HasValue { get { return _cached != null; } }
        /// <summary>Level dang cache (0 neu chua tai).</summary>
        public static int Level { get { return _cached != null ? _cached.Level : 0; } }

        /// <summary>Lay ho so: uu tien cache; chua co thi tai 1 lan va gom cac callback goi cung luc.</summary>
        public static void Get(Action<PlayerProfileInfo> onResult, Action<AuthError> onFailure = null, bool forceRefresh = false)
        {
            if (_cached != null && !forceRefresh)
            {
                if (onResult != null) onResult(_cached);
                return;
            }

            if (onResult != null) _okWaiters.Add(onResult);
            if (onFailure != null) _failWaiters.Add(onFailure);
            if (_fetching) return;   // da co request dang chay -> ghep chung, khong goi lai

            var svc = BackendBootstrap.PlayerProfileService;
            if (svc == null)
            {
                Debug.LogWarning("[ProfileCache] Chua co PlayerProfileService.");
                _okWaiters.Clear();
                _failWaiters.Clear();
                return;
            }

            _fetching = true;
            svc.GetMyProfile(
                p =>
                {
                    _cached = p;
                    _fetching = false;
                    var list = _okWaiters.ToArray();
                    _okWaiters.Clear();
                    _failWaiters.Clear();
                    foreach (var cb in list) cb(p);
                },
                e =>
                {
                    _fetching = false;
                    var list = _failWaiters.ToArray();
                    _okWaiters.Clear();
                    _failWaiters.Clear();
                    foreach (var cb in list) cb(e);
                });
        }

        /// <summary>Buoc tai lai tu PlayFab (bo qua cache). Dung sau khi biet ho so da doi (vd len cap).</summary>
        public static void Refresh(Action<PlayerProfileInfo> onResult = null, Action<AuthError> onFailure = null)
        {
            Get(onResult, onFailure, true);
        }

        /// <summary>Gan cache thu cong (vd sau khi doi ten hien thi -> cap nhat luon khong can tai lai).</summary>
        public static void Set(PlayerProfileInfo profile)
        {
            if (profile != null) { _cached = profile; if (Changed != null) Changed(); }
        }

        /// <summary>Xoa cache (vd khi logout, hoac biet chac level/ho so da thay doi).</summary>
        public static void Invalidate()
        {
            _cached = null; if (Changed != null) Changed();
        }
    }
}
