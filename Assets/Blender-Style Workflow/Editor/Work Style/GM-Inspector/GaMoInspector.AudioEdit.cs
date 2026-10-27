// GaMoInspector.AudioEdit.cs  -  part of CustomsEditor (GaMo Inspector)
// Panel "Edit Audio" cho AudioClip: waveform, chon vung, Trim/Delete/Reverse/Mono + hieu ung bang thanh truot (EQ Bass/Mid/Treble + Low/High Cut, preset Bright/Muffled/Radio/Reverse, Fade In/Out co chon thoi gian, Gain, Normalize, Trim Silence, Speed, Pitch giu nguyen do dai, Reverb), nghe thu, luu WAV
// Cach hoat dong: doc samples vao bo nho -> xu ly tren ban sao (co Undo/Redo) -> ghi ra file WAV (Save As / Overwrite). Hieu ung thanh truot: keo = nghe thu, tha chuot = ap dung.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UIE = UnityEngine.UIElements;
using UObject = UnityEngine.Object;

namespace GaMo.BlenderEditorTools
{
    public partial class CustomsEditor
    {
        // =====================================================
        //  Dữ liệu / trạng thái
        // =====================================================
        sealed class AeSnap
        {
            public float[] d;
            public int ch, selA, selB, ver;
        }

        enum AeCurve
        {
            Linear,
            Smooth,
            Quad
        }

        // Thông số hiệu ứng điều khiển bằng thanh trượt
        struct AeFx
        {
            public bool trim, norm;
            public float sil, speed, gain, normDb, fadeIn, fadeOut;
            public AeCurve curve;
            public float bass, mid, treble; // EQ (dB): kệ trầm 200 Hz, chuông 1 kHz, kệ cao 4 kHz
            public bool hpfOn, lpfOn; // Low Cut (high-pass) / High Cut (low-pass)
            public float hpf, lpf; // tần số cắt (Hz)
            public bool reverse; // phát ngược vùng áp dụng
            public float pitch; // Pitch Shift (bán cung): giọng trầm / cao hơn, giữ nguyên độ dài
            public float revMix, revRoom; // Reverb (%): lượng tiếng vang / độ lớn phòng

            public static AeFx Def
            {
                get
                {
                    return new AeFx
                    {
                        sil = -60f, speed = 1f, normDb = -1f, curve = AeCurve.Linear,
                        hpf = 200f, lpf = 6000f, revRoom = 50f
                    };
                }
            }

            public bool EqOn
            {
                get
                {
                    return Mathf.Abs(bass) >= 0.05f || Mathf.Abs(mid) >= 0.05f || Mathf.Abs(treble) >= 0.05f
                           || hpfOn || lpfOn;
                }
            }

            public bool SpeedOn
            {
                get { return Mathf.Abs(speed - 1f) >= 0.001f; }
            }

            public bool PitchOn
            {
                get { return Mathf.Abs(pitch) >= 0.05f; }
            }

            public bool ReverbOn
            {
                get { return revMix >= 0.5f; }
            }

            public bool GainOn
            {
                get { return Mathf.Abs(gain) >= 0.001f; }
            }

            public bool FadeOn
            {
                get { return fadeIn > 0.0005f || fadeOut > 0.0005f; }
            }

            public bool RegionFx // hiệu ứng chỉ tác động lên vùng (Pitch / EQ / Reverse / Reverb / Gain / Normalize / Fade)
            {
                get { return norm || GainOn || FadeOn || EqOn || reverse || PitchOn || ReverbOn; }
            }

            public bool Any
            {
                get { return trim || SpeedOn || RegionFx; }
            }

            public bool Same(AeFx o)
            {
                return trim == o.trim && (!trim || Mathf.Approximately(sil, o.sil))
                       && Mathf.Approximately(speed, o.speed) && Mathf.Approximately(gain, o.gain)
                       && norm == o.norm && (!norm || Mathf.Approximately(normDb, o.normDb))
                       && Mathf.Approximately(fadeIn, o.fadeIn) && Mathf.Approximately(fadeOut, o.fadeOut)
                       && (!FadeOn || curve == o.curve)
                       && Mathf.Approximately(bass, o.bass) && Mathf.Approximately(mid, o.mid)
                       && Mathf.Approximately(treble, o.treble) && reverse == o.reverse
                       && Mathf.Approximately(pitch, o.pitch)
                       && Mathf.Approximately(revMix, o.revMix) && (!ReverbOn || Mathf.Approximately(revRoom, o.revRoom))
                       && hpfOn == o.hpfOn && (!hpfOn || Mathf.Approximately(hpf, o.hpf))
                       && lpfOn == o.lpfOn && (!lpfOn || Mathf.Approximately(lpf, o.lpf));
            }
        }

        const string AeOpenKey = "GaMo.CustomsEditor.AudioEditOpen";
        const long AeMaxSamples = 150000000L; // ~600MB float: quá thì từ chối nạp
        const int AeMaxUndo = 8;
        const float AeWaveH = 120f;
        static readonly int AeWaveHash = "GaMoAudioWave".GetHashCode();
        static readonly string[] AeFmtNames = { "16-bit PCM", "24-bit PCM", "32-bit float" };

        UIE.IMGUIContainer _aeChunk;
        bool _aeOpen, _aeOpenInit;
        string _aePath = "";
        int _aeState; // 0 chưa nạp, 1 đang nạp, 2 sẵn sàng, 3 lỗi
        string _aeErr;
        float[] _aeData; // samples xen kẽ kênh (interleaved), -1..1
        int _aeCh = 1, _aeFreq = 44100;
        int _aeVer, _aeVerCounter, _aeSavedVer;
        int _aeFmt; // 0 = 16-bit, 1 = 24-bit, 2 = float32
        bool _aeBackup = true, _aeIsWav;
        readonly List<AeSnap> _aeUndo = new List<AeSnap>();
        readonly List<AeSnap> _aeRedo = new List<AeSnap>();

        // chọn vùng / con trỏ / zoom (đơn vị: frame)
        int _aeSelA = -1, _aeSelB = -1, _aeCursor;
        int _aeViewStart, _aeViewLen; // _aeViewLen <= 0: xem toàn bộ
        int _aeDrag, _aeAnchor;
        float _aeDragX;

        // cache peaks của waveform
        float[] _aePeaks;
        int _aePkVer = -1, _aePkW, _aePkStart, _aePkLen, _aePkLanes;
        int _aePeakVer = -1;
        float _aePeakVal;

        // Hiệu ứng thanh trượt: _aeFx = giá trị trên thanh trượt, _aeFxBuilt = giá trị đã tính vào _aeData
        AeFx _aeFx = AeFx.Def, _aeFxBuilt = AeFx.Def;
        float[] _aeBase; // dữ liệu gốc (sau các thao tác cắt / xóa...); _aeData = _aeBase + hiệu ứng
        int _aeBaseVer;
        bool _aeFxRegionSet, _aeFxHasRegion; // vùng áp dụng Gain / Normalize / Fade
        double _aeFxFa, _aeFxFb; // vùng đó dưới dạng tỉ lệ 0..1 trên độ dài clip
        float _aeFxBuiltLin = 1f, _aeFxPrePeak = -1f; // hệ số gain đã tính vào _aeData, peak vùng trước gain
        int _aeRpVer = -1, _aeRpA, _aeRpB; // cache peak của vùng
        float _aeRpVal;

        // nghe thử
        bool _aePlaying, _aeLoop;
        AudioClip _aeTmp;
        int _aePlayStart, _aePlayEnd, _aePlayPos;
        double _aePumpAt, _aePlayT0;

        string _aeInfo = "", _aeTimeStr = "";

        int AeFrames
        {
            get { return _aeData == null ? 0 : _aeData.Length / Mathf.Max(1, _aeCh); }
        }

        bool AeHasSel
        {
            get { return _aeSelA >= 0 && _aeSelB > _aeSelA; }
        }

        bool AeDirty
        {
            get { return _aeState == 2 && _aeVer != _aeSavedVer; }
        }

        int AeViewLenNow
        {
            get
            {
                int t = AeFrames;
                return (_aeViewLen <= 0 || _aeViewLen >= t) ? Mathf.Max(1, t) : _aeViewLen;
            }
        }

        // =====================================================
        //  Gắn vào thân Asset (gọi từ BuildAssetBody)
        // =====================================================
        void AddAudioEditSection(UIE.VisualElement cc)
        {
            _aeChunk = null;
            if (!(_asset is AudioClip) || _assets.Length != 1) return;
            string p = AssetDatabase.GetAssetPath(_asset);
            if (string.IsNullOrEmpty(p) || !AssetDatabase.IsMainAsset(_asset)) return;

            var sec = new UIE.VisualElement();
            sec.style.marginBottom = 8;
            _aeChunk = AssetChunk(DrawAudioEditChunk, true);
            sec.Add(_aeChunk);
            cc.Add(sec);
        }

        void AeRepaint()
        {
            if (_aeChunk != null && _aeChunk.panel != null) _aeChunk.MarkDirtyRepaint();
            else if (this != null) Repaint();
        }

        void AeDone()
        {
            AeRepaint();
            GUIUtility.ExitGUI();
        }

        void AeReset(string path)
        {
            AeStopPlayback();
            _aePath = path ?? "";
            _aeData = null;
            _aeUndo.Clear();
            _aeRedo.Clear();
            _aeState = 0;
            _aeErr = null;
            _aeSelA = _aeSelB = -1;
            _aeCursor = 0;
            _aeViewStart = 0;
            _aeViewLen = 0;
            _aeVer = _aeVerCounter = _aeSavedVer = 0;
            _aePkVer = -1;
            _aePeakVer = -1;
            _aePeaks = null;
            _aeBase = null;
            _aeBaseVer = 0;
            AeFxClear();
            _aeRpVer = -1;
        }

        // =====================================================
        //  Vẽ panel
        // =====================================================
        void DrawAudioEditChunk()
        {
            var clip = _asset as AudioClip;
            if (clip == null) return;
            var ev = Event.current;

            if (ev.type == EventType.Layout)
            {
                string cur = AssetDatabase.GetAssetPath(clip);
                if (cur != _aePath) AeReset(cur);
                if (_aeState == 2) AeBuildLabels();
            }

            if (!_aeOpenInit)
            {
                _aeOpenInit = true;
                _aeOpen = EditorPrefs.GetBool(AeOpenKey, false);
            }

            bool open;
            var oldIcon = EditorGUIUtility.GetIconSize();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                open = GUILayout.Toggle(_aeOpen, GUIContent.none, EditorStyles.foldout, GUILayout.Width(14),
                    GUILayout.Height(18));
                EditorGUIUtility.SetIconSize(new Vector2(16, 16));
                GUILayout.Label(
                    new GUIContent(AeDirty ? " Edit Audio *" : " Edit Audio",
                        EditorGUIUtility.IconContent("AudioClip Icon").image),
                    EditorStyles.boldLabel, GUILayout.Height(18));
                EditorGUIUtility.SetIconSize(oldIcon);
                GUILayout.FlexibleSpace();
                if (_aeOpen && _aeState == 2) GUILayout.Label(_aeInfo, EditorStyles.miniLabel);
            }

            if (open != _aeOpen)
            {
                _aeOpen = open;
                EditorPrefs.SetBool(AeOpenKey, open);
                if (!open) AeStopPlayback();
                AeDone();
            }

            if (!_aeOpen) return;

            if (_aeState == 0 && ev.type == EventType.Layout) AeQueueLoad();
            if (_aeState == 0 || _aeState == 1)
            {
                EditorGUILayout.HelpBox("Đang nạp samples…", MessageType.None);
                return;
            }

