// =============================================================
//  ClipTrimmerTab.cs
//  Tab "✂️ Cắt Clip" cho AnimatorControllerCreator.
//
//  CÁCH DÙNG:
//    1. Copy file này vào cùng thư mục với AnimatorControllerCreator.cs
//    2. Trong AnimatorControllerCreator.cs, áp dụng 3 patch nhỏ
//       (xem file PATCH_INSTRUCTIONS.md đi kèm).
//    3. Khi gọi tab.Draw(...), truyền thêm position.height của
//       EditorWindow để nút "Cắt & Lưu" chia đúng tỉ lệ 9.4/0.6
//       giống các tab khác — VD: tab.Draw(so, prop, position.height).
//
//  TÍNH NĂNG:
//    • Kéo thả nhiều AnimationClip vào danh sách.
//    • Nhập Start Frame / End Frame cho từng clip.
//    • Tick chọn clip, dùng 4 nút "Start -1/+1" / "End -1/+1" để chỉnh
//      hàng loạt các clip đang được chọn.
//    • Preview thông tin (duration, frame range).
//    • Nút "✂️ Cắt & Lưu" — tạo clip mới đã cắt, lưu ra disk.
//    • Tên clip mới = GIỮ NGUYÊN tên gốc (không thêm suffix) — để
//      AnimatorControllerCreator vẫn tự nhận diện & gán Motion đúng
//      theo tên khi build Animator Controller.
//    • Toggle "Ghi đè clip gốc":
//        ON  → cắt xong ghi thẳng vào clip gốc (giữ nguyên GUID/reference),
//              ô thư mục output bị ẩn.
//        OFF → lưu ra thư mục output riêng (tool chặn nếu trùng thư mục gốc),
//              cắt lại cùng tên sẽ tự ghi đè bản cắt trước trong output.
// =============================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Toàn bộ state + UI logic cho Tab "Cắt Clip".
/// Được khởi tạo và gọi từ AnimatorControllerCreator.
/// </summary>
[System.Serializable]
public class ClipTrimmerTab
{
    // ── Serialized (giữ qua domain reload) ───────────────────
    [SerializeField] public List<AnimationClip> trimClips   = new List<AnimationClip>();
    [SerializeField] private List<int>          startFrames = new List<int>();
    [SerializeField] private List<int>          endFrames   = new List<int>();
    [SerializeField] private List<bool>         selected    = new List<bool>();

    // ── UI State ──────────────────────────────────────────────
    private Vector2 scroll;
    private string  outputPath = "Assets/Animations/Trimmed";

    [SerializeField] private bool overrideExisting = true;

    // ── Styles (khởi tạo lazy) ────────────────────────────────
    private GUIStyle _labelGray;
    private GUIStyle _labelGreen;
    private GUIStyle _labelRed;
    private GUIStyle _headerStyle;

