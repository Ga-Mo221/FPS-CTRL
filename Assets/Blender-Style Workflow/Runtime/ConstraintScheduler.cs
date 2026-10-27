using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    /// <summary>
    /// Bộ lập lịch TRUNG TÂM cho mọi <see cref="ConstraintBehaviour"/> trong scene (mọi nhân vật).
    /// Thay vì N component tự LateUpdate() với thứ tự không xác định, toàn bộ chạy trong ĐÚNG 1
    /// LateUpdate của <see cref="ConstraintSchedulerRunner"/> (DefaultExecutionOrder 150) theo thứ tự cố định.
    ///
    /// Vòng đời 1 frame (xem <see cref="Tick"/>):
    ///   1. Nếu dirty (đăng ký/huỷ, đổi target/bones/order, reparent...) → <see cref="Rebuild"/>: dựng đồ thị
    ///      phụ thuộc + topo sort. Chỉ tốn khi cấu trúc đổi, KHÔNG chạy mỗi frame.
    ///   2. Duyệt mảng đã sắp xếp, gọi Solve() từng constraint (bỏ qua ExternallyDriven / đang tắt).
    ///   3. Stage cuối: hoàn tất job DynamicBone (<see cref="DynamicBoneManager"/>) — luôn SAU mọi constraint
    ///      (IK/DampedTrack/Transform) để vật lý tóc/vải đọc đúng pose cuối.
    ///
    /// Vì sao dùng MonoBehaviour runner thay vì chèn vào PlayerLoop: code game (ConstraintManager order 300,
    /// DeathCameraController order 1000, WeaponCollisionResolver order 100...) dựa vào việc constraint chạy
    /// XEN GIỮA các LateUpdate khác. Một entry PlayerLoop chỉ đứng được trước hoặc sau TOÀN BỘ LateUpdate.
    /// </summary>
    public static class ConstraintScheduler
    {
        /// <summary>
        /// true (mặc định): sắp theo phụ thuộc trước, ExecutionOrder/loại chỉ phá hoà.
        /// false: chỉ sắp theo ExecutionOrder → loại → độ sâu hierarchy (bỏ qua đồ thị phụ thuộc).
        /// </summary>
        public static bool ResolveDependencies
        {
            get => s_ResolveDependencies;
            set
            {
                if (s_ResolveDependencies == value) return;
                s_ResolveDependencies = value;
                s_Dirty = true;
            }
        }

        /// <summary>Tăng mỗi lần sắp xếp lại — Inspector dùng để biết khi nào cần vẽ lại.</summary>
        public static int Version { get; private set; }

        /// <summary>Số constraint đang đăng ký.</summary>
        public static int RegisteredCount => s_Registered.Count;

        private static bool s_ResolveDependencies = true;
        private static readonly List<ConstraintBehaviour> s_Registered = new List<ConstraintBehaviour>();
        private static ConstraintBehaviour[] s_Sorted = new ConstraintBehaviour[0];
        private static int s_SortedCount;
        private static bool s_Dirty;
        private static int s_SerialCounter;

        // ── Buffer tái sử dụng cho Rebuild (tránh alloc mỗi lần sắp xếp lại) ─────────────
        private static readonly List<Transform> s_Inputs = new List<Transform>();
        private static readonly List<Transform> s_Outputs = new List<Transform>();
        private static readonly List<Transform> s_AllInputs = new List<Transform>();
        private static int[] s_InputStart = new int[0];
        private static int[] s_InputCount = new int[0];
        private static int[] s_LocalCount = new int[0];   // input local-only nối tiếp sau input thường (cùng s_InputStart)
        private static readonly List<Transform> s_LocalInputs = new List<Transform>();
        private static int[] s_Depth = new int[0];
        private static int[] s_InDegree = new int[0];
        private static int[] s_EdgeMark = new int[0];
        private static List<int>[] s_Successors = new List<int>[0];
        private static readonly Dictionary<Transform, int> s_WriterHead = new Dictionary<Transform, int>();
        private static readonly List<int> s_WriterNode = new List<int>();   // linked list: node index
        private static readonly List<int> s_WriterNext = new List<int>();   // linked list: next entry (-1 = hết)
        private static readonly List<int> s_Heap = new List<int>();
        private static ConstraintBehaviour[] s_Nodes = new ConstraintBehaviour[0];
        // InstanceID các constraint đã cảnh báo cycle — tránh spam log mỗi lần sắp xếp lại (VD mỗi lần đổi súng)
        private static readonly HashSet<int> s_ReportedCycle = new HashSet<int>();

#if UNITY_EDITOR
        // Chỉ dùng cho Inspector: snapshot thứ tự đã sắp xếp
        public static IReadOnlyList<ConstraintBehaviour> EditorGetExecutionList()
        {
            if (s_Dirty) Rebuild();
            var list = new List<ConstraintBehaviour>(s_SortedCount);
            for (int i = 0; i < s_SortedCount; i++) list.Add(s_Sorted[i]);
            return list;
        }
#endif

        // Reset static khi vào Play Mode với "Enter Play Mode Options" (tắt Domain Reload)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            for (int i = 0; i < s_Registered.Count; i++)
                if (s_Registered[i] != null) s_Registered[i].schedulerRegistered = false;
            s_Registered.Clear();
            Array.Clear(s_Sorted, 0, s_Sorted.Length);
            s_SortedCount = 0;
            s_Dirty = true;
            s_ReportedCycle.Clear();
            ConstraintSchedulerRunner.ResetStatic();
        }

        // ══════════════════════════════ ĐĂNG KÝ ══════════════════════════════

        public static void Register(ConstraintBehaviour c)
        {
            if (c == null || c.schedulerRegistered) return;
            c.schedulerRegistered = true;
            c.schedulerSerial = ++s_SerialCounter;
            s_Registered.Add(c);
            s_Dirty = true;
            ConstraintSchedulerRunner.EnsureExists();
        }

        public static void Unregister(ConstraintBehaviour c)
        {
            if (c == null || !c.schedulerRegistered) return;
            c.schedulerRegistered = false;
            c.resolvedIndex = -1;
            c.inDependencyCycle = false;
            s_Registered.Remove(c);
            s_Dirty = true;
        }

        /// <summary>Đánh dấu cần sắp xếp lại ở lần Tick kế tiếp (rẻ, gọi thoải mái).</summary>
        public static void MarkDirty() => s_Dirty = true;

        // ══════════════════════════════ CHẠY MỖI FRAME ══════════════════════════════

        /// <summary>Gọi bởi <see cref="ConstraintSchedulerRunner"/> trong LateUpdate. Không tự gọi.</summary>
        internal static void Tick()
        {
            if (s_Dirty) Rebuild();

            // Duyệt snapshot mảng: Solve() của constraint này có thể tắt/bật constraint khác
            // (Unregister chỉ sửa s_Registered + bật dirty, không đụng mảng đang duyệt).
            ConstraintBehaviour[] sorted = s_Sorted;
            int count = s_SortedCount;
            for (int i = 0; i < count; i++)
            {
                ConstraintBehaviour c = sorted[i];
                if (c == null || !c.schedulerRegistered || c.ExternallyDriven || !c.isActiveAndEnabled) continue;

                // Cô lập exception như khi mỗi component tự LateUpdate: 1 constraint lỗi không làm dừng cả chuỗi
                try { c.Solve(); }
                catch (Exception e) { Debug.LogException(e, c); }
            }

            // Stage cuối: DynamicBone (chỉ Play Mode — DynamicBoneManager không chạy ở Edit Mode)
            if (Application.isPlaying) DynamicBoneManager.SchedulerLateTick();
        }

        // ══════════════════════════════ SẮP XẾP ══════════════════════════════

        private static void Rebuild()
        {
            s_Dirty = false;

            // Dọn instance đã bị Destroy mà không kịp OnDisable (hiếm, VD domain reload giữa chừng)
            for (int i = s_Registered.Count - 1; i >= 0; i--)
                if (s_Registered[i] == null) s_Registered.RemoveAt(i);

            int n = s_Registered.Count;
            EnsureCapacity(n);
            for (int i = 0; i < n; i++)
            {
                s_Nodes[i] = s_Registered[i];
                s_Nodes[i].inDependencyCycle = false;
            }

            // 1. Thu thập inputs/outputs từng constraint + độ sâu hierarchy của output chính
            s_AllInputs.Clear();
            s_WriterHead.Clear();
            s_WriterNode.Clear();
            s_WriterNext.Clear();

            for (int i = 0; i < n; i++)
            {
                s_Inputs.Clear();
                s_Outputs.Clear();
                try { s_Nodes[i].CollectDependencies(s_Inputs, s_Outputs); }
                catch (Exception e) { Debug.LogException(e, s_Nodes[i]); }

                s_InputStart[i] = s_AllInputs.Count;
                for (int k = 0; k < s_Inputs.Count; k++)
                    if (s_Inputs[k] != null) s_AllInputs.Add(s_Inputs[k]);
                s_InputCount[i] = s_AllInputs.Count - s_InputStart[i];

                s_LocalInputs.Clear();
                try { s_Nodes[i].CollectLocalDependencies(s_LocalInputs); }
                catch (Exception e) { Debug.LogException(e, s_Nodes[i]); }
                for (int k = 0; k < s_LocalInputs.Count; k++)
                    if (s_LocalInputs[k] != null) s_AllInputs.Add(s_LocalInputs[k]);
                s_LocalCount[i] = s_AllInputs.Count - s_InputStart[i] - s_InputCount[i];

                int depth = int.MaxValue;
                for (int k = 0; k < s_Outputs.Count; k++)
                {
                    Transform o = s_Outputs[k];
                    if (o == null) continue;
                    depth = Mathf.Min(depth, HierarchyDepth(o));

                    // Map Transform bị ghi → danh sách constraint ghi nó (linked list trong 2 List song song)
                    int entry = s_WriterNode.Count;
                    s_WriterNode.Add(i);
                    s_WriterNext.Add(s_WriterHead.TryGetValue(o, out int head) ? head : -1);
                    s_WriterHead[o] = entry;
                }
                s_Depth[i] = depth == int.MaxValue ? 0 : depth;
            }

            // 2. Dựng cạnh B → A khi A đọc X và B ghi X hoặc tổ tiên của X
            //    (input local-only: chỉ khi B ghi đúng X, không xét tổ tiên)
            for (int i = 0; i < n; i++)
            {
                s_InDegree[i] = 0;
                s_EdgeMark[i] = -1;
                if (s_Successors[i] == null) s_Successors[i] = new List<int>(4);
                else s_Successors[i].Clear();
            }

            if (s_ResolveDependencies && s_WriterHead.Count > 0)
            {
                for (int a = 0; a < n; a++)
                {
                    int start = s_InputStart[a], localStart = start + s_InputCount[a], end = localStart + s_LocalCount[a];
                    for (int k = start; k < end; k++)
                    {
                        // Đi ngược lên tổ tiên: world pose của X phụ thuộc mọi Transform cha của nó
                        bool localOnly = k >= localStart;
                        for (Transform t = s_AllInputs[k]; t != null; t = localOnly ? null : t.parent)
                        {
                            if (!s_WriterHead.TryGetValue(t, out int entry)) continue;
                            for (; entry >= 0; entry = s_WriterNext[entry])
                            {
                                int b = s_WriterNode[entry];
                                if (b == a || s_EdgeMark[b] == a) continue; // tự phụ thuộc / cạnh trùng
                                s_EdgeMark[b] = a;
                                s_Successors[b].Add(a);
                                s_InDegree[a]++;
                            }
                        }
                    }
                }
            }

            // 3. Topo sort (Kahn) — node sẵn sàng lấy theo khoá ưu tiên nhỏ nhất (min-heap)
            if (s_Sorted.Length < n) s_Sorted = new ConstraintBehaviour[Mathf.NextPowerOfTwo(Mathf.Max(n, 16))];
            else Array.Clear(s_Sorted, n, s_Sorted.Length - n);

            s_Heap.Clear();
            for (int i = 0; i < n; i++)
                if (s_InDegree[i] == 0) HeapPush(i);

            int written = 0;
            bool hasCycle = false;
            while (written < n)
            {
                int pick;
                if (s_Heap.Count > 0)
                {
                    pick = HeapPop();
                }
                else
                {
                    // Vòng lặp phụ thuộc: phá vòng bằng node còn lại có khoá ưu tiên nhỏ nhất
                    // (inDependencyCycle đánh dấu chính xác bằng MarkCycleMembers sau khi sắp xong)
                    hasCycle = true;
                    pick = -1;
                    for (int i = 0; i < n; i++)
                    {
                        if (s_InDegree[i] < 0) continue; // đã xếp
                        if (pick < 0 || Compare(i, pick) < 0) pick = i;
                    }
                }

                s_InDegree[pick] = -1; // đánh dấu đã xếp
                s_Sorted[written] = s_Nodes[pick];
                s_Nodes[pick].resolvedIndex = written;
                written++;

                List<int> succ = s_Successors[pick];
                for (int k = 0; k < succ.Count; k++)
                {
                    int s = succ[k];
                    if (s_InDegree[s] <= 0) continue; // đã xếp (bị phá vòng trước đó)
                    if (--s_InDegree[s] == 0) HeapPush(s);
                }
            }

            s_SortedCount = n;
            if (hasCycle)
            {
                MarkCycleMembers(n);
                LogCycle(n);
            }
            Array.Clear(s_Nodes, 0, n);
            Version++;
        }

        // ── Tarjan SCC (lặp, không đệ quy): chỉ node thuộc thành phần liên thông mạnh ≥ 2 mới thật sự nằm
        //    trong vòng. Node phía SAU vòng (chỉ bị kẹt in-degree khi Kahn dừng) không bị đánh dấu.
        private static int[] s_SccIndex = new int[0];
        private static int[] s_SccLow = new int[0];
        private static int[] s_SccId = new int[0];
        private static int[] s_SccIter = new int[0];
        private static readonly List<int> s_SccStack = new List<int>();
        private static readonly List<int> s_SccCall = new List<int>();
        private static readonly List<int> s_SccMembers = new List<int>();
        private static int s_SccCount;

        private static void MarkCycleMembers(int n)
        {
            if (s_SccIndex.Length < n)
            {
                int cap = s_Nodes.Length;
                s_SccIndex = new int[cap]; s_SccLow = new int[cap]; s_SccId = new int[cap]; s_SccIter = new int[cap];
            }
            for (int i = 0; i < n; i++) { s_SccIndex[i] = -1; s_SccId[i] = -1; s_Nodes[i].inDependencyCycle = false; }
            s_SccStack.Clear();
            s_SccCount = 0;
            int counter = 0;

            for (int root = 0; root < n; root++)
            {
                if (s_SccIndex[root] >= 0) continue;
                s_SccIndex[root] = s_SccLow[root] = counter++;
                s_SccIter[root] = 0;
                s_SccStack.Add(root);
                s_SccCall.Clear();
                s_SccCall.Add(root);

                while (s_SccCall.Count > 0)
                {
                    int v = s_SccCall[s_SccCall.Count - 1];
                    List<int> succ = s_Successors[v];
                    if (s_SccIter[v] < succ.Count)
                    {
                        int w = succ[s_SccIter[v]++];
                        if (s_SccIndex[w] < 0)
                        {
                            s_SccIndex[w] = s_SccLow[w] = counter++;
                            s_SccIter[w] = 0;
                            s_SccStack.Add(w);
                            s_SccCall.Add(w);
                        }
                        else if (s_SccId[w] < 0) // w còn trên stack
                            s_SccLow[v] = Mathf.Min(s_SccLow[v], s_SccIndex[w]);
                        continue;
                    }

                    s_SccCall.RemoveAt(s_SccCall.Count - 1);
                    if (s_SccCall.Count > 0)
                    {
                        int parent = s_SccCall[s_SccCall.Count - 1];
                        s_SccLow[parent] = Mathf.Min(s_SccLow[parent], s_SccLow[v]);
                    }
                    if (s_SccLow[v] != s_SccIndex[v]) continue;

                    // v là gốc 1 SCC → pop stack tới v
                    int id = s_SccCount++, size = 0, top;
                    s_SccMembers.Clear();
                    do
                    {
                        top = s_SccStack[s_SccStack.Count - 1];
                        s_SccStack.RemoveAt(s_SccStack.Count - 1);
                        s_SccId[top] = id;
                        s_SccMembers.Add(top);
                        size++;
                    } while (top != v);
                    if (size < 2) continue;
                    for (int m = 0; m < s_SccMembers.Count; m++) s_Nodes[s_SccMembers[m]].inDependencyCycle = true;
                }
            }
        }

        private static void LogCycle(int n)
        {
            bool hasNew = false;
            for (int i = 0; i < n; i++)
                if (s_Nodes[i].inDependencyCycle && s_ReportedCycle.Add(s_Nodes[i].GetInstanceID())) hasNew = true;
            if (!hasNew) return;

            var sb = new StringBuilder();
            sb.Append("[ConstraintScheduler] Phát hiện vòng lặp phụ thuộc giữa các constraint — đã phá vòng theo " +
                      "Execution Order/loại/độ sâu hierarchy, 1 constraint trong vòng sẽ đọc pose trễ 1 frame. " +
                      "Bỏ 1 cạnh trong vòng (đổi target/cấu trúc) để hết:");
            for (int id = 0; id < s_SccCount; id++)
            {
                s_SccMembers.Clear();
                for (int i = 0; i < n; i++)
                    if (s_SccId[i] == id && s_Nodes[i].inDependencyCycle) s_SccMembers.Add(i);
                if (s_SccMembers.Count < 2) continue;

                sb.Append("\n Vòng ").Append(s_SccMembers.Count).Append(" constraint (A → B = A phải chạy trước B):");
                for (int m = 0; m < s_SccMembers.Count; m++)
                {
                    int b = s_SccMembers[m];
                    List<int> succ = s_Successors[b];
                    for (int k = 0; k < succ.Count; k++)
                    {
                        int a = succ[k];
                        if (s_SccId[a] != id) continue;
                        sb.Append("\n  • ").Append(Describe(b)).Append(" → ").Append(Describe(a))
                          .Append("  (").Append(EdgeReason(b, a)).Append(')');
                    }
                }
            }
            Debug.LogWarning(sb.ToString());
        }

        private static string Describe(int i)
        {
            ConstraintBehaviour c = s_Nodes[i];
            return "#" + c.resolvedIndex + " " + c.GetType().Name + " @ " + c.name;
        }

        // Lý do cạnh b → a: a đọc Transform X, b ghi X (hoặc tổ tiên của X với input thường).
        private static string EdgeReason(int b, int a)
        {
            int start = s_InputStart[a], localStart = start + s_InputCount[a], end = localStart + s_LocalCount[a];
            for (int k = start; k < end; k++)
            {
                Transform x = s_AllInputs[k];
                bool localOnly = k >= localStart;
                for (Transform t = x; t != null; t = localOnly ? null : t.parent)
                {
                    if (!s_WriterHead.TryGetValue(t, out int entry)) continue;
                    for (; entry >= 0; entry = s_WriterNext[entry])
                        if (s_WriterNode[entry] == b)
                            return t == x ? "đọc " + x.name : "đọc " + x.name + " nằm dưới " + t.name;
                }
            }
            return "?";
        }

        /// <summary>Khoá ưu tiên: ExecutionOrder → DefaultTypeOrder → độ sâu hierarchy → thứ tự đăng ký.</summary>
        private static int Compare(int a, int b)
        {
            ConstraintBehaviour ca = s_Nodes[a], cb = s_Nodes[b];
            int r = ca.ExecutionOrder.CompareTo(cb.ExecutionOrder);
            if (r != 0) return r;
            r = ca.DefaultTypeOrder.CompareTo(cb.DefaultTypeOrder);
            if (r != 0) return r;
            r = s_Depth[a].CompareTo(s_Depth[b]);
            if (r != 0) return r;
            return ca.schedulerSerial.CompareTo(cb.schedulerSerial);
        }

        private static int HierarchyDepth(Transform t)
        {
            int d = 0;
            while (t.parent != null) { t = t.parent; d++; }
            return d;
        }

        private static void EnsureCapacity(int n)
        {
            if (s_Nodes.Length >= n) return;
            int cap = Mathf.NextPowerOfTwo(Mathf.Max(n, 16));
            Array.Resize(ref s_Nodes, cap);
            Array.Resize(ref s_InputStart, cap);
            Array.Resize(ref s_InputCount, cap);
            Array.Resize(ref s_LocalCount, cap);
            Array.Resize(ref s_Depth, cap);
            Array.Resize(ref s_InDegree, cap);
            Array.Resize(ref s_EdgeMark, cap);
            Array.Resize(ref s_Successors, cap);
        }

        // ── Min-heap trên index node, so sánh bằng Compare() ──────────────────────────
        private static void HeapPush(int node)
        {
            s_Heap.Add(node);
            int i = s_Heap.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (Compare(s_Heap[i], s_Heap[parent]) >= 0) break;
                (s_Heap[i], s_Heap[parent]) = (s_Heap[parent], s_Heap[i]);
                i = parent;
            }
        }

        private static int HeapPop()
        {
            int top = s_Heap[0];
            int last = s_Heap.Count - 1;
            s_Heap[0] = s_Heap[last];
            s_Heap.RemoveAt(last);

            int i = 0, count = s_Heap.Count;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, min = i;
                if (l < count && Compare(s_Heap[l], s_Heap[min]) < 0) min = l;
                if (r < count && Compare(s_Heap[r], s_Heap[min]) < 0) min = r;
                if (min == i) break;
                (s_Heap[i], s_Heap[min]) = (s_Heap[min], s_Heap[i]);
                i = min;
            }
            return top;
        }
    }
}
