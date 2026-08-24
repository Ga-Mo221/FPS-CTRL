using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    // Sinh các dòng kill feed từ một dòng mẫu (template) bằng OBJECT POOL.
    // Dòng được tái sử dụng (bật/tắt) thay vì Instantiate/Destroy, nên kill feed
    // hoạt động liên tục mà không sinh rác (GC) mỗi lần có mạng hạ gục.
    // Dùng chung cho cả HUD DM và FFA.
    // Cách dùng: gán dòng mẫu vào _entryTemplate rồi gọi AddKill(...) mỗi khi có mạng hạ gục.
    public class KillFeedView : MonoBehaviour
    {
        [Tooltip("Dòng mẫu (một KillFeedEntryView). Sẽ bị tắt và nhân bản để làm đầy pool.")]
        [SerializeField] private KillFeedEntryView _entryTemplate;

        [Tooltip("Cha chứa các dòng. Để trống thì lấy cha của dòng mẫu.")]
        [SerializeField] private Transform _container;

        [Tooltip("Mạng mới nhất hiện ở trên cùng thay vì dưới cùng.")]
        [SerializeField] private bool _newestOnTop = true;

        [Min(1)][SerializeField] private int _maxEntries = 5; // số dòng tối đa hiện cùng lúc

        [Tooltip("Số giây một dòng tồn tại trước khi trả về pool. 0 = không tự động xóa.")]
        [Min(0f)][SerializeField] private float _entryLifetime = 5f;

        [Tooltip("Số dòng tạo sẵn từ đầu để những mạng đầu tiên không sinh rác. 0 = dùng Max Entries.")]
        [Min(0)][SerializeField] private int _initialPoolSize = 0;

        private readonly List<KillFeedEntryView> _active = new List<KillFeedEntryView>();   // dòng đang hiện
        private readonly Queue<KillFeedEntryView> _pool = new Queue<KillFeedEntryView>();    // dòng rảnh chờ tái sử dụng
        private readonly Dictionary<KillFeedEntryView, Coroutine> _timers = new Dictionary<KillFeedEntryView, Coroutine>();

        private void Awake()
        {
            if (_entryTemplate == null) return;
            if (_container == null) _container = _entryTemplate.transform.parent;
            _entryTemplate.gameObject.SetActive(false);

            // Tạo sẵn một số dòng cho pool (làm nóng).
            int warm = _initialPoolSize > 0 ? _initialPoolSize : _maxEntries;
            for (int i = 0; i < warm; i++)
                _pool.Enqueue(CreateEntry());
        }

        // Thêm một dòng hạ gục. Trả về view vừa hiện (hoặc null nếu chưa cấu hình).
        public KillFeedEntryView AddKill(string killer, string weaponname, string victim, bool killerIsLocal)
        {
            if (_entryTemplate == null || _container == null) return null;

            var entry = Rent();
            entry.SetData(killer, weaponname, victim, killerIsLocal);
            entry.gameObject.SetActive(true);
            if (_newestOnTop) entry.transform.SetAsFirstSibling();
            else entry.transform.SetAsLastSibling();

            _active.Add(entry);
            TrimToMax();

            // Hẹn giờ trả dòng về pool sau _entryLifetime giây.
            if (_entryLifetime > 0f)
                _timers[entry] = StartCoroutine(ReleaseAfter(entry, _entryLifetime));

            return entry;
        }

        // Trả tất cả dòng đang hiện về pool (xóa sạch kill feed).
        public void Clear()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                Release(_active[i]);
        }

        // ---- Bên trong pool ----

        private KillFeedEntryView CreateEntry()
        {
            var entry = Instantiate(_entryTemplate, _container);
            entry.gameObject.SetActive(false);
            return entry;
        }

        // Mượn một dòng từ pool (hoặc tạo mới nếu pool hết).
        private KillFeedEntryView Rent()
        {
            var entry = _pool.Count > 0 ? _pool.Dequeue() : CreateEntry();
            entry.transform.SetParent(_container, false);
            return entry;
        }

        // Trả một dòng về pool và dừng hẹn giờ của nó.
        private void Release(KillFeedEntryView entry)
        {
            if (entry == null) return;

            Coroutine co;
            if (_timers.TryGetValue(entry, out co))
            {
                if (co != null) StopCoroutine(co);
                _timers.Remove(entry);
            }

            _active.Remove(entry);
            entry.gameObject.SetActive(false);
            _pool.Enqueue(entry);
        }

        // Nếu vượt quá số dòng tối đa thì bỏ bớt dòng cũ nhất.
        private void TrimToMax()
        {
            while (_active.Count > _maxEntries)
                Release(_active[0]); // cũ nhất trước
        }

        private IEnumerator ReleaseAfter(KillFeedEntryView entry, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            Release(entry);
        }
    }
}
