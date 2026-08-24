using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;

namespace Game.UI
{
    // HUD cho chế độ Team Deathmatch (2 đội). Bổ sung 2 panel điểm đội
    // (tên, số mạng, thanh tiến độ) lên trên phần HUD dùng chung (GameHudControllerBase).
    // Controller này còn giữ và điều khiển BẢNG XẾP HẠNG TDM cho 2 đội:
    // SetScoreboardAlpha(...) và SetScoreboardBravo(...).
    public class GameDMHudController : GameHudControllerBase
    {
        [TabGroup("Base", "Team")]
        [FoldoutGroup("Base/Team/Đội 1")]
        [SerializeField] private TMP_Text _team1NameLabel;
        [FoldoutGroup("Base/Team/Đội 1")]
        [SerializeField] private TMP_Text _team1KillsLabel;
        [FoldoutGroup("Base/Team/Đội 1")]
        [SerializeField] private UIFillBar _team1Fill;   // thanh điểm đội 1

        [FoldoutGroup("Base/Team/Đội 2")]
        [SerializeField] private TMP_Text _team2NameLabel;
        [FoldoutGroup("Base/Team/Đội 2")]
        [SerializeField] private TMP_Text _team2KillsLabel;
        [FoldoutGroup("Base/Team/Đội 2")]
        [SerializeField] private UIFillBar _team2Fill;   // thanh điểm đội 2

        [FoldoutGroup("Base/Team/Luật")]
        [Tooltip("Số mạng cần đạt để thắng (dùng để tính độ đầy thanh điểm).")]
        [SerializeField] private int _killTarget = 100;

        [FoldoutGroup("Base/Team/Scoreboard - phần đầu")]
        [SerializeField] private TMP_Text _sbTitle;      // tên chế độ (dòng nhỏ phía trên)
        [FoldoutGroup("Base/Team/Scoreboard - phần đầu")]
        [SerializeField] private Sprite _youSprite;      // nền teal cho dòng của mình

        [FoldoutGroup("Base/Team/Scoreboard - Alpha (Đội 1)")]
        [SerializeField] private TMP_Text _sbAlphaName;
        [FoldoutGroup("Base/Team/Scoreboard - Alpha (Đội 1)")]
        [SerializeField] private TMP_Text _sbAlphaScore;
        [FoldoutGroup("Base/Team/Scoreboard - Alpha (Đội 1)")]
        [SerializeField] private ScoreRow[] _sbAlphaRows;

        [FoldoutGroup("Base/Team/Scoreboard - Bravo (Đội 2)")]
        [SerializeField] private TMP_Text _sbBravoName;
        [FoldoutGroup("Base/Team/Scoreboard - Bravo (Đội 2)")]
        [SerializeField] private TMP_Text _sbBravoScore;
        [FoldoutGroup("Base/Team/Scoreboard - Bravo (Đội 2)")]
        [SerializeField] private ScoreRow[] _sbBravoRows;
        
        private void Start()
        {
            if (GameManager.Instance != null)
                if (GameManager.Instance.GetGamePlayMode() != GamePlayMode.Team)
                    gameObject.SetActive(false);
            
            if (nPlayer != null && !nPlayer.IsLocalPlayer)
                gameObject.SetActive(false);
        }

        // Đặt mục tiêu số mạng cần đạt và cập nhật dòng mục tiêu trên HUD.
        public override void SetKillTarget(int target)
        {
            _killTarget = Mathf.Max(1, target);
            if (_objectiveLabel != null) _objectiveLabel.text = $"MỤC TIÊU {_killTarget} KILLS";
        }

        // Cập nhật tên + số mạng đội 1 (và độ đầy thanh điểm theo _killTarget).
        public override void SetTeam1(string name, int kills)
        {
            if (_team1NameLabel != null) _team1NameLabel.text = name;
            if (_team1KillsLabel != null) _team1KillsLabel.text = kills.ToString();
            if (_team1Fill != null) _team1Fill.SetValue(kills, _killTarget);
        }

        // Cập nhật tên + số mạng đội 2.
        public override void SetTeam2(string name, int kills)
        {
            if (_team2NameLabel != null) _team2NameLabel.text = name;
            if (_team2KillsLabel != null) _team2KillsLabel.text = kills.ToString();
            if (_team2Fill != null) _team2Fill.SetValue(kills, _killTarget);
        }

        // Đặt tiêu đề scoreboard (tên chế độ).
        public override void SetScoreboardTitle(string title)
        {
            if (_sbTitle != null) _sbTitle.text = title;
        }

        // Đặt tên + điểm + danh sách người chơi cho đội Alpha (đội 1).
        public override void SetScoreboardAlpha(string teamName, int teamScore, IList<ScoreEntry> rows)
        {
            if (_sbAlphaName != null) _sbAlphaName.text = teamName;
            if (_sbAlphaScore != null) _sbAlphaScore.text = teamScore.ToString();
            FillRows(_sbAlphaRows, rows);
        }

        // Đặt tên + điểm + danh sách người chơi cho đội Bravo (đội 2).
        public override void SetScoreboardBravo(string teamName, int teamScore, IList<ScoreEntry> rows)
        {
            if (_sbBravoName != null) _sbBravoName.text = teamName;
            if (_sbBravoScore != null) _sbBravoScore.text = teamScore.ToString();
            FillRows(_sbBravoRows, rows);
        }

        // Đổ danh sách vào mảng dòng của một đội; dòng thừa sẽ bị ẩn.
        private void FillRows(ScoreRow[] rowsArr, IList<ScoreEntry> entries)
        {
            if (rowsArr == null) return;
            for (int i = 0; i < rowsArr.Length; i++)
            {
                if (rowsArr[i] == null) continue;
                if (entries != null && i < entries.Count)
                {
                    ScoreEntry e = entries[i];
                    rowsArr[i].Set(e.rank, e.name, e.kills, e.deaths, e.mark, null, _youSprite);
                }
                else rowsArr[i].Hide();
            }
        }
    }
}
