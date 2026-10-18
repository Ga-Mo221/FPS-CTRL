// ============================================================
//  TrueInputSystem Tutorials — TISTutPlayground.cs
//
//  Quản lý sân chơi 3D dùng chung cho mọi bài: nhân vật, camera, vật liệu,
//  và hàm tạo đồ vật (khối màu, vòng đích, ngôi sao). Bài học gọi qua
//  TISTutPlayground.Instance — đồ vật tạo trong Stage của bài sẽ tự xoá khi đổi bài.
// ============================================================

using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [DefaultExecutionOrder(-60)]
    [AddComponentMenu("True Input System/Tutorials/TIS Tut Playground")]
    public class TISTutPlayground : MonoBehaviour
    {
        public static TISTutPlayground Instance { get; private set; }

        [SerializeField] TISTutPlayer      _player;
        [SerializeField] TISTutOrbitCamera _camera;
        [SerializeField] Transform         _toys;

        [Header("Vật liệu")]
        public Material grass;
        public Material coral;
        public Material sun;
        public Material lilac;
        public Material mint;
        public Material ink;
        public Material paper;

        public TISTutPlayer      Player => _player;
        public TISTutOrbitCamera Camera => _camera;
        /// <summary>Nhóm đồ vật trang trí mặc định (bài có thể ẩn đi).</summary>
        public Transform         Toys   => _toys;

        public static readonly Vector3 PlayerStart = new Vector3(0f, 1.1f, 0f);

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Đưa nhân vật + camera về trạng thái đầu bài.</summary>
        public void ResetForLesson()
        {
            if (_player != null)
            {
                _player.ResetAbilities();
                _player.Teleport(PlayerStart);
            }
            if (_camera != null) _camera.ResetView();
            if (_toys != null) _toys.gameObject.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // ── Tạo đồ vật ────────────────────────────────────────

        public GameObject Spawn(PrimitiveType type, string name, Material mat, Vector3 pos, Vector3 scale,
                                Transform parent, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Destroy(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>Vòng tròn đích phẳng trên mặt đất.</summary>
        public TISTutZone Zone(string name, Material mat, Vector3 pos, float radius, Transform parent)
        {
            var go = Spawn(PrimitiveType.Cylinder, name, mat, new Vector3(pos.x, 0.03f, pos.z),
                           new Vector3(radius * 2f, 0.03f, radius * 2f), parent, collider: false);
            var z = go.AddComponent<TISTutZone>();
            z.radius = radius;
            return z;
        }

        /// <summary>Ngôi sao (khối xoay) để thu thập.</summary>
        public TISTutCollectible Star(Vector3 pos, Transform parent)
        {
            var go = Spawn(PrimitiveType.Cube, "Star", sun, pos, Vector3.one * 0.6f, parent, collider: false);
            go.transform.rotation = Quaternion.Euler(45f, 0f, 45f);
            return go.AddComponent<TISTutCollectible>();
        }
    }
}