            if (_aeState == 3)
            {
                EditorGUILayout.HelpBox(_aeErr ?? "Không nạp được audio.", MessageType.Warning);
                if (GUILayout.Button("Thử lại", EditorStyles.miniButton))
                {
                    _aeState = 0;
                    AeDone();
                }

                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.inspectorDefaultMargins))
                AeMainGUI();
        }

        void AeBuildLabels()
        {
            double fr = Math.Max(1, _aeFreq);
            int t = AeFrames;
            _aeInfo = _aeCh + "ch · " + _aeFreq + " Hz · " + (t / fr).ToString("0.000") + "s · peak " + AePeakDb() +
                      " dB";
            if (_aePlaying) _aeTimeStr = "▶ " + (_aePlayPos / fr).ToString("0.000") + "s" + AePrevName();
            else if (AeHasSel)
                _aeTimeStr = (_aeSelA / fr).ToString("0.000") + " – " + (_aeSelB / fr).ToString("0.000")
                             + "s  (" + ((_aeSelB - _aeSelA) / fr).ToString("0.000") + "s)";
            else _aeTimeStr = "Cursor " + (_aeCursor / fr).ToString("0.000") + "s";
        }

        string AePeakDb()
        {
            if (_aePeakVer != _aeVer)
            {
                float p = 0f;
                var d = _aeData;
                if (d != null)
                    for (int i = 0; i < d.Length; i++)
                    {
                        float a = d[i] < 0 ? -d[i] : d[i];
                        if (a > p) p = a;
                    }

                _aePeakVal = p;
                _aePeakVer = _aeVer;
            }

            return _aePeakVal <= 1e-9f ? "-inf" : (20f * Mathf.Log10(_aePeakVal)).ToString("0.0");
        }

        static bool AeBtn(string label, string tip, bool enabled)
        {
            using (new EditorGUI.DisabledScope(!enabled))
                return GUILayout.Button(new GUIContent(label, tip), EditorStyles.miniButton);
        }

        void AeMainGUI()
        {
            float oldLw = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 96f;
            try
            {
                AeMainGUIInner();
            }
            finally
            {
                EditorGUIUtility.labelWidth = oldLw;
            }
        }

        void AeMainGUIInner()
        {
            bool hasSel = AeHasSel;
            bool hasData = AeFrames > 0;

            // Âm thanh Editor đang tắt (nút Mute Audio của Game view): nghe thử sẽ im lặng => báo + nút bật lại ngay tại đây
            if (EditorUtility.audioMasterMute)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox("Âm thanh Editor đang bị Mute (nút Mute Audio ở Game view) nên Play sẽ không nghe thấy gì.",
                        MessageType.Warning);
                    if (GUILayout.Button("Unmute", GUILayout.Width(64), GUILayout.Height(38)))
                        EditorUtility.audioMasterMute = false;
                }
            }

            // ----- Transport -----
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(_aePlaying ? "■ Stop" : "▶ Play", EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    if (_aePlaying) AeStopPlayback();
                    else AePlay();
                    AeDone();
                }

                _aeLoop = GUILayout.Toggle(_aeLoop, new GUIContent("Loop", "Lặp lại vùng chọn / đoạn đang nghe"),
                    EditorStyles.miniButton, GUILayout.Width(40));
                GUILayout.Label(_aeTimeStr, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                bool fxOn = _aeFx.Any || _aeFxBuilt.Any;
                if (AeBtn("Undo",
                        fxOn ? "Đặt lại các thanh trượt hiệu ứng về mặc định" : "Hoàn tác thao tác audio",
                        fxOn || _aeUndo.Count > 0))
                {
                    if (fxOn) AeFxReset();
                    else AeUndoOp();
                    AeDone();
                }

                if (AeBtn("Redo", "Làm lại", _aeRedo.Count > 0))
                {
                    AeRedoOp();
                    AeDone();
                }
            }

            // ----- Waveform -----
            AeWaveGUI();
            if (_aeViewLen > 0)
            {
                var sr = GUILayoutUtility.GetRect(0, 14, GUILayout.ExpandWidth(true));
                float nv = GUI.HorizontalScrollbar(sr, _aeViewStart, _aeViewLen, 0, AeFrames);
                if ((int)nv != _aeViewStart)
                {
                    _aeViewStart = (int)nv;
                    AeClampView();
                    AeRepaint();
                }
            }

            // ----- Zoom / chọn -----
            using (new EditorGUILayout.HorizontalScope())
            {
                if (AeBtn("Fit", "Xem toàn bộ", hasData))
                {
                    AeZoomAll();
                    AeDone();
                }

                if (AeBtn("Zoom Sel", "Phóng to vào vùng chọn", hasSel))
                {
                    AeZoomSel();
                    AeDone();
                }

                if (AeBtn("+", "Phóng to", hasData))
                {
                    AeZoomBy(0.5f);
                    AeDone();
                }

                if (AeBtn("−", "Thu nhỏ", hasData))
                {
                    AeZoomBy(2f);
                    AeDone();
                }

                if (AeBtn("All", "Chọn tất cả (hoặc double-click waveform)", hasData))
                {
                    _aeSelA = 0;
                    _aeSelB = AeFrames;
                    AeDone();
                }

                if (AeBtn("None", "Bỏ chọn", hasSel))
                {
                    _aeSelA = _aeSelB = -1;
                    AeDone();
                }
            }

            GUILayout.Space(4);

            // ----- Cắt / xóa -----
            using (new EditorGUILayout.HorizontalScope())
            {
                if (AeBtn("Trim", "Giữ lại vùng chọn, bỏ phần còn lại", hasSel))
                {
                    AeTrim();
                    AeDone();
                }

                if (AeBtn("Delete", "Xóa vùng chọn (nối 2 đầu lại)", hasSel))
                {
                    AeDelete();
                    AeDone();
                }

                if (AeBtn("Silence", "Biến vùng chọn thành im lặng", hasSel))
                {
                    AeSilence();
                    AeDone();
                }
            }

            // ----- Hiệu ứng: kéo thanh trượt là áp dụng (không có nút Apply) -----
            GUILayout.Space(4);
            double fr = Math.Max(1, _aeFreq);
            int ra, rb;
            AeFxRegionFrames(AeFrames, out ra, out rb);
            bool whole = ra <= 0 && rb >= AeFrames;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent("Hiệu ứng", AeTipFx), EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                string rg = whole ? "cả clip" : ((ra / fr).ToString("0.00") + " – " + (rb / fr).ToString("0.00") + "s");
                GUILayout.Label(new GUIContent("EQ / Reverse / Gain / Fade: " + rg, AeTipRegion),
                    EditorStyles.miniLabel);
            }

            float regSec = hasData ? Mathf.Max(0.01f, (float)((rb - ra) / fr)) : 0.01f;

            // ----- Preset nhanh: Bright / Muffled / Radio / Reverse / Reset Effects -----
            using (new EditorGUI.DisabledScope(!hasData))
            using (new EditorGUILayout.HorizontalScope())
            {
                AeEqPresetBtn("Bright", AeTipBright, AePrBright);
                AeEqPresetBtn("Muffled", AeTipMuffled, AePrMuffled);
                AeEqPresetBtn("Radio", AeTipRadio, AePrRadio);

                bool rvOn = GUILayout.Toggle(_aeFx.reverse, new GUIContent("Reverse", AeTipReverse),
                    EditorStyles.miniButton);
                if (rvOn != _aeFx.reverse)
                {
                    AeFxTouchRegion();
                    _aeFx.reverse = rvOn;
                    AeFxRebuild();
                    AeDone();
                }

                bool anyFx = _aeFx.Any || _aeFxBuilt.Any;
                if (AeBtn("Reset Effects", "Xóa mọi hiệu ứng đang áp dụng (EQ, preset, Reverse, Gain, Fade, Speed...)",
                        anyFx))
                {
                    AeFxReset();
                    AeDone();
                }
            }

            using (new EditorGUI.DisabledScope(!hasData))
            {
                // EQ: Bass / Mid / Treble (áp dụng khi thả chuột)
                EditorGUI.BeginChangeCheck();
                float bass = EditorGUILayout.Slider(new GUIContent("Bass (dB)", AeTipBass), _aeFx.bass, -12f, 12f);
                AeTipOver(AeTipBass);
                if (EditorGUI.EndChangeCheck())
                {
                    _aeFx.bass = Mathf.Round(bass * 10f) / 10f;
                    AeFxChanged(true);
                }

                EditorGUI.BeginChangeCheck();
                float mid = EditorGUILayout.Slider(new GUIContent("Mid (dB)", AeTipMid), _aeFx.mid, -12f, 12f);
                AeTipOver(AeTipMid);
                if (EditorGUI.EndChangeCheck())
                {
                    _aeFx.mid = Mathf.Round(mid * 10f) / 10f;
                    AeFxChanged(true);
                }

                EditorGUI.BeginChangeCheck();
                float treble = EditorGUILayout.Slider(new GUIContent("Treble (dB)", AeTipTreble), _aeFx.treble, -12f,
                    12f);
                AeTipOver(AeTipTreble);
                if (EditorGUI.EndChangeCheck())
                {
                    _aeFx.treble = Mathf.Round(treble * 10f) / 10f;
                    AeFxChanged(true);
                }

                // Low Cut (high-pass) - tick để bật
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    bool hpOn = EditorGUILayout.ToggleLeft(new GUIContent("Low Cut (Hz)", AeTipLowCut), _aeFx.hpfOn,
                        GUILayout.Width(EditorGUIUtility.labelWidth));
                    if (EditorGUI.EndChangeCheck())
                    {
                        _aeFx.hpfOn = hpOn;
                        AeFxChanged(true);
                    }

                    using (new EditorGUI.DisabledScope(!_aeFx.hpfOn))
                    {
                        EditorGUI.BeginChangeCheck();
                        float hp = EditorGUILayout.Slider(_aeFx.hpf, 20f, 2000f);
                        AeTipOver(AeTipLowCut);
                        if (EditorGUI.EndChangeCheck())
                        {
                            _aeFx.hpf = Mathf.Round(hp);
                            AeFxChanged(true);
                        }
                    }
                }

                // High Cut (low-pass) - tick để bật
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    bool lpOn = EditorGUILayout.ToggleLeft(new GUIContent("High Cut (Hz)", AeTipHighCut), _aeFx.lpfOn,
                        GUILayout.Width(EditorGUIUtility.labelWidth));
                    if (EditorGUI.EndChangeCheck())
                    {
                        _aeFx.lpfOn = lpOn;
                        AeFxChanged(true);
                    }

                    using (new EditorGUI.DisabledScope(!_aeFx.lpfOn))
                    {
                        EditorGUI.BeginChangeCheck();
                        float lp = EditorGUILayout.Slider(_aeFx.lpf, 500f, 20000f);
                        AeTipOver(AeTipHighCut);
                        if (EditorGUI.EndChangeCheck())
                        {
                            _aeFx.lpf = Mathf.Round(lp);
                            AeFxChanged(true);
                        }
                    }
                }

                // Fade In
                EditorGUI.BeginChangeCheck();
                float fadeIn = EditorGUILayout.Slider(new GUIContent("Fade In (s)", AeTipFadeIn), _aeFx.fadeIn, 0f,
                    regSec);
                AeTipOver(AeTipFadeIn);
                if (EditorGUI.EndChangeCheck())
                {
                    _aeFx.fadeIn = Mathf.Round(fadeIn * 1000f) / 1000f;
                    AeFxChanged(true);
                }

                // Fade Out
                EditorGUI.BeginChangeCheck();
                float fadeOut = EditorGUILayout.Slider(new GUIContent("Fade Out (s)", AeTipFadeOut), _aeFx.fadeOut,
                    0f, regSec);
                AeTipOver(AeTipFadeOut);
                if (EditorGUI.EndChangeCheck())
                {
                    _aeFx.fadeOut = Mathf.Round(fadeOut * 1000f) / 1000f;
                    AeFxChanged(true);
                }

                // Đường cong fade
                EditorGUI.BeginChangeCheck();
                var curve = (AeCurve)EditorGUILayout.EnumPopup(new GUIContent("Fade Curve", AeTipCurve), _aeFx.curve);
                AeTipOver(AeTipCurve);
                if (EditorGUI.EndChangeCheck())
                {
                    _aeFx.curve = curve;
                    AeFxChanged(true);
                }

                // Gain
                EditorGUI.BeginChangeCheck();
                float gain = EditorGUILayout.Slider(new GUIContent("Gain (dB)", AeTipGain), _aeFx.gain, -36f, 24f);
                AeTipOver(AeTipGain);
                if (EditorGUI.EndChangeCheck())
                {
                    _aeFx.gain = Mathf.Round(gain * 10f) / 10f;
                    AeFxChanged(true);
                }

                // Normalize (tick để bật)
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    bool normOn = EditorGUILayout.ToggleLeft(new GUIContent("Normalize (dB)", AeTipNorm), _aeFx.norm,
                        GUILayout.Width(EditorGUIUtility.labelWidth));
                    if (EditorGUI.EndChangeCheck())
                    {
                        _aeFx.norm = normOn;
                        AeFxChanged(true);
                    }

                    using (new EditorGUI.DisabledScope(!_aeFx.norm))
                    {
                        EditorGUI.BeginChangeCheck();
                        float normDb = EditorGUILayout.Slider(_aeFx.normDb, -24f, 0f);
                        AeTipOver(AeTipNorm);
                        if (EditorGUI.EndChangeCheck())
                        {
                            _aeFx.normDb = Mathf.Round(normDb * 10f) / 10f;
                            AeFxChanged(true);
                        }
                    }
                }

                // Trim Silence (tick để bật)
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    bool trimOn = EditorGUILayout.ToggleLeft(new GUIContent("Trim Silence (dB)", AeTipTrim),
                        _aeFx.trim, GUILayout.Width(EditorGUIUtility.labelWidth));
                    if (EditorGUI.EndChangeCheck())
                    {
                        _aeFx.trim = trimOn;
                        AeFxChanged(false);
                    }

                    using (new EditorGUI.DisabledScope(!_aeFx.trim))
                    {
                        EditorGUI.BeginChangeCheck();
                        float sil = EditorGUILayout.Slider(_aeFx.sil, -90f, -10f);
                        AeTipOver(AeTipTrim);
                        if (EditorGUI.EndChangeCheck())
                        {
                            _aeFx.sil = Mathf.Round(sil);
                            AeFxChanged(false);
                        }
                    }
                }

                // Speed (thanh trượt theo thang log: 0.25x ... 1x ... 4x)
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(new GUIContent("Speed (x)", AeTipSpeed),
                        GUILayout.Width(EditorGUIUtility.labelWidth));
                    EditorGUI.BeginChangeCheck();
                    float l2 = GUILayout.HorizontalSlider(Mathf.Log(Mathf.Clamp(_aeFx.speed, 0.25f, 4f), 2f), -2f, 2f);
                    AeTipOver(AeTipSpeed);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _aeFx.speed = Mathf.Abs(l2) < 0.04f ? 1f : Mathf.Round(Mathf.Pow(2f, l2) * 100f) / 100f;
                        AeFxChanged(false);
                    }

                    EditorGUI.BeginChangeCheck();
                    float spd = EditorGUILayout.DelayedFloatField(Mathf.Clamp(_aeFx.speed, 0.25f, 4f),
                        GUILayout.Width(50));
                    if (EditorGUI.EndChangeCheck())
                    {
                        _aeFx.speed = Mathf.Clamp(Mathf.Round(spd * 100f) / 100f, 0.25f, 4f);
                        AeFxChanged(false);
                    }
                }

                // Pitch (bán cung): giọng trầm / cao hơn mà độ dài giữ nguyên. Kéo gần 0 sẽ tự dính về 0.
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(new GUIContent("Pitch (semitone)", AeTipPitch),
                        GUILayout.Width(EditorGUIUtility.labelWidth));
                    EditorGUI.BeginChangeCheck();
                    float pt = EditorGUILayout.Slider(_aeFx.pitch, -12f, 12f);
                    AeTipOver(AeTipPitch);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _aeFx.pitch = Mathf.Abs(pt) < 0.15f ? 0f : Mathf.Round(pt * 10f) / 10f;
                        AeFxChanged(true);
                    }
                }

                // Reverb: Mix = lượng tiếng vang (0 = tắt), Room = độ lớn phòng (vang ngắn / dài)
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(new GUIContent("Reverb Mix (%)", AeTipReverb),
                        GUILayout.Width(EditorGUIUtility.labelWidth));
                    EditorGUI.BeginChangeCheck();
                    float rm = EditorGUILayout.Slider(_aeFx.revMix, 0f, 100f);
                    AeTipOver(AeTipReverb);
                    if (EditorGUI.EndChangeCheck())
                    {
                        _aeFx.revMix = Mathf.Round(rm);
                        AeFxChanged(true);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(new GUIContent("Reverb Room (%)", AeTipReverb),
                        GUILayout.Width(EditorGUIUtility.labelWidth));
                    using (new EditorGUI.DisabledScope(!_aeFx.ReverbOn))
                    {
                        EditorGUI.BeginChangeCheck();
                        float rr = EditorGUILayout.Slider(_aeFx.revRoom, 0f, 100f);
                        AeTipOver(AeTipReverb);
                        if (EditorGUI.EndChangeCheck())
                        {
                            _aeFx.revRoom = Mathf.Round(rr);
                            AeFxChanged(true);
                        }
                    }
                }
            }

            // ----- Mono / Reset / Chốt -----
            using (new EditorGUILayout.HorizontalScope())
            {
                if (AeBtn("Mono", "Trộn các kênh thành mono", _aeCh > 1))
                {
                    AeMono();
                    AeDone();
                }

                GUILayout.FlexibleSpace();
                bool fxAny = _aeFx.Any || _aeFxBuilt.Any;
                if (AeBtn("Chốt", "Giữ vĩnh viễn các hiệu ứng đang áp dụng (để chỉnh tiếp vùng khác). Có thể Undo.",
                        fxAny))
                {
                    AeFxBake();
                    AeDone();
                }
            }

            GUILayout.Space(6);

            // ----- Lưu -----
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent("Format", AeTipFmt), GUILayout.Width(44));
                _aeFmt = Mathf.Clamp(EditorGUILayout.Popup(_aeFmt, AeFmtNames, GUILayout.Width(110)), 0, 2);
                AeTipOver(AeTipFmt);
                GUILayout.Label(new GUIContent(AeFmtSizeLabel(), "Dung lượng file WAV ước tính khi Save As / Overwrite"),
                    EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                _aeBackup = GUILayout.Toggle(_aeBackup,
                    new GUIContent("Backup", "Khi ghi đè: lưu bản sao file gốc thành .bak~ (Unity bỏ qua file đuôi ~)"),
                    GUILayout.Width(62));
            }

            bool canOverwrite = _aeIsWav && !_assetImmutable &&
                                !_aePath.StartsWith("Packages/", StringComparison.Ordinal);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (AeBtn("Save As…", "Lưu cả clip thành file WAV mới", hasData)) AeQueueSaveAs(false);
                if (AeBtn("Export Sel…", "Chỉ lưu vùng chọn thành file WAV mới", hasSel)) AeQueueSaveAs(true);
                if (AeBtn("Overwrite",
                        canOverwrite ? "Ghi đè file WAV gốc" : "Chỉ ghi đè được file .wav ngoài Packages",
                        canOverwrite && hasData)) AeQueueOverwrite();
            }

            if (!_aeIsWav)
                GUILayout.Label("File gốc không phải WAV: chỉ lưu được bản mới (Save As).", EditorStyles.miniLabel);

            AeFxCommitIfIdle();
        }

        // =====================================================
        //  Waveform
        // =====================================================
        void AeClampView()
        {
            int t = AeFrames;
            if (_aeViewLen <= 0 || _aeViewLen >= t)
            {
                _aeViewLen = 0;
                _aeViewStart = 0;
                return;
            }

            _aeViewLen = Mathf.Max(32, _aeViewLen);
            _aeViewStart = Mathf.Clamp(_aeViewStart, 0, Mathf.Max(0, t - _aeViewLen));
        }

        int AeXToFrame(Rect r, float x)
        {
            double t = (x - r.x) / Math.Max(1f, r.width);
            int f = _aeViewStart + (int)Math.Round(t * AeViewLenNow);
            return Mathf.Clamp(f, 0, AeFrames);
        }

        float AeFrameToX(Rect r, int f)
        {
            return r.x + (float)((f - _aeViewStart) / (double)AeViewLenNow) * r.width;
        }

        void AeWaveGUI()
        {
            var ev = Event.current;
            var r = GUILayoutUtility.GetRect(0, AeWaveH, GUILayout.ExpandWidth(true));
            int id = GUIUtility.GetControlID(AeWaveHash, FocusType.Passive, r);
            int total = AeFrames;

            switch (ev.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                {
                    if (ev.button != 0 || total <= 0 || !r.Contains(ev.mousePosition)) break;
                    GUIUtility.hotControl = id;
                    GUI.FocusControl(null);
                    if (ev.clickCount >= 2)
                    {
                        _aeSelA = 0;
                        _aeSelB = total;
                        _aeDrag = 0;
                    }
                    else
                    {
                        float mx = ev.mousePosition.x;
                        _aeDrag = 1;
                        if (AeHasSel)
                        {
                            if (Mathf.Abs(mx - AeFrameToX(r, _aeSelA)) < 6f)
                            {
                                _aeDrag = 2;
                                _aeAnchor = _aeSelB;
                            }
                            else if (Mathf.Abs(mx - AeFrameToX(r, _aeSelB)) < 6f)
                            {
                                _aeDrag = 2;
                                _aeAnchor = _aeSelA;
                            }
                        }

                        if (_aeDrag == 1)
                        {
                            _aeAnchor = AeXToFrame(r, mx);
                            _aeDragX = mx;
                            _aeCursor = _aeAnchor;
                            _aeSelA = _aeSelB = -1; // click không kéo = bỏ chọn
                        }
                    }

                    ev.Use();
                    AeRepaint();
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (GUIUtility.hotControl != id) break;
                    if (_aeDrag != 0)
                    {
                        int f = AeXToFrame(r, ev.mousePosition.x);
                        if (_aeDrag == 2 || Mathf.Abs(ev.mousePosition.x - _aeDragX) > 3f)
                        {
                            _aeSelA = Mathf.Min(_aeAnchor, f);
                            _aeSelB = Mathf.Max(_aeAnchor, f);
                        }
                    }

                    ev.Use();
                    AeRepaint();
                    break;
                }
                case EventType.MouseUp:
                {
                    if (GUIUtility.hotControl != id) break;
                    GUIUtility.hotControl = 0;
                    if (_aeSelB <= _aeSelA) _aeSelA = _aeSelB = -1;
                    _aeDrag = 0;
                    ev.Use();
                    AeRepaint();
                    break;
                }
                case EventType.Repaint:
                    AePaintWave(r);
                    break;
            }
        }

        void AeEnsurePeaks(int w, int lanes)
        {
            int total = AeFrames, vs = _aeViewStart, vl = AeViewLenNow;
            if (_aePeaks != null && _aePkVer == _aeVer && _aePkW == w && _aePkStart == vs && _aePkLen == vl &&
                _aePkLanes == lanes) return;
            int need = lanes * w * 2;
            if (_aePeaks == null || _aePeaks.Length != need) _aePeaks = new float[need];
            var d = _aeData;
            int ch = _aeCh;
            double fpc = vl / (double)w;
            for (int l = 0; l < lanes; l++)
            for (int x = 0; x < w; x++)
            {
                int f0 = vs + (int)(x * fpc);
                int f1 = vs + (int)((x + 1) * fpc);
                if (f1 <= f0) f1 = f0 + 1;
                if (f1 > total) f1 = total;
                float mn = 0f, mx = 0f;
                if (f0 < total)
                {
                    mn = float.MaxValue;
                    mx = float.MinValue;
                    int stride = Math.Max(1, (f1 - f0) / 256);
                    for (int f = f0; f < f1; f += stride)
                    {
                        float v = d[f * ch + l];
                        if (v < mn) mn = v;
                        if (v > mx) mx = v;
                    }
                }

                int i = (l * w + x) * 2;
                _aePeaks[i] = mn;
                _aePeaks[i + 1] = mx;
            }

            _aePkVer = _aeVer;
            _aePkW = w;
            _aePkStart = vs;
            _aePkLen = vl;
            _aePkLanes = lanes;
        }

        void AePaintWave(Rect r)
        {
            EditorGUI.DrawRect(r, new Color(0.11f, 0.11f, 0.11f));
            int total = AeFrames;
            if (total <= 0 || _aeData == null) return;

            int w = Mathf.Max(1, (int)r.width);
            int lanes = Mathf.Min(_aeCh, 2);
            AeEnsurePeaks(w, lanes);
            float laneH = r.height / lanes;

            for (int l = 0; l < lanes; l++)
            {
                float cy = r.y + laneH * (l + 0.5f);
                float half = laneH * 0.5f - 2f;
                EditorGUI.DrawRect(new Rect(r.x, cy, r.width, 1f), new Color(1f, 1f, 1f, 0.12f));
                if (l > 0) EditorGUI.DrawRect(new Rect(r.x, r.y + laneH * l, r.width, 1f), new Color(0f, 0f, 0f, 0.6f));
                int baseI = l * w * 2;
                for (int x = 0; x < w; x++)
                {
                    float mn = _aePeaks[baseI + x * 2], mx = _aePeaks[baseI + x * 2 + 1];
                    float y0 = cy - Mathf.Clamp(mx, -1f, 1f) * half;
                    float y1 = cy - Mathf.Clamp(mn, -1f, 1f) * half;
                    if (y1 - y0 < 1f) y1 = y0 + 1f;
                    EditorGUI.DrawRect(new Rect(r.x + x, y0, 1f, y1 - y0), Accent);
                }
            }

            if (AeHasSel)
            {
                float xa = AeFrameToX(r, _aeSelA), xb = AeFrameToX(r, _aeSelB);
                float ca = Mathf.Clamp(xa, r.x, r.xMax), cb = Mathf.Clamp(xb, r.x, r.xMax);
                if (cb > ca) EditorGUI.DrawRect(new Rect(ca, r.y, cb - ca, r.height), new Color(1f, 1f, 1f, 0.18f));
                var edge = new Color(1f, 1f, 1f, 0.9f);
                if (xa >= r.x && xa <= r.xMax)
                {
                    EditorGUI.DrawRect(new Rect(xa, r.y, 2f, r.height), edge);
                    EditorGUIUtility.AddCursorRect(new Rect(xa - 4f, r.y, 8f, r.height), MouseCursor.ResizeHorizontal);
                }

                if (xb >= r.x && xb <= r.xMax)
                {
                    EditorGUI.DrawRect(new Rect(xb - 1f, r.y, 2f, r.height), edge);
                    EditorGUIUtility.AddCursorRect(new Rect(xb - 4f, r.y, 8f, r.height), MouseCursor.ResizeHorizontal);
                }
            }
            else
            {
                float cx = AeFrameToX(r, _aeCursor);
                if (cx >= r.x && cx <= r.xMax)
                    EditorGUI.DrawRect(new Rect(cx, r.y, 1f, r.height), new Color(1f, 1f, 1f, 0.9f));
            }

            if (_aePlaying)
            {
                float px = AeFrameToX(r, _aePlayPos);
                if (px >= r.x && px <= r.xMax)
                    EditorGUI.DrawRect(new Rect(px, r.y, 2f, r.height), new Color(0.35f, 1f, 0.45f));
            }

            AePaintFxOverlay(r);
        }

        void AeZoomAll()
        {
            _aeViewStart = 0;
            _aeViewLen = 0;
        }

        void AeZoomSel()
        {
            if (!AeHasSel) return;
            _aeViewStart = _aeSelA;
            _aeViewLen = _aeSelB - _aeSelA;
            AeClampView();
        }

        void AeZoomBy(float factor)
        {
            int t = AeFrames;
            if (t < 64) return;
            int len = AeViewLenNow;
            int newLen = Mathf.Clamp((int)(len * factor), 32, t);
            int center = (_aeCursor >= _aeViewStart && _aeCursor < _aeViewStart + len)
                ? _aeCursor
                : _aeViewStart + len / 2;
            _aeViewStart = center - newLen / 2;
            _aeViewLen = newLen;
            AeClampView();
        }

        // =====================================================
        //  Thao tác xử lý audio (mỗi thao tác tạo bản mới + đẩy vào Undo)
        // =====================================================
        void AeRange(out int a, out int b)
        {
            if (AeHasSel)
            {
                a = Mathf.Clamp(_aeSelA, 0, AeFrames);
                b = Mathf.Clamp(_aeSelB, 0, AeFrames);
            }
            else
            {
                a = 0;
                b = AeFrames;
            }
        }

        void AeCommit(float[] nd, int nch, int selA, int selB, int cursor)
        {
            AeStopPlayback();
            _aeUndo.Add(new AeSnap { d = _aeData, ch = _aeCh, selA = _aeSelA, selB = _aeSelB, ver = _aeVer });
            while (_aeUndo.Count > AeMaxUndo) _aeUndo.RemoveAt(0);
            _aeRedo.Clear();
            _aeData = nd;
            _aeBase = nd; // thao tác cắt / xóa... là thao tác "chốt": hiệu ứng thanh trượt (nếu có) đã nằm trong nd
            _aeCh = nch;
            _aeVer = ++_aeVerCounter;
            _aeBaseVer = _aeVer;
            AeFxClear(); // thanh trượt về mặc định
            int t = AeFrames;
            _aeSelA = selA;
            _aeSelB = selB;
            if (_aeSelB > t) _aeSelB = t;
            if (_aeSelB <= _aeSelA) _aeSelA = _aeSelB = -1;
            _aeCursor = Mathf.Clamp(cursor, 0, t);
            AeClampView();
        }

        void AeRestore(AeSnap s)
        {
            AeStopPlayback();
            _aeData = s.d;
            _aeBase = s.d;
            _aeCh = s.ch;
            _aeSelA = s.selA;
            _aeSelB = s.selB;
            _aeVer = s.ver;
            _aeBaseVer = s.ver;
            AeFxClear();
            _aeCursor = Mathf.Clamp(_aeCursor, 0, AeFrames);
            AeClampView();
        }

        void AeUndoOp()
        {
            if (_aeUndo.Count == 0) return;
            var s = _aeUndo[_aeUndo.Count - 1];
            _aeUndo.RemoveAt(_aeUndo.Count - 1);
            _aeRedo.Add(new AeSnap { d = _aeData, ch = _aeCh, selA = _aeSelA, selB = _aeSelB, ver = _aeVer });
            while (_aeRedo.Count > AeMaxUndo) _aeRedo.RemoveAt(0);
            AeRestore(s);
        }

        void AeRedoOp()
        {
            if (_aeRedo.Count == 0) return;
            var s = _aeRedo[_aeRedo.Count - 1];
            _aeRedo.RemoveAt(_aeRedo.Count - 1);
            _aeUndo.Add(new AeSnap { d = _aeData, ch = _aeCh, selA = _aeSelA, selB = _aeSelB, ver = _aeVer });
            AeRestore(s);
        }

        float AeRangePeak(int a, int b)
        {
            if (_aeRpVer != _aeVer || _aeRpA != a || _aeRpB != b)
            {
                float pk = 0f;
                var d = _aeData;
                for (int i = a * _aeCh; i < b * _aeCh; i++)
                {
                    float v = d[i] < 0 ? -d[i] : d[i];
                    if (v > pk) pk = v;
                }

                _aeRpVal = pk;
                _aeRpVer = _aeVer;
                _aeRpA = a;
                _aeRpB = b;
            }

            return _aeRpVal;
        }

        // =====================================================
        //  Hiệu ứng bằng thanh trượt (không có nút Apply)
        //  _aeData = _aeBase -> Trim Silence -> Speed -> Pitch -> EQ -> Reverse -> Reverb -> Normalize -> Gain -> Fade
        //  Kéo thanh trượt: nghe thử ngay khi đang Play. Thả chuột: tính lại _aeData (không đẩy Undo, có Reset / Chốt).
        // =====================================================
        const string AeTipFx =
            "Kéo thanh trượt để chỉnh: đang Play thì nghe thay đổi ngay, thả chuột là áp dụng vào audio (không cần nút Apply).\n" +
            "Thứ tự xử lý: Trim Silence → Speed → Pitch → EQ → Reverse → Reverb → Normalize → Gain → Fade.\n" +
            "Pitch / EQ / preset / Reverse / Reverb được tính khi thả chuột (đang Play sẽ nghe ngay sau khi thả).\n" +
            "Reset Effects: xóa mọi hiệu ứng đang áp dụng.  Chốt: giữ vĩnh viễn kết quả hiện tại để chỉnh tiếp (có thể Undo).";

        const string AeTipRegion =
            "Pitch, EQ, Reverse, Reverb, Gain, Normalize và Fade chỉ tác động lên vùng này: vùng chọn tại lúc bắt đầu kéo thanh trượt (không chọn = cả clip).\n" +
            "Muốn đổi vùng: bấm Reset Effects hoặc Chốt rồi chọn lại vùng khác.";

        const string AeTipGain =
            "Gain: tăng / giảm âm lượng theo dB.\n" +
            "• 0 dB = giữ nguyên\n" +
            "• +6 dB ≈ gấp đôi biên độ, -6 dB ≈ còn một nửa\n" +
            "• ±3 dB là mức thay đổi vừa đủ nghe rõ\n" +
            "Tăng quá cao làm đỉnh vượt 0 dBFS → bị méo (clipping) khi lưu 16 / 24-bit.";

        const string AeTipNorm =
            "Normalize: tự khuếch đại để đỉnh (peak) lớn nhất của vùng đạt đúng mức dB đã chọn.\n" +
            "• -1 dB: to nhất mà vẫn chừa chút headroom (hay dùng)\n" +
            "• -3 / -6 dB: chừa nhiều headroom hơn để trộn tiếp\n" +
            "Chỉ đổi âm lượng chung, không nén dynamics. Tick ô để bật; Gain được cộng thêm sau Normalize.";

        const string AeTipTrim =
            "Trim Silence: tự cắt khoảng lặng ở ĐẦU và CUỐI clip. Đoạn có biên độ dưới ngưỡng dB được coi là im lặng.\n" +
            "• -60 dB: chỉ cắt phần lặng thật sự\n" +
            "• -40 dB: cắt mạnh tay hơn, có thể ăn mất đuôi vang nhỏ / hơi thở\n" +
            "• -80..-90 dB: gần như chỉ cắt phần hoàn toàn bằng 0\n" +
            "Tick ô để bật. Không đụng đến khoảng lặng ở giữa clip.";

        const string AeTipSpeed =
            "Speed: đổi tốc độ bằng cách resample (giống tua băng).\n" +
            "• >1x: nhanh hơn, cao độ cao hơn, clip ngắn lại\n" +
            "• <1x: chậm hơn, cao độ thấp hơn, clip dài ra\n" +
            "• Phạm vi 0.25x – 4x, kéo gần 1x sẽ tự dính về đúng 1x\n" +
            "Nghe thử khi Play chỉ tới 3x (giới hạn của Unity), kết quả sau khi thả chuột thì đủ 4x.";

        const string AeTipPitch =
            "Pitch: đổi cao độ theo bán cung (semitone) mà KHÔNG đổi độ dài / tốc độ (khác Speed).\n" +
            "• Âm: giọng trầm hơn (-12 = thấp 1 quãng tám)\n" +
            "• Dương: giọng cao hơn (+12 = cao 1 quãng tám)\n" +
            "• ±2..±4: đổi giọng nhân vật nhẹ nhàng; càng xa 0 càng dễ nghe giống giọng máy\n" +
            "Chỉ tác động lên vùng áp dụng. Áp dụng khi thả chuột (không nghe thử khi đang kéo).";

        const string AeTipReverb =
            "Reverb: thêm tiếng vang như đang ở trong phòng / hang / hội trường.\n" +
            "• Mix: lượng tiếng vang trộn vào (0 = tắt). 15–30%: vang tự nhiên; 50%+: rất xa, như trong hang\n" +
            "• Room: độ lớn phòng. Thấp = phòng nhỏ, vang ngắn; cao = hội trường lớn, vang dài\n" +
            "Đuôi vang bị cắt ở cuối vùng áp dụng (có giảm dần ngắn để không nghe tiếng tách): muốn nghe hết đuôi vang " +
            "thì clip cần có khoảng lặng ở cuối. Mix cao có thể làm đỉnh vượt 0 dBFS → dùng Normalize.\n" +
            "Chỉ tác động lên vùng áp dụng. Áp dụng khi thả chuột.";

        const string AeTipFadeIn =
            "Fade In: âm lượng tăng dần từ im lặng lên bình thường trong khoảng thời gian (giây) chọn ở đây, " +
            "tính từ ĐẦU vùng áp dụng (đầu vùng chọn hoặc đầu clip).\n0 = tắt.";

        const string AeTipFadeOut =
            "Fade Out: âm lượng giảm dần về im lặng trong khoảng thời gian (giây) chọn ở đây, " +
            "kết thúc ở CUỐI vùng áp dụng (cuối vùng chọn hoặc cuối clip).\n0 = tắt.";

        const string AeTipCurve =
            "Dạng đường cong của Fade In / Fade Out:\n" +
            "• Linear: tăng / giảm đều\n" +
            "• Smooth: đường cong chữ S, mượt ở cả hai đầu\n" +
            "• Quad: bình phương – Fade In tăng chậm lúc đầu rồi nhanh dần, Fade Out giảm nhanh lúc đầu rồi kéo đuôi dài";

        const string AeTipBass =
            "Bass: tăng / giảm dải trầm (kệ thấp, dưới ~200 Hz).\n" +
            "• +3..+6 dB: dày, ấm, nhiều lực hơn (tiếng nổ, bước chân)\n" +
            "• -3..-6 dB: bớt ù, bớt bùng\n" +
            "Áp dụng khi thả chuột. Tăng nhiều có thể làm đỉnh vượt 0 dBFS → dùng Gain / Normalize để hạ lại.";

        const string AeTipMid =
            "Mid: tăng / giảm dải trung (~1 kHz, dải của giọng nói).\n" +
            "• +: rõ tiếng, 'nằm phía trước' hơn\n" +
            "• -: lùi ra sau, bớt gắt ở dải trung\n" +
            "Áp dụng khi thả chuột.";

        const string AeTipTreble =
            "Treble: tăng / giảm dải cao (kệ cao, trên ~4 kHz).\n" +
            "• +: sáng, nhiều chi tiết, 'lanh lảnh'\n" +
            "• -: dịu, đỡ chói, bớt tiếng xì\n" +
            "Áp dụng khi thả chuột.";

        const string AeTipLowCut =
            "Low Cut (high-pass): cắt bỏ phần trầm DƯỚI tần số này (24 dB/octave).\n" +
            "Dùng để bỏ tiếng ù, rumble, gió. 80–120 Hz cho giọng nói, 300–500 Hz cho âm bộ đàm / điện thoại.\n" +
            "Tick ô để bật.";

        const string AeTipHighCut =
            "High Cut (low-pass): cắt bỏ phần cao TRÊN tần số này (24 dB/octave).\n" +
            "Càng thấp càng 'bị bịt kín' (1–2 kHz: sau bức tường; 3–4 kHz: bộ đàm / điện thoại).\n" +
            "Tick ô để bật.";

        const string AeTipBright =
            "Bright: âm thanh sáng hơn (Treble +7 dB, Bass -2 dB). Bấm lần nữa để tắt.";

        const string AeTipMuffled =
            "Muffled: âm thanh bị bịt kín (Treble -12 dB, High Cut 1800 Hz). Bấm lần nữa để tắt.";

        const string AeTipRadio =
            "Radio: chất âm bộ đàm (Low Cut 400 Hz + High Cut 3400 Hz, Mid +5 dB). Bấm lần nữa để tắt.";

        const string AeTipReverse =
            "Reverse: phát ngược vùng áp dụng (không chọn = cả clip). Bật / tắt được như các hiệu ứng khác.\n" +
            "Khác với Delete / Trim, đây là hiệu ứng nên Reset Effects sẽ trả về như cũ.";

        // ---- Preset EQ ----
        struct AeEqPreset
        {
            public float bass, mid, treble, hpf, lpf; // hpf / lpf = 0: không cắt
        }

        static readonly AeEqPreset AePrBright = new AeEqPreset { bass = -2f, treble = 7f };
        static readonly AeEqPreset AePrMuffled = new AeEqPreset { treble = -12f, lpf = 1800f };
        static readonly AeEqPreset AePrRadio = new AeEqPreset { mid = 5f, hpf = 400f, lpf = 3400f };

        bool AeEqIs(AeEqPreset p)
        {
            var f = _aeFx;
            return Mathf.Approximately(f.bass, p.bass) && Mathf.Approximately(f.mid, p.mid)
                                                       && Mathf.Approximately(f.treble, p.treble)
                                                       && f.hpfOn == (p.hpf > 0f) &&
                                                       (!f.hpfOn || Mathf.Approximately(f.hpf, p.hpf))
                                                       && f.lpfOn == (p.lpf > 0f) &&
                                                       (!f.lpfOn || Mathf.Approximately(f.lpf, p.lpf));
        }

        void AeEqSet(AeEqPreset p)
        {
            _aeFx.bass = p.bass;
            _aeFx.mid = p.mid;
            _aeFx.treble = p.treble;
            _aeFx.hpfOn = p.hpf > 0f;
            _aeFx.lpfOn = p.lpf > 0f;
            if (p.hpf > 0f) _aeFx.hpf = p.hpf;
            if (p.lpf > 0f) _aeFx.lpf = p.lpf;
        }

        // Nút preset dạng toggle: sáng = đang áp dụng; bấm lại = tắt (đưa EQ về phẳng)
        void AeEqPresetBtn(string label, string tip, AeEqPreset p)
        {
            bool on = AeEqIs(p);
            bool nv = GUILayout.Toggle(on, new GUIContent(label, tip), EditorStyles.miniButton);
            if (nv == on) return;
            AeFxTouchRegion();
            AeEqSet(nv ? p : default(AeEqPreset));
            AeFxRebuild();
            AeDone();
        }

        // ---- Bộ lọc biquad (RBJ Audio EQ Cookbook) ----
        struct AeBiq
        {
            public double b0, b1, b2, a1, a2;
        }

        static AeBiq AeBiqNorm(double b0, double b1, double b2, double a0, double a1, double a2)
        {
            return new AeBiq { b0 = b0 / a0, b1 = b1 / a0, b2 = b2 / a0, a1 = a1 / a0, a2 = a2 / a0 };
        }

        static AeBiq AeBiqLowShelf(double fs, double f0, double db)
        {
            double A = Math.Pow(10.0, db / 40.0), w = 2.0 * Math.PI * f0 / fs;
            double cs = Math.Cos(w), sn = Math.Sin(w), al = sn / Math.Sqrt(2.0), sa = 2.0 * Math.Sqrt(A) * al;
            return AeBiqNorm(A * ((A + 1) - (A - 1) * cs + sa), 2 * A * ((A - 1) - (A + 1) * cs),
                A * ((A + 1) - (A - 1) * cs - sa),
                (A + 1) + (A - 1) * cs + sa, -2 * ((A - 1) + (A + 1) * cs), (A + 1) + (A - 1) * cs - sa);
        }

        static AeBiq AeBiqHighShelf(double fs, double f0, double db)
        {
            double A = Math.Pow(10.0, db / 40.0), w = 2.0 * Math.PI * f0 / fs;
            double cs = Math.Cos(w), sn = Math.Sin(w), al = sn / Math.Sqrt(2.0), sa = 2.0 * Math.Sqrt(A) * al;
            return AeBiqNorm(A * ((A + 1) + (A - 1) * cs + sa), -2 * A * ((A - 1) + (A + 1) * cs),
                A * ((A + 1) + (A - 1) * cs - sa),
                (A + 1) - (A - 1) * cs + sa, 2 * ((A - 1) - (A + 1) * cs), (A + 1) - (A - 1) * cs - sa);
        }

        static AeBiq AeBiqPeak(double fs, double f0, double q, double db)
        {
            double A = Math.Pow(10.0, db / 40.0), w = 2.0 * Math.PI * f0 / fs;
            double cs = Math.Cos(w), al = Math.Sin(w) / (2.0 * q);
            return AeBiqNorm(1 + al * A, -2 * cs, 1 - al * A, 1 + al / A, -2 * cs, 1 - al / A);
        }

        static AeBiq AeBiqLowPass(double fs, double f0, double q)
        {
            double w = 2.0 * Math.PI * f0 / fs, cs = Math.Cos(w), al = Math.Sin(w) / (2.0 * q);
            return AeBiqNorm((1 - cs) / 2, 1 - cs, (1 - cs) / 2, 1 + al, -2 * cs, 1 - al);
        }

        static AeBiq AeBiqHighPass(double fs, double f0, double q)
        {
            double w = 2.0 * Math.PI * f0 / fs, cs = Math.Cos(w), al = Math.Sin(w) / (2.0 * q);
            return AeBiqNorm((1 + cs) / 2, -(1 + cs), (1 + cs) / 2, 1 + al, -2 * cs, 1 - al);
        }

        // Áp EQ lên frame [a, b) của d (sửa tại chỗ). Bộ lọc chạy riêng từng kênh.
        static void AeApplyEq(float[] d, int ch, int freq, int a, int b, AeFx fx)
        {
            double fs = Math.Max(8000, freq), maxF = fs * 0.45;
            var fl = new List<AeBiq>(8);
            if (fx.hpfOn)
            {
                double f = Math.Min(Math.Max(fx.hpf, 10.0), maxF);
                fl.Add(AeBiqHighPass(fs, f, 0.7071));
                fl.Add(AeBiqHighPass(fs, f, 0.7071));
            }

            if (fx.lpfOn)
            {
                double f = Math.Min(Math.Max(fx.lpf, 100.0), maxF);
                fl.Add(AeBiqLowPass(fs, f, 0.7071));
                fl.Add(AeBiqLowPass(fs, f, 0.7071));
            }

            if (Mathf.Abs(fx.bass) >= 0.05f) fl.Add(AeBiqLowShelf(fs, Math.Min(200.0, maxF), fx.bass));
            if (Mathf.Abs(fx.mid) >= 0.05f) fl.Add(AeBiqPeak(fs, Math.Min(1000.0, maxF), 0.7, fx.mid));
            if (Mathf.Abs(fx.treble) >= 0.05f) fl.Add(AeBiqHighShelf(fs, Math.Min(4000.0, maxF), fx.treble));

            int n = fl.Count;
            if (n == 0 || b <= a) return;
            var bq = fl.ToArray();
            var z1 = new double[n];
            var z2 = new double[n];
            for (int c = 0; c < ch; c++)
            {
                Array.Clear(z1, 0, n);
                Array.Clear(z2, 0, n);
                for (int i = a; i < b; i++)
                {
                    int idx = i * ch + c;
                    double x = d[idx];
                    for (int k = 0; k < n; k++)
                    {
                        double y = bq[k].b0 * x + z1[k];
                        z1[k] = bq[k].b1 * x - bq[k].a1 * y + z2[k];
                        z2[k] = bq[k].b2 * x - bq[k].a2 * y;
                        x = y;
                    }

                    d[idx] = (float)x;
                }
            }
        }

        // Pitch Shift giữ nguyên độ dài cho frame [a, b) của d (sửa tại chỗ):
        // 1) kéo giãn thời gian bằng WSOLA (cao độ giữ nguyên) theo hệ số r = 2^(semi/12) => dài r lần
        // 2) resample lại đúng độ dài cũ (đọc nhanh gấp r) => cao độ nhân r.
        // Vị trí ghép tính trên bản trộn mono rồi dùng chung cho mọi kênh => stereo không lệch pha.
        static void AePitchShift(float[] d, int ch, int freq, int a, int b, float semis)
        {
            int total = d.Length / ch, L = b - a;
            double r = Math.Pow(2.0, semis / 12.0);
            int N = Mathf.Clamp(Mathf.RoundToInt(freq * 0.04f), 256, 8192) & ~1; // khung ~40 ms
            if (L < N * 2 || Math.Abs(r - 1.0) < 1e-4) return;
            int hs = N / 2;            // bước tổng hợp: Hann chồng 50% => tổng cửa sổ = 1
            double ha = hs / r;        // bước phân tích
            int tol = N / 4;           // biên độ dò vị trí ghép quanh vị trí danh nghĩa
            int outLen = (int)Math.Ceiling(L * r) + 2;
            int K = outLen / hs + 2;

            var m = new float[L];      // bản trộn mono của vùng (chỉ dùng để dò vị trí ghép)
            for (int i = 0; i < L; i++)
            {
                int bi = (a + i) * ch;
                float s = 0f;
                for (int c = 0; c < ch; c++) s += d[bi + c];
                m[i] = s / ch;
            }

            // WSOLA: khung k ghép ở đầu ra k*hs; lấy từ đầu vào quanh k*ha, chọn chỗ giống nhất với "phần nối tự nhiên" của khung trước
            var pos = new int[K];
            int prev = 0;
            for (int k = 1; k < K; k++)
            {
                int nominal = (int)Math.Round(k * ha);
                int target = prev + hs;
                int best = Mathf.Clamp(nominal, 0, L - 1);
                double bestC = double.NegativeInfinity;
                if (target + hs <= L)
                {
                    for (int dl = Math.Max(0, nominal - tol); dl <= nominal + tol && dl + hs <= L; dl += 2)
                    {
                        double cc = 0, e = 1e-12;
                        for (int j = 0; j < hs; j += 4)
                        {
                            float x = m[dl + j];
                            cc += x * m[target + j];
                            e += x * x;
                        }
                        cc /= Math.Sqrt(e);
                        if (cc > bestC) { bestC = cc; best = dl; }
                    }
                }
                pos[k] = best;
                prev = best;
            }

            var win = new float[N];
            for (int j = 0; j < N; j++) win[j] = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * j / N);

            // Đọc quá cuối vùng thì lấy mẫu thật phía sau (nếu còn) để đuôi vùng không bị nhỏ dần
            int srcLen = L + N + 1;
            var src = new float[srcLen];
            var y = new float[outLen];
            for (int c = 0; c < ch; c++)
            {
                for (int i = 0; i < srcLen; i++)
                {
                    int f = a + i;
                    src[i] = f < total ? d[f * ch + c] : 0f;
                }

                Array.Clear(y, 0, outLen);
                for (int k = 0; k < K; k++)
                {
                    int o = k * hs, p = pos[k];
                    for (int j = 0; j < N; j++)
                    {
                        int oi = o + j;
                        if (oi >= outLen) break;
                        int pi = p + j;
                        if (pi >= srcLen) break;
                        float w = (k == 0 && j < hs) ? 1f : win[j]; // khung đầu: không có khung trước để chồng => không fade-in
                        y[oi] += src[pi] * w;
                    }
                }

                for (int i = 0; i < L; i++)
                {
                    double t = i * r;
                    int i0 = Math.Min((int)t, outLen - 1);
                    int i1 = Math.Min(i0 + 1, outLen - 1);
                    float fr = (float)(t - i0);
                    d[(a + i) * ch + c] = y[i0] + (y[i1] - y[i0]) * fr;
                }
            }
        }

        // Reverb kiểu Freeverb (Schroeder/Moorer): 8 comb lọc thấp song song + 4 all-pass nối tiếp.
        // Độ dài delay theo chuẩn 44.1 kHz rồi quy đổi theo tần số mẫu; mỗi kênh lệch thêm 23 mẫu để stereo rộng.
        static readonly int[] AeCombTune = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
        static readonly int[] AeApTune = { 556, 441, 341, 225 };

        // Áp Reverb lên frame [a, b) của d (sửa tại chỗ): out = dry * (1 - mix) + wet * mix. Đuôi vang dừng ở b (giảm dần 20 ms).
        static void AeApplyReverb(float[] d, int ch, int freq, int a, int b, float mix, float room)
        {
            int L = b - a;
            if (L <= 0 || mix <= 0f) return;
            mix = Mathf.Clamp01(mix);
            double sc = freq / 44100.0;
            float fb = 0.7f + 0.28f * Mathf.Clamp01(room); // độ dài đuôi vang
            const float damp = 0.2f;                       // lọc thấp trong vòng hồi tiếp: vang càng về sau càng tối
            const float inGain = 0.015f, wetGain = 3f;     // hằng số của Freeverb: tiếng vang Mix 50% cân với tiếng gốc

            var inp = new float[L];                        // tiếng vang của mọi kênh lấy từ bản trộn mono
            for (int i = 0; i < L; i++)
            {
                int bi = (a + i) * ch;
                float s = 0f;
                for (int c = 0; c < ch; c++) s += d[bi + c];
                inp[i] = s / ch * inGain;
            }

            int ramp = Math.Max(1, Math.Min(L, (int)(freq * 0.02f)));
            var cb = new float[8][];
            var ab = new float[4][];
            var ci = new int[8];
            var ai = new int[4];
            var st = new float[8];
            for (int c = 0; c < ch; c++)
            {
                int spread = c * 23;
                for (int k = 0; k < 8; k++) { cb[k] = new float[Math.Max(1, (int)((AeCombTune[k] + spread) * sc))]; ci[k] = 0; st[k] = 0f; }
                for (int k = 0; k < 4; k++) { ab[k] = new float[Math.Max(1, (int)((AeApTune[k] + spread) * sc))]; ai[k] = 0; }

                for (int i = 0; i < L; i++)
                {
                    float x = inp[i], acc = 0f;
                    for (int k = 0; k < 8; k++)
                    {
                        var bf = cb[k];
                        float o = bf[ci[k]];
                        st[k] = o * (1f - damp) + st[k] * damp;
                        bf[ci[k]] = x + st[k] * fb;
                        if (++ci[k] >= bf.Length) ci[k] = 0;
                        acc += o;
                    }

                    for (int k = 0; k < 4; k++)
                    {
                        var bf = ab[k];
                        float bo = bf[ai[k]];
                        float o = bo - acc;
                        bf[ai[k]] = acc + bo * 0.5f;
                        if (++ai[k] >= bf.Length) ai[k] = 0;
                        acc = o;
                    }

                    float wet = acc * wetGain;
                    if (i >= L - ramp) wet *= (L - i) / (float)ramp; // cắt đuôi vang ở cuối vùng: giảm dần thay vì tách
                    int idx = (a + i) * ch + c;
                    d[idx] = d[idx] * (1f - mix) + wet * mix;
                }
            }
        }

        static void AeReverseRange(float[] d, int ch, int a, int b)
        {
            int n = b - a;
            for (int i = 0; i < n / 2; i++)
            {
                int p = (a + i) * ch, q = (b - 1 - i) * ch;
                for (int c = 0; c < ch; c++)
                {
                    float tmp = d[p + c];
                    d[p + c] = d[q + c];
                    d[q + c] = tmp;
                }
            }
        }

        const string AeTipFmt =
            "Định dạng khi lưu file WAV (lúc chỉnh sửa trong bộ nhớ luôn là float 32-bit nên không mất chất lượng):\n\n" +
            "• 16-bit PCM – chuẩn CD, dải động ~96 dB, nhỏ nhất (1x). Hợp cho SFX / nhạc game.\n" +
            "• 24-bit PCM – dải động ~144 dB, nặng 1.5x. Hợp để lưu bản master hoặc chỉnh sửa tiếp.\n" +
            "• 32-bit float – giữ nguyên độ chính xác và cả mức vượt 0 dBFS (không bị clip), nặng 2x. Hợp cho file trung gian.\n\n" +
            "Unity sẽ nén lại theo Import Settings nên bản dùng trong game thường chọn 16-bit.";

        static void AeTipOver(string tip)
        {
            var r = GUILayoutUtility.GetLastRect();
            GUI.Label(r, new GUIContent(string.Empty, tip));
        }

        string AeFmtSizeLabel()
        {
            int bps = _aeFmt == 0 ? 2 : (_aeFmt == 1 ? 3 : 4);
            long bytes = (long)AeFrames * Mathf.Max(1, _aeCh) * bps + 44;
            return "≈ " + EditorUtility.FormatBytes(bytes);
        }

        static float AePeakOf(float[] d, int ch, int a, int b)
        {
            float pk = 0f;
            for (int i = a * ch; i < b * ch; i++)
            {
                float v = d[i] < 0 ? -d[i] : d[i];
                if (v > pk) pk = v;
            }

            return pk;
        }

        static float AeCurveGain(float t, AeCurve c)
        {
            switch (c)
            {
                case AeCurve.Smooth: return t * t * (3f - 2f * t);
                case AeCurve.Quad: return t * t;
                default: return t;
            }
        }

        static bool AeQuiet(float[] d, int ch, int f, float thr)
        {
            for (int c = 0; c < ch; c++)
            {
                float v = d[f * ch + c];
                if (v > thr || v < -thr) return false;
            }

            return true;
        }

        // Trả về chính d nếu không cắt gì
        static float[] AeTrimSilenceData(float[] d, int ch, float db)
        {
            int t = d.Length / ch;
            if (t == 0) return d;
            float thr = Mathf.Pow(10f, db / 20f);
            int first = 0;
            while (first < t && AeQuiet(d, ch, first, thr)) first++;
            if (first >= t) return d; // toàn bộ im lặng: không cắt
            int last = t - 1;
            while (last > first && AeQuiet(d, ch, last, thr)) last--;
            if (first == 0 && last == t - 1) return d;
            var nd = new float[(last - first + 1) * ch];
            Array.Copy(d, first * ch, nd, 0, nd.Length);
            return nd;
        }

        // Trả về chính d nếu quá ngắn
        static float[] AeResampleData(float[] d, int ch, float speed)
        {
            int t = d.Length / ch;
            if (t < 2) return d;
            int nt = Mathf.Max(1, (int)(t / speed));
            var nd = new float[nt * ch];
            for (int i = 0; i < nt; i++)
            {
                double p = i * (double)speed;
                int i0 = Math.Min((int)p, t - 1);
                int i1 = Math.Min(i0 + 1, t - 1);
                float fr = (float)(p - i0);
                for (int c = 0; c < ch; c++)
                {
                    float x0 = d[i0 * ch + c], x1 = d[i1 * ch + c];
                    nd[i * ch + c] = x0 + (x1 - x0) * fr;
                }
            }

            return nd;
        }

        void AeFxClear()
        {
            _aeFx = AeFx.Def;
            _aeFxBuilt = AeFx.Def;
            _aeFxRegionSet = false;
            _aeFxHasRegion = false;
            _aeFxBuiltLin = 1f;
            _aeFxPrePeak = -1f;
        }

        // Chốt vùng áp dụng Gain / Normalize / Fade ngay lúc thanh trượt đầu tiên được kéo
        void AeFxTouchRegion()
        {
            if (_aeFxRegionSet) return;
            _aeFxRegionSet = true;
            int t = AeFrames;
            if (AeHasSel && t > 0)
            {
                _aeFxHasRegion = true;
                _aeFxFa = Math.Max(0, _aeSelA) / (double)t;
                _aeFxFb = Math.Min(t, _aeSelB) / (double)t;
            }
            else _aeFxHasRegion = false;
        }

        void AeFxRegionFrames(int frames, out int a, out int b)
        {
            a = 0;
            b = frames;
            if (_aeFxRegionSet)
            {
                if (!_aeFxHasRegion) return;
                a = Mathf.Clamp((int)Math.Round(_aeFxFa * frames), 0, frames);
                b = Mathf.Clamp((int)Math.Round(_aeFxFb * frames), 0, frames);
                if (b <= a)
                {
                    a = 0;
                    b = frames;
                }
            }
            else if (AeHasSel)
            {
                a = Mathf.Clamp(_aeSelA, 0, frames);
                b = Mathf.Clamp(_aeSelB, 0, frames);
                if (b <= a)
                {
                    a = 0;
                    b = frames;
                }
            }
        }

        // Thanh trượt vừa đổi giá trị (chưa tính lại dữ liệu, chỉ preview)
        void AeFxChanged(bool regionFx)
        {
            if (regionFx) AeFxTouchRegion();
            AePreviewRefresh();
            AeRepaint();
        }

        // Thả chuột / gõ xong số: tính lại dữ liệu theo thông số mới
        void AeFxCommitIfIdle()
        {
            var ev = Event.current;
            if (ev.type == EventType.Layout || ev.type == EventType.Repaint) return;
            if (GUIUtility.hotControl != 0) return; // còn đang kéo
            if (_aeFx.Same(_aeFxBuilt)) return;
            AeFxRebuild();
            AeDone();
        }

        void AeFxReset()
        {
            _aeFx = AeFx.Def;
            _aeFxRegionSet = false;
            _aeFxHasRegion = false;
            AeFxRebuild();
        }

        // Giữ vĩnh viễn hiệu ứng hiện tại vào dữ liệu gốc (1 bước Undo)
        void AeFxBake()
        {
            if (_aeBase == null) return;
            if (object.ReferenceEquals(_aeData, _aeBase))
            {
                AeFxClear();
                return;
            }

            _aeUndo.Add(new AeSnap { d = _aeBase, ch = _aeCh, selA = _aeSelA, selB = _aeSelB, ver = _aeBaseVer });
            while (_aeUndo.Count > AeMaxUndo) _aeUndo.RemoveAt(0);
            _aeRedo.Clear();
            _aeBase = _aeData;
            _aeBaseVer = _aeVer;
            AeFxClear();
        }

        void AeFxRebuild()
        {
            if (_aeBase == null) return;
            var fx = _aeFx;
            if (fx.RegionFx && !_aeFxRegionSet) AeFxTouchRegion();

            int ch = Mathf.Max(1, _aeCh);
            float[] src = _aeBase, cur = src;
            bool owned = false; // cur là mảng mới tạo (được phép sửa tại chỗ)
            int oldLen = _aeData == null ? 0 : _aeData.Length;
            bool wasPlaying = _aePlaying;

            if (fx.trim)
            {
                var tr = AeTrimSilenceData(cur, ch, fx.sil);
                if (!object.ReferenceEquals(tr, cur))
                {
                    cur = tr;
                    owned = true;
                }
            }

            if (fx.SpeedOn)
            {
                var rs = AeResampleData(cur, ch, Mathf.Clamp(fx.speed, 0.25f, 4f));
                if (!object.ReferenceEquals(rs, cur))
                {
                    cur = rs;
                    owned = true;
                }
            }

            _aeFxPrePeak = -1f;
            _aeFxBuiltLin = 1f;
            int frames = cur.Length / ch;
            if (fx.RegionFx && frames > 0)
            {
                int a, b;
                AeFxRegionFrames(frames, out a, out b);
                if (fx.EqOn || fx.reverse || fx.PitchOn || fx.ReverbOn)
                {
                    if (!owned)
                    {
                        cur = (float[])cur.Clone();
                        owned = true;
                    }

                    if (fx.PitchOn) AePitchShift(cur, ch, _aeFreq, a, b, fx.pitch);
                    if (fx.EqOn) AeApplyEq(cur, ch, _aeFreq, a, b, fx);
                    if (fx.reverse) AeReverseRange(cur, ch, a, b);
                    if (fx.ReverbOn) AeApplyReverb(cur, ch, _aeFreq, a, b, fx.revMix / 100f, fx.revRoom / 100f);
                }

                float pk = AePeakOf(cur, ch, a, b);
                _aeFxPrePeak = pk;
                float lin = 1f;
                if (fx.norm && pk > 1e-9f) lin *= Mathf.Pow(10f, fx.normDb / 20f) / pk;
                if (fx.GainOn) lin *= Mathf.Pow(10f, fx.gain / 20f);
                int n = b - a;
                int fi = Mathf.Clamp(Mathf.RoundToInt(fx.fadeIn * _aeFreq), 0, n);
                int fo = Mathf.Clamp(Mathf.RoundToInt(fx.fadeOut * _aeFreq), 0, n);
                bool needLin = Mathf.Abs(lin - 1f) > 1e-6f;
                if (needLin || fi > 1 || fo > 1)
                {
                    if (!owned)
                    {
                        cur = (float[])cur.Clone();
                        owned = true;
                    }

                    if (needLin)
                        for (int i = a * ch; i < b * ch; i++)
                            cur[i] *= lin;
                    for (int i = 0; i < fi; i++)
                    {
                        float g = AeCurveGain((i + 0.5f) / fi, fx.curve);
                        int bi = (a + i) * ch;
                        for (int c = 0; c < ch; c++) cur[bi + c] *= g;
                    }

                    for (int i = 0; i < fo; i++)
                    {
                        float g = AeCurveGain(1f - (i + 0.5f) / fo, fx.curve);
                        int bi = (b - fo + i) * ch;
                        for (int c = 0; c < ch; c++) cur[bi + c] *= g;
                    }
                }

                _aeFxBuiltLin = lin;
            }

            _aeFxBuilt = fx;
            if (!fx.RegionFx)
            {
                _aeFxRegionSet = false;
                _aeFxHasRegion = false;
            }

            _aeData = cur;
            _aeVer = object.ReferenceEquals(cur, src) ? _aeBaseVer : ++_aeVerCounter;

            bool lenChanged = cur.Length != oldLen;
            int t = AeFrames;
            if (lenChanged) _aeSelA = _aeSelB = -1; // độ dài đổi: vùng chọn cũ không còn đúng
            else if (_aeSelB > t) _aeSelB = t;
            if (_aeSelB <= _aeSelA) _aeSelA = _aeSelB = -1;
            _aeCursor = Mathf.Clamp(_aeCursor, 0, t);
            AeClampView();

            if (wasPlaying)
            {
                if (!lenChanged && _aeSrc != null)
                {
                    // cùng độ dài (gain / normalize / fade): đổi nguồn phát tại chỗ, nghe liền mạch
                    _aePlaySrc = _aeData;
                    _aePrevGain = 1f;
                    _aeSrc.pitch = 1f;
                }
                else AePlay(); // độ dài đổi (speed / trim silence): phát lại từ đầu bản mới
            }
        }

        // Preview khi đang kéo: chỉ bù phần chênh giữa thanh trượt và bản đã tính (gain / normalize qua PCM callback, speed qua pitch)
        void AePreviewRefresh()
        {
            if (_aeSrc == null || _aeData == null) return;
            var cur = _aeFx;
            float g = 1f;
            if (cur.norm || cur.GainOn || _aeFxBuiltLin != 1f)
            {
                float newLin = 1f;
                if (cur.norm)
                {
                    float pk = _aeFxPrePeak;
                    if (pk < 0f)
                    {
                        int a, b;
                        AeFxRegionFrames(AeFrames, out a, out b);
                        pk = AeRangePeak(a, b); // chưa có hiệu ứng nào: dữ liệu hiện tại chính là bản trước gain
                    }

                    if (pk > 1e-9f) newLin *= Mathf.Pow(10f, cur.normDb / 20f) / pk;
                }

                if (cur.GainOn) newLin *= Mathf.Pow(10f, cur.gain / 20f);
                g = newLin / Mathf.Max(1e-6f, _aeFxBuiltLin);
            }

            float pitch = Mathf.Clamp(cur.speed, 0.25f, 4f) / Mathf.Max(0.01f, _aeFxBuilt.speed);
            _aePlaySrc = _aeData;
            _aePrevGain = g;
            _aeSrc.pitch = Mathf.Clamp(pitch, 0.1f, 3f); // Unity chỉ cho pitch tối đa 3
        }

        string AePrevName()
        {
            var c = _aeFx;
            var bt = _aeFxBuilt;
            string s = "";
            if (!Mathf.Approximately(c.gain, bt.gain)) s += "  · Gain " + c.gain.ToString("0.##") + " dB";
            if (c.norm != bt.norm || (c.norm && !Mathf.Approximately(c.normDb, bt.normDb)))
                s += "  · Normalize " + (c.norm ? c.normDb.ToString("0.##") + " dB" : "off");
            if (!Mathf.Approximately(c.speed, bt.speed)) s += "  · Speed " + c.speed.ToString("0.##") + "x";
            return s;
        }

        // Vẽ đè lên waveform: thanh cam = vùng áp dụng Gain/Normalize/Fade, đường vàng = đường cong fade (theo thanh trượt)
        void AePaintFxOverlay(Rect r)
        {
            int total = AeFrames;
            if (total <= 0) return;
            int a, b;
            AeFxRegionFrames(total, out a, out b);
            bool regionOn = _aeFx.RegionFx || _aeFxBuilt.RegionFx;
            if (!regionOn) return;

            if (!(a <= 0 && b >= total))
            {
                float xa = Mathf.Clamp(AeFrameToX(r, a), r.x, r.xMax);
                float xb = Mathf.Clamp(AeFrameToX(r, b), r.x, r.xMax);
                if (xb > xa) EditorGUI.DrawRect(new Rect(xa, r.y, xb - xa, 3f), new Color(1f, 0.6f, 0.15f, 0.95f));
            }

            int n = b - a;
            int fi = Mathf.Clamp(Mathf.RoundToInt(_aeFx.fadeIn * _aeFreq), 0, n);
            int fo = Mathf.Clamp(Mathf.RoundToInt(_aeFx.fadeOut * _aeFreq), 0, n);
            if (fi > 1) AeDrawFadeEnv(r, a, fi, true);
            if (fo > 1) AeDrawFadeEnv(r, b - fo, fo, false);
        }

        void AeDrawFadeEnv(Rect r, int f0, int len, bool fadeIn)
        {
            const int N = 40;
            var up = new List<Vector3>(N + 1);
            var dn = new List<Vector3>(N + 1);
            float cy = r.center.y, half = r.height * 0.5f - 2f;
            for (int i = 0; i <= N; i++)
            {
                float t = i / (float)N;
                float g = AeCurveGain(fadeIn ? t : 1f - t, _aeFx.curve);
                float x = AeFrameToX(r, f0 + (int)(t * len));
                if (x < r.x || x > r.xMax) continue;
                up.Add(new Vector3(x, cy - g * half, 0f));
                dn.Add(new Vector3(x, cy + g * half, 0f));
            }

            if (up.Count < 2) return;
            var old = Handles.color;
            Handles.color = new Color(1f, 0.85f, 0.2f, 0.95f);
            Handles.DrawAAPolyLine(2f, up.ToArray());
            Handles.DrawAAPolyLine(2f, dn.ToArray());
            Handles.color = old;
        }

        void AeTrim()
        {
            if (!AeHasSel) return;
            int a, b;
            AeRange(out a, out b);
            int ch = _aeCh;
            var nd = new float[(b - a) * ch];
            Array.Copy(_aeData, a * ch, nd, 0, nd.Length);
            AeCommit(nd, ch, -1, -1, 0);
        }

        void AeDelete()
        {
            if (!AeHasSel) return;
            int a, b;
            AeRange(out a, out b);
            int ch = _aeCh, t = AeFrames;
            var nd = new float[(t - (b - a)) * ch];
            Array.Copy(_aeData, 0, nd, 0, a * ch);
            Array.Copy(_aeData, b * ch, nd, a * ch, (t - b) * ch);
            AeCommit(nd, ch, -1, -1, a);
        }

        void AeSilence()
        {
            if (!AeHasSel) return;
            int a, b;
            AeRange(out a, out b);
            var nd = (float[])_aeData.Clone();
            Array.Clear(nd, a * _aeCh, (b - a) * _aeCh);
            AeCommit(nd, _aeCh, _aeSelA, _aeSelB, _aeCursor);
        }

        void AeReverse()
        {
            int a, b;
            AeRange(out a, out b);
            int n = b - a, ch = _aeCh;
            if (n < 2) return;
            var nd = (float[])_aeData.Clone();
            for (int i = 0; i < n / 2; i++)
            {
                int p = (a + i) * ch, q = (b - 1 - i) * ch;
                for (int c = 0; c < ch; c++)
                {
                    float tmp = nd[p + c];
                    nd[p + c] = nd[q + c];
                    nd[q + c] = tmp;
                }
            }

            AeCommit(nd, ch, _aeSelA, _aeSelB, _aeCursor);
        }

        void AeMono()
        {
            int ch = _aeCh;
            if (ch < 2) return;
            int t = AeFrames;
            var nd = new float[t];
            for (int f = 0; f < t; f++)
            {
                float s = 0f;
                for (int c = 0; c < ch; c++) s += _aeData[f * ch + c];
                nd[f] = s / ch;
            }

            AeCommit(nd, 1, _aeSelA, _aeSelB, _aeCursor);
        }

        // =====================================================
        //  Nạp samples
        // =====================================================
        void AeQueueLoad()
        {
            _aeState = 1;
            string target = _aePath;
            EditorApplication.delayCall += () =>
            {
                if (this == null || _aePath != target) return;
                AeLoad();
                AeRepaint();
            };
        }

        void AeLoad()
        {
            var clip = _asset as AudioClip;
            string path = _aePath;
            if (clip == null || string.IsNullOrEmpty(path))
            {
                _aeState = 3;
                _aeErr = "Không có AudioClip.";
                return;
            }

            try
            {
                _aeIsWav = path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);
                float[] d = null;
                int ch = 0, fr = 0, bits = 0;
                bool fl = false;
                string err = null;
                bool ok = false;
                if (_aeIsWav && (long)clip.samples * clip.channels <= AeMaxSamples)
                    ok = AeReadWav(path, out d, out ch, out fr, out bits, out fl);
                if (ok) _aeFmt = fl ? 2 : (bits == 24 ? 1 : (bits == 32 ? 2 : 0));
                else
                {
                    ok = AeLoadViaClip(clip, path, out d, out ch, out fr, out err);
                    _aeFmt = 0;
                }

                if (!ok)
                {
                    _aeState = 3;
                    _aeErr = err ?? "Không đọc được samples.";
                    return;
                }

                _aeData = d;
                _aeCh = ch;
                _aeFreq = fr;
                _aeUndo.Clear();
                _aeRedo.Clear();
                _aeVer = _aeVerCounter = _aeSavedVer = 0;
                _aePkVer = -1;
                _aePeakVer = -1;
                _aeSelA = _aeSelB = -1;
                _aeCursor = 0;
                _aeViewStart = 0;
                _aeViewLen = 0;
                _aeBase = d;
                _aeBaseVer = 0;
                AeFxClear();
                _aeRpVer = -1;
                _aeState = 2;
            }
            catch (Exception ex)
            {
                _aeState = 3;
                _aeErr = "Lỗi nạp audio: " + ex.Message;
            }
        }

        static bool AeGetData(AudioClip clip, long total, out float[] d)
        {
            d = null;
            if (clip == null || clip.loadType == AudioClipLoadType.Streaming) return false;
            if (clip.loadState != AudioDataLoadState.Loaded)
            {
                clip.LoadAudioData();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (clip.loadState == AudioDataLoadState.Loading && sw.ElapsedMilliseconds < 8000)
                    System.Threading.Thread.Sleep(10);
            }

            var buf = new float[total];
            if (!clip.GetData(buf, 0)) return false;
            d = buf;
            return true;
        }

        // Clip không phải WAV (mp3/ogg/...) hoặc WAV đọc trực tiếp không được: dùng AudioClip.GetData.
        // Load Type = Streaming thì GetData không dùng được => tạm đặt Decompress On Load, nạp xong trả lại như cũ.
        static bool AeLoadViaClip(AudioClip clip, string path, out float[] d, out int ch, out int fr, out string err)
        {
            d = null;
            err = null;
            ch = clip.channels;
            fr = clip.frequency;
            long total = (long)clip.samples * ch;
            if (total <= 0)
            {
                err = "Clip rỗng.";
                return false;
            }

            if (total > AeMaxSamples)
            {
                err = "Clip quá dài để sửa trong bộ nhớ (" + (total / 1000000) + "M samples).";
                return false;
            }

            if (AeGetData(clip, total, out d)) return true;

            var imp = AssetImporter.GetAtPath(path) as AudioImporter;
            if (imp == null)
            {
                err = "Không đọc được samples (không có AudioImporter).";
                return false;
            }

            var old = imp.defaultSampleSettings;
            var tmp = old;
            tmp.loadType = AudioClipLoadType.DecompressOnLoad;
            bool ok = false;
            try
            {
                imp.defaultSampleSettings = tmp;
                imp.SaveAndReimport();
                var c2 = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                ok = c2 != null && AeGetData(c2, total, out d);
            }
            finally
            {
                var imp2 = AssetImporter.GetAtPath(path) as AudioImporter;
                if (imp2 != null)
                {
                    imp2.defaultSampleSettings = old;
                    imp2.SaveAndReimport();
                }
            }

            if (!ok)
            {
                d = null;
                err =
                    "Không đọc được samples. Thử đặt Load Type = Decompress On Load trong Import Settings (kể cả mục override theo platform) rồi bấm Thử lại.";
            }

            return ok;
        }

        // =====================================================
        //  WAV đọc / ghi
        // =====================================================
        static bool AeReadWav(string path, out float[] data, out int ch, out int freq, out int bits, out bool isFloat)
        {
            data = null;
            ch = 0;
            freq = 0;
            bits = 0;
            isFloat = false;
            byte[] b = File.ReadAllBytes(path);
            if (b.Length < 12 || b[0] != 'R' || b[1] != 'I' || b[2] != 'F' || b[3] != 'F'
                || b[8] != 'W' || b[9] != 'A' || b[10] != 'V' || b[11] != 'E') return false;

            int tag = 0, dataPos = -1;
            long dataLen = 0, pos = 12;
            while (pos + 8 <= b.Length)
            {
                int p = (int)pos;
                string id = Encoding.ASCII.GetString(b, p, 4);
                long len = BitConverter.ToUInt32(b, p + 4);
                int body = p + 8;
                if (id == "fmt " && len >= 16 && body + 16 <= b.Length)
                {
                    tag = BitConverter.ToUInt16(b, body);
                    ch = BitConverter.ToUInt16(b, body + 2);
                    freq = BitConverter.ToInt32(b, body + 4);
                    bits = BitConverter.ToUInt16(b, body + 14);
                    if (tag == 0xFFFE && len >= 26 && body + 26 <= b.Length)
                        tag = BitConverter.ToUInt16(b, body + 24); // WAVE_FORMAT_EXTENSIBLE
                }
                else if (id == "data")
                {
                    dataPos = body;
                    dataLen = Math.Min(len, (long)b.Length - body);
                    break;
                }

                pos = body + len + (len & 1);
            }

            if (dataPos < 0 || ch <= 0 || freq <= 0) return false;

            bool pcm = tag == 1 && (bits == 8 || bits == 16 || bits == 24 || bits == 32);
            bool flt = tag == 3 && bits == 32;
            if (!pcm && !flt) return false; // ADPCM / định dạng lạ: để GetData lo

            int bps = bits / 8;
            long total = (dataLen / ((long)bps * ch)) * ch;
            if (total <= 0 || total > AeMaxSamples) return false;

            var d = new float[total];
            int o = dataPos;
            if (flt) Buffer.BlockCopy(b, dataPos, d, 0, (int)(total * 4));
            else if (bits == 16)
                for (long i = 0; i < total; i++, o += 2)
                    d[i] = (short)(b[o] | (b[o + 1] << 8)) / 32768f;
            else if (bits == 24)
                for (long i = 0; i < total; i++, o += 3)
                {
                    int v = b[o] | (b[o + 1] << 8) | (b[o + 2] << 16);
                    if ((v & 0x800000) != 0) v |= unchecked((int)0xFF000000);
                    d[i] = v / 8388608f;
                }
            else if (bits == 32)
                for (long i = 0; i < total; i++, o += 4)
                    d[i] = BitConverter.ToInt32(b, o) / 2147483648f;
            else
                for (long i = 0; i < total; i++, o++)
                    d[i] = (b[o] - 128) / 128f;

            isFloat = flt;
            data = d;
            return true;
        }

        static void AeWriteWav(string path, float[] d, int f0, int frames, int ch, int freq, int fmt)
        {
            int bps = fmt == 0 ? 2 : (fmt == 1 ? 3 : 4);
            bool fl = fmt == 2;
            long dataLen = (long)frames * ch * bps;
            if (dataLen > int.MaxValue - 1024) throw new IOException("Dữ liệu quá lớn cho WAV (> 2GB).");
            int fmtSize = fl ? 18 : 16;
            int factSize = fl ? 12 : 0;
            int pad = (int)(dataLen & 1);
            int riff = 4 + (8 + fmtSize) + factSize + (8 + (int)dataLen + pad);

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(fs))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write(riff);
                w.Write(Encoding.ASCII.GetBytes("WAVE"));
                w.Write(Encoding.ASCII.GetBytes("fmt "));
                w.Write(fmtSize);
                w.Write((short)(fl ? 3 : 1));
                w.Write((short)ch);
                w.Write(freq);
                w.Write(freq * ch * bps);
                w.Write((short)(ch * bps));
                w.Write((short)(bps * 8));
                if (fl)
                {
                    w.Write((short)0);
                    w.Write(Encoding.ASCII.GetBytes("fact"));
                    w.Write(4);
                    w.Write(frames);
                }

                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write((int)dataLen);

                const int chunk = 1 << 16;
                var buf = new byte[chunk * bps];
                long total = (long)frames * ch;
                long src = (long)f0 * ch;
                for (long i = 0; i < total;)
                {
                    int n = (int)Math.Min(chunk, total - i);
                    if (fl) Buffer.BlockCopy(d, (int)((src + i) * 4), buf, 0, n * 4);
                    else if (bps == 2)
                        for (int k = 0, o = 0; k < n; k++)
                        {
                            int s = Mathf.RoundToInt(Mathf.Clamp(d[src + i + k], -1f, 1f) * 32767f);
                            buf[o++] = (byte)s;
                            buf[o++] = (byte)(s >> 8);
                        }
                    else
                        for (int k = 0, o = 0; k < n; k++)
                        {
                            int s = Mathf.RoundToInt(Mathf.Clamp(d[src + i + k], -1f, 1f) * 8388607f);
                            buf[o++] = (byte)s;
                            buf[o++] = (byte)(s >> 8);
                            buf[o++] = (byte)(s >> 16);
                        }

                    w.Write(buf, 0, n * bps);
                    i += n;
                }

                if (pad == 1) w.Write((byte)0);
            }
        }

        // =====================================================
        //  Lưu
        // =====================================================
        void AeQueueSaveAs(bool selOnly)
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null || _aeState != 2) return;
                string dir = Path.GetDirectoryName(_aePath);
                dir = string.IsNullOrEmpty(dir) ? "Assets" : dir.Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(_aePath) + (selOnly ? "_sel" : "_edit");
                string p = EditorUtility.SaveFilePanelInProject(selOnly ? "Export Selection" : "Save Audio As", name,
                    "wav",
                    "Lưu thành file WAV", dir);
                if (string.IsNullOrEmpty(p)) return;
                AeWriteTo(p, selOnly);
            };
        }

        void AeQueueOverwrite()
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null || _aeState != 2) return;
                string msg = "Ghi đè file gốc:\n" + _aePath + "\n\n"
                             + (_aeBackup ? "Sẽ tạo bản sao lưu .bak~ cạnh file." : "KHÔNG có sao lưu.");
                if (!EditorUtility.DisplayDialog("Ghi đè file gốc", msg, "Ghi đè", "Hủy")) return;
                AeWriteTo(_aePath, false);
            };
        }

        void AeWriteTo(string path, bool selOnly)
        {
            try
            {
                AeStopPlayback();
                int total = AeFrames;
                int a = 0, b = total;
                if (selOnly && AeHasSel)
                {
                    a = _aeSelA;
                    b = _aeSelB;
                }

                if (b - a <= 0) return;

                bool same = path == _aePath;
                bool existed = File.Exists(path);
                if (same && _aeBackup && existed) File.Copy(path, path + ".bak~", true);

                AeWriteWav(path, _aeData, a, b - a, _aeCh, _aeFreq, _aeFmt);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                if (!same && !existed) AeCopyImportSettings(_aePath, path);

                if (same)
                {
                    if (a == 0 && b == total) _aeSavedVer = _aeVer;
                    else AeReset(_aePath); // file đổi khác bộ nhớ: nạp lại từ file
                }

                var saved = AssetDatabase.LoadAssetAtPath<UObject>(path);
                if (saved != null) EditorGUIUtility.PingObject(saved);
                Debug.Log("[GaMo] Đã lưu audio: " + path);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Lưu audio lỗi", ex.Message, "OK");
            }

            AeRepaint();
        }

        // File mới tạo: lấy lại cài đặt import của file gốc (Load Type, Compression, Preload...)
        static void AeCopyImportSettings(string srcPath, string dstPath)
        {
            try
            {
                var so = AssetImporter.GetAtPath(srcPath) as AudioImporter;
                var ni = AssetImporter.GetAtPath(dstPath) as AudioImporter;
                if (so == null || ni == null || so == ni) return;
                ni.defaultSampleSettings =
                    so.defaultSampleSettings; // preloadAudioData nằm trong struct này, khỏi gán riêng (API cũ đã obsolete)
                ni.loadInBackground = so.loadInBackground;
                ni.SaveAndReimport();
            }
            catch (Exception)
            {
            }
        }

        // =====================================================
        //  Nghe thử bằng AudioSource ẩn (không dùng UnityEditor.AudioUtil)
        // =====================================================
        GameObject _aeGo;
        AudioSource _aeSrc;

        // Nguồn phát live: PCM callback nhân gain ngay khi phát (chạy trên audio thread)
        volatile float _aePrevGain = 1f;
        float[] _aePlaySrc;
        int _aePlayCh = 1;
        volatile int _aeReadPos;

        void AePcmRead(float[] buf)
        {
            var src = _aePlaySrc;
            float g = _aePrevGain;
            int n = buf.Length, p = _aeReadPos;
            int m = src == null ? 0 : src.Length - p;
            if (m < 0) m = 0;
            if (m > n) m = n;
            int i = 0;
            for (; i < m; i++) buf[i] = src[p + i] * g;
            for (; i < n; i++) buf[i] = 0f;
            _aeReadPos = p + n;
        }

        void AePcmSetPos(int framePos)
        {
            _aeReadPos = framePos * _aePlayCh;
        }

        void AePlay()
        {
            if (_aeData == null || AeFrames <= 0) return;
            AeStopPlayback();

            int total = AeFrames, start, end;
            if (AeHasSel)
            {
                start = _aeSelA;
                end = _aeSelB;
            }
            else
            {
                start = Mathf.Clamp(_aeCursor, 0, total - 1);
                end = total;
            }

            try
            {
                _aePlaySrc = _aeData;
                _aePlayCh = _aeCh;
                _aeReadPos = 0;
                _aeTmp = AudioClip.Create("GaMoAudioEditPreview", total, _aeCh, _aeFreq, true, AePcmRead, AePcmSetPos);

                _aeGo = new GameObject("GaMoAudioEditPreview") { hideFlags = HideFlags.HideAndDontSave };
                _aeSrc = _aeGo.AddComponent<AudioSource>();
                _aeSrc.playOnAwake = false;
                _aeSrc.loop = false;
                _aeSrc.spatialBlend = 0f;
                _aeSrc.volume = 1f;
                _aeSrc.ignoreListenerPause = true;
                _aeSrc.clip = _aeTmp;
                _aeSrc.Play();
                _aeSrc.timeSamples = Mathf.Clamp(start, 0, total - 1);
                AePreviewRefresh(); // áp thông số đang chỉnh (gain / speed) ngay từ đầu phiên play

                _aePlayStart = start;
                _aePlayEnd = end;
                _aePlayPos = start;
                _aePlaying = true;
                _aePlayT0 = EditorApplication.timeSinceStartup;
                EditorApplication.update -= AePump;
                EditorApplication.update += AePump;
                AssemblyReloadEvents.beforeAssemblyReload -= AeBeforeReload;
                AssemblyReloadEvents.beforeAssemblyReload += AeBeforeReload;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[GaMo] Phát audio lỗi: " + ex.Message);
                AeStopPlayback();
            }
        }

        void AeBeforeReload()
        {
            AeStopPlayback();
        }

        void AeStopPlayback()
        {
            bool was = _aePlaying;
            EditorApplication.update -= AePump;
            AssemblyReloadEvents.beforeAssemblyReload -= AeBeforeReload;
            _aePlaying = false;
            if (_aeSrc != null) _aeSrc.Stop();
            if (_aeGo != null) DestroyImmediate(_aeGo);
            _aeGo = null;
            _aeSrc = null;
            if (_aeTmp != null)
            {
                DestroyImmediate(_aeTmp);
                _aeTmp = null;
            }

            _aePlaySrc = null;

            if (was) AeRepaint();
        }

        void AePump()
        {
            if (this == null || !_aePlaying || _aeSrc == null)
            {
                AeStopPlayback();
                return;
            }

            if (_asset == null || AssetDatabase.GetAssetPath(_asset) != _aePath)
            {
                AeStopPlayback();
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now - _aePumpAt < 0.033) return;
            _aePumpAt = now;

            bool grace = now - _aePlayT0 < 0.3;
            int pos = _aeSrc.timeSamples;
            if (!grace) _aePlayPos = Mathf.Min(pos, _aePlayEnd);

            bool done = !grace && (!_aeSrc.isPlaying || pos >= _aePlayEnd);
            if (done)
            {
                if (_aeLoop)
                {
                    _aeSrc.Stop();
                    _aeSrc.Play();
                    _aeSrc.timeSamples = Mathf.Clamp(_aePlayStart, 0, AeFrames - 1);
                    _aePlayT0 = now;
                    _aePlayPos = _aePlayStart;
                }
                else
                {
                    AeStopPlayback();
                    return;
                }
            }

            AeRepaint();
        }
    }
}
#endif