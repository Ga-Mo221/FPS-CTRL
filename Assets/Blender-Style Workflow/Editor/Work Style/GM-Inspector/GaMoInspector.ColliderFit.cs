// GaMoInspector.ColliderFit.cs  -  part of CustomsEditor (GaMo Inspector)
// Muc "Fit to Target" duoi panel Collider: chon 1 GameObject (co Renderer) lam muc tieu, collider se boc theo mesh cua muc tieu
// du collider nam o object cha / con / anh em. Do bounds theo truc LOCAL cua collider nen collider xoay / scale van boc dung.
// Ho tro: BoxCollider, SphereCollider, CapsuleCollider, BoxCollider2D, CircleCollider2D.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    public partial class CustomsEditor
    {
        // Mỗi collider nhớ target của nó (2 list song song, Unity tự serialize => giữ qua recompile)
        [SerializeField] List<Component> _fitCols = new List<Component>();
        [SerializeField] List<GameObject> _fitObjs = new List<GameObject>();
        [SerializeField] bool _fitChildrenInit, _fitChildren = true, _fitAutoAxis = true;
        [SerializeField] float _fitPad;

        const string FitTipTarget =
            "Object chứa Renderer mà collider sẽ bọc theo. Có thể là object cha, con hoặc object khác trong scene.";
        const string FitTipFind = "Tự tìm Renderer gần nhất: chính object này, rồi tới cha, rồi tới con.";

        static bool FitSupported(Component c)
        {
            return c is BoxCollider || c is SphereCollider || c is CapsuleCollider
                || c is BoxCollider2D || c is CircleCollider2D;
        }

        GameObject GetFitTarget(Component c)
        {
            int i = _fitCols.IndexOf(c);
            return i >= 0 && i < _fitObjs.Count ? _fitObjs[i] : null;
        }

        void SetFitTarget(Component c, GameObject t)
        {
            int i = _fitCols.IndexOf(c);
            if (t == null)
            {
                if (i >= 0) { _fitCols.RemoveAt(i); if (i < _fitObjs.Count) _fitObjs.RemoveAt(i); }
                return;
            }
            if (i >= 0 && i < _fitObjs.Count) { _fitObjs[i] = t; return; }

            for (int k = _fitCols.Count - 1; k >= 0; k--) // dọn mục đã mất
                if (_fitCols[k] == null || k >= _fitObjs.Count || _fitObjs[k] == null)
                {
                    _fitCols.RemoveAt(k);
                    if (k < _fitObjs.Count) _fitObjs.RemoveAt(k);
                }
            while (_fitObjs.Count > _fitCols.Count) _fitObjs.RemoveAt(_fitObjs.Count - 1);
            if (_fitCols.Count >= 64) { _fitCols.RemoveAt(0); _fitObjs.RemoveAt(0); }
            _fitCols.Add(c);
            _fitObjs.Add(t);
        }

        static GameObject FitFindNearest(Component c)
        {
            for (var t = c.transform; t != null; t = t.parent)
                if (t.GetComponent<Renderer>() != null) return t.gameObject;
            var rc = c.GetComponentInChildren<Renderer>();
            return rc != null ? rc.gameObject : null;
        }

        // =====================================================
        //  Vẽ
        // =====================================================
        void DrawColliderFitChunk(Component col)
        {
            if (col == null) return;
            if (!_fitChildrenInit) { _fitChildrenInit = true; _fitChildren = true; }

            using (new EditorGUILayout.VerticalScope(EditorStyles.inspectorDefaultMargins))
            {
                GUILayout.Label("Fit to Target", EditorStyles.boldLabel);

                var cur = GetFitTarget(col);
                bool changed;
                GameObject nt;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    nt = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Target", FitTipTarget), cur, typeof(GameObject), true);
                    changed = EditorGUI.EndChangeCheck();
                    if (GUILayout.Button(new GUIContent("Tìm", FitTipFind), EditorStyles.miniButton, GUILayout.Width(36)))
                    {
                        nt = FitFindNearest(col);
                        changed = true;
                        GUI.FocusControl(null);
                    }
                }
                if (changed)
                {
                    if (nt != null && EditorUtility.IsPersistent(nt)) nt = null; // chỉ nhận object trong scene
                    SetFitTarget(col, nt);
                    cur = nt;
                    if (nt != null) FitNow(col, nt);
                    else if (nt == null && col != null) ShowNotification(new GUIContent("Không tìm thấy Renderer phù hợp"));
                }

                float oldLw = EditorGUIUtility.labelWidth;
                EditorGUI.BeginChangeCheck();
                using (new EditorGUILayout.HorizontalScope())
                {
                    _fitChildren = EditorGUILayout.ToggleLeft(new GUIContent("Gồm object con", "Tính cả Renderer của các object con của target"),
                        _fitChildren, GUILayout.Width(120));
                    EditorGUIUtility.labelWidth = 54f;
                    _fitPad = EditorGUILayout.FloatField(new GUIContent("Padding", "Nở thêm mỗi phía (đơn vị local của collider). Âm = thu nhỏ."), _fitPad);
                    EditorGUIUtility.labelWidth = oldLw;
                }
                if (col is CapsuleCollider)
                    _fitAutoAxis = EditorGUILayout.ToggleLeft(new GUIContent("Tự chọn trục Capsule (trục dài nhất)"), _fitAutoAxis);
                bool optChanged = EditorGUI.EndChangeCheck();

                using (new EditorGUI.DisabledScope(cur == null))
                    if (GUILayout.Button(new GUIContent("Fit", "Bọc collider theo mesh của Target"), GUILayout.Height(22)) || (optChanged && cur != null))
                        FitNow(col, cur);

                if (cur == null)
                    GUILayout.Label("Chọn object có Renderer (hoặc bấm Tìm) để collider bọc theo.", EditorStyles.miniLabel);
                else if (col is SphereCollider || col is CapsuleCollider || col is CircleCollider2D)
                    GUILayout.Label("Scale không đều thì Sphere / Capsule chỉ bọc xấp xỉ.", EditorStyles.miniLabel);
            }
        }

        void FitNow(Component col, GameObject target)
        {
            if (col == null || target == null) return;
            if (!FitTryBounds(col.transform, target, _fitChildren, out Bounds b))
            {
                ShowNotification(new GUIContent("Target không có Renderer để đo"));
                return;
            }
            if (Mathf.Abs(_fitPad) > 1e-6f) b.Expand(_fitPad * 2f);
            if (FitApply(col, b, _fitAutoAxis))
            {
                InvalidateStruct();
                Repaint();
            }
        }

        // =====================================================
        //  Đo bounds của target trong không gian local của collider
        // =====================================================
        static Vector3 FitCorner(Bounds b, int i)
        {
            return b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
        }

        static bool FitTryBounds(Transform space, GameObject target, bool children, out Bounds result)
        {
            result = new Bounds();
            bool has = false;
            Matrix4x4 w2l = space.worldToLocalMatrix;
            var rends = children ? target.GetComponentsInChildren<Renderer>(false) : target.GetComponents<Renderer>();

            foreach (var r in rends)
            {
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;

                Bounds lb = default(Bounds);
                bool localOk = false;
                if (r is MeshRenderer)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null) { lb = mf.sharedMesh.bounds; localOk = true; }
                }
                else if (r is SpriteRenderer sr && sr.sprite != null) { lb = sr.sprite.bounds; localOk = true; }

                Matrix4x4 m = localOk ? w2l * r.transform.localToWorldMatrix : w2l;
                Bounds src = localOk ? lb : r.bounds; // Skinned / loại khác: dùng bounds thế giới
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = m.MultiplyPoint3x4(FitCorner(src, i));
                    if (!has) { result = new Bounds(p, Vector3.zero); has = true; }
                    else result.Encapsulate(p);
                }
            }
            return has;
        }

        // =====================================================
        //  Áp bounds (không gian local của collider) vào collider
        // =====================================================
        static bool FitApply(Component col, Bounds b, bool autoAxis)
        {
            if (col is BoxCollider box)
            {
                Undo.RecordObject(box, "Fit Collider");
                box.center = b.center;
                box.size = b.size;
                return true;
            }
            if (col is SphereCollider sph)
            {
                Undo.RecordObject(sph, "Fit Collider");
                var e = b.extents;
                sph.center = b.center;
                sph.radius = Mathf.Max(e.x, Mathf.Max(e.y, e.z));
                return true;
            }
            if (col is CapsuleCollider cap)
            {
                Undo.RecordObject(cap, "Fit Collider");
                var e = b.extents;
                int dir = cap.direction;
                if (autoAxis) dir = (e.x >= e.y && e.x >= e.z) ? 0 : (e.y >= e.z ? 1 : 2);
                float r = 0f;
                for (int i = 0; i < 3; i++)
                    if (i != dir) r = Mathf.Max(r, e[i]);
                cap.direction = dir;
                cap.center = b.center;
                cap.radius = r;
                cap.height = Mathf.Max(b.size[dir], r * 2f);
                return true;
            }
            if (col is BoxCollider2D b2)
            {
                Undo.RecordObject(b2, "Fit Collider");
                b2.offset = new Vector2(b.center.x, b.center.y);
                b2.size = new Vector2(b.size.x, b.size.y);
                return true;
            }
            if (col is CircleCollider2D c2)
            {
                Undo.RecordObject(c2, "Fit Collider");
                c2.offset = new Vector2(b.center.x, b.center.y);
                c2.radius = Mathf.Max(b.extents.x, b.extents.y);
                return true;
            }
            return false;
        }
    }
}
#endif
