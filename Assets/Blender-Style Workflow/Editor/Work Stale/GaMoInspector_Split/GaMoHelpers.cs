// GaMoHelpers.cs  -  part of GaMo Inspector tool
// Cac lop tien ich nho: clipboard component, phien Play, component gan day, hang doi them script moi
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UEditor = UnityEditor.Editor;
using UIE = UnityEngine.UIElements;
using UEUI = UnityEditor.UIElements;
using UObject = UnityEngine.Object;

namespace GaMo.BlenderEditorTools
{

    // Ghi nhớ LOẠI component vừa Copy. Unity không có API công khai để hỏi clipboard đang chứa gì,
    // nên cửa sổ tự theo dõi (SessionState: giữ qua recompile / Play, mất khi tắt Unity).
    // Lưu ý: chỉ biết các lần Copy bấm trong cửa sổ này; Copy ở Inspector gốc của Unity thì không nhận được.
    internal static class CustomsComponentClipboard
    {
        const string Key = "GaMo.CustomsEditor.CopiedComponentType";

        public static Type CopiedType
        {
            get
            {
                string n = SessionState.GetString(Key, "");
                if (n.Length == 0) return null;
                try { return Type.GetType(n); } catch (Exception) { return null; }
            }
        }

        public static void Copy(Component c)
        {
            if (c == null) return;
            if (ComponentUtility.CopyComponent(c)) SessionState.SetString(Key, c.GetType().AssemblyQualifiedName);
        }
    }

    // Cờ "đang trong phiên Play" (từ ExitingEditMode tới EnteredEditMode). Lưu bằng SessionState nên sống qua domain reload,
    // và là static [InitializeOnLoad] nên được hạ đúng lúc kể cả khi cửa sổ Customs Editor đang đóng.
    [InitializeOnLoad]
    internal static class CustomsPlaySession
    {
        const string Key = "GaMo.CustomsEditor.PlaySession";

        public static bool Active
        {
            get { return SessionState.GetBool(Key, false); }
            set { SessionState.SetBool(Key, value); }
        }

        static CustomsPlaySession()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.EnteredEditMode) Active = false;
            };
        }
    }

    // Danh sách component thêm gần đây (dùng chung cho popup Add Component và kéo-thả script), lưu EditorPrefs
    internal static class CustomsRecentComponents
    {
        const string Key = "GaMo.CustomsEditor.RecentComponents";
        const int Max = 8;

        public static List<string> Load()
        {
            string v = EditorPrefs.GetString(Key, "");
            return v.Length == 0 ? new List<string>() : new List<string>(v.Split('|'));
        }

        public static void Record(Type t)
        {
            if (t == null) return;
            var list = Load();
            string n = t.AssemblyQualifiedName;
            list.Remove(n);
            list.Insert(0, n);
            if (list.Count > Max) list.RemoveRange(Max, list.Count - Max);
            EditorPrefs.SetString(Key, string.Join("|", list));
        }
    }

    // "New Script": file .cs vừa tạo cần Unity compile (domain reload) mới có class. Ghi hàng đợi vào SessionState,
    // sau khi reload xong thì gắn component vào các object đã chọn lúc tạo.
    internal static class CustomsPendingScriptAdd
    {
        const string Key = "GaMo.CustomsEditor.PendingScript";

#pragma warning disable CS0618 // GetInstanceID / InstanceIDToObject bị đánh dấu cũ ở các bản Unity 6 mới
        public static void Enqueue(string className, GameObject[] gos)
        {
            var sb = new StringBuilder();
            sb.Append(className).Append('|').Append(DateTime.UtcNow.Ticks);
            foreach (var g in gos)
                if (g != null) sb.Append('|').Append(g.GetInstanceID());
            SessionState.SetString(Key, sb.ToString());
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        static void OnReload()
        {
            string s = SessionState.GetString(Key, "");
            if (s.Length == 0) return;
            EditorApplication.delayCall += () => Process(s);
        }

        static void Process(string s)
        {
            var p = s.Split('|');
            if (p.Length < 3 || !long.TryParse(p[1], out long ticks)
                || (DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalMinutes > 10)
            {
                SessionState.EraseString(Key); // hỏng hoặc quá 10 phút: bỏ
                return;
            }

            Type type = null;
            foreach (var t in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
                if (t.Name == p[0] && !t.IsAbstract) { type = t; break; }
            if (type == null) return; // chưa compile xong (hoặc lỗi compile): giữ lại, thử ở lần reload sau

            SessionState.EraseString(Key);
            for (int i = 2; i < p.Length; i++)
            {
                if (!int.TryParse(p[i], out int id)) continue;
                var go = EditorUtility.InstanceIDToObject(id) as GameObject;
                if (go != null) Undo.AddComponent(go, type);
            }
            CustomsRecentComponents.Record(type);
        }
#pragma warning restore CS0618
    }
}
#endif
