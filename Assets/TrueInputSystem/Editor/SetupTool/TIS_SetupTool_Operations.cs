// ============================================================
//  TrueInputSystem — InputBridgeSetupTool_Operations.cs  [Partial]
//  Phần 5/5: Tất cả logic business:
//  ─ Map CRUD: Add, Remove, Duplicate, Rename, ToggleEnabled
//  ─ Action CRUD: Add, Remove, Duplicate, Move, Options menu
//  ─ Profile: New, Load, Generate (.asset), Assign to Scene
//  ─ Validation
//  ─ TISBindingInputPopup (popup gõ binding path thủ công)
// ============================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GaMo.TrueInputSystem.Editor
{
    public partial class TISSetupTool
    {
        // ══════════════════════════════════════════════════════
        //  MAP OPERATIONS
        // ══════════════════════════════════════════════════════

        private void AddNewMap()
        {
            SafeRecord("Add Map");
            string newName = GetUniqueName("NewMap",
                _workingProfile.actionMaps.Select(m => m.mapName));
            _workingProfile.actionMaps.Add(new TISActionMapData
            {
                mapName          = newName,
                enabledByDefault = true,
                actions          = new List<TISInputActionDefinition>(),
            });
            _selectedMapIndex = _workingProfile.actionMaps.Count - 1;
            MarkDirty();
        }

        private void RemoveMap(int index)
        {
            if (_workingProfile.actionMaps.Count <= 1)
            {
                EditorUtility.DisplayDialog("Không thể xoá",
                    "Profile phải có ít nhất một Action Map.", "OK");
                return;
            }
            bool confirm = EditorUtility.DisplayDialog("Xoá Action Map",
                $"Bạn có chắc muốn xoá map \"{_workingProfile.actionMaps[index].mapName}\"?\n" +
                $"Tất cả {_workingProfile.actionMaps[index].actions.Count} action bên trong sẽ bị mất.",
                "Xoá", "Huỷ");
            if (!confirm) return;

            SafeRecord("Remove Map");
            _workingProfile.actionMaps.RemoveAt(index);
            _selectedMapIndex = Mathf.Clamp(_selectedMapIndex, 0,
                _workingProfile.actionMaps.Count - 1);
            MarkDirty();
        }

        private void DuplicateMap(int index)
        {
            SafeRecord("Duplicate Map");
            var clone = _workingProfile.actionMaps[index].Clone();
            clone.mapName = GetUniqueName(clone.mapName + "_Copy",
                _workingProfile.actionMaps.Select(m => m.mapName));
            _workingProfile.actionMaps.Insert(index + 1, clone);
            _selectedMapIndex = index + 1;
            MarkDirty();
        }

        private void ToggleMapEnabled(int index)
        {
            SafeRecord("Toggle Map Enabled");
            _workingProfile.actionMaps[index].enabledByDefault =
                !_workingProfile.actionMaps[index].enabledByDefault;
            MarkDirty();
        }

        private void BeginRename(int index)
        {
            _isRenaming     = true;
            _renamingAction = false;
            _renamingIndex  = index;
            _renameBuffer   = _workingProfile.actionMaps[index].mapName;
        }

        /// <summary>Đổi tên hành động thứ <paramref name="index"/> trong nhóm đang chọn.</summary>
        private void BeginRenameAction(int index)
        {
            var actions = _workingProfile.actionMaps[_selectedMapIndex].actions;
            if (actions == null || index < 0 || index >= actions.Count) return;
            _isRenaming     = true;
            _renamingAction = true;
            _renamingIndex  = index;
            _renameBuffer   = actions[index].actionName;
        }

        private void CommitRename()
        {
            if (_renamingAction)
            {
                var actions = _workingProfile.actionMaps[_selectedMapIndex].actions;
                if (actions != null && _renamingIndex >= 0 && _renamingIndex < actions.Count)
                {
                    SafeRecord("Đổi tên hành động");
                    actions[_renamingIndex].actionName = _renameBuffer;
                    MarkDirty();
                }
            }
            else if (_renamingIndex >= 0 && _renamingIndex < _workingProfile.actionMaps.Count)
            {
                SafeRecord("Rename Map");
                _workingProfile.actionMaps[_renamingIndex].mapName = _renameBuffer;
                MarkDirty();
            }
            CancelRename();
        }

        private void CancelRename()
        {
            _isRenaming     = false;
            _renamingAction = false;
            _renamingIndex  = -1;
            _renameBuffer   = "";
            _renameFieldScreenRect = default;
        }

        // ══════════════════════════════════════════════════════
        //  ACTION OPERATIONS
        // ══════════════════════════════════════════════════════

        private void AddNewAction(TISActionMapData map)
        {
            SafeRecord("Add Action");
            string newName = GetUniqueName("NewAction",
                _workingProfile.actionMaps.SelectMany(m => m.actions).Select(a => a.actionName));
            map.actions.Add(new TISInputActionDefinition
            {
                actionName    = newName,
                actionType    = TISInputActionType.Button,
                isSinglePress = true,
            });
            MarkDirty();
        }

        private void RemoveAction(TISActionMapData map, int index)
        {
            SafeRecord("Remove Action");
            map.actions.RemoveAt(index);
            MarkDirty();
        }

        private void DuplicateAction(TISActionMapData map, int index)
        {
            SafeRecord("Duplicate Action");
            var clone = map.actions[index].Clone();
            clone.actionName = GetUniqueName(clone.actionName + "_Copy",
                _workingProfile.actionMaps.SelectMany(m => m.actions).Select(a => a.actionName));
            map.actions.Insert(index + 1, clone);
            MarkDirty();
        }

        private void MoveAction(TISActionMapData map, int index, int direction)
        {
            int newIdx = index + direction;
            if (newIdx < 0 || newIdx >= map.actions.Count) return;
            SafeRecord("Move Action");
            (map.actions[index], map.actions[newIdx]) = (map.actions[newIdx], map.actions[index]);
            MarkDirty();
        }

        // ══════════════════════════════════════════════════════
        //  PROFILE OPERATIONS
        // ══════════════════════════════════════════════════════

        private void NewProfile()
        {
            _workingProfile = TISInputProfileConfig.CreateDefault();
            _workingProfile.profileName = "New Profile";
            _workingProfile.hideFlags   = HideFlags.HideAndDontSave;
            _sourceAsset       = null;
            _selectedMapIndex  = 0;
            _isDirty           = false;
            _validationRan     = false;
            _validationErrors.Clear();
            _showLanding = false;
        }

        private void LoadFromAssetDialog()
        {
            string path = EditorUtility.OpenFilePanel("Mở TISInputProfileConfig", "Assets", "asset");
            if (string.IsNullOrEmpty(path)) return;
            if (path.StartsWith(Application.dataPath))
                path = "Assets" + path.Substring(Application.dataPath.Length);
            var asset = AssetDatabase.LoadAssetAtPath<TISInputProfileConfig>(path);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("Lỗi", "File không phải TISInputProfileConfig.asset!", "OK");
                return;
            }
            LoadFromAsset(asset);
        }

        /// <summary>Load profile asset vào working copy. Gọi được từ reflection (TISCreateDefaultProfileMenu).</summary>
        private void LoadFromAsset(TISInputProfileConfig asset)
        {
            _sourceAsset    = asset;
            _workingProfile = asset.CreateRuntimeCopy();
            _workingProfile.hideFlags   = HideFlags.HideAndDontSave;
            _workingProfile.profileName = asset.profileName;
            _selectedMapIndex = 0;
            _isDirty          = false;
            _validationRan    = false;
            _validationErrors.Clear();
            _showLanding = false;
            //Debug.Log($"[TIS Setup] Đã load: {AssetDatabase.GetAssetPath(asset)}");
        }

        private void GenerateAsset()
        {
            const string defaultFolder = "Assets/TrueInputSystem/Profiles";

            if (!AssetDatabase.IsValidFolder("Assets/TrueInputSystem"))
                AssetDatabase.CreateFolder("Assets", "TrueInputSystem");
            if (!AssetDatabase.IsValidFolder(defaultFolder))
                AssetDatabase.CreateFolder("Assets/TrueInputSystem", "Profiles");

            string assetPath;

            if (_sourceAsset != null)
            {
                assetPath = AssetDatabase.GetAssetPath(_sourceAsset);
            }
            else
            {
                string safeName = _workingProfile.profileName
                    .Replace(" ", "_").Replace("/", "_").Replace("\\", "_");
                assetPath = $"{defaultFolder}/{safeName}.asset";

                if (File.Exists(Path.Combine(Application.dataPath, assetPath.Replace("Assets/", ""))))
                {
                    bool ow = EditorUtility.DisplayDialog("File đã tồn tại",
                        $"File \"{assetPath}\" đã tồn tại.\nGhi đè không?",
                        "Ghi đè", "Chọn tên khác");
                    if (!ow)
                    {
                        assetPath = EditorUtility.SaveFilePanelInProject(
                            "Lưu Profile", safeName, "asset",
                            "Chọn nơi lưu TISInputProfileConfig", defaultFolder);
                        if (string.IsNullOrEmpty(assetPath)) return;
                    }
                }
            }

            TISInputProfileConfig targetAsset;
            if (_sourceAsset != null)
            {
                targetAsset = _sourceAsset;
                Undo.RecordObject(targetAsset, "Generate Profile");
            }
            else
            {
                targetAsset = ScriptableObject.CreateInstance<TISInputProfileConfig>();
            }

            // Copy working data → target asset
            targetAsset.profileName     = _workingProfile.profileName;
            targetAsset.description     = _workingProfile.description;
            targetAsset.enablePC        = _workingProfile.enablePC;        // thêm
            targetAsset.enableGamepad   = _workingProfile.enableGamepad;   // thêm
            targetAsset.enableMobile    = _workingProfile.enableMobile;    // thêm
            targetAsset.mobileSettings  = _workingProfile.mobileSettings;
            targetAsset.cameraSettings  = _workingProfile.cameraSettings;
            targetAsset.gamepadSettings = _workingProfile.gamepadSettings;
            targetAsset.motionSettings  = _workingProfile.motionSettings;
            targetAsset.actionMaps      = _workingProfile.actionMaps.Select(m => m.Clone()).ToList();

            if (_sourceAsset != null)
            {
                EditorUtility.SetDirty(targetAsset);
                AssetDatabase.SaveAssets();
            }
            else
            {
                AssetDatabase.CreateAsset(targetAsset, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                _sourceAsset = targetAsset;
            }

            _isDirty = false;
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = targetAsset;
            Debug.Log($"[TIS Setup] ✓ Đã lưu: \"{targetAsset.profileName}\" → {assetPath}");
            EditorUtility.DisplayDialog("Generate Thành Công",
                $"✓ Profile \"{targetAsset.profileName}\" đã lưu tại:\n{assetPath}", "OK");
        }

        // ── Save / Save As ────────────────────────────────────

        /// <summary>
        /// Save nhanh (Ctrl+S):
        /// - Có _sourceAsset → ghi đè trực tiếp, không hỏi, không dialog.
        /// - Chưa có → chuyển sang SaveAssetAs() để chọn nơi lưu.
        /// </summary>
        internal void SaveAsset()
        {
            if (_sourceAsset != null)
            {
                WriteWorkingDataToAsset(_sourceAsset);
                _isDirty       = false;
                _validationRan = false;
                Repaint();
                Debug.Log($"[TIS Setup] ✓ Saved → {AssetDatabase.GetAssetPath(_sourceAsset)}");
            }
            else
            {
                // Chưa có file → phải chọn nơi lưu
                SaveAssetAs();
            }
        }

        /// <summary>
        /// Save As — luôn mở file picker, tạo file mới hoặc ghi đè vị trí mới.
        /// </summary>
        internal void SaveAssetAs()
        {
            const string defaultFolder = "Assets/TrueInputSystem/Profiles";

            if (!AssetDatabase.IsValidFolder("Assets/TrueInputSystem"))
                AssetDatabase.CreateFolder("Assets", "TrueInputSystem");
            if (!AssetDatabase.IsValidFolder(defaultFolder))
                AssetDatabase.CreateFolder("Assets/TrueInputSystem", "Profiles");

            string safeName = _workingProfile.profileName
                .Replace(" ", "_").Replace("/", "_").Replace("\\", "_");

            string assetPath = EditorUtility.SaveFilePanelInProject(
                "Lưu Profile As", safeName, "asset",
                "Chọn nơi lưu TISInputProfileConfig", defaultFolder);

            if (string.IsNullOrEmpty(assetPath)) return;

            // Nếu file đã tồn tại → ghi đè (người dùng đã confirm qua dialog OS)
            var existing = AssetDatabase.LoadAssetAtPath<TISInputProfileConfig>(assetPath);
            if (existing != null)
            {
                WriteWorkingDataToAsset(existing);
                _sourceAsset = existing;
            }
            else
            {
                var newAsset = ScriptableObject.CreateInstance<TISInputProfileConfig>();
                WriteWorkingDataToAsset(newAsset);
                AssetDatabase.CreateAsset(newAsset, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                _sourceAsset = newAsset;
            }

            _isDirty       = false;
            _validationRan = false;
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = _sourceAsset;
            Repaint();
            Debug.Log($"[TIS Setup] ✓ Saved As → {assetPath}");
        }

        /// <summary>Copy toàn bộ working data vào target asset và flush.</summary>
        private void WriteWorkingDataToAsset(TISInputProfileConfig target)
        {
            Undo.RecordObject(target, "Save Profile");
            target.profileName     = _workingProfile.profileName;
            target.description     = _workingProfile.description;
            target.enablePC        = _workingProfile.enablePC;        // thêm
            target.enableGamepad   = _workingProfile.enableGamepad;   // thêm
            target.enableMobile    = _workingProfile.enableMobile;    // thêm
            target.mobileSettings  = _workingProfile.mobileSettings;
            target.cameraSettings  = _workingProfile.cameraSettings;
            target.gamepadSettings = _workingProfile.gamepadSettings;
            target.motionSettings  = _workingProfile.motionSettings;
            target.actionMaps      = _workingProfile.actionMaps.Select(m => m.Clone()).ToList();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
        }

        //[Obsolete("Obsolete")]
        private void AssignToScene()
        {
            if (_sourceAsset == null)
            {
                bool gen = EditorUtility.DisplayDialog("Chưa có file .asset",
                    "Bạn cần Generate file .asset trước khi gán vào scene.\nGenerate ngay không?",
                    "Generate", "Huỷ");
                if (gen) GenerateAsset();
                if (_sourceAsset == null) return;
            }

            var bridge = FindAnyObjectByType<TISInputBridge>();
            if (bridge == null)
            {
                EditorUtility.DisplayDialog("Không tìm thấy TISInputBridge",
                    "Không có TISInputBridge component nào trong scene hiện tại.\n" +
                    "1. Tạo Empty GameObject\n2. Add Component → TISInputBridge\n3. Bấm lại 'Gán vào Scene'",
                    "OK");
                return;
            }

            var so          = new SerializedObject(bridge);
            var profileProp = so.FindProperty("_profile");
            if (profileProp != null)
            {
                profileProp.objectReferenceValue = _sourceAsset;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(bridge);
                Debug.Log($"[TIS Setup] ✓ Đã gán \"{_sourceAsset.profileName}\" → TISInputBridge.");
                EditorUtility.DisplayDialog("Gán Thành Công",
                    $"Profile \"{_sourceAsset.profileName}\" đã được gán vào TISInputBridge.", "OK");
            }
            else
            {
                Debug.LogError("[TIS Setup] Không tìm thấy field '_profile' trong TISInputBridge.");
            }
        }

        // ══════════════════════════════════════════════════════
        //  VALIDATION
        // ══════════════════════════════════════════════════════

        private void RunValidation()
        {
            _validationErrors = _workingProfile.Validate();
            _validationRan    = true;
            if (_validationErrors.Count == 0)
                Debug.Log($"[TIS Setup] ✓ Profile \"{_workingProfile.profileName}\" hợp lệ.");
            else
                foreach (var e in _validationErrors)
                    Debug.LogWarning($"[TIS Setup] ✗ {e}");
            Repaint();
        }
    }

    // ══════════════════════════════════════════════════════════
    //  BINDING INPUT POPUP
    //  Popup nhỏ cho phép gõ binding path tự do (right-click).
    // ══════════════════════════════════════════════════════════

    public class TISBindingInputPopup : PopupWindowContent
    {
        private string         _value;
        private Action<string> _onChange;
        private bool           _focused;

        public static void Show(string current, Action<string> onChange)
        {
            var popup = new TISBindingInputPopup { _value = current, _onChange = onChange };
    
            var rect = GUILayoutUtility.GetLastRect();
            rect.x += -50f;   // chỉnh ngang nếu cần
            rect.y += 200f;  // bù offset dọc — thử 20f trước
    
            PopupWindow.Show(rect, popup);
        }

        public override Vector2 GetWindowSize() => new Vector2(360f, 60f);

        public override void OnGUI(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.13f, 0.14f, 0.17f));

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.Space(8);
            GUILayout.Label("Path:", GUILayout.Width(36));
            GUI.SetNextControlName("BindingField");
            _value = GUILayout.TextField(_value, GUILayout.ExpandWidth(true));
            if (!_focused)
            {
                EditorGUI.FocusTextInControl("BindingField");
                _focused = true;
            }
            if (GUILayout.Button("✔", GUILayout.Width(26)))
            {
                _onChange(_value);
                editorWindow.Close();
            }
            GUILayout.Space(8);
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("  Gõ Unity Input System path  (VD: <Keyboard>/space)",
                new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = new Color(0.45f, 0.50f, 0.55f) } });

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            { _onChange(_value); editorWindow.Close(); Event.current.Use(); }
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            { editorWindow.Close(); Event.current.Use(); }
        }
    }
}
#endif