using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.UI
{
    // Mô tả một DÒNG của bảng xếp hạng (scoreboard). Đây KHÔNG phải MonoBehaviour.
    // HUD controller (GameFFAHudController / GameDMHudController) giữ một mảng ScoreRow
    // và đổ dữ liệu vào, nhờ vậy cả bảng được điều khiển bởi 1 controller duy nhất
    // thay vì gắn 1 component lên từng dòng.
    // Cách dùng: kéo thả các ô (root, background, rankLabel, nameLabel...) ở Inspector, rồi gọi Set().
    [Serializable]
    public class ScoreRow
    {
        // Kiểu tô sáng của dòng: không tô / hạng 1 (vàng) / dòng của mình (teal).
        public enum Mark { None, Rank1, You }

        public GameObject root;        // cả dòng (dùng để bật/tắt hiển thị)
        public Image background;       // nền highlight của dòng (tùy chọn)
        public TMP_Text rankLabel;
        public TMP_Text nameLabel;
        public GameObject youTag;      // nhãn "BẠN" (tùy chọn)
        public TMP_Text killsLabel;
        public TMP_Text deathsLabel;

        // Các màu dựng sẵn cho chữ trong dòng.
        static readonly Color Light = new Color(0.90f, 0.93f, 0.96f, 1f);
        static readonly Color Muted = new Color(0.55f, 0.62f, 0.67f, 1f);
        static readonly Color Gold  = new Color(1f, 0.82f, 0.35f, 1f);
        static readonly Color Teal  = new Color(0.31f, 0.84f, 0.79f, 1f);

        // Đổ dữ liệu vào dòng và áp dụng kiểu tô sáng.
        // rank1Sprite / youSprite là texture nền cho hạng 1 và cho dòng của mình.
        public void Set(int rank, string playerName, int kills, int deaths, Mark mark, Sprite rank1Sprite, Sprite youSprite)
        {
            if (root != null) root.SetActive(true);
            if (rankLabel != null) rankLabel.text = rank.ToString();
            if (nameLabel != null) nameLabel.text = playerName;
            if (killsLabel != null) killsLabel.text = kills.ToString();
            if (deathsLabel != null) deathsLabel.text = deaths.ToString();

            Color nameColor = Light, rankColor = Muted;
            if (mark == Mark.Rank1) { nameColor = Gold; rankColor = Gold; }
            else if (mark == Mark.You) { nameColor = Teal; rankColor = Teal; }
            if (nameLabel != null) nameLabel.color = nameColor;
            if (rankLabel != null) rankLabel.color = rankColor;

            if (background != null)
            {
                if (mark == Mark.Rank1 && rank1Sprite != null) { background.sprite = rank1Sprite; background.enabled = true; }
                else if (mark == Mark.You && youSprite != null) { background.sprite = youSprite; background.enabled = true; }
                else background.enabled = false;
            }
            if (youTag != null) youTag.SetActive(mark == Mark.You);
        }

        // Ẩn dòng này đi (dùng khi số người chơi ít hơn số dòng có sẵn).
        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }
    }

    // Dữ liệu thô của một dòng scoreboard (đưa vào cho HUD controller đổ vào ScoreRow).
    [Serializable]
    public struct ScoreEntry
    {
        public int rank;
        public string name;
        public int kills;
        public int deaths;
        public ScoreRow.Mark mark;

        public ScoreEntry(int rank, string name, int kills, int deaths, ScoreRow.Mark mark)
        {
            this.rank = rank; this.name = name; this.kills = kills; this.deaths = deaths; this.mark = mark;
        }
    }
}
