// ============================================================
//  TrueInputSystem Tutorials — TISTutController.cs
//
//  Bộ điều khiển một scene hướng dẫn:
//    • Gom các bài học (con của _lessonRoot, theo thứ tự)
//    • Mở / đóng bài, cập nhật khung giao diện, bảng API, nhiệm vụ
//    • Lưu tiến độ (PlayerPrefs) theo mode
//    • Chuyển scene khi bấm tab Chuột + Phím / Tay cầm / Mobile
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GaMo.TrueInputSystem.Tutorials
{
    [DefaultExecutionOrder(-50)]
    [AddComponentMenu("True Input System/Tutorials/TIS Tut Controller")]
    public class TISTutController : MonoBehaviour
    {
        public const string ScenesFolder = "Assets/TrueInputSystem/Tutorials/Scenes/";
        public static readonly string[] SceneNames =
        {
            "TIS_Tut_01_ChuotBanPhim",
            "TIS_Tut_02_TayCam",
            "TIS_Tut_03_Mobile",
        };

        [SerializeField] TISTutTheme     _theme;
        [SerializeField] TISTutMode      _mode;
        [SerializeField] TISTutShellView _view;
        [SerializeField] Transform       _lessonRoot;
        [SerializeField, Tooltip("Mở lại bài đang học dở lần trước.")]
        bool _resumeLastLesson = true;
        [SerializeField, Range(5f, 60f)] float _liveRefreshHz = 20f;

        readonly List<TISTutLesson> _lessons = new List<TISTutLesson>();
        int   _index = -1;
        float _liveTimer;
        float _toastTimer;
        TISActiveDevice? _shownDevice;

        public TISTutMode      Mode    => _mode;
        public TISTutShellView View    => _view;
        public int             Index   => _index;
        public TISTutLesson    Current => _index >= 0 && _index < _lessons.Count ? _lessons[_index] : null;
        public IReadOnlyList<TISTutLesson> Lessons => _lessons;

        /// <summary>Nơi bài học đặt UI riêng (phím minh hoạ, joystick...) đè lên sân chơi.</summary>
        public RectTransform OverlayRoot => _view != null ? _view.overlayRoot : null;

        public event Action<TISTutLesson> OnLessonChanged;

        // ══════════════════════════════════════════════════════

        void Awake()
        {
            if (_theme != null) TISTutTheme.Current = _theme;
            TISTutTheme.Mode = _mode;

            _lessons.Clear();
            if (_lessonRoot != null)
            {
                for (int i = 0; i < _lessonRoot.childCount; i++)
                {
                    var l = _lessonRoot.GetChild(i).GetComponent<TISTutLesson>();
                    if (l == null) continue;
                    _lessons.Add(l);
                    if (l.Playground != null) l.Playground.SetActive(false);
                }
            }

            BuildView();
        }

        void Start()
        {
            int start = _resumeLastLesson ? PlayerPrefs.GetInt(IndexKey, 0) : 0;
            Goto(Mathf.Clamp(start, 0, Mathf.Max(0, _lessons.Count - 1)));
        }

        void OnDestroy()
        {
            Current?.Exit();
        }

        void BuildView()
        {
            if (_view == null) return;

            if (_view.lessonItemTemplate != null) _view.lessonItemTemplate.gameObject.SetActive(false);
            if (_view.objectiveTemplate != null)  _view.objectiveTemplate.gameObject.SetActive(false);
            if (_view.liveRowTemplate != null)    _view.liveRowTemplate.gameObject.SetActive(false);
            if (_view.toastGroup != null)         _view.toastGroup.alpha = 0f;

            for (int i = 0; i < _lessons.Count; i++)
            {
                int idx = i;
                var item = _view.AddLessonItem(i + 1, _lessons[i].Title);
                item.Button.onClick.AddListener(() => Goto(idx));
            }

            Hook(_view.prevButton, Prev);
            Hook(_view.nextButton, Next);
            Hook(_view.setupTab,   () => _view.ShowCodePage(false));
            Hook(_view.codeTab,    () => _view.ShowCodePage(true));
            Hook(_view.copyButton, CopyCode);
            Hook(_view.tabKeyboardMouse, () => LoadMode(TISTutMode.KeyboardMouse));
            Hook(_view.tabGamepad,       () => LoadMode(TISTutMode.Gamepad));
            Hook(_view.tabMobile,        () => LoadMode(TISTutMode.Mobile));

            if (_view.tabKeyboardMouse != null) _view.tabKeyboardMouse.IsOn = _mode == TISTutMode.KeyboardMouse;
            if (_view.tabGamepad != null)       _view.tabGamepad.IsOn       = _mode == TISTutMode.Gamepad;
            if (_view.tabMobile != null)        _view.tabMobile.IsOn        = _mode == TISTutMode.Mobile;

            _view.ShowCodePage(false);
        }

        static void Hook(TISTutButton b, UnityEngine.Events.UnityAction a)
        {
            if (b != null) b.onClick.AddListener(a);
        }

        // ══════════════════════════════════════════════════════
        //  ĐIỀU HƯỚNG BÀI
        // ══════════════════════════════════════════════════════

        public void Next() => Goto(_index + 1);
        public void Prev() => Goto(_index - 1);

        public void Goto(int index)
        {
            if (_lessons.Count == 0) return;
            index = Mathf.Clamp(index, 0, _lessons.Count - 1);
            if (index == _index) return;

            Current?.Exit();
            ResetInputState();
            _index = index;
            PlayerPrefs.SetInt(IndexKey, _index);

            var lesson = Current;
            if (_view != null)
            {
                _view.ClearLiveRows();
                ShowLessonText(lesson);
                _view.SetObjectives(lesson.Objectives);
                RefreshListStates();
                if (_view.prevButton != null) _view.prevButton.interactable = _index > 0;
                if (_view.nextButton != null) _view.nextButton.interactable = _index < _lessons.Count - 1;
            }

            lesson.Enter(this);
            for (int i = 0; i < lesson.ObjectiveCount; i++)
                if (lesson.IsObjectiveDone(i)) _view?.SetObjectiveDone(i, true);
            UpdateProgress();
            OnLessonChanged?.Invoke(lesson);
        }

        /// <summary>Trả bridge về trạng thái sạch giữa các bài (map, override, tracker).</summary>
        static void ResetInputState()
        {
            var tis = TISInputBridge.Instance;
            if (tis == null) return;
            tis.EnableDefaultMaps();
            tis.ClearMoveOverride();
            tis.ClearLookOverride();
            tis.ClearZoomOverride();
            tis.SetObjectTrackerEnabled(false);
        }

        void ShowLessonText(TISTutLesson lesson)
        {
            var v = _view;
            if (v.lessonCounter != null) v.lessonCounter.text = $"BÀI HỌC · {_index + 1}/{_lessons.Count}";
            if (v.lessonTitle != null)   v.lessonTitle.text   = lesson.Title;
            if (v.lessonSummary != null) v.lessonSummary.text = lesson.Summary;
            if (v.footerHint != null)
                v.footerHint.text = _mode == TISTutMode.Gamepad
                    ? lesson.FooterHint + "\n<size=80%><color=#9A9F9C>VIEW + RB / LB: chuyển bài · VIEW + Y: xem code</color></size>"
                    : lesson.FooterHint;

            if (v.setupText != null)
            {
                var steps = lesson.SetupSteps;
                if (steps.Length == 0) v.setupText.text = "<color=#9A9F9C>Bài này không cần setup thêm.</color>";
                else
                {
                    var sb = new System.Text.StringBuilder();
                    string accent = "#" + ColorUtility.ToHtmlStringRGB(TISTutTheme.Current.AccentStrong());
                    for (int i = 0; i < steps.Length; i++)
                    {
                        if (i > 0) sb.Append("\n\n");
                        sb.Append("<color=").Append(accent).Append("><b>").Append(i + 1).Append("</b></color>   ").Append(steps[i]);
                    }
                    v.setupText.text = sb.ToString();
                }
            }
            if (v.codeText != null) v.codeText.text = TISTutCodeHighlighter.Highlight(lesson.Code);
            if (v.codeTab != null)  v.codeTab.interactable = !string.IsNullOrEmpty(lesson.Code);
        }

        void RefreshListStates()
        {
            for (int i = 0; i < _lessons.Count; i++)
                _view.GetLessonItem(i)?.SetState(i == _index, IsLessonDone(i));
        }

        // ══════════════════════════════════════════════════════
        //  NHIỆM VỤ & TIẾN ĐỘ
        // ══════════════════════════════════════════════════════

        internal void NotifyObjective(TISTutLesson lesson, int index)
        {
            if (lesson != Current) return;
            _view?.SetObjectiveDone(index, true);
            UpdateProgress();

            if (lesson.AllDone)
            {
                PlayerPrefs.SetInt(DoneKey(_index), 1);
                RefreshListStates();
                Toast(_index < _lessons.Count - 1 ? "Hoàn thành! Sang bài tiếp theo nhé →" : "Hoàn thành tất cả bài học!");
            }
        }

        void UpdateProgress()
        {
            if (_view == null || _view.progressText == null) return;
            var l = Current;
            int n = l.ObjectiveCount, d = 0;
            for (int i = 0; i < n; i++) if (l.IsObjectiveDone(i)) d++;
            _view.progressText.transform.parent.gameObject.SetActive(n > 0);
            _view.progressText.text = d == n ? "XONG ✓" : $"TIẾN ĐỘ {d}/{n}";
        }

        public bool IsLessonDone(int index) => PlayerPrefs.GetInt(DoneKey(index), 0) == 1;

        /// <summary>Xoá tiến độ của scene này.</summary>
        public void ResetProgress()
        {
            for (int i = 0; i < _lessons.Count; i++) PlayerPrefs.DeleteKey(DoneKey(i));
            PlayerPrefs.DeleteKey(IndexKey);
            RefreshListStates();
        }

        string IndexKey => $"TISTut_{_mode}_Index";
        string DoneKey(int i) => $"TISTut_{_mode}_{(i < _lessons.Count ? _lessons[i].GetType().Name : i.ToString())}_Done";

        // ══════════════════════════════════════════════════════
        //  LIVE API · TOAST · CODE
        // ══════════════════════════════════════════════════════

        internal void AddWatch(string apiName, Func<string> value, Func<float> bar01)
        {
            if (_view == null) return;
            _view.AddLiveRow().Bind(apiName, value, bar01);
        }

        public void Toast(string message)
        {
            if (_view == null || _view.toastGroup == null) return;
            if (_view.toastText != null) _view.toastText.text = message;
            _toastTimer = 2.2f;
        }

        void CopyCode()
        {
            var l = Current;
            if (l == null || string.IsNullOrEmpty(l.Code)) return;
            GUIUtility.systemCopyBuffer = l.Code;
            Toast("Đã sao chép code mẫu");
        }

        void Update()
        {
            Current?.Tick();
            HandleShortcuts();

            if (_view == null) return;

            // Bảng API — refresh giới hạn tần số (đỡ cấp phát chuỗi mỗi frame)
            _liveTimer -= Time.unscaledDeltaTime;
            bool full = _liveTimer <= 0f;
            if (full) _liveTimer = 1f / _liveRefreshHz;
            var rows = _view.LiveRows;
            for (int i = 0; i < _view.LiveRowCount; i++)
                rows[i].Refresh(false, full);

            // Toast mờ dần
            if (_view.toastGroup != null)
            {
                _toastTimer -= Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(_toastTimer / 0.3f);
                if (!Mathf.Approximately(_view.toastGroup.alpha, a)) _view.toastGroup.alpha = a;
            }

            UpdateDeviceChip();
        }

        /// <summary>
        /// Phím tắt chuyển bài không cần chuột:
        /// tay cầm VIEW + RB / LB (VIEW + Y: đổi tab Setup ↔ Code) · bàn phím PageDown / PageUp.
        /// </summary>
        void HandleShortcuts()
        {
            var gp = UnityEngine.InputSystem.Gamepad.current;
            if (gp != null && gp.selectButton.isPressed)
            {
                if (gp.rightShoulder.wasPressedThisFrame) Next();
                if (gp.leftShoulder.wasPressedThisFrame)  Prev();
                if (gp.buttonNorth.wasPressedThisFrame && _view != null && _view.codePage != null)
                    _view.ShowCodePage(!_view.codePage.activeSelf);
            }
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.pageDownKey.wasPressedThisFrame) Next();
                if (kb.pageUpKey.wasPressedThisFrame)   Prev();
            }
        }

        void UpdateDeviceChip()
        {
            var tis = TISInputBridge.Instance;
            if (tis == null || _view.deviceChipText == null) return;
            var dev = tis.ActiveDevice;
            if (_shownDevice == dev) return;
            _shownDevice = dev;

            var th = TISTutTheme.Current;
            TISTutMode m = dev switch
            {
                TISActiveDevice.Gamepad => TISTutMode.Gamepad,
                TISActiveDevice.Mobile  => TISTutMode.Mobile,
                _                       => TISTutMode.KeyboardMouse,
            };
            string label = dev switch
            {
                TISActiveDevice.Gamepad => "● Đang dùng: Tay cầm",
                TISActiveDevice.Mobile  => "● Đang dùng: Cảm ứng",
                _                       => "● Đang dùng: Chuột + Phím",
            };
            _view.deviceChipText.text  = label;
            _view.deviceChipText.color = th.ink;
            if (_view.deviceChip != null) _view.deviceChip.color = th.Accent(m);
        }

        // ══════════════════════════════════════════════════════
        //  CHUYỂN SCENE
        // ══════════════════════════════════════════════════════

        public void LoadMode(TISTutMode mode)
        {
            if (mode == _mode) return;
            string name = SceneNames[(int)mode];
#if UNITY_EDITOR
            // Editor: nạp thẳng theo đường dẫn — không cần thêm scene vào Build Settings
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenesFolder + name + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            if (Application.CanStreamedLevelBeLoaded(name)) SceneManager.LoadScene(name);
            else Toast("Thêm scene " + name + " vào Build Settings để chuyển mode");
#endif
        }
    }
}
