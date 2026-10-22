// GaMoInspector.Binding.cs  -  part of CustomsEditor (GaMo Inspector)
// Gan GameObject/Asset dang chon, dung Editor tuong ung
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
    public partial class CustomsEditor
    {
        void Bind(GameObject go)
        {
            SaveStateOfCurrent(); // lưu tab của object đang rời đi (kể cả tab do tự chọn)
            // Chế độ GameObject chỉ có Preview khi là prefab có model: đang tách Preview => mở lại cho prefab mới, không thì đóng
            var pfSrc = Selection.gameObjects.Length <= 1 ? PrefabPreviewSource(go) : null;
            if (pfSrc != null) CustomsNativePreview.ReopenFor(this, new UObject[] { pfSrc });
            else CustomsNativePreview.CloseFor(this);
            ClearAssetState();    // rời chế độ Asset (nếu đang ở đó)
            _compsCache = null;
            _structVer++; _dataVer++;
            _matRO.Clear(); _validCache = null; // icon / tab content cache theo Type (không phụ thuộc object): xóa ở RefreshStylesIfNeeded + OnProjectChanged

            _go = go;
            _gos.Clear();
            if (go != null)
            {
                _gos.Add(go); // object chính luôn đứng đầu
                foreach (var g in Selection.gameObjects)
                    if (g != null && g != go) _gos.Add(g);
            }
            _selected.Clear();
            ClearEditors();
            _ovrDirty = true; _ovrAt = 0; _dropTarget = null; _pressTab = null;

            if (_go != null)
            {
                var cs = GetComps(); // đã lọc HideInInspector
                ResolvePendingState(cs);

                // Ưu tiên: tab riêng của object này (chỉ khi chọn 1 object) -> tab chọn gần nhất -> component đầu tiên
                if (_gos.Count <= 1 && _perObject.TryGetValue(_go, out var mem) && mem.Count > 0)
                    SelectByTypes(cs, mem);
                if (_selected.Count == 0)
                    SelectByTypes(cs, _lastTypes);

                if (_selected.Count == 0)
                {
                    var def = PickDefaultTab(cs);
                    if (def != null) _selected.Add(def);
                }
            }
            Repaint();
            RequestSync();
        }


        // =====================================================
        //  Chế độ ASSET: chọn file trong Project (ScriptableObject, RP Asset, Volume Profile,
        //  InputActions, GlobalSettings, Material, Texture...) => hiện đúng Inspector của asset đó.
        // =====================================================
        UObject _asset;                                   // asset chính (active)
        UObject[] _assets = new UObject[0];               // tất cả asset cùng type đang chọn (multi-edit)
        UEditor _importerEditor;                          // editor của Importer (Import Settings), nếu asset có
        UEditor _objectEditor;                            // editor của chính asset
        bool _showPreview = true;
        float _previewHeight = 220f;                      // chiều cao khung Preview ở đáy cửa sổ (kéo thanh chia để đổi)
        const string PreviewHPrefKey = "GaMo.CustomsEditor.PreviewHeight";

        void BindSelection()
        {
            var active = Selection.activeObject;
            PushHistory(active);
            BindToObject(active);
        }

        // File model (.fbx, .obj, .blend...) chọn trong Project: Unity trả về GameObject root, nhưng Inspector gốc
        // hiện Model Import Settings chứ không hiện component => đưa vào chế độ Asset.
        static bool IsModelAsset(GameObject go)
        {
            return go != null && EditorUtility.IsPersistent(go)
                   && PrefabUtility.GetPrefabAssetType(go) == PrefabAssetType.Model
                   && AssetDatabase.IsMainAsset(go);
        }

        void BindToObject(UObject active)
        {
            if (active is GameObject mg && IsModelAsset(mg)) { BindAsset(active); return; }
            if (active == null || active is GameObject) { Bind(active as GameObject); return; }
            if (active is Component comp) { Bind(comp.gameObject); return; }
            BindAsset(active);
        }

        void BindAsset(UObject obj)
        {
            SaveStateOfCurrent();
            _go = null;
            _gos.Clear();
            _selected.Clear();
            ClearEditors();
            ClearAssetState();

            _compsCache = null;
            _structVer++; _dataVer++;
            _hdrDirty = true;
            _asset = obj;
            _validCache = null;
            var list = new List<UObject> { obj };
            foreach (var o in Selection.objects)
                if (o != null && o != obj && o.GetType() == obj.GetType() && (!(o is GameObject og) || IsModelAsset(og)))
                    list.Add(o);
            _assets = list.ToArray();
            CustomsNativePreview.ReopenFor(this, _assets); // đang tách Preview: mở lại cho asset mới (cùng vị trí)

            BuildAssetEditors();
            ResetScroll();
            Repaint();
            RequestSync();
        }

        void ClearAssetState()
        {
            _asset = null;
            _assets = new UObject[0];
            _previewConstant = false;
            DestroyAssetEditors();
        }

        void DestroyAssetEditors()
        {
            // Hủy editor của object (GameObjectInspector của FBX...) TRƯỚC, rồi mới tới importer editor.
            // Hai editor đã được nối với nhau (LinkImporterToAssetEditor): importer editor dọn SerializedObject của editor kia
            // khi bị hủy, nên hủy importer trước thì OnDisable của GameObjectInspector đọc property đã Dispose => NullReferenceException.
            DestroyPrefabPreview(); // editor Preview của prefab (chế độ GameObject): dọn cùng chỗ để mọi đường rời / reload / đóng cửa sổ đều hủy
            DestroyMotionPreview(); // editor Preview của Motion (State trong Animator)
            var objEd = _objectEditor;
            var impEd = _importerEditor;
            _objectEditor = null;
            _importerEditor = null;
            // Tháo liên kết trước: importer editor không còn giữ / dọn SerializedObject của object editor nữa,
            // nên dù thứ tự hủy thế nào (kể cả lúc recompile / đóng cửa sổ) OnDisable của GameObjectInspector cũng không đọc property đã Dispose.
            if (impEd != null && objEd != null) LinkImporterToAssetEditor(impEd, null, true);
            if (objEd != null) DestroyImmediate(objEd);
            if (impEd != null) DestroyImmediate(impEd);
            _expectImporter = false;
            _expectObject = false;
        }

        void BuildAssetEditors()
        {
            if (_reloading) return;
            DestroyAssetEditors();
            if (_asset == null) return;

            _assetImmutable = IsImmutableAsset();
            _assetNotEditable = false;
            foreach (var ao in _assets)
                if (ao != null && (ao.hideFlags & HideFlags.NotEditable) != 0) { _assetNotEditable = true; break; }

            // Importer: chỉ lấy khi MỌI asset đang chọn đều có importer "đặc biệt" (Texture, Model, ScriptedImporter như InputActions...).
            // NativeFormat/Default importer (ScriptableObject .asset, Material...) không có gì để vẽ nên bỏ qua.
            var importers = new List<UObject>();
            foreach (var o in _assets)
            {
                if (o is Material) continue; // Material không hiện Import Settings
                string path = AssetDatabase.GetAssetPath(o);
                if (string.IsNullOrEmpty(path) || !AssetDatabase.IsMainAsset(o)) continue;
                var imp = AssetImporter.GetAtPath(path);
                if (imp == null) continue;
                if (IsPlainImporter(imp.GetType()) || IsNativeFormatImporter(imp)) continue;
                importers.Add(imp);
            }
            if (importers.Count > 0 && importers.Count == _assets.Length)
                _importerEditor = importers.Count > 1 ? UEditor.CreateEditor(importers.ToArray()) : UEditor.CreateEditor(importers[0]);

            // Chốt chặn thứ 2: editor của importer "trơn" (hiện Main Object File ID...) thì bỏ, giống Inspector gốc.
            if (_importerEditor != null && IsPlainImporter(_importerEditor.GetType()))
                DestroyImmediate(_importerEditor);

            // Một số importer editor (vd InputActions) tự vẽ hết nội dung asset => không vẽ thêm "Imported Object".
            bool showObject = _importerEditor == null || ShowsImportedObject(_importerEditor);
            bool isModel = _asset is GameObject;       // FBX: Inspector gốc chỉ hiện Model Import Settings + Preview
            if (isModel) showObject = false;
            _drawObjectBody = showObject;

            // Model: vẫn dựng object editor (không vẽ thân) để nối với importer editor => importer có Preview.
            if (showObject || isModel || _importerEditor != null)
            {
                _objectEditor = _assets.Length > 1 ? UEditor.CreateEditor(_assets) : UEditor.CreateEditor(_asset);
                LinkImporterToAssetEditor(_importerEditor, _objectEditor);
            }

            _expectImporter = _importerEditor != null;
            _expectObject = _objectEditor != null;
        }

        // Editor đã bị hủy (sau Apply/reimport, recompile...) => cần dựng lại
        bool AssetEditorsStale()
        {
            if (!_expectImporter && !_expectObject) return true;
            if (_expectImporter && (_importerEditor == null || _importerEditor.target == null)) return true;
            if (_expectObject && (_objectEditor == null || _objectEditor.target == null)) return true;
            return false;
        }

        // Importer của file Unity-native (.asset, .mat, .prefab, .unity...): Inspector gốc không hiện Import Settings cho loại này.
        static bool IsPlainImporter(Type t)
        {
            for (; t != null && t != typeof(object); t = t.BaseType)
            {
                string n = t.Name;
                if (n.StartsWith("MonoImporter", StringComparison.Ordinal) ||            // file .cs: editor MonoScriptImporterInspector không gọi base.OnEnable => Unity báo lỗi khi hủy
                    n.StartsWith("MonoScriptImporter", StringComparison.Ordinal) ||
                    n.Contains("NativeFormat") ||
                    n.Contains("NativeImporter") ||
                    n.Contains("MaterialImporter") ||
                    n.StartsWith("DefaultImporter", StringComparison.Ordinal) ||
                    n.StartsWith("PrefabImporter", StringComparison.Ordinal) ||
                    n.StartsWith("DefaultAssetImporter", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        // Importer của file Unity-native chỉ có ô "Main Object File ID": nhận ra bằng property thay vì đoán theo tên type
        // (tên type/editor đổi theo bản Unity nên đoán theo tên có thể lọt).
        static bool IsNativeFormatImporter(AssetImporter imp)
        {
            SerializedObject so = null;
            try
            {
                so = new SerializedObject(imp);
                return so.FindProperty("m_MainObjectFileID") != null || so.FindProperty("mainObjectFileID") != null;
            }
            catch (Exception) { return false; }
            finally { if (so != null) so.Dispose(); }
        }

        static void LinkImporterToAssetEditor(UEditor importerEd, UEditor assetEd, bool unlink = false)
        {
            if (importerEd == null || (assetEd == null && !unlink)) return;
            try
            {
                var m = importerEd.GetType().GetMethod("InternalSetAssetImporterTargetEditor", AnyInst);
                if (m != null) m.Invoke(importerEd, new object[] { assetEd });
            }
            catch (Exception) { }
        }

        static bool ShowsImportedObject(UEditor importerEd)
        {
            try
            {
                var p = importerEd.GetType().GetProperty("showImportedObject",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.PropertyType == typeof(bool)) return (bool)p.GetValue(importerEd);
            }
            catch (Exception) { }
            return true;
        }

        void ResetScroll()
        {
            if (_scrollV != null) _scrollV.scrollOffset = Vector2.zero;
        }

        // Editor dùng để vẽ Preview ở đáy cửa sổ (importer ưu tiên, giống Inspector gốc)
        UEditor GetPreviewEditor()
        {
            // State trong Animator: Preview của Motion được ưu tiên (chỉ tồn tại khi đang chọn đúng 1 State có Motion)
            if (_asset != null && _motionPrevEditor != null && _motionPrevEditor.target != null && _motionPrevEditor.HasPreviewGUI()) return _motionPrevEditor;
            if (_importerEditor != null && _importerEditor.target != null && _importerEditor.HasPreviewGUI()) return _importerEditor;
            if (_objectEditor != null && _objectEditor.target != null && _objectEditor.HasPreviewGUI()) return _objectEditor;
            if (_asset == null && _pfPrevEditor != null && _pfPrevEditor.target != null && _pfPrevEditor.HasPreviewGUI()) return _pfPrevEditor; // prefab có model
            return null;
        }

        // State trong Animator Controller: editor của State không có Preview riêng => dựng editor cho Motion của nó
        // (AnimationClip / BlendTree) để khung Preview ở đáy chạy được animation, giống khi chọn Transition.
        [NonSerialized] UEditor _motionPrevEditor;

        // Gọi ở SyncUI (chế độ Asset): đổi Motion của State (kéo clip khác vào ô Motion) => dựng lại
        void EnsureMotionPreview()
        {
            if (_reloading) return;
            var st = _assets.Length == 1 ? _asset as UnityEditor.Animations.AnimatorState : null;
            Motion motion = st != null ? st.motion : null;
            if (_motionPrevEditor != null && (_motionPrevEditor.target == null || _motionPrevEditor.target != motion)) DestroyMotionPreview();
            if (motion != null && _motionPrevEditor == null) _motionPrevEditor = UEditor.CreateEditor(motion);
        }

        void DestroyMotionPreview()
        {
            var ed = _motionPrevEditor;
            _motionPrevEditor = null;
            if (ed != null) DestroyImmediate(ed);
        }

        // Chế độ Asset: Import Settings + Imported Object. Mỗi phần = header (IMGUI) + thân (IMGUI hoặc InspectorElement).
    }
}
#endif
