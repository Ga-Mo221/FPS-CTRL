// GaMoInspector.MeshPreview.cs  -  part of CustomsEditor (GaMo Inspector)
// Ve MESH DA SUA (ban sao _meWork trong MeshEdit.cs) vao khung Preview o day cua so, thay cho preview cua mesh goc.
// Hien duoc: Shaded / Normals (mau), Wire, vector Normals, Back-face (mat bi lat to do), Pivot (goc toa do + truc XYZ) va Bounds
// (hop cam = bounds hien tai, hop trang mo = bounds truoc khi sua). Keo chuot = xoay, cuon = zoom, double-click = ve goc nhin mac dinh.
// Dung PreviewRenderUtility + shader "Hidden/Internal-Colored" (khong phu thuoc pipeline), do sang bang mau dinh bake san.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace GaMo.BlenderEditorTools
{
    public partial class CustomsEditor
    {
        [NonSerialized] PreviewRenderUtility _mePru;
        [NonSerialized] Material _meMatFront, _meMatBack, _meMatLine;
        [NonSerialized] Mesh _meDisp, _meDispRed, _meWire, _meNrm;
        [NonSerialized] int _meDispVer = -1, _meDispShade = -1, _meRedVer = -1, _meWireVer = -1, _meNrmVer = -1;
        [NonSerialized] bool _mePreviewFail;
        [NonSerialized] GUIStyle _meAxisStyle;
        float _meViewRadius;
        int _meDragHandle = -1;                       // đang kéo gizmo pivot: -1 = không, 0/1/2 = trục X/Y/Z, 3 = chấm giữa (mặt phẳng màn hình)
        static readonly Color MeHl = new Color(1f, 0.9f, 0.2f);
        bool _meShowOriginal;                         // true = hiện Preview của mesh gốc thay vì bản đang sửa
        int _meVer;                                   // tăng mỗi khi mesh làm việc đổi (MeOp / MeUndo / MeReset)

        bool _meShowWire, _meShowNormals, _meShowBack = true, _meShowPivot = true;
        int _meShade;                                 // 0 = Shaded, 1 = Normals (màu)
        float _meYaw = -30f, _mePitch = 20f, _meZoom = 1f;

        static readonly string[] MeShadeNames = { "Shaded", "Normals" };
        static readonly int MePrevHash = "GaMoMeshPrev".GetHashCode();

        // Khung Preview ở đáy dùng bản vẽ riêng khi đang mở panel Edit Mesh của đúng mesh này
        bool MeHasWork()
        {
            var mm = _asset as Mesh;
            return mm != null && _meFor == mm && _meOpen && _meWork != null && _meWork.isReadable && !_mePreviewFail;
        }

        bool MeWantsPreview() { return MeHasWork() && !_meShowOriginal; }

        // =====================================================
        //  Thanh cài đặt trên tiêu đề Preview
        // =====================================================
        void MePreviewSettings()
        {
            GUILayout.Space(4);
            _meShade = EditorGUILayout.Popup(_meShade, MeShadeNames, EditorStyles.toolbarPopup, GUILayout.Width(66));
            _meShowWire = GUILayout.Toggle(_meShowWire, new GUIContent("Wire", "Hiện lưới tam giác"), EditorStyles.toolbarButton);
            _meShowNormals = GUILayout.Toggle(_meShowNormals, new GUIContent("Nrm", "Vẽ vector normals (tối đa ~4000 đường). Normals chĩa vào trong sẽ bị che: xem thêm chế độ Normals / Back."),
                EditorStyles.toolbarButton);
            _meShowBack = GUILayout.Toggle(_meShowBack, new GUIContent("Back", "Tô đỏ mặt sau của tam giác: thấy đỏ ở mặt ngoài = mặt đang bị lật"), EditorStyles.toolbarButton);
            _meShowPivot = GUILayout.Toggle(_meShowPivot, new GUIContent("Pivot", "Chấm trắng + trục XYZ = pivot (gốc tọa độ). KÉO chấm trắng = dịch pivot theo mặt phẳng màn hình, kéo trục X/Y/Z = dịch theo trục. Hộp cam = bounds hiện tại, hộp trắng mờ = bounds trước khi sửa."),
                EditorStyles.toolbarButton);
        }

        // =====================================================
        //  Khởi tạo / dọn
        // =====================================================
        static Material MeMakeMat(Shader sh, int cull)
        {
            var m = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            m.SetInt("_Cull", cull);
            m.SetInt("_ZWrite", 1);
            return m;
        }

        bool MeEnsurePreview()
        {
            if (_mePreviewFail) return false;
            if (_mePru != null && _meMatFront != null) return true;
            try
            {
                var sh = Shader.Find("Hidden/Internal-Colored");
                if (sh == null) { _mePreviewFail = true; return false; }
                _mePru = new PreviewRenderUtility();
                var cam = _mePru.camera;
                cam.fieldOfView = 30f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.17f, 0.17f, 0.18f, 1f);
                _meMatFront = MeMakeMat(sh, 2); // Cull Back
                _meMatBack = MeMakeMat(sh, 1);  // Cull Front => chỉ thấy mặt sau
                _meMatLine = MeMakeMat(sh, 0);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GaMo] Không dựng được preview mesh: " + ex.Message);
                _mePreviewFail = true;
                MePreviewCleanup();
                return false;
            }
        }

        void MePreviewCleanup()
        {
            if (_mePru != null)
            {
                try { _mePru.Cleanup(); } catch (Exception) { }
                _mePru = null;
            }
            if (_meMatFront != null) UObject.DestroyImmediate(_meMatFront);
            if (_meMatBack != null) UObject.DestroyImmediate(_meMatBack);
            if (_meMatLine != null) UObject.DestroyImmediate(_meMatLine);
            _meMatFront = _meMatBack = _meMatLine = null;
            if (_meDisp != null) UObject.DestroyImmediate(_meDisp);
            if (_meDispRed != null) UObject.DestroyImmediate(_meDispRed);
            if (_meWire != null) UObject.DestroyImmediate(_meWire);
            if (_meNrm != null) UObject.DestroyImmediate(_meNrm);
            _meDisp = _meDispRed = _meWire = _meNrm = null;
            _meDispVer = _meRedVer = _meWireVer = _meNrmVer = -1;
            _meDispShade = -1;
        }

        // =====================================================
        //  Dựng mesh hiển thị (màu đỉnh bake sẵn, wire, normals)
        // =====================================================
        static Color[] MeColors(Mesh src, int shade)
        {
            int vc = src.vertexCount;
            var cols = new Color[vc];
            var n = src.normals;
            bool hasN = n.Length == vc;
            var L = new Vector3(0.35f, 0.75f, -0.55f).normalized; // ánh sáng cố định theo mesh: mặt quay về L sáng, quay ngược tối
            for (int i = 0; i < vc; i++)
            {
                if (!hasN) { cols[i] = new Color(0.7f, 0.7f, 0.72f, 1f); continue; }
                if (shade == 1)
                {
                    cols[i] = new Color(n[i].x * 0.5f + 0.5f, n[i].y * 0.5f + 0.5f, n[i].z * 0.5f + 0.5f, 1f);
                }
                else
                {
                    float d = Mathf.Max(0f, Vector3.Dot(n[i], L));
                    float k = 0.22f + 0.78f * d;
                    cols[i] = new Color(0.80f * k, 0.80f * k, 0.83f * k, 1f);
                }
            }
            return cols;
        }

        static Mesh MeMakeDisplay(Mesh src, Color[] cols)
        {
            var d = new Mesh { hideFlags = HideFlags.HideAndDontSave, name = "GaMoMeshPrev" };
            d.indexFormat = src.indexFormat;
            d.vertices = src.vertices;
            d.subMeshCount = src.subMeshCount;
            for (int s = 0; s < src.subMeshCount; s++)
                if (src.GetTopology(s) == MeshTopology.Triangles) d.SetTriangles(src.GetTriangles(s), s);
            d.colors = cols;
            d.RecalculateBounds();
            return d;
        }

        static Mesh MeMakeWire(Mesh src)
        {
            var v = src.vertices;
            var n = src.normals;
            bool hasN = n.Length == v.Length;
            float eps = Mathf.Max(1e-5f, src.bounds.size.magnitude * 0.0006f); // nhích ra ngoài một chút để không z-fight với mặt
            var wv = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++) wv[i] = hasN ? v[i] + n[i] * eps : v[i];

            var set = new HashSet<long>();
            var idx = new List<int>();
            for (int s = 0; s < src.subMeshCount && set.Count < 250000; s++)
            {
                if (src.GetTopology(s) != MeshTopology.Triangles) continue;
                var tr = src.GetTriangles(s);
                for (int i = 0; i + 2 < tr.Length; i += 3)
                {
                    for (int e = 0; e < 3; e++)
                    {
                        int a = tr[i + e], b = tr[i + (e + 1) % 3];
                        long lo = a < b ? a : b, hi = a < b ? b : a;
                        if (set.Add((lo << 32) | hi)) { idx.Add(a); idx.Add(b); }
                    }
                }
            }

            var w = new Mesh { hideFlags = HideFlags.HideAndDontSave, name = "GaMoMeshWire" };
            w.indexFormat = v.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            w.vertices = wv;
            var cols = new Color[wv.Length];
            for (int i = 0; i < cols.Length; i++) cols[i] = new Color(1f, 1f, 1f, 0.45f);
            w.colors = cols;
            w.SetIndices(idx.ToArray(), MeshTopology.Lines, 0);
            return w;
        }

        static Mesh MeMakeNormals(Mesh src)
        {
            var v = src.vertices;
            var n = src.normals;
            if (v.Length == 0 || n.Length != v.Length) return null;
            float len = Mathf.Max(1e-5f, src.bounds.size.magnitude * 0.05f);
            int stride = Mathf.Max(1, v.Length / 4000);
            var pts = new List<Vector3>();
            var cols = new List<Color>();
            var idx = new List<int>();
            for (int i = 0; i < v.Length; i += stride)
            {
                idx.Add(pts.Count); pts.Add(v[i]); cols.Add(new Color(0.1f, 0.6f, 0.9f, 1f));
                idx.Add(pts.Count); pts.Add(v[i] + n[i] * len); cols.Add(new Color(0.4f, 1f, 1f, 1f));
            }
            var m = new Mesh { hideFlags = HideFlags.HideAndDontSave, name = "GaMoMeshNrm" };
            m.vertices = pts.ToArray();
            m.colors = cols.ToArray();
            m.SetIndices(idx.ToArray(), MeshTopology.Lines, 0);
            return m;
        }

        void MeBuildPreviewMeshes()
        {
            var src = _meWork;
            if (_meDisp == null || _meDispVer != _meVer || _meDispShade != _meShade)
            {
                if (_meDisp != null) UObject.DestroyImmediate(_meDisp);
                _meDisp = MeMakeDisplay(src, MeColors(src, _meShade));
                _meDispVer = _meVer;
                _meDispShade = _meShade;
            }
            if (_meShowBack && (_meDispRed == null || _meRedVer != _meVer))
            {
                if (_meDispRed != null) UObject.DestroyImmediate(_meDispRed);
                var red = new Color[src.vertexCount];
                for (int i = 0; i < red.Length; i++) red[i] = new Color(0.85f, 0.15f, 0.15f, 1f);
                _meDispRed = MeMakeDisplay(src, red);
                _meRedVer = _meVer;
            }
            if (_meShowWire && (_meWire == null || _meWireVer != _meVer))
            {
                if (_meWire != null) UObject.DestroyImmediate(_meWire);
                _meWire = MeMakeWire(src);
                _meWireVer = _meVer;
            }
            if (_meShowNormals && (_meNrm == null || _meNrmVer != _meVer))
            {
                if (_meNrm != null) UObject.DestroyImmediate(_meNrm);
                _meNrm = MeMakeNormals(src);
                _meNrmVer = _meVer;
            }
        }

        // =====================================================
        //  Vẽ vào khung Preview (gọi từ DrawPreviewPanel)
        // =====================================================
        void MeDrawPreview(Rect r)
        {
            var ev = Event.current;
            int id = GUIUtility.GetControlID(MePrevHash, FocusType.Passive, r);
            switch (ev.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (r.Contains(ev.mousePosition))
                    {
                        GUIUtility.hotControl = id;
                        _meDragHandle = _meShowPivot ? MeHitHandle(r, ev.mousePosition - r.position) : -1;
                        if (_meDragHandle < 0 && ev.clickCount >= 2) { _meYaw = -30f; _mePitch = 20f; _meZoom = 1f; }
                        ev.Use();
                        RepaintPreviewOnly();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        if (_meDragHandle >= 0) MeDragPivot(r, ev.delta);
                        else
                        {
                            _meYaw += ev.delta.x * 0.5f;
                            _mePitch = Mathf.Clamp(_mePitch + ev.delta.y * 0.5f, -89f, 89f);
                        }
                        ev.Use();
                        RepaintPreviewOnly();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        _meDragHandle = -1;
                        ev.Use();
                        RepaintPreviewOnly();
                    }
                    break;
                case EventType.ScrollWheel:
                    if (r.Contains(ev.mousePosition))
                    {
                        _meZoom = Mathf.Clamp(_meZoom * (1f + ev.delta.y * 0.04f), 0.15f, 6f);
                        ev.Use();
                        RepaintPreviewOnly();
                    }
                    break;
                case EventType.Repaint:
                    MePaintPreview(r);
                    break;
            }
        }

        // Khoảng cách từ điểm p đến đoạn a-b (pixel)
        static float MeSegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float l2 = ab.sqrMagnitude;
            float t = l2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
            return (p - (a + ab * t)).magnitude;
        }

        // Trục / chấm pivot nào nằm dưới con trỏ (mp: toạ độ trong khung Preview). -1 = không có.
        int MeHitHandle(Rect r, Vector2 mp)
        {
            if (_mePru == null || _meWork == null) return -1;
            var cam = _mePru.camera;
            Vector3 o;
            if (!MeProj(r, cam, Vector3.zero, out o)) return -1;
            var p = new Vector2(o.x, o.y);
            if ((mp - p).magnitude <= 10f) return 3;

            float len = Mathf.Max(1e-4f, _meWork.bounds.size.magnitude) * 0.3f;
            var dirs = new[] { Vector3.right, Vector3.up, Vector3.forward };
            int best = -1;
            float bd = 8f;
            for (int i = 0; i < 3; i++)
            {
                Vector3 e;
                if (!MeProj(r, cam, dirs[i] * len, out e)) continue;
                float d = MeSegDist(mp, p, new Vector2(e.x, e.y));
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        // Kéo gizmo: md = độ dời chuột (pixel). Pivot dịch +world so với mesh => cộng vào ô "Dịch pivot" và áp trực tiếp.
        void MeDragPivot(Rect r, Vector2 md)
        {
            if (_mePru == null || _meWork == null) return;
            var cam = _mePru.camera;
            Vector3 world;
            if (_meDragHandle == 3)
            {
                float dist = Vector3.Dot(-cam.transform.position, cam.transform.forward);
                if (dist <= 1e-5f) return;
                float wpp = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1f, r.height);
                world = (cam.transform.right * md.x - cam.transform.up * md.y) * wpp;
            }
            else
            {
                Vector3 axis = _meDragHandle == 0 ? Vector3.right : (_meDragHandle == 1 ? Vector3.up : Vector3.forward);
                float len = Mathf.Max(1e-4f, _meWork.bounds.size.magnitude) * 0.3f;
                Vector3 o, e;
                if (!MeProj(r, cam, Vector3.zero, out o) || !MeProj(r, cam, axis * len, out e)) return;
                var sv = new Vector2(e.x - o.x, e.y - o.y);
                float sq = sv.sqrMagnitude;
                if (sq < 4f) return; // trục chĩa thẳng vào camera: không kéo được
                world = axis * (Vector2.Dot(md, sv) / sq * len);
            }
            var no = _meOffset + world;
            if (no.sqrMagnitude < 1e-12f) no = Vector3.zero;
            _meOffset = no;
            MeLivePivot();
            Repaint(); // để ô "Dịch pivot" trong panel Edit Mesh cập nhật theo
        }

        void MePaintPreview(Rect r)
        {
            if (r.width < 8f || r.height < 8f) return;
            var m = _meWork;
            if (m == null) return;
            if (!MeEnsurePreview())
            {
                GUI.Label(r, "Không dựng được preview mesh", EditorStyles.centeredGreyMiniLabel);
                return;
            }
            MeBuildPreviewMeshes();

            var b = m.bounds;
            float radius = Mathf.Max(1e-4f, _meViewRadius, b.extents.magnitude);
            var cam = _mePru.camera;
            _mePru.BeginPreview(r, GUIStyle.none);

            cam.aspect = r.width / r.height;
            float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.1f * _meZoom;
            var rot = Quaternion.Euler(_mePitch, _meYaw, 0f);
            cam.transform.rotation = rot;
            cam.transform.position = b.center - rot * Vector3.forward * dist;  // bám tâm mesh: mesh đứng yên, chấm pivot đi theo con trỏ khi kéo
            float near = Mathf.Max(radius * 0.01f, dist - radius * 3f);
            cam.nearClipPlane = Mathf.Min(near, dist * 0.9f);
            cam.farClipPlane = dist + radius * 4f;

            var id = Matrix4x4.identity;
            for (int s = 0; s < _meDisp.subMeshCount; s++) _mePru.DrawMesh(_meDisp, id, _meMatFront, s);
            if (_meShowBack && _meDispRed != null)
                for (int s = 0; s < _meDispRed.subMeshCount; s++) _mePru.DrawMesh(_meDispRed, id, _meMatBack, s);
            if (_meShowWire && _meWire != null) _mePru.DrawMesh(_meWire, id, _meMatLine, 0);
            if (_meShowNormals && _meNrm != null) _mePru.DrawMesh(_meNrm, id, _meMatLine, 0);
            cam.Render();

            var tex = _mePru.EndPreview();
            GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, false);

            if (_meShowPivot) MePaintOverlay(r, cam, b);
        }

        // =====================================================
        //  Lớp phủ 2D: pivot + trục + bounds (luôn nằm trên cùng, không bị mesh che)
        // =====================================================
        static bool MeProj(Rect r, Camera cam, Vector3 w, out Vector3 p)
        {
            var vp = cam.WorldToViewportPoint(w);
            p = new Vector3(vp.x * r.width, (1f - vp.y) * r.height, 0f);
            return vp.z > 0.01f;
        }

        static Vector3 MeCorner(Bounds b, int i)
        {
            return b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
        }

        static void MeBox(Rect r, Camera cam, Bounds b, float width)
        {
            for (int i = 0; i < 8; i++)
                for (int bit = 1; bit <= 4; bit <<= 1)
                {
                    if ((i & bit) != 0) continue;
                    if (MeProj(r, cam, MeCorner(b, i), out Vector3 a) && MeProj(r, cam, MeCorner(b, i | bit), out Vector3 c))
                        Handles.DrawAAPolyLine(width, a, c);
                }
        }

        void MeAxis(Rect r, Camera cam, Vector3 o, Vector3 dir, Color col, string label)
        {
            if (!MeProj(r, cam, dir, out Vector3 e)) return;
            EditorGUIUtility.AddCursorRect(new Rect(e.x - 8f, e.y - 8f, 16f, 16f), MouseCursor.MoveArrow);
            Handles.color = new Color(0f, 0f, 0f, 0.7f);
            Handles.DrawAAPolyLine(5f, o, e);
            Handles.color = col;
            Handles.DrawAAPolyLine(2.5f, o, e);
            if (_meAxisStyle == null) _meAxisStyle = new GUIStyle(EditorStyles.miniBoldLabel);
            SetTextColor(_meAxisStyle, col);
            GUI.Label(new Rect(e.x + 2f, e.y - 8f, 16f, 16f), label, _meAxisStyle);
        }

        void MePaintOverlay(Rect r, Camera cam, Bounds b)
        {
            float diag = Mathf.Max(1e-4f, b.size.magnitude);
            var oldCol = Handles.color;
            GUI.BeginClip(r);

            if (_meOps > 0 && _meFor != null)
            {
                Handles.color = new Color(1f, 1f, 1f, 0.28f);
                MeBox(r, cam, _meFor.bounds, 1.2f);
            }
            Handles.color = new Color(Accent.r, Accent.g, Accent.b, 0.9f);
            MeBox(r, cam, b, 1.5f);

            if (MeProj(r, cam, Vector3.zero, out Vector3 o))
            {
                float len = diag * 0.3f;
                EditorGUIUtility.AddCursorRect(new Rect(o.x - 10f, o.y - 10f, 20f, 20f), MouseCursor.MoveArrow);
                MeAxis(r, cam, o, new Vector3(len, 0f, 0f), _meDragHandle == 0 ? MeHl : new Color(1f, 0.3f, 0.3f), "X");
                MeAxis(r, cam, o, new Vector3(0f, len, 0f), _meDragHandle == 1 ? MeHl : new Color(0.4f, 1f, 0.4f), "Y");
                MeAxis(r, cam, o, new Vector3(0f, 0f, len), _meDragHandle == 2 ? MeHl : new Color(0.4f, 0.6f, 1f), "Z");

                var ring = new Vector3[17];
                for (int i = 0; i < ring.Length; i++)
                {
                    float a = i / 16f * Mathf.PI * 2f;
                    float rr = _meDragHandle == 3 ? 7f : 5f;
                    ring[i] = new Vector3(o.x + Mathf.Cos(a) * rr, o.y + Mathf.Sin(a) * rr, 0f);
                }
                Handles.color = Color.black;
                Handles.DrawAAPolyLine(5f, ring);
                Handles.color = _meDragHandle == 3 ? MeHl : Color.white;
                Handles.DrawAAPolyLine(2.5f, ring);
            }

            Handles.color = oldCol;
            GUI.EndClip();
        }
    }
}
#endif