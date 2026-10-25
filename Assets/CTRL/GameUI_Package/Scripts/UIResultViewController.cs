using System;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using TMPro;
using GaMo.TrueInputSystem;

namespace Game.UI
{
    /// <summary>
    /// Presentation controller for the post-match (Win/Result) screen.
    /// Feeds result, MVP, rewards, level progress and personal stats.
    /// </summary>
    public class UIResultViewController : MonoBehaviour
    {
        [FoldoutGroup("Result")]
        [SerializeField] private TMP_Text _resultTitleLabel;   // "CHIẾN THẮNG" / "THẤT BẠI"
        [FoldoutGroup("Result")]
        [SerializeField] private TMP_Text _winnerNameLabel;
        [FoldoutGroup("Result")]
        [SerializeField] private TMP_Text _winScoreLabel;
        [FoldoutGroup("Result")]
        [SerializeField] private TMP_Text _loseScoreLabel;
        [FoldoutGroup("Result")]
        [SerializeField] private string _victoryText = "CHIẾN THẮNG";
        [FoldoutGroup("Result")]
        [SerializeField] private string _defeatText = "THẤT BẠI";
        [FoldoutGroup("Result")]
        [SerializeField] private UnityEngine.UI.Image _overlay; // nen mau thang/thua
        [FoldoutGroup("Result")]
        [Tooltip("UIGradient cua 'Color Overlay' (trong -> tu tim con ten 'Color Overlay'). Doi mau theo thang/thua giong ban thiet ke Win/Lose.")]
        [SerializeField] private UIGradient _colorOverlay;
        [FoldoutGroup("Result")]
        [SerializeField] private string _drawText = "HÒA";
        [FoldoutGroup("Result")]
        [SerializeField] private Color _winAccent = new Color(0.310f, 0.839f, 0.788f, 1f);   // teal (ban Win.prefab)
        [FoldoutGroup("Result")]
        [SerializeField] private Color _loseAccent = new Color(0.860f, 0.300f, 0.270f, 1f);  // do (ban Lose.prefab)
        [FoldoutGroup("Result")]
        [SerializeField] private Color _drawAccent = new Color(0.800f, 0.820f, 0.850f, 1f);
        // Color Overlay = mau Image x UIGradient. Thang: GIONG HET PlayPanel (Image teal + gradient top/bottom);
        // thua: cung chi so, dao kenh mau sang do; hoa: xam cung do sang.
        [FoldoutGroup("Result")]
        [SerializeField] private Color _winOverlayColor = new Color(0.120f, 0.592f, 0.528f, 1f);   // = PlayPanel/Color Overlay (Image)
        [FoldoutGroup("Result")]
        [SerializeField] private Color _winGradientTop = new Color(0.098f, 0.196f, 0.196f, 0.545f); // = PlayPanel (UIGradient top)
        [FoldoutGroup("Result")]
        [SerializeField] private Color _loseOverlayColor = new Color(0.592f, 0.120f, 0.120f, 1f);
        [FoldoutGroup("Result")]
        [SerializeField] private Color _loseGradientTop = new Color(0.196f, 0.098f, 0.098f, 0.545f);
        [FoldoutGroup("Result")]
        [SerializeField] private Color _drawOverlayColor = new Color(0.400f, 0.420f, 0.440f, 1f);
        [FoldoutGroup("Result")]
        [SerializeField] private Color _drawGradientTop = new Color(0.180f, 0.180f, 0.185f, 0.545f);
        [FoldoutGroup("Result")]
        [SerializeField] private Color _gradientBottom = new Color(0f, 0f, 0f, 0f);                 // = PlayPanel (UIGradient bottom)
        [FoldoutGroup("Result")]
        [Tooltip("Bat = doi ca mau Image cua lop overlay. Tat = giu mau Image (vd BG den), chi doi mau gradient - dung khi BG lam overlay.")]
        [SerializeField] private bool _tintOverlayGraphic = true;
        [FoldoutGroup("Result")]
        [Tooltip("Mau doi XANH (Blue/Alpha) cho ti so.")]
        [SerializeField] private Color _blueTeamColor = new Color(0.310f, 0.839f, 0.788f, 1f);
        [FoldoutGroup("Result")]
        [Tooltip("Mau doi DO (Red/Beta) cho ti so.")]
        [SerializeField] private Color _redTeamColor = new Color(0.920f, 0.300f, 0.270f, 1f);

