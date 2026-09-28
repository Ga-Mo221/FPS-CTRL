#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    /// <summary>
    /// Trục cục bộ (local axis) của transform mà xương được vẽ dọc theo.
    /// </summary>
    public enum BoneAxis
    {
        X = 0,
        Y = 1,
        Z = 2
    }

    [AddComponentMenu("Bone Render")]
    public class SimpleBoneRenderer : MonoBehaviour
    {
        public Transform rootBone;

        public List<Transform> bones = new List<Transform>();
        public Color boneColor = Color.pink;

        // Chiều dài mặc định (world unit) của xương KHÔNG có xương con "connected".
        // Xương có con connected thì chiều dài do vị trí xương con quyết định, không dùng giá trị này.
        [Range(0.01f, 1f)] public float boneLength = 0.05f;

        // Bán kính (world unit) của phần đế xương, độc lập với chiều dài.
        [Range(0.001f, 0.2f)] public float boneRadius = 0.01f;

        public bool drawJoints = false;
        public Color jointColor = Color.white;
        [Range(5, 36)] public int jointFontSize = 15;

        // Show / Hide khung xương trong Scene view — áp dụng ở MỌI chế độ (Edit lẫn Play),
        // có hiệu lực ngay lập tức khi bấm nút trong Inspector.
        public bool show = true;

        // Trục cục bộ mà xương được vẽ dọc theo (mặc định Y, giống Blender).
        // Đổi trong Inspector bằng 3 nút X / Y / Z nằm dưới nút Show.
        public BoneAxis boneAxis = BoneAxis.Y;

        // Đảo chiều trục đã chọn (dùng khi trục cục bộ của bone bị "ngược", ví dụ xương
        // hướng theo -Y thay vì +Y sau khi import từ Blender/FBX). Đổi bằng nút Invert
        // nằm cạnh 3 nút X / Y / Z.
        public bool invertAxis = false;

        // Bật: trong Play Mode, xương giữ nguyên xương con "connected" đã xác định lúc
        // KHÔNG phải Play Mode, không kiểm tra lại mỗi frame xem xương con có còn thẳng
        // hàng với trục hay không. Cần thiết vì animation/IK lúc chơi có thể làm xương con
        // lệch trục tạm thời, khiến nó liên tục "rớt kết nối" rồi "nối lại", gây giật hình.
        // Tắt: luôn kiểm tra thẳng hàng mỗi frame kể cả trong Play Mode (hành vi cũ).
        public bool lockTailInPlayMode = true;

        // --- Cache xương con "connected" xác định gần nhất lúc KHÔNG phải Play Mode ---
        // Được Editor script đọc/ghi để phục vụ lockTailInPlayMode ở trên.
        //
        // QUAN TRỌNG: lưu bằng 2 List<Transform> song song có [SerializeField] thay vì
        // Dictionary NonSerialized. Lý do: khi bấm Play, Unity mặc định làm "domain reload"
        // (trừ khi tắt trong Edit Mode Settings) — quá trình này XÓA SẠCH mọi field
        // NonSerialized trước khi Play Mode thực sự chạy. Nếu dùng Dictionary NonSerialized,
        // đúng lúc Play bắt đầu thì cache "ai đang connected với ai" đã bị xóa trắng, khiến
        // lockTailInPlayMode luôn rơi vào nhánh "không kết nối" (vì Play Mode chỉ ĐỌC cache,
        // không bao giờ GHI lại) — mất kết nối vĩnh viễn cho tới khi thoát Play Mode.
        // List<Transform> có [SerializeField] thì được Unity serialize/deserialize xuyên qua
        // domain reload (khác với ghi ra file scene trên đĩa), nên dữ liệu sống sót đúng lúc
        // cần. Dictionary chỉ dùng làm cache tra cứu nhanh O(1) dựng lại từ 2 list này.
        [SerializeField, HideInInspector] private List<Transform> _connectedParents = new List<Transform>();
        [SerializeField, HideInInspector] private List<Transform> _connectedChildrenSerialized = new List<Transform>();

        [System.NonSerialized] private Dictionary<Transform, Transform> _connectedChildCache;
        [System.NonSerialized] private bool _connectedCacheBuilt;

        private void RebuildConnectedChildCacheFromSerialized()
        {
            if (_connectedChildCache == null)
                _connectedChildCache = new Dictionary<Transform, Transform>();
            else
                _connectedChildCache.Clear();

            int count = Mathf.Min(_connectedParents.Count, _connectedChildrenSerialized.Count);
            for (int i = 0; i < count; i++)
            {
                if (_connectedParents[i] != null && _connectedChildrenSerialized[i] != null)
                    _connectedChildCache[_connectedParents[i]] = _connectedChildrenSerialized[i];
            }

            _connectedCacheBuilt = true;
        }

        /// <summary>Tra cứu xương con "connected" gần nhất đã xác định cho bone cha này.</summary>
        public bool TryGetConnectedChild(Transform parent, out Transform child)
        {
            if (!_connectedCacheBuilt) RebuildConnectedChildCacheFromSerialized();
            return _connectedChildCache.TryGetValue(parent, out child);
        }

        /// <summary>Ghi/cập nhật xương con "connected" cho bone cha này (chỉ gọi lúc KHÔNG Play Mode).</summary>
        public void SetConnectedChild(Transform parent, Transform child)
        {
            if (!_connectedCacheBuilt) RebuildConnectedChildCacheFromSerialized();

            // Không đổi thì bỏ qua, tránh ghi lại 2 list serialize (và làm scene dirty) mỗi
            // lần OnGUI trong khi quan hệ connected thực ra vẫn y như cũ.
            if (_connectedChildCache.TryGetValue(parent, out Transform existing) && existing == child)
                return;

            _connectedChildCache[parent] = child;
            SyncSerializedListsFromCache();
        }

        /// <summary>Xóa xương con "connected" đã ghi cho bone cha này (chỉ gọi lúc KHÔNG Play Mode).</summary>
        public void RemoveConnectedChild(Transform parent)
        {
            if (!_connectedCacheBuilt) RebuildConnectedChildCacheFromSerialized();

            if (!_connectedChildCache.Remove(parent)) return; // không có sẵn -> khỏi sync

            SyncSerializedListsFromCache();
        }

        private void SyncSerializedListsFromCache()
        {
            _connectedParents.Clear();
            _connectedChildrenSerialized.Clear();

            foreach (var kv in _connectedChildCache)
            {
                _connectedParents.Add(kv.Key);
                _connectedChildrenSerialized.Add(kv.Value);
            }
        }

        // Bật: khi click chọn bone trong Scene view, đồng thời "ping" bone đó trong Hierarchy
        // (cuộn tới, mở các object cha và nháy sáng vị trí). Tắt: chỉ chọn, giữ hành vi mặc định.
        public bool pingInHierarchy = false;

        // --- Cache tra cứu nhanh O(1) thay cho bones.Contains() O(n) ---
        private HashSet<Transform> _boneSet;

        // _boneSetDirty được set = true bởi OnValidate (Inspector thay đổi bất kỳ)
        // thay vì chỉ so sánh Count — tránh bỏ sót trường hợp user swap 1 phần tử
        // mà không thay đổi số lượng.
        private bool _boneSetDirty = true;

        public HashSet<Transform> BoneSet
        {
            get
            {
                if (_boneSetDirty || _boneSet == null)
                    RebuildBoneSet();
                return _boneSet;
            }
        }

        private void RebuildBoneSet()
        {
            if (_boneSet == null)
                _boneSet = new HashSet<Transform>(bones.Count);
            else
                _boneSet.Clear();

            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i] != null) _boneSet.Add(bones[i]);
            }

            _boneSetDirty = false;
        }

        // Buffer tái sử dụng để tránh GetComponentsInChildren<T>() cấp phát mảng mới mỗi lần.
        private readonly List<Transform> _populateBuffer = new List<Transform>(64);

        public void PopulateChildren()
        {
            if (rootBone == null)
            {
                Debug.LogError("Vui lòng kéo gán Root Bone trước khi load!");
                return;
            }
            
            UnityEditor.Undo.RecordObject(this, "Populate Bones");
            bones.Clear();

            // Dùng overload List<T> để tái sử dụng buffer, không cấp phát mảng mới.
            _populateBuffer.Clear();
            rootBone.GetComponentsInChildren<Transform>(_populateBuffer);

            // GetComponentsInChildren luôn trả về kết quả duy nhất (không duplicate),
            // nên bỏ hoàn toàn check bones.Contains() (trước đây là O(n²) không cần thiết).
            for (int i = 0; i < _populateBuffer.Count; i++)
            {
                Transform child = _populateBuffer[i];
                if (child != rootBone)
                    bones.Add(child);
            }

            RebuildBoneSet();
            
            UnityEditor.EditorUtility.SetDirty(this);

            Debug.Log($"Đã load thành công {bones.Count} xương con từ {rootBone.name}!");
        }

        private void OnValidate()
        {
            // Đánh dấu dirty thay vì null-hóa trực tiếp để RebuildBoneSet()
            // có thể Clear() và tái sử dụng HashSet đã cấp phát, thay vì tạo mới.
            _boneSetDirty = true;
        }
    }
}
#endif