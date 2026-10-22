// GaMoInspector.AnimEdit.cs  -  part of CustomsEditor (GaMo Inspector)
// Panel "Edit Animation" cho AnimationClip: chon khoang [Start, End] (theo frame) roi cat clip ngay tren inspector.
//  - Clip trong FBX (sub-asset cua Model): ap vao Import Settings (firstFrame / lastFrame), khong pha file FBX.
//  - File .anim: Overwrite (co Undo) hoac Save As ra file .anim moi.
//  - Moi loai clip deu Save As duoc. Cat = giu key trong khoang, them key bien o 2 dau, dich ve t = 0, cat ca event / sprite curve.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UIE = UnityEngine.UIElements;
using UObject = UnityEngine.Object;

namespace GaMo.BlenderEditorTools
{
    public partial class CustomsEditor
    {
        const string AnOpenKey = "GaMo.CustomsEditor.AnimEditOpen";

        bool _anOpen, _anOpenInit;
        AnimationClip _anFor;       // clip đang chỉnh (đổi clip => reset khoảng chọn)
        string _anPath = "";
        int _anMode;                // 0 = chỉ Save As, 1 = clip của Model (FBX), 2 = file .anim
        float _anA, _anB;           // khoảng giữ lại (giây, trên clip hiện tại)
        bool _anPreview, _anRangeWasOn;

        const string AnTipRange =
            "Khoảng GIỮ LẠI (đơn vị frame). Phần ngoài khoảng sẽ bị cắt bỏ, phần giữ lại được dời về t = 0.";

        // =====================================================
        //  Gắn vào thân Asset (gọi từ BuildAssetBody)
        // =====================================================
        void AddAnimEditSection(UIE.VisualElement cc)
        {
            if (!(_asset is AnimationClip) || _assets.Length != 1) return;
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(_asset))) return;