        [FoldoutGroup("MVP")]
        [SerializeField] private TMP_Text _mvpNameLabel;
        [FoldoutGroup("MVP")]
        [SerializeField] private UnityEngine.UI.Image _mvpAvatar;

        [FoldoutGroup("Rewards")]
        [SerializeField] private TMP_Text _expLabel;
        [FoldoutGroup("Rewards")]
        [SerializeField] private TMP_Text _creditLabel;
        [FoldoutGroup("Rewards")]
        [SerializeField] private TMP_Text _levelLabel;
        [FoldoutGroup("Rewards")]
        [SerializeField] private TMP_Text _xpForNextLabel;
        [FoldoutGroup("Rewards")]
        [SerializeField] private TMP_Text _xpGainedLabel;
        [FoldoutGroup("Rewards")]
        [SerializeField] private UIFillBar _levelProgressBar;

        [FoldoutGroup("Stats")]
        [SerializeField] private TMP_Text _killsLabel;
        [FoldoutGroup("Stats")]
        [SerializeField] private TMP_Text _deathsLabel;
        [FoldoutGroup("Stats")]
        [SerializeField] private TMP_Text _assistsLabel;
        [FoldoutGroup("Stats")]
        [SerializeField] private TMP_Text _kdLabel;
        [FoldoutGroup("Stats")]
        [SerializeField] private TMP_Text _streakLabel;

        [FoldoutGroup("Actions")]
        [SerializeField] private TISGamepadUIButton _playAgainButton;
        
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0;
                _canvasGroup.blocksRaycasts = false;
            }
            
            if (_playAgainButton != null) _playAgainButton.gameObject.SetActive(false);
            NoWrap(_resultTitleLabel); NoWrap(_winnerNameLabel); NoWrap(_winScoreLabel); NoWrap(_loseScoreLabel); NoWrap(_mvpNameLabel);
            NoWrap(_expLabel); NoWrap(_creditLabel); NoWrap(_levelLabel); NoWrap(_xpForNextLabel); NoWrap(_xpGainedLabel);
            NoWrap(_killsLabel); NoWrap(_deathsLabel); NoWrap(_assistsLabel); NoWrap(_kdLabel); NoWrap(_streakLabel);
        }
        
        public void LeaveRoom()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.NewGame();
                GameManager.Instance.NetworkRunnerHandler.LeaveRoom();
            }
        }

        [Button]
        public void ResultVisible(float time)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            
            if (!gm.GetLocalGameOverUI())
                StartCoroutine(startVisible(time));
            else if (_canvasGroup != null && _playAgainButton != null)
            {
                _canvasGroup.alpha = 1;
                _playAgainButton?.gameObject.SetActive(true);
                _canvasGroup.blocksRaycasts = true;
            }
        }

        private IEnumerator startVisible(float time)
        {
            float startTime = 0;
            while (startTime < time)
            {
                startTime += Time.deltaTime;
                if (_canvasGroup != null) _canvasGroup.alpha = startTime / time;
                yield return null;
            }
            if (_canvasGroup != null) _canvasGroup.alpha = 1;
            
            if (GameManager.Instance != null)
                GameManager.Instance.SetLocalGameOverUI(true);
            StartCoroutine(DisplayButton());
        }

        private IEnumerator DisplayButton()
        {
            yield return new WaitForSeconds(3f);
            GameManager.Instance?.SetOnUIOptions(true);
            _playAgainButton?.gameObject.SetActive(true);
            if (_canvasGroup != null)
                _canvasGroup.blocksRaycasts = true;
        }

        // ---- Public setters ----
        private static void NoWrap(TMP_Text t)
        {
            if (t == null) return;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
        }

        public void SetResult(MatchResult result)
        {
            // 1 = thang, 0 = thua, 2 = hoa.
            Color accent = result.LocalPlayerWon == 1 ? _winAccent : result.LocalPlayerWon == 0 ? _loseAccent : _drawAccent;
            if (_resultTitleLabel != null)
            {
                if (result.LocalPlayerWon == 1) _resultTitleLabel.text = _victoryText;
                else if (result.LocalPlayerWon == 0) _resultTitleLabel.text = _defeatText;
                else _resultTitleLabel.text = _drawText;
                _resultTitleLabel.color = accent;
            }
            if (_colorOverlay == null) { var t = transform.Find("Color Overlay"); if (t == null) t = transform.Find("BG"); if (t != null) _colorOverlay = t.GetComponent<UIGradient>(); } 
            if (_colorOverlay != null)
            {
                Color top = result.LocalPlayerWon == 1 ? _winGradientTop : result.LocalPlayerWon == 0 ? _loseGradientTop : _drawGradientTop;
                Color baseCol = result.LocalPlayerWon == 1 ? _winOverlayColor : result.LocalPlayerWon == 0 ? _loseOverlayColor : _drawOverlayColor;
                _colorOverlay.enabled = true; _colorOverlay.gameObject.SetActive(true);
                var g = _colorOverlay.GetComponent<UnityEngine.UI.Graphic>(); if (g != null && _tintOverlayGraphic) g.color = baseCol;
                _colorOverlay.SetColors(top, _gradientBottom);
            }
            if (_winnerNameLabel != null) _winnerNameLabel.text = result.WinnerTeamName;
            if (_winScoreLabel != null) _winScoreLabel.text = result.WinningScore;
            if (_loseScoreLabel != null) _loseScoreLabel.text = result.LosingScore.ToString();

            if (_overlay != null)
            {
                // Tat lop phu phang - chi dung Color Overlay theo chi so PlayPanel (Nyx yeu cau).
                _overlay.enabled = false;
            }
        }

        [FoldoutGroup("Result")]
        [SerializeField] private Color _myScoreColor = new Color(0.310f, 0.839f, 0.788f, 1f);    // teal
        [FoldoutGroup("Result")]
        [SerializeField] private Color _enemyScoreColor = new Color(1.000f, 0.416f, 0.239f, 1f); // cam (ban Win/Lose.prefab)

        /// <summary> Che do Team: TRAI = diem doi MINH, PHAI = diem doi dich; moi so to theo MAU DOI (Xanh/Do).
        /// Vd minh doi Xanh thang 10-2 -> "10" xanh - "2" do; ben thua (doi Do) thay "2" do - "10" xanh.</summary>
        public void SetTeamScores(int myScore, bool myTeamIsBlue, int enemyScore)
        {
            if (_winScoreLabel != null) { _winScoreLabel.text = myScore.ToString(); _winScoreLabel.color = myTeamIsBlue ? _blueTeamColor : _redTeamColor; }
            if (_loseScoreLabel != null) { _loseScoreLabel.text = enemyScore.ToString(); _loseScoreLabel.color = myTeamIsBlue ? _redTeamColor : _blueTeamColor; }
        }

        /// <summary> o ti so ben trai = doi thang. winnerIsLocal -> trai teal/phai cam, nguoc lai.</summary>
        public void SetTeamScoreColors(bool winnerIsLocal)
        {
            if (_winScoreLabel != null) _winScoreLabel.color = winnerIsLocal ? _myScoreColor : _enemyScoreColor;
            if (_loseScoreLabel != null) _loseScoreLabel.color = winnerIsLocal ? _enemyScoreColor : _myScoreColor;
        }

        public void SetMvp(KillInfo killInfo)
        {
            var mvpName = killInfo.PlayerName;
            if (_mvpNameLabel != null) _mvpNameLabel.text = mvpName;
            if (_mvpAvatar != null)
            {
                _mvpAvatar.enabled = false;
                if (!string.IsNullOrEmpty(killInfo.AvatarUrl)) StartCoroutine(LoadMvpAvatar(killInfo.AvatarUrl));
            }
        }

        private System.Collections.IEnumerator LoadMvpAvatar(string url)
        {
            using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success) yield break;
                var tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
                if (tex == null || _mvpAvatar == null) yield break;
                _mvpAvatar.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                _mvpAvatar.enabled = true;
            }
        }

        public void SetRewards(RewardInfo reward)
        {
            if (_expLabel != null)
                _expLabel.text = $"+{reward.Experience:N0}";
                    
            if (_creditLabel != null) 
                _creditLabel.text = $"+{reward.Credits:N0}";
            
            if (_levelLabel != null) 
                _levelLabel.text = $"{reward.Level} / {reward.Level + 1}";
            
            if (_xpForNextLabel != null) 
                _xpForNextLabel.text = $"{reward.CurrentLevelXp:N0} / {reward.XpForNextLevel:N0}";
            
            if (_xpGainedLabel != null) 
                _xpGainedLabel.text = $"+{reward.Experience:N0} trận này";
            
            if (_levelProgressBar != null) 
                _levelProgressBar.SetValue(reward.CurrentLevelXp, reward.XpForNextLevel);
        }

        public void SetStats(PlayerMatchStats stats)
        {
            if (_killsLabel != null) _killsLabel.text = stats.Kills.ToString();
            if (_deathsLabel != null) _deathsLabel.text = stats.Deaths.ToString();
            if (_assistsLabel != null)  _assistsLabel.text = stats.Assist.ToString();
            if (_kdLabel != null) _kdLabel.text = stats.KillDeathRatio.ToString("0.00");
            if (_streakLabel != null) _streakLabel.text = stats.BestStreak.ToString();
        }
    }
    
    [Serializable]
    public struct MatchResult
    {
        public string WinnerTeamName { get; private set; }
        public string WinningScore { get; private set; }
        public int LosingScore { get; private set; }
        public int LocalPlayerWon { get; private set; }

        /// <summary>
        /// localPlayerWon = 1 là team(true), = 0 là team(false), = 2 là solo
        /// </summary>
        /// <param name="winnerTeamName"></param>
        /// <param name="winningScore"></param>
        /// <param name="losingScore"></param>
        /// <param name="localPlayerWon"></param>
        public MatchResult(string winnerTeamName, string winningScore, int losingScore, int localPlayerWon)
        {
            WinnerTeamName = winnerTeamName;
            WinningScore = winningScore;
            LosingScore = losingScore;
            LocalPlayerWon = localPlayerWon;
        }
    }

    [Serializable]
    public struct RewardInfo
    {
        public int Experience { get; private set; }
        public int Credits { get; private set; }
        public int Level { get; private set; }
        public int CurrentLevelXp { get; private set; }
        public int XpForNextLevel { get; private set; }

        public RewardInfo(int experience, int credits, int level, int currentLevelXp, int xpForNextLevel)
        {
            Experience = experience;
            Credits = credits;
            Level = level;
            CurrentLevelXp = currentLevelXp;
            XpForNextLevel = xpForNextLevel;
        }
    }

    [Serializable]
    public struct PlayerMatchStats
    {
        public int Kills { get; private set; }
        public int Deaths { get; private set; }
        public int Assist { get; private set; }
        public float KillDeathRatio { get; private set; }
        public int BestStreak { get; private set; }

        public PlayerMatchStats(int kills, int deaths, int assist, float killDeathRatio, int bestStreak)
        {
            Kills = kills;
            Deaths = deaths;
            Assist = assist;
            KillDeathRatio = killDeathRatio;
            BestStreak = bestStreak;
        }
    }
}
