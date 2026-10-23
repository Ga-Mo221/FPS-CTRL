// ============================================================
//  TrueInputSystem Tutorials — TISTutShellView.cs
//
//  Giữ tham chiếu tới mọi phần của khung giao diện (do Editor builder dựng):
//    Thanh trên   : logo · 3 tab mode · chip "Đang dùng"
//    Cột trái     : danh sách bài học
//    Giữa         : sân chơi 3D (cửa sổ camera) · tiêu đề bài · nhiệm vụ "Thử ngay" · toast
//    Cột phải     : "API đang chạy" · card Setup / Code mẫu
//    Chân trang   : Bài trước · gợi ý · Bài tiếp theo
// ============================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/TIS Tut Shell View")]
    public class TISTutShellView : MonoBehaviour
    {
        [Header("Thanh trên")]
        public TISTutButton tabKeyboardMouse;
        public TISTutButton tabGamepad;
        public TISTutButton tabMobile;
        public TISTutShape  deviceChip;
        public TMP_Text     deviceChipText;

        [Header("Danh sách bài")]
        public TMP_Text         lessonCounter;
        public RectTransform    lessonListRoot;
        public TISTutLessonItem lessonItemTemplate;

        [Header("Sân chơi")]
        public RectTransform playground;
        public RectTransform overlayRoot;
        public TMP_Text      lessonTitle;
        public TMP_Text      lessonSummary;
        public TMP_Text      tryBadge;
        public RectTransform objectiveRoot;
        public TISTutObjectiveRow objectiveTemplate;
        public TISTutShape   progressChip;
        public TMP_Text      progressText;
        public CanvasGroup   toastGroup;
        public TMP_Text      toastText;

        [Header("API đang chạy")]
        public RectTransform liveRoot;
        public TISTutLiveRow liveRowTemplate;
        public TMP_Text      liveEmptyText;

        [Header("Card Setup / Code")]
        public TISTutButton  setupTab;
        public TISTutButton  codeTab;
        public GameObject    setupPage;
        public TMP_Text      setupText;
        public GameObject    codePage;
        public TMP_Text      codeText;
        public TISTutButton  copyButton;

        [Header("Chân trang")]
        public TISTutButton prevButton;
        public TISTutButton nextButton;
        public TMP_Text     footerHint;

        // ── Danh sách bài ─────────────────────────────────────

        readonly List<TISTutLessonItem>   _items      = new List<TISTutLessonItem>();
        readonly List<TISTutObjectiveRow> _objectives = new List<TISTutObjectiveRow>();
        readonly List<TISTutLiveRow>      _liveRows   = new List<TISTutLiveRow>();
        int _liveUsed;

        public IReadOnlyList<TISTutLiveRow> LiveRows => _liveRows;
        // Kẹp theo danh sách thật: biên dịch lại script lúc đang Play (hot reload) khôi phục được
        // _liveUsed nhưng tạo lại _liveRows rỗng → tránh truy cập ngoài phạm vi.
        public int LiveRowCount => Mathf.Min(_liveUsed, _liveRows.Count);

        public TISTutLessonItem AddLessonItem(int number, string title)
        {
            var item = Instantiate(lessonItemTemplate, lessonListRoot);
            item.gameObject.SetActive(true);
            item.name = $"Lesson {number:00}";
            item.Setup(number, title);
            _items.Add(item);
            return item;
        }

        public TISTutLessonItem GetLessonItem(int index) => index >= 0 && index < _items.Count ? _items[index] : null;

        // ── Nhiệm vụ ──────────────────────────────────────────

        public void SetObjectives(string[] objectives)
        {
            for (int i = 0; i < _objectives.Count; i++) _objectives[i].gameObject.SetActive(false);
            for (int i = 0; i < objectives.Length; i++)
            {
                if (i >= _objectives.Count)
                {
                    var row = Instantiate(objectiveTemplate, objectiveRoot);
                    row.name = $"Objective {i}";
                    _objectives.Add(row);
                }
                _objectives[i].gameObject.SetActive(true);
                _objectives[i].Setup(objectives[i]);
            }
            if (objectiveRoot != null) objectiveRoot.gameObject.SetActive(objectives.Length > 0);
        }

        public void SetObjectiveDone(int index, bool done)
        {
            if (index >= 0 && index < _objectives.Count) _objectives[index].SetDone(done);
        }

        // ── Live API ──────────────────────────────────────────

        public void ClearLiveRows()
        {
            for (int i = 0; i < _liveRows.Count; i++) _liveRows[i].gameObject.SetActive(false);
            _liveUsed = 0;
            if (liveEmptyText != null) liveEmptyText.gameObject.SetActive(true);
        }

        public TISTutLiveRow AddLiveRow()
        {
            if (_liveUsed > _liveRows.Count) _liveUsed = _liveRows.Count;
            if (_liveUsed >= _liveRows.Count)
            {
                var row = Instantiate(liveRowTemplate, liveRoot);
                row.name = $"Live {_liveRows.Count}";
                _liveRows.Add(row);
            }
            var r = _liveRows[_liveUsed++];
            r.gameObject.SetActive(true);
            if (liveEmptyText != null) liveEmptyText.gameObject.SetActive(false);
            return r;
        }

        // ── Card Setup / Code ─────────────────────────────────

        public void ShowCodePage(bool code)
        {
            if (setupPage != null) setupPage.SetActive(!code);
            if (codePage != null)  codePage.SetActive(code);
            if (setupTab != null)  setupTab.IsOn = !code;
            if (codeTab != null)   codeTab.IsOn = code;
            if (copyButton != null) copyButton.gameObject.SetActive(code);
        }
    }
}
