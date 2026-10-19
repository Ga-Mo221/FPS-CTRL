#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using GaMo.TrueInputSystem;

namespace GaMo.TrueInputSystem.EditorTools
{
    /// <summary>
    /// Tien ich setup dieu huong tay cam cho 1 panel UI:
    /// - Doi Button thuong -> TISGamepadUIButton (giu nguyen onClick/mau/targetGraphic).
    /// - Gan TISUINavigationContext len panel.
    /// - Tu noi navUp/Down/Left/Right theo VI TRI man hinh.
    /// - Set defaultContent + isFirstSelected (nut duoc focus khi mo panel).
    /// Dung: chon panel GameObject roi Tools/TIS/Setup Controller Nav (selected panel).
    /// </summary>
    public static class TISControllerNavSetup
    {
        private static MonoScript _tisScript;
        private static MonoScript TisScript
        {
            get
            {
                if (_tisScript != null) return _tisScript;
                foreach (var g in AssetDatabase.FindAssets("t:MonoScript TIS_GamepadUIButton"))
                {
                    var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(g));
                    if (ms != null && ms.GetClass() == typeof(TISGamepadUIButton)) { _tisScript = ms; break; }
                }
                return _tisScript;
            }
        }

        [MenuItem("Tools/TIS/Setup Controller Nav (selected panel)")]
        private static void SetupSelected()
        {
            var go = Selection.activeGameObject;
            if (go == null) { Debug.LogWarning("[TISNav] Hay chon 1 panel GameObject truoc."); return; }
            int n = SetupPanel(go, true);
            Debug.Log("[TISNav] Da setup controller nav cho '" + go.name + "': " + n + " button.");
            EditorUtility.SetDirty(go);
        }

        public static int SetupPanel(GameObject root, bool skipBackgrounds)
        {
            if (root == null) return 0;
            var goList = new List<GameObject>();
            foreach (var b in root.GetComponentsInChildren<Button>(true))
            {
                if (b == null || !b.interactable) continue;
                string nm = b.gameObject.name.ToLowerInvariant();
                if (skipBackgrounds && (nm == "bg" || nm.Contains("background") || nm.StartsWith("overlay") || nm.Contains("plus"))) continue;
                goList.Add(b.gameObject);
            }
            if (goList.Count == 0) return 0;

            var tis = TisScript;
            foreach (var go in goList)
            {
                var btn = go.GetComponent<Button>();
                if (btn == null || btn is TISGamepadUIButton) continue;
                var so = new SerializedObject(btn);
                var mp = so.FindProperty("m_Script");
                if (mp != null) { mp.objectReferenceValue = tis; so.ApplyModifiedProperties(); }
            }

            var ctx = root.GetComponent<TISUINavigationContext>();
            if (ctx == null) ctx = root.AddComponent<TISUINavigationContext>();

            var btns = new List<TISGamepadUIButton>();
            foreach (var go in goList)
            {
                var t = go.GetComponent<TISGamepadUIButton>();
                if (t != null) btns.Add(t);
            }

            foreach (var t in btns)
            {
                var so = new SerializedObject(t);
                var pc = so.FindProperty("context"); if (pc != null) pc.objectReferenceValue = ctx;
                var pl = so.FindProperty("layer"); if (pl != null) pl.enumValueIndex = (int)TISGamepadUIButton.NavigationLayer.Content;
                var ph = so.FindProperty("holdDelay"); if (ph != null) ph.floatValue = 0.2f;
                var pe = so.FindProperty("isNavigationEnabled"); if (pe != null) pe.boolValue = true;
                var pf = so.FindProperty("isFirstSelected"); if (pf != null) pf.boolValue = false;
                so.ApplyModifiedProperties();
                // highlight khi duoc chon (controller di toi -> nut sang)
                if (t.GetComponent(typeof(Game.UI.TISSelectHighlight)) == null)
                    t.gameObject.AddComponent(typeof(Game.UI.TISSelectHighlight));
            }

            AutoWireNav(root.transform, btns);

            TISGamepadUIButton def = null; float bestY = float.NegativeInfinity, bestX = 0f;
            foreach (var t in btns)
            {
                Vector3 p = PosOf(root.transform, t.transform);
                if (def == null || p.y > bestY + 0.01f || (Mathf.Abs(p.y - bestY) <= 0.01f && p.x < bestX))
                { def = t; bestY = p.y; bestX = p.x; }
            }
            if (def != null)
            {
                var soD = new SerializedObject(def);
                var pf = soD.FindProperty("isFirstSelected"); if (pf != null) pf.boolValue = true;
                soD.ApplyModifiedProperties();
                var soC = new SerializedObject(ctx);
                var dc = soC.FindProperty("defaultContent"); if (dc != null) { dc.objectReferenceValue = def; soC.ApplyModifiedProperties(); }
            }
            return btns.Count;
        }

        // Vi tri tuong doi cua child so voi root = cong don anchoredPosition len chuoi cha.
        // Khong can canvas render (CalculateRelativeRectTransformBounds hong trong prefab-contents).
        private static Vector3 PosOf(Transform root, Transform child)
        {
            Vector2 sum = Vector2.zero;
            Transform t = child;
            int guard = 0;
            while (t != null && t != root && guard++ < 64)
            {
                var rt = t as RectTransform;
                if (rt != null) sum += rt.anchoredPosition;
                t = t.parent;
            }
            return new Vector3(sum.x, sum.y, 0f);
        }

        private static void AutoWireNav(Transform root, List<TISGamepadUIButton> btns)
        {
            var pos = new Dictionary<TISGamepadUIButton, Vector3>();
            foreach (var x in btns) pos[x] = PosOf(root, x.transform);
            foreach (var b in btns)
            {
                Vector3 bp = pos[b];
                TISGamepadUIButton up = null, down = null, left = null, right = null;
                float su = float.MaxValue, sd = float.MaxValue, sl = float.MaxValue, sr = float.MaxValue;
                foreach (var o in btns)
                {
                    if (o == b) continue;
                    Vector3 d = pos[o] - bp;
                    if (d.y > 0.001f)  { float s = ScoreDir(d, Vector2.up);    if (s < su) { su = s; up = o; } }
                    if (d.y < -0.001f) { float s = ScoreDir(d, Vector2.down);  if (s < sd) { sd = s; down = o; } }
                    if (d.x < -0.001f) { float s = ScoreDir(d, Vector2.left);  if (s < sl) { sl = s; left = o; } }
                    if (d.x > 0.001f)  { float s = ScoreDir(d, Vector2.right); if (s < sr) { sr = s; right = o; } }
                }
                var so = new SerializedObject(b);
                var pu = so.FindProperty("navUp");    if (pu != null) pu.objectReferenceValue = up;
                var pd = so.FindProperty("navDown");  if (pd != null) pd.objectReferenceValue = down;
                var ple = so.FindProperty("navLeft"); if (ple != null) ple.objectReferenceValue = left;
                var pr = so.FindProperty("navRight"); if (pr != null) pr.objectReferenceValue = right;
                so.ApplyModifiedProperties();
            }
        }

        private static float ScoreDir(Vector3 delta, Vector2 dir)
        {
            Vector2 d2 = new Vector2(delta.x, delta.y);
            float dist = d2.magnitude;
            if (dist < 0.0001f) return float.MaxValue;
            float along = Vector2.Dot(d2 / dist, dir);
            if (along <= 0.2f) return float.MaxValue;
            float perp = Mathf.Abs(Vector2.Dot(d2, new Vector2(-dir.y, dir.x)));
            return dist + perp * 2f + (1f - along) * dist;
        }
    }
}
#endif