            var sec = new UIE.VisualElement();
            sec.style.marginBottom = 8;
            sec.Add(AssetChunk(DrawAnimEditChunk, true));
            cc.Add(sec);
        }

        void AnReset(AnimationClip clip)
        {
            _anFor = clip;
            _anA = 0f;
            _anB = clip != null ? clip.length : 0f;
            _anPath = clip != null ? AssetDatabase.GetAssetPath(clip) : "";
            _anMode = 0;
            if (string.IsNullOrEmpty(_anPath)) return;
            var imp = AssetImporter.GetAtPath(_anPath);
            if (imp is ModelImporter) _anMode = 1;
            else if (AssetDatabase.IsMainAsset(clip) && _anPath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)) _anMode = 2;
        }

        // =====================================================
        //  Vẽ panel
        // =====================================================
        void DrawAnimEditChunk()
        {
            var clip = _asset as AnimationClip;
            if (clip == null) return;
            var ev = Event.current;

            if (ev.type == EventType.Layout && _anFor != clip) AnReset(clip);
            if (_anFor != clip) return; // chưa Layout lần nào cho clip này

            if (!_anOpenInit)
            {
                _anOpenInit = true;
                _anOpen = EditorPrefs.GetBool(AnOpenKey, false);
            }

            bool open;
            var oldIcon = EditorGUIUtility.GetIconSize();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                open = GUILayout.Toggle(_anOpen, GUIContent.none, EditorStyles.foldout, GUILayout.Width(14), GUILayout.Height(18));
                EditorGUIUtility.SetIconSize(new Vector2(16, 16));
                GUILayout.Label(new GUIContent(" Edit Animation", EditorGUIUtility.IconContent("AnimationClip Icon").image),
                    EditorStyles.boldLabel, GUILayout.Height(18));
                EditorGUIUtility.SetIconSize(oldIcon);
                GUILayout.FlexibleSpace();
            }
            if (open != _anOpen)
            {
                _anOpen = open;
                EditorPrefs.SetBool(AnOpenKey, open);
                Repaint();
            }
            if (!_anOpen) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.inspectorDefaultMargins))
                AnMainGUI(clip);
        }

        static bool AnBtn(string label, string tip, bool enabled)
        {
            using (new EditorGUI.DisabledScope(!enabled))
                return GUILayout.Button(new GUIContent(label, tip), EditorStyles.miniButton);
        }

        void AnMainGUI(AnimationClip clip)
        {
            float len = Mathf.Max(0.0001f, clip.length);
            float fps = clip.frameRate > 0f ? clip.frameRate : 30f;
            float total = Mathf.Max(1f, Mathf.Round(len * fps));
            bool canTrim = total >= 2f;

            GUILayout.Space(2);
            GUILayout.Label(len.ToString("0.000") + "s · " + fps.ToString("0.##") + " fps · " + total + " frames",
                EditorStyles.miniLabel);

            float fa = Mathf.Clamp(Mathf.Round(_anA * fps), 0f, total - 1f);
            float fb = Mathf.Clamp(Mathf.Round(_anB * fps), fa + 1f, total);

            float oldLw = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 90f;
            try
            {
                using (new EditorGUI.DisabledScope(!canTrim))
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.MinMaxSlider(new GUIContent("Range", AnTipRange), ref fa, ref fb, 0f, total);
                    int ia = EditorGUILayout.IntField("Start (frame)", Mathf.RoundToInt(fa));
                    int ib = EditorGUILayout.IntField("End (frame)", Mathf.RoundToInt(fb));
                    if (EditorGUI.EndChangeCheck())
                    {
                        fa = Mathf.Clamp(ia, 0f, total - 1f);
                        fb = Mathf.Clamp(ib, fa + 1f, total);
                        _anA = fa / fps;
                        _anB = fb >= total ? len : fb / fps;
                    }
                }
            }
            finally { EditorGUIUtility.labelWidth = oldLw; }

            float keep = Mathf.Max(0f, _anB - _anA);
            bool full = _anA <= 0.0005f && _anB >= len - 0.0005f;
            GUILayout.Label("Giữ " + (_anA).ToString("0.000") + "s – " + (_anB).ToString("0.000") + "s  →  clip dài "
                            + keep.ToString("0.000") + "s (" + Mathf.Round(keep * fps) + " frames)", EditorStyles.miniLabel);
            
            EditorGUI.BeginChangeCheck();
            _anPreview = EditorGUILayout.ToggleLeft(new GUIContent("Preview khoảng chọn (loop)",
                "Khung Preview ở đáy chỉ chạy lặp đúng đoạn đang chọn"), _anPreview);
            if (EditorGUI.EndChangeCheck()) RepaintPreviewOnly();

            GUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (AnBtn("Reset", "Chọn lại cả clip", !full)) { _anA = 0f; _anB = len; GUI.FocusControl(null); }

                if (_anMode == 1)
                {
                    bool ok = canTrim && !full && !_assetImmutable;
                    if (AnBtn("Apply to Import Settings",
                            "Đổi First / Last frame của clip này trong Model Import Settings (không sửa file FBX, chỉnh lại được bất cứ lúc nào).", ok))
                        AnQueueApplyImporter(clip, _anA, _anB);
                }
                else if (_anMode == 2)
                {
                    bool ok = canTrim && !full && !_assetImmutable && !_anPath.StartsWith("Packages/", StringComparison.Ordinal);
                    if (AnBtn("Overwrite", "Cắt trực tiếp file .anim gốc (có hỏi xác nhận, Ctrl+Z được).", ok))
                        AnQueueOverwrite(clip, _anA, _anB);
                }

                if (AnBtn("Save As…", "Cắt rồi lưu thành file .anim mới (giữ nguyên clip gốc).", canTrim))
                    AnQueueSaveAs(clip, _anA, _anB);
            }

            if (_anMode == 1)
                GUILayout.Label("Clip của FBX: cắt bằng cách đổi First/Last frame ở Import Settings; Save As để xuất ra .anim riêng.",
                    EditorStyles.miniLabel);
        }

        // =====================================================
        //  Lõi cắt
        // =====================================================
        sealed class AnTrimmed
        {
            public readonly List<EditorCurveBinding> fb = new List<EditorCurveBinding>();
            public readonly List<AnimationCurve> fc = new List<AnimationCurve>();
            public readonly List<EditorCurveBinding> ob = new List<EditorCurveBinding>();
            public readonly List<ObjectReferenceKeyframe[]> oc = new List<ObjectReferenceKeyframe[]>();
            public readonly List<EditorCurveBinding> obAll = new List<EditorCurveBinding>();
            public AnimationEvent[] ev;
            public AnimationClipSettings st;
            public float len;
        }

        const float AnEps = 1e-3f;

        static Keyframe AnBoundaryKey(AnimationCurve c, float t, float newTime)
        {
            float v = c.Evaluate(t);
            float inT = (v - c.Evaluate(t - AnEps)) / AnEps;
            float outT = (c.Evaluate(t + AnEps) - v) / AnEps;
            var ks = c.keys;
            for (int i = ks.Length - 1; i >= 0; i--)
            {
                if (ks[i].time > t) continue;
                if (float.IsInfinity(ks[i].outTangent)) inT = outT = float.PositiveInfinity; // curve dạng bước (Constant): giữ bước
                break;
            }
            if (float.IsNaN(inT)) inT = 0f;
            if (float.IsNaN(outT)) outT = 0f;
            return new Keyframe(newTime, v, inT, outT);
        }

        static AnimationCurve AnTrimCurve(AnimationCurve c, float a, float b)
        {
            float len = b - a;
            var keys = c.keys;
            var res = new List<Keyframe>(keys.Length + 2);
            foreach (var k in keys)
            {
                if (k.time < a - AnEps || k.time > b + AnEps) continue;
                var kk = k; // copy struct => giữ tangent mode
                kk.time = Mathf.Clamp(k.time - a, 0f, len);
                res.Add(kk);
            }
            if (res.Count == 0 || res[0].time > AnEps) res.Insert(0, AnBoundaryKey(c, a, 0f));
            if (res[res.Count - 1].time < len - AnEps) res.Add(AnBoundaryKey(c, b, len));

            var nc = new AnimationCurve(res.ToArray());
            nc.preWrapMode = c.preWrapMode;
            nc.postWrapMode = c.postWrapMode;
            return nc;
        }

        static ObjectReferenceKeyframe[] AnTrimObj(ObjectReferenceKeyframe[] ks, float a, float b)
        {
            if (ks == null || ks.Length == 0) return null;
            var list = new List<ObjectReferenceKeyframe>();
            bool hasPrev = false;
            ObjectReferenceKeyframe prev = default(ObjectReferenceKeyframe);
            foreach (var k in ks)
            {
                if (k.time <= a + AnEps) { prev = k; hasPrev = true; }
                else if (k.time <= b + AnEps)
                {
                    var kk = k;
                    kk.time = Mathf.Max(0f, k.time - a);
                    list.Add(kk);
                }
            }
            if (hasPrev && (list.Count == 0 || list[0].time > AnEps))
                list.Insert(0, new ObjectReferenceKeyframe { time = 0f, value = prev.value });
            return list.Count > 0 ? list.ToArray() : null;
        }

        static AnTrimmed AnCollect(AnimationClip src, float a, float b)
        {
            var d = new AnTrimmed { len = b - a };

            foreach (var bd in AnimationUtility.GetCurveBindings(src))
            {
                var c = AnimationUtility.GetEditorCurve(src, bd);
                if (c == null || c.length == 0) continue;
                d.fb.Add(bd);
                d.fc.Add(AnTrimCurve(c, a, b));
            }
            foreach (var bd in AnimationUtility.GetObjectReferenceCurveBindings(src))
            {
                d.obAll.Add(bd);
                var t = AnTrimObj(AnimationUtility.GetObjectReferenceCurve(src, bd), a, b);
                if (t == null) continue;
                d.ob.Add(bd);
                d.oc.Add(t);
            }

            var evs = AnimationUtility.GetAnimationEvents(src);
            var keep = new List<AnimationEvent>();
            if (evs != null)
                foreach (var e in evs)
                {
                    if (e.time < a - AnEps || e.time > b + AnEps) continue;
                    e.time = Mathf.Clamp(e.time - a, 0f, d.len);
                    keep.Add(e);
                }
            d.ev = keep.ToArray();

            d.st = AnimationUtility.GetAnimationClipSettings(src);
            d.st.startTime = 0f;
            d.st.stopTime = d.len;
            return d;
        }

        static void AnApply(AnimationClip dst, AnTrimmed d, bool clearFirst)
        {
            if (clearFirst)
            {
                dst.ClearCurves();
                foreach (var bd in d.obAll) AnimationUtility.SetObjectReferenceCurve(dst, bd, null);
            }
            for (int i = 0; i < d.fb.Count; i++) AnimationUtility.SetEditorCurve(dst, d.fb[i], d.fc[i]);
            for (int i = 0; i < d.ob.Count; i++) AnimationUtility.SetObjectReferenceCurve(dst, d.ob[i], d.oc[i]);
            AnimationUtility.SetAnimationEvents(dst, d.ev);
            AnimationUtility.SetAnimationClipSettings(dst, d.st);
        }

        // =====================================================
        //  Các thao tác (chạy ở delayCall: ngoài vòng OnGUI cho an toàn)
        // =====================================================
        void AnQueueSaveAs(AnimationClip clip, float a, float b)
        {
            string srcPath = AssetDatabase.GetAssetPath(clip);
            EditorApplication.delayCall += () =>
            {
                if (this == null || clip == null) return;
                string dir = Path.GetDirectoryName(srcPath);
                dir = string.IsNullOrEmpty(dir) ? "Assets" : dir.Replace('\\', '/');
                string p = EditorUtility.SaveFilePanelInProject("Save Trimmed Animation", clip.name + "_trim", "anim",
                    "Lưu clip đã cắt thành file .anim", dir);
                if (string.IsNullOrEmpty(p)) return;

                try
                {
                    var d = AnCollect(clip, a, b);
                    var nc = new AnimationClip
                    {
                        frameRate = clip.frameRate,
                        wrapMode = clip.wrapMode,
                        legacy = clip.legacy,
                        localBounds = clip.localBounds
                    };
                    AnApply(nc, d, false);

                    var exist = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
                    if (exist != null && exist != clip)
                    {
                        EditorUtility.CopySerialized(nc, exist); // giữ nguyên GUID file cũ
                        UObject.DestroyImmediate(nc);
                        EditorUtility.SetDirty(exist);
                        nc = exist;
                    }
                    else if (exist == null) AssetDatabase.CreateAsset(nc, p);
                    else { UObject.DestroyImmediate(nc); Debug.LogWarning("[GaMo] Không thể ghi đè chính clip nguồn bằng Save As."); return; }

                    AssetDatabase.SaveAssets();
                    EditorGUIUtility.PingObject(nc);
                    Debug.Log("[GaMo] Đã lưu clip đã cắt: " + p);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    EditorUtility.DisplayDialog("Lưu animation lỗi", ex.Message, "OK");
                }
                Repaint();
            };
        }

        void AnQueueOverwrite(AnimationClip clip, float a, float b)
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null || clip == null) return;
                string path = AssetDatabase.GetAssetPath(clip);
                if (!EditorUtility.DisplayDialog("Cắt trực tiếp file gốc",
                        "Sẽ cắt clip và ghi đè:\n" + path + "\n\nGiữ " + a.ToString("0.000") + "s – " + b.ToString("0.000") + "s.\nCó thể Ctrl+Z.",
                        "Cắt", "Hủy")) return;
                try
                {
                    Undo.RegisterCompleteObjectUndo(clip, "GaMo Trim Animation");
                    var d = AnCollect(clip, a, b);
                    AnApply(clip, d, true);
                    EditorUtility.SetDirty(clip);
                    AssetDatabase.SaveAssets();
                    _anFor = null; // Layout kế tiếp reset khoảng chọn theo độ dài mới
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    EditorUtility.DisplayDialog("Cắt animation lỗi", ex.Message, "OK");
                }
                Repaint();
            };
        }

        void AnQueueApplyImporter(AnimationClip clip, float a, float b)
        {
            string path = AssetDatabase.GetAssetPath(clip);
            string clipName = clip.name;
            float clipLen = clip.length;
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null || clipLen <= 1e-5f) return;

                var arr = imp.clipAnimations;
                if (arr == null || arr.Length == 0) arr = imp.defaultClipAnimations;
                int idx = -1;
                for (int i = 0; i < arr.Length; i++)
                    if (arr[i].name == clipName) { idx = i; break; }
                if (idx < 0)
                {
                    EditorUtility.DisplayDialog("Không tìm thấy clip",
                        "Không thấy clip \"" + clipName + "\" trong danh sách Clips của Import Settings.", "OK");
                    return;
                }

                try
                {
                    var ca = arr[idx];
                    float f0 = ca.firstFrame, f1 = ca.lastFrame;
                    float span = f1 - f0;
                    // Quy đổi giây trên clip -> frame của take gốc theo tỉ lệ, không phụ thuộc fps nguồn.
                    ca.firstFrame = Mathf.Round((f0 + a / clipLen * span) * 100f) / 100f;
                    ca.lastFrame = Mathf.Round((f0 + b / clipLen * span) * 100f) / 100f;
                    imp.clipAnimations = arr;
                    imp.SaveAndReimport();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    EditorUtility.DisplayDialog("Áp Import Settings lỗi", ex.Message, "OK");
                    return;
                }

                _anFor = null;
                _assetDirty = true;
                RequestSync();
                EditorApplication.delayCall += () =>
                {
                    if (this == null) return;
                    if (_lock) { if (_lockedObj != null) BindToObject(_lockedObj); }
                    else BindSelection();
                };
            };
        }
    }
}
#endif
