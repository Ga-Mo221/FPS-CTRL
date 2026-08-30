using System;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
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
                _canvasGroup.alpha = 0;
            
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
            _playAgainButton.gameObject.SetActive(true);
        }

        // ---- Public setters ----
        public void SetResult(MatchResult result)
        {
            if (_resultTitleLabel != null) _resultTitleLabel.text = result.localPlayerWon ? _victoryText : _defeatText;
            if (_winnerNameLabel != null) _winnerNameLabel.text = result.winnerTeamName;
            if (_winScoreLabel != null) _winScoreLabel.text = result.winningScore.ToString();
            if (_loseScoreLabel != null) _loseScoreLabel.text = result.losingScore.ToString();
        }

        public void SetMvp(KillInfo killInfo)
        {
            var mvpName = killInfo.PlayerName;
            if (_mvpNameLabel != null) _mvpNameLabel.text = mvpName;
        }

        public void SetRewards(RewardInfo reward)
        {
            if (_expLabel != null) _expLabel.text = reward.experience.ToString("N0");
            if (_creditLabel != null) _creditLabel.text = reward.credits.ToString("N0");
            if (_levelLabel != null) _levelLabel.text = reward.level.ToString();
            if (_xpForNextLabel != null) _xpForNextLabel.text = reward.xpForNextLevel.ToString("N0");
            if (_xpGainedLabel != null) _xpGainedLabel.text = $"+{reward.xpGainedThisMatch:N0}";
            if (_levelProgressBar != null) _levelProgressBar.SetValue(reward.currentLevelXp, reward.xpForNextLevel);
        }

        public void SetStats(PlayerMatchStats stats)
        {
            if (_killsLabel != null) _killsLabel.text = stats.kills.ToString();
            if (_deathsLabel != null) _deathsLabel.text = stats.deaths.ToString();
            if (_kdLabel != null) _kdLabel.text = stats.killDeathRatio.ToString("0.00");
            if (_streakLabel != null) _streakLabel.text = stats.bestStreak.ToString();
        }
    }
    
    [Serializable]
    public struct MatchResult
    {
        public string winnerTeamName;
        public int winningScore;
        public int losingScore;
        public bool localPlayerWon;
    }

    [Serializable]
    public struct RewardInfo
    {
        public int experience;
        public int credits;
        public int level;
        public int currentLevelXp;   // xp into the current level
        public int xpForNextLevel;   // xp needed to reach next level
        public int xpGainedThisMatch;
    }

    [Serializable]
    public struct PlayerMatchStats
    {
        public int kills;
        public int deaths;
        public float killDeathRatio;
        public int bestStreak;
    }
}
