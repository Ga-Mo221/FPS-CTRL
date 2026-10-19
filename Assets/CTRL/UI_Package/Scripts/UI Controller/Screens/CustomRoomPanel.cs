// [Aemy 2026-10-17] FILE MOI. Khung "PHONG TUY CHINH" o tab CHOI: luat choi, moc ha guc, nhap ma de vao phong.
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Backend.Models;

namespace Game.UI
{
    /// <summary>
    /// Chi trinh bay + phat su kien (SRP). MenuShellBinder lo tao phong / vao phong / dong bo to doi.
    /// - Luat: 2 nut TEAM DM / FFA.
    /// - Moc ha guc: ◄ gia tri ► (10 · 20 · 30 · 50 · 75 · 100).
    /// - [Aemy 2026-10-19] Thoi gian: ◄ gia tri ► (5 · 10 · 15 · 20 · 30 phut, mac dinh 10).
    /// - Ma phong: o nhap + nut VAO PHONG (phong rieng, nguoi la khong vao duoc).
    /// </summary>
    public class CustomRoomPanel : MonoBehaviour
    {
        [SerializeField] private Button _ruleTdm;
        [SerializeField] private Button _ruleFfa;
        [SerializeField] private Button _killPrev;
        [SerializeField] private Button _killNext;
        [SerializeField] private TMP_Text _killValue;
        [SerializeField] private Button _timePrev;
        [SerializeField] private Button _timeNext;
        [SerializeField] private TMP_Text _timeValue;
        [SerializeField] private TMP_InputField _codeInput;
        [SerializeField] private Button _joinButton;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private Color _onColor = new Color(0.31f, 0.84f, 0.79f, 1f);
        [SerializeField] private Color _offColor = new Color(0.11f, 0.13f, 0.15f, 1f);
        [SerializeField] private Color _onText = new Color(0.04f, 0.08f, 0.09f, 1f);
        [SerializeField] private Color _offText = new Color(0.85f, 0.88f, 0.9f, 1f);

        [Header("Tay cam: noi o map hang duoi <-> khung nay khi hien")]
        [SerializeField] private GaMo.TrueInputSystem.TISGamepadUIButton[] _navAbove;
        [SerializeField] private GaMo.TrueInputSystem.TISGamepadUIButton _navFirst;
        [SerializeField] private GaMo.TrueInputSystem.TISGamepadUIButton _navFallback;

        private void OnEnable() { if (_navAbove != null) foreach (var b in _navAbove) if (b != null) b.navDown = _navFirst != null ? _navFirst : _navFallback; }
        private void OnDisable() { if (_navAbove != null) foreach (var b in _navAbove) if (b != null) b.navDown = _navFallback; }

        private static readonly int[] KillSteps = { 10, 20, 30, 50, 75, 100 };
        private int _killIdx = 1;
        private static readonly int[] TimeSteps = { 5, 10, 15, 20, 30 };
        private int _timeIdx = 1;
        private bool _locked;

        public MatchMode Rule { get; private set; } = MatchMode.TDM;
        public int KillTarget { get { return KillSteps[Mathf.Clamp(_killIdx, 0, KillSteps.Length - 1)]; } }
        /// <summary>Thoi gian tran (phut).</summary>
        public int MatchMinutes { get { return TimeSteps[Mathf.Clamp(_timeIdx, 0, TimeSteps.Length - 1)]; } }

        /// <summary>Doi luat / moc ha guc (truong nhom phat cho to doi).</summary>
        public event Action Changed;
        /// <summary>Nguoi choi bam VAO PHONG voi ma da nhap (chua kiem tra dinh dang).</summary>
        public event Action<string> JoinRequested;

        private void Awake()
        {
            if (_ruleTdm != null) _ruleTdm.onClick.AddListener(() => SetRule(MatchMode.TDM, true));
            if (_ruleFfa != null) _ruleFfa.onClick.AddListener(() => SetRule(MatchMode.FFA, true));
            if (_killPrev != null) _killPrev.onClick.AddListener(() => StepKill(-1));
            if (_killNext != null) _killNext.onClick.AddListener(() => StepKill(1));
            if (_timePrev != null) _timePrev.onClick.AddListener(() => StepTime(-1));
            if (_timeNext != null) _timeNext.onClick.AddListener(() => StepTime(1));
            if (_joinButton != null) _joinButton.onClick.AddListener(RaiseJoin);
            if (_codeInput != null)
            {
                _codeInput.characterLimit = 6;
                _codeInput.onValueChanged.AddListener(v => { var up = v.ToUpperInvariant(); if (up != v) _codeInput.SetTextWithoutNotify(up); });
                _codeInput.onSubmit.AddListener(_ => RaiseJoin());
            }
            Render();
        }

        private void RaiseJoin()
        {
            if (JoinRequested != null) JoinRequested(_codeInput != null ? _codeInput.text : "");
        }

        private void StepKill(int dir)
        {
            if (_locked) return;
            _killIdx = Mathf.Clamp(_killIdx + dir, 0, KillSteps.Length - 1);
            Render();
            if (Changed != null) Changed();
        }

        private void StepTime(int dir)
        {
            if (_locked) return;
            _timeIdx = Mathf.Clamp(_timeIdx + dir, 0, TimeSteps.Length - 1);
            Render();
            if (Changed != null) Changed();
        }

        /// <summary>Dat thoi gian (phut) gan nhat trong cac moc (thanh vien nhan tu truong nhom).</summary>
        public void SetMatchMinutes(int minutes)
        {
            int best = 0;
            for (int i = 0; i < TimeSteps.Length; i++) if (Mathf.Abs(TimeSteps[i] - minutes) < Mathf.Abs(TimeSteps[best] - minutes)) best = i;
            _timeIdx = best; Render();
        }

        public void SetRule(MatchMode rule, bool notify)
        {
            if (_locked && notify) return;
            Rule = rule == MatchMode.TDM ? MatchMode.TDM : MatchMode.FFA;
            Render();
            if (notify && Changed != null) Changed();
        }

        public void SetKillTarget(int kill)
        {
            int best = 0;
            for (int i = 0; i < KillSteps.Length; i++) if (Mathf.Abs(KillSteps[i] - kill) < Mathf.Abs(KillSteps[best] - kill)) best = i;
            _killIdx = best; Render();
        }

        /// <summary>Thanh vien to doi: chi xem luat/moc cua truong nhom; van nhap ma de vao phong khac khi da roi doi.</summary>
        public void SetLocked(bool locked)
        {
            _locked = locked;
            foreach (var b in new[] { _ruleTdm, _ruleFfa, _killPrev, _killNext, _timePrev, _timeNext }) if (b != null) b.interactable = !locked;
        }

        public void SetStatus(string text)
        {
            if (_status != null) _status.text = text ?? "";
        }

        private void Render()
        {
            Paint(_ruleTdm, Rule == MatchMode.TDM);
            Paint(_ruleFfa, Rule == MatchMode.FFA);
            if (_killValue != null) _killValue.text = KillTarget.ToString();
            if (_timeValue != null) _timeValue.text = MatchMinutes.ToString();
        }

        private void Paint(Button b, bool on)
        {
            if (b == null) return;
            var img = b.targetGraphic as Image; if (img == null) img = b.GetComponent<Image>();
            if (img != null) img.color = on ? _onColor : _offColor;
            var t = b.GetComponentInChildren<TMP_Text>(true);
            if (t != null) t.color = on ? _onText : _offText;
        }
    }
}