    private GUIStyle LabelGray  => _labelGray  ??= new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
    private GUIStyle LabelGreen => _labelGreen ??= new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.4f, 0.9f, 0.4f) } };
    private GUIStyle LabelRed   => _labelRed   ??= new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(1f,   0.4f, 0.4f) } };
    private GUIStyle HeaderStyle => _headerStyle ??= new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };

    // ─────────────────────────────────────────────────────────
    // ENTRY POINT — gọi từ AnimatorControllerCreator.OnGUI()
    // ─────────────────────────────────────────────────────────
    public void Draw(SerializedObject ownerSO, SerializedProperty trimClipsProp, float windowHeight = 680f)
    {
        GetSplitHeights(windowHeight, out float scrollHeight, out float footerHeight);

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(scrollHeight));
        EditorGUILayout.Space(4);

        // ── Override toggle ───────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        overrideExisting = EditorGUILayout.Toggle(overrideExisting, GUILayout.Width(18));
        EditorGUILayout.LabelField(
            overrideExisting
                ? "Ghi đè clip gốc  (không dùng thư mục output)"
                : "Lưu ra thư mục output  (không ghi đè clip gốc)",
            overrideExisting ? LabelGreen : LabelGray);
        EditorGUILayout.EndHorizontal();

        // ── Output folder (ẩn khi override) ──────────────────
        if (!overrideExisting)
        {
            EditorGUILayout.LabelField("Thư mục lưu kết quả", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            outputPath = EditorGUILayout.TextField(outputPath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string chosen = EditorUtility.OpenFolderPanel("Chọn thư mục lưu clip đã cắt", outputPath, "");
                if (!string.IsNullOrEmpty(chosen) && chosen.StartsWith(Application.dataPath))
                    outputPath = "Assets" + chosen.Substring(Application.dataPath.Length);
            }
            EditorGUILayout.EndHorizontal();
        }

        DrawSeparator();

        // ── Drag & drop list ──────────────────────────────────
        EditorGUILayout.LabelField("Danh sách Clips cần cắt", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(trimClipsProp, new GUIContent("Clips"), true);
        bool listChanged = EditorGUI.EndChangeCheck();
        ownerSO.ApplyModifiedProperties();

        // Đồng bộ startFrames / endFrames với số lượng clip
        SyncFrameLists(listChanged);

        bool hasClips = trimClips != null && trimClips.Count > 0;

        if (!hasClips)
        {
            EditorGUILayout.EndScrollView();
            DrawFooter(footerHeight);
            return;
        }

        DrawSeparator();

        // ── Bảng nhập frame ───────────────────────────────────
        EditorGUILayout.LabelField("Thiết lập Frame Range", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        // ── 4 nút bulk ±1 frame cho các clip đang được chọn ───
        int selCount = CountSelected();
        GUI.enabled = selCount > 0;

        GUIStyle bulkBtn = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold, fixedHeight = 24 };

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Start  -1", bulkBtn)) BulkShift(shiftStart: -1, shiftEnd: 0);
        if (GUILayout.Button("Start  +1", bulkBtn)) BulkShift(shiftStart: +1, shiftEnd: 0);
        GUILayout.Space(10);
        if (GUILayout.Button("End  -1", bulkBtn)) BulkShift(shiftStart: 0, shiftEnd: -1);
        if (GUILayout.Button("End  +1", bulkBtn)) BulkShift(shiftStart: 0, shiftEnd: +1);
        EditorGUILayout.EndHorizontal();

        GUI.enabled = true;
        EditorGUILayout.Space(4);

        // Header
        EditorGUILayout.BeginHorizontal();
        bool allSelected = selCount == CountNonNull();
        bool newAll = EditorGUILayout.Toggle(allSelected, GUILayout.Width(18));
        if (newAll != allSelected)
            for (int i = 0; i < selected.Count; i++)
                if (i < trimClips.Count && trimClips[i] != null)
                    selected[i] = newAll;

        EditorGUILayout.LabelField("Clip",         HeaderStyle, GUILayout.Width(180));
        EditorGUILayout.LabelField("FPS / Frames", HeaderStyle, GUILayout.Width(100));
        EditorGUILayout.LabelField("Start Frame",  HeaderStyle, GUILayout.Width(80));
        EditorGUILayout.LabelField("End Frame",    HeaderStyle, GUILayout.Width(80));
        EditorGUILayout.LabelField("Tên output",   HeaderStyle);
        EditorGUILayout.EndHorizontal();

        DrawSeparator();

        for (int i = 0; i < trimClips.Count; i++)
        {
            var clip = trimClips[i];

            EditorGUILayout.BeginHorizontal();

            if (clip == null)
            {
                selected[i] = false;
                EditorGUILayout.Toggle(false, GUILayout.Width(18));
                EditorGUILayout.LabelField($"[{i}] (null)", LabelGray, GUILayout.Width(180));
                EditorGUILayout.LabelField("—", LabelGray, GUILayout.Width(100));
                startFrames[i] = EditorGUILayout.IntField(startFrames[i], GUILayout.Width(80));
                endFrames[i]   = EditorGUILayout.IntField(endFrames[i],   GUILayout.Width(80));
                EditorGUILayout.LabelField("—", LabelGray);
                EditorGUILayout.EndHorizontal();
                continue;
            }

            selected[i] = EditorGUILayout.Toggle(selected[i], GUILayout.Width(18));

            float fps        = clip.frameRate;
            int   totalFrames = Mathf.RoundToInt(clip.length * fps);

            // Clamp giá trị hợp lệ
            startFrames[i] = Mathf.Clamp(startFrames[i], 0, totalFrames);
            endFrames[i]   = Mathf.Clamp(endFrames[i],   0, totalFrames);

            bool valid = endFrames[i] > startFrames[i];

            // Col: tên clip
            EditorGUILayout.LabelField($"[{i}] {clip.name}", GUILayout.Width(180));

            // Col: fps / tổng frames
            EditorGUILayout.LabelField($"{fps:F0} fps / {totalFrames}f", LabelGray, GUILayout.Width(100));

            // Col: start
            startFrames[i] = EditorGUILayout.IntField(startFrames[i], GUILayout.Width(80));

            // Col: end
            endFrames[i] = EditorGUILayout.IntField(endFrames[i], GUILayout.Width(80));

            // Col: tên output (giữ nguyên tên gốc — chỉ hiển thị thêm frame range để tham khảo)
            if (!valid)
                EditorGUILayout.LabelField("⚠ End phải > Start", LabelRed);
            else
            {
                float newDur = (endFrames[i] - startFrames[i]) / fps;
                EditorGUILayout.LabelField($"{clip.name}  [{startFrames[i]}-{endFrames[i]}]  ({newDur:F3}s)", LabelGreen);
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.EndScrollView();

        DrawFooter(footerHeight);
    }

    // ─────────────────────────────────────────────────────────
    // FOOTER: nút "Cắt & Lưu" — không tô màu, cao theo footerHeight
    // ─────────────────────────────────────────────────────────
    private void DrawFooter(float footerHeight)
    {
        EditorGUILayout.BeginVertical(GUILayout.Height(footerHeight));
        GUILayout.FlexibleSpace();

        int validCount = CountValid();
        GUI.enabled = validCount > 0;

        GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
            { fontSize = 14, fontStyle = FontStyle.Bold, fixedHeight = Mathf.Clamp(footerHeight - 16f, 28f, 80f) };

        if (GUILayout.Button(validCount > 0
            ? $"Cắt & Lưu  ({validCount} clip hợp lệ)"
            : "Cắt & Lưu  (chưa có clip hợp lệ)", btnStyle))
            ExecuteTrim();

        GUI.enabled = true;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndVertical();
    }

    // ─────────────────────────────────────────────────────────
    // Chia vùng nội dung theo tỉ lệ 9.4/0.6 (scroll : footer),
    // giống hệt GetSplitHeights() bên AnimatorControllerCreator.
    // ─────────────────────────────────────────────────────────
    private void GetSplitHeights(float windowHeight, out float scrollHeight, out float footerHeight)
    {
        const float toolbarReserved = 45f; // Space(3) + Toolbar(20) + Space(10) + lề
        float available = Mathf.Max(windowHeight - toolbarReserved, 200f);
        scrollHeight = available * 0.94f;
        footerHeight = available * 0.06f;
    }

    // ─────────────────────────────────────────────────────────
    // SYNC
    // ─────────────────────────────────────────────────────────
    private void SyncFrameLists(bool forceSync)
    {
        if (trimClips == null) trimClips = new List<AnimationClip>();

        int target = trimClips.Count;

        while (startFrames.Count < target) startFrames.Add(0);
        while (endFrames.Count  < target) endFrames.Add(0);
        while (selected.Count   < target) selected.Add(false);

        // Khi list rút ngắn (user xóa clip), gán end = totalFrames nếu vẫn là 0
        if (forceSync)
        {
            for (int i = 0; i < target; i++)
            {
                var clip = trimClips[i];
                if (clip != null && endFrames[i] == 0)
                    endFrames[i] = Mathf.RoundToInt(clip.length * clip.frameRate);
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    // COUNT SELECTED / NON-NULL
    // ─────────────────────────────────────────────────────────
    private int CountSelected()
    {
        int count = 0;
        for (int i = 0; i < selected.Count; i++)
            if (selected[i] && i < trimClips.Count && trimClips[i] != null)
                count++;
        return count;
    }

    private int CountNonNull()
    {
        int count = 0;
        if (trimClips == null) return 0;
        foreach (var c in trimClips) if (c != null) count++;
        return count;
    }

    // ─────────────────────────────────────────────────────────
    // BULK SHIFT ±1 FRAME CHO CÁC CLIP ĐANG ĐƯỢC CHỌN
    // ─────────────────────────────────────────────────────────
    private void BulkShift(int shiftStart, int shiftEnd)
    {
        for (int i = 0; i < trimClips.Count; i++)
        {
            if (!selected[i] || trimClips[i] == null) continue;
            int total = Mathf.RoundToInt(trimClips[i].length * trimClips[i].frameRate);
            startFrames[i] = Mathf.Clamp(startFrames[i] + shiftStart, 0, total);
            endFrames[i]   = Mathf.Clamp(endFrames[i]   + shiftEnd,   0, total);
        }
    }

    // ─────────────────────────────────────────────────────────
    // COUNT VALID
    // ─────────────────────────────────────────────────────────
    private int CountValid()
    {
        int count = 0;
        for (int i = 0; i < trimClips.Count; i++)
        {
            if (trimClips[i] != null && endFrames[i] > startFrames[i])
                count++;
        }
        return count;
    }

    // ─────────────────────────────────────────────────────────
    // EXECUTE TRIM
    // ─────────────────────────────────────────────────────────
    private void ExecuteTrim()
    {
        // Đảm bảo thư mục tồn tại
        EnsureFolder(outputPath);

        int success = 0, skip = 0, fail = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < trimClips.Count; i++)
            {
                var clip = trimClips[i];
                if (clip == null || endFrames[i] <= startFrames[i]) { skip++; continue; }

                try
                {
                    bool skipped = TrimClip(clip, startFrames[i], endFrames[i], overrideExisting);
                    if (skipped) skip++;
                    else         success++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[ClipTrimmer] Lỗi cắt '{clip.name}': {ex.Message}");
                    fail++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog(
            "✅ Cắt Clip hoàn tất",
            $"Thành công : {success} clip\n" +
            $"Bỏ qua     : {skip} clip (null hoặc frame không hợp lệ)\n" +
            $"Lỗi        : {fail} clip (xem Console)\n\n" +
            $"Clip đã lưu tại: {outputPath}",
            "OK");
    }

    // ─────────────────────────────────────────────────────────
    // CORE: CẮT 1 CLIP
    // ─────────────────────────────────────────────────────────
    /// <returns><c>true</c> nếu bị skip (file đã tồn tại và overrideExisting==false).</returns>
    private bool TrimClip(AnimationClip source, int startFrame, int endFrame, bool overrideExisting)
    {
        float fps      = source.frameRate;
        float startTime = startFrame / fps;
        float endTime   = endFrame   / fps;
        float newLength = endTime - startTime;

        // Tạo clip mới
        var newClip = new AnimationClip
        {
            name      = BuildOutputName(source.name, startFrame, endFrame),
            frameRate = fps,
            wrapMode  = source.wrapMode,
        };

        // Copy settings
        AnimationUtility.SetAnimationClipSettings(newClip,
            AnimationUtility.GetAnimationClipSettings(source));

        // ── Copy tất cả EditorCurveBinding (Float curves) ────
        foreach (var binding in AnimationUtility.GetCurveBindings(source))
        {
            var srcCurve = AnimationUtility.GetEditorCurve(source, binding);
            var newCurve = TrimFloatCurve(srcCurve, startTime, endTime);
            AnimationUtility.SetEditorCurve(newClip, binding, newCurve);
        }

        // ── Copy Object Reference curves ─────────────────────
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
        {
            var keys    = AnimationUtility.GetObjectReferenceCurve(source, binding);
            var newKeys = TrimObjectCurve(keys, startTime, endTime);
            AnimationUtility.SetObjectReferenceCurve(newClip, binding, newKeys);
        }

        // ── Copy Events ───────────────────────────────────────
        var srcEvents = AnimationUtility.GetAnimationEvents(source);
        var newEvents = new System.Collections.Generic.List<AnimationEvent>();
        foreach (var ev in srcEvents)
        {
            if (ev.time >= startTime && ev.time <= endTime)
            {
                var ne = new AnimationEvent();
                ne.time             = ev.time - startTime;
                ne.functionName     = ev.functionName;
                ne.floatParameter   = ev.floatParameter;
                ne.intParameter     = ev.intParameter;
                ne.stringParameter  = ev.stringParameter;
                ne.objectReferenceParameter = ev.objectReferenceParameter;
                ne.messageOptions   = ev.messageOptions;
                newEvents.Add(ne);
            }
        }
        AnimationUtility.SetAnimationEvents(newClip, newEvents.ToArray());

        // ── Lưu asset ─────────────────────────────────────────
        string sourcePath = AssetDatabase.GetAssetPath(source);

        if (overrideExisting)
        {
            // Ghi đè thẳng vào clip gốc — CopySerialized giữ nguyên GUID/reference
            EditorUtility.CopySerialized(newClip, source);
            EditorUtility.SetDirty(source);
            Debug.Log($"[ClipTrimmer] ✅ Đã ghi đè clip gốc: {sourcePath} [{startFrame}-{endFrame}]");
        }
        else
        {
            // Lưu ra outputPath
            string assetPath   = $"{outputPath}/{newClip.name}.anim";

            if (string.Equals(assetPath, sourcePath, System.StringComparison.OrdinalIgnoreCase))
                throw new System.Exception(
                    $"Thư mục output trùng với thư mục clip gốc — sẽ ghi đè '{source.name}'. Hãy chọn thư mục output khác.");

            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath);

            AssetDatabase.CreateAsset(newClip, assetPath);
            Debug.Log($"[ClipTrimmer] ✅ Đã cắt: {source.name} [{startFrame}-{endFrame}] → {assetPath}");
        }

        return false; // not skipped
    }

    // ─────────────────────────────────────────────────────────
    // HELPER: CẮT FLOAT CURVE
    // ─────────────────────────────────────────────────────────
    private static AnimationCurve TrimFloatCurve(AnimationCurve src, float startTime, float endTime)
    {
        var keys = new List<Keyframe>();

        // Lấy giá trị tại startTime (nội suy nếu cần)
        bool addedStart = false;

        foreach (var kf in src.keys)
        {
            if (kf.time < startTime - 0.0001f) continue;
            if (kf.time > endTime   + 0.0001f) break;

            // Nếu key đầu tiên trong range chưa ở đúng startTime → thêm key tại startTime
            if (!addedStart && kf.time > startTime + 0.0001f)
            {
                float v = src.Evaluate(startTime);
                keys.Add(new Keyframe(0f, v));
                addedStart = true;
            }

            var newKf = kf;
            newKf.time = Mathf.Max(0f, kf.time - startTime);
            keys.Add(newKf);
            addedStart = true;
        }

        // Đảm bảo luôn có key tại startTime
        if (!addedStart)
        {
            float v = src.Evaluate(startTime);
            keys.Add(new Keyframe(0f, v));
        }

        // Đảm bảo có key tại endTime (nội suy)
        float lastT = keys.Count > 0 ? keys[keys.Count - 1].time : -1f;
        float expectedEnd = endTime - startTime;
        if (lastT < expectedEnd - 0.0001f)
        {
            float v = src.Evaluate(endTime);
            keys.Add(new Keyframe(expectedEnd, v));
        }

        return new AnimationCurve(keys.ToArray());
    }

    // ─────────────────────────────────────────────────────────
    // HELPER: CẮT OBJECT REFERENCE CURVE
    // ─────────────────────────────────────────────────────────
    private static ObjectReferenceKeyframe[] TrimObjectCurve(
        ObjectReferenceKeyframe[] src, float startTime, float endTime)
    {
        var result = new List<ObjectReferenceKeyframe>();
        foreach (var kf in src)
        {
            if (kf.time < startTime - 0.0001f) continue;
            if (kf.time > endTime   + 0.0001f) break;
            result.Add(new ObjectReferenceKeyframe
            {
                time  = Mathf.Max(0f, kf.time - startTime),
                value = kf.value
            });
        }
        return result.ToArray();
    }

    // ─────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────
    /// <summary>
    /// Tên clip output = tên gốc, KHÔNG thêm suffix "_trim_start-end".
    /// Lý do: AnimatorControllerCreator tự động gán Motion cho state bằng
    /// cách so khớp tên clip (AddStateAndAssignClip) — nếu đổi tên, clip
    /// đã cắt sẽ không còn khớp với tên state nữa và bị bỏ trống Motion.
    /// </summary>
    private static string BuildOutputName(string sourceName, int start, int end)
        => sourceName;

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        // Tạo từng cấp folder nếu chưa có
        string[] parts  = path.Split('/');
        string   current = parts[0]; // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void DrawSeparator()
    {
        EditorGUILayout.Space(4);
        Rect r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(0.4f, 0.4f, 0.4f, 0.4f));
        EditorGUILayout.Space(4);
    }
}
#endif