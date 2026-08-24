using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;

namespace Game.UI
{
    // HUD cho chế độ Free-For-All (đấu đơn). Bổ sung hạng + số mạng cá nhân
    // lên trên phần HUD dùng chung (GameHudControllerBase).
    // Controller này còn giữ và điều khiển BẢNG XẾP HẠNG FFA:
    // đặt tiêu đề/mục tiêu và đổ danh sách người chơi qua SetScoreboardEntries().
    public class GameFFAHudController : GameHudControllerBase
    {
        [TabGroup("Base", "Solo")]
        [FoldoutGroup("Base/Solo/Cá nhân")]
        [SerializeField] private TMP_Text _rankLabel;   // ví dụ "#2"
        [FoldoutGroup("Base/Solo/Cá nhân")]
        [SerializeField] private TMP_Text _killsLabel;  // ví dụ "21"

        [FoldoutGroup("Base/Solo/Cá nhân")]
        [SerializeField] private string _rankPrefix = "#";

        [FoldoutGroup("Base/Solo/Nội dung scoreboard")]
        [SerializeField] private TMP_Text _sbTitle;      // tên chế độ (dòng nhỏ phía trên)
        [FoldoutGroup("Base/Solo/Nội dung scoreboard")]
        [SerializeField] private TMP_Text _sbObjective;  // dòng mục tiêu / thời gian
        [FoldoutGroup("Base/Solo/Nội dung scoreboard")]
        [SerializeField] private ScoreRow[] _sbRows;     // 10 dòng xếp hạng
        [FoldoutGroup("Base/Solo/Nội dung scoreboard")]
        [SerializeField] private Sprite _rank1Sprite;    // nền vàng cho hạng 1
        [FoldoutGroup("Base/Solo/Nội dung scoreboard")]
        [SerializeField] private Sprite _youSprite;      // nền teal cho dòng của mình

        private void Start()
        {
            if (GameManager.Instance != null)
                if (GameManager.Instance.GetGamePlayMode() != GamePlayMode.Solo)
                    gameObject.SetActive(false);
            
            if (nPlayer != null && !nPlayer.IsLocalPlayer)
                gameObject.SetActive(false);
        }

        // Đặt hạng cá nhân (kèm tiền tố, ví dụ "#2").
        public override void SetRank(int rank)
        {
            if (_rankLabel != null) _rankLabel.text = $"{_rankPrefix}{rank}";
        }

        // Đặt số mạng cá nhân.
        public override void SetKills(int kills)
        {
            if (_killsLabel != null) _killsLabel.text = kills.ToString();
        }

        // Đặt mục tiêu số mạng cần đạt để thắng.
        public override void SetKillTarget(int target)
        {
            if (_objectiveLabel != null) _objectiveLabel.text = $"MỤC TIÊU {target} KILLS";
        }

        // Đặt tiêu đề scoreboard (tên chế độ).
        public override void SetScoreboardTitle(string title)
        {
            if (_sbTitle != null) _sbTitle.text = title;
        }

        // Đặt dòng mục tiêu / thời gian trên scoreboard.
        public override void SetScoreboardObjective(string objective)
        {
            if (_sbObjective != null) _sbObjective.text = objective;
        }

        // Đổ danh sách người chơi vào các dòng (từ trên xuống); dòng thừa sẽ bị ẩn.
        public override void SetScoreboardEntries(IList<ScoreEntry> entries)
        {
            if (_sbRows == null) return;
            for (int i = 0; i < _sbRows.Length; i++)
            {
                if (_sbRows[i] == null) continue;
                if (entries != null && i < entries.Count)
                {
                    ScoreEntry e = entries[i];
                    _sbRows[i].Set(e.rank, e.name, e.kills, e.deaths, e.mark, _rank1Sprite, _youSprite);
                }
                else _sbRows[i].Hide();
            }
        }

    }
}
