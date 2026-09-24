using System.Collections.Generic;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    [AddComponentMenu("Bone Render")]
    public class SimpleBoneRenderer : MonoBehaviour
    {
        public Transform rootBone;

        public List<Transform> bones = new List<Transform>();
        public Color boneColor = Color.pink;
        [Range(0.01f, 0.5f)] public float boneSize = 0.02f;

        public bool drawJoints = false;
        public Color jointColor = Color.white;
        [Range(10, 36)] public int jointFontSize = 10;

        // Show / Hide khung xương trong Scene view — áp dụng ở MỌI chế độ (Edit lẫn Play),
        // có hiệu lực ngay lập tức khi bấm nút trong Inspector.
        public bool show = true;

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

#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Populate Bones");
#endif
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

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
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