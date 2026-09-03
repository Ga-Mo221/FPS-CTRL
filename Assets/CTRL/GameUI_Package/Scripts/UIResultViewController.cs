using System;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using TMPro;
using TrueInputSystem;

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

        [FoldoutGroup("MVP")]
        [SerializeField] private TMP_Text _mvpNameLabel;

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
        }
        
        public void LeaveRoom()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.NetworkRunnerHandler.LeaveRoom();
        }

        [Button]
        public void ResultVisible(float time)
        {
            StartCoroutine(startVisible(time));
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
            StartCoroutine(DisplayButton());
        }

        private IEnumerator DisplayButton()
        {
            yield return new WaitForSeconds(3f);
            _playAgainButton?.gameObject.SetActive(true);
            if (_canvasGroup != null)
                _canvasGroup.blocksRaycasts = true;
        }

        // ---- Public setters ----
        public void SetResult(MatchResult result)
        {
            if (_resultTitleLabel != null)
            {
                if (result.LocalPlayerWon == 1)
                    _resultTitleLabel.text = _victoryText;
                else if (result.LocalPlayerWon == 0)
                    _resultTitleLabel.text = _defeatText;
                else if (result.LocalPlayerWon == 2)
                    _resultTitleLabel.text = "trận đấu đã kết thúc";
            }
            if (_winnerNameLabel != null) _winnerNameLabel.text = result.WinnerTeamName;
            if (_winScoreLabel != null) _winScoreLabel.text = result.WinningScore;
            if (_loseScoreLabel != null) _loseScoreLabel.text = result.LosingScore.ToString();
        }

        public void SetMvp(KillInfo killInfo)
        {
            var mvpName = killInfo.PlayerName;
            if (_mvpNameLabel != null) _mvpNameLabel.text = mvpName;
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
                _xpForNextLabel.text = $"{reward.CurrentLevelXp:N0} / {reward.XpForNextLevel:NO}";
            
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
