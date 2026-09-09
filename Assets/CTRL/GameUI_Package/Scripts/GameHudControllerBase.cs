using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using TMPro;

namespace Game.UI
{
    // Lớp HUD dùng chung trong trận (panel đạn, kill feed, crosshair, mục tiêu/đồng hồ).
    // HUD của DM và FFA đều kế thừa lớp này nên phần code chung chỉ viết 1 lần (DRY/SRP).
    // Cách dùng: kéo thả các ô ở Inspector rồi gọi các setter bên dưới để cập nhật HUD.
    // Scoreboard: giữ phím (mặc định Tab) để hiện, thả ra thì ẩn - xử lý trong Update().
    public abstract class GameHudControllerBase : MonoBehaviour
    {
        [TabGroup("Base", "Base")]
        [FoldoutGroup("Base/Base/Vũ khí hiện tại")]
        [SerializeField] private UIWeaponController _weaponcontroller;

        [FoldoutGroup("Base/Base/Trận đấu")]
        [SerializeField] protected TMP_Text _timerLabel;
        [FoldoutGroup("Base/Base/Trận đấu")]
        [SerializeField] protected TMP_Text _objectiveLabel;

        [FoldoutGroup("Base/Base/HP UI")]
        [SerializeField] private UIHPPlayer _uihpPlayer;

        [FoldoutGroup("Base/Base/Crosshair")]
        [SerializeField] private UICrosshair _crosshair;

        [FoldoutGroup("Base/Base/Kill feed")]
        [SerializeField] private KillFeedView _killFeed;

        [FoldoutGroup("Base/Base/Scoreboard (giữ phím để xem)")]
        [SerializeField] private UIScoreBoard _scoreboard;              // cả bảng xếp hạng
        
        [FoldoutGroup("Base/Base/Chat Box")]
        [SerializeField] private UIChatBox _uiChatBox;
        
        [FoldoutGroup("Base/Base/UI Loadout")] 
        [SerializeField] private SelectLoadoutController _loadout;
        
        [FoldoutGroup("Base/Base/UI Drag Item")] 
        [SerializeField] private GameObject _dragItemImage;

        // public NPlayer nPlayer;
        //
        // private void Awake()
        // {
        //     nPlayer = GetComponentInParent<NPlayer>();
        // }
        

        // ---- Các setter dùng chung ----
        
        // Cập nhật đồng hồ từ số giây còn lại (định dạng mm:ss).
        public void SetTimer(float secondsRemaining)
        {
            if (_timerLabel == null) return;
            int t = Mathf.Max(0, Mathf.FloorToInt(secondsRemaining));
            _timerLabel.text = $"{t / 60:00}:{t % 60:00}";
        }

        // ========================== WEAPON CTRL ===============================
        
        // Cập nhật tên súng + số đạn (trong băng / dự trữ).
        public void SetBullet(int currentBullet, int maxBullet)
            => _weaponcontroller?.SetBullet(currentBullet, maxBullet);
        
        public void SetWeaponMode(GunRecoilController.FiringMode mode)
            => _weaponcontroller?.SetWeaponMode(mode);
        
        public void SwitchWeaponUI(WeaponController ctrl)
            => _weaponcontroller?.SetWeaponActive(ctrl);
        
        public void DisplayPBindingKeyMap()
            => _weaponcontroller?.DisplayPBindingKeyMap();

        // ========================== CROSSHAIR =================================
        
        public void SetCenterVisible(bool visible)
            => _crosshair?.HideCenter(visible);
        
        public void SetSizeCrosshair(float width, float height)
            => _crosshair?.SetSize(width, height);
        
        public void SetGapCrosshair(float gap)
            => _crosshair?.SetGap(gap);
        
        public void SetColorCrosshair(Color color)
            => _crosshair?.SetColor(color);
        
        public void SetCrosshairVisible(bool visible)
            => _crosshair?.SetADS(visible);
        
        public void CrosshairHit() => _crosshair?.Hit();
        
        // ============================ UI HP ======================================

        public void InitHPStart(int HP, PlayerData data,  CharComponentManager componentManager)
            => _uihpPlayer?.Init(HP, data, componentManager);
        
        public void SetHP(int newHP)
            => _uihpPlayer?.SetHP(newHP);
        
        public void SetDead(bool isDead)
            => _uihpPlayer?.SetDead(isDead);

        public void Flash(float distance, float radius)
            => _uihpPlayer.Flash(distance, radius);

        public void SetDeadStatus(string killer, string weaponName)
            => _uihpPlayer?.SetDeadStatus(killer, weaponName);
        
        public void SetRecoverTime(int time)
            => _uihpPlayer?.SetRecoverTime(time);
        
        // ========================== KILL IN GAME ======================================
        
        
        // Thêm một dòng vào kill feed.
        public void AddKill(string killer, string weaponname, string victim)
        {
            bool isLocal = false;
            if (NPlayer.Local.Nickname.Contains(killer)) isLocal = true;
            if (_killFeed != null) _killFeed.AddKill(killer, weaponname, victim, isLocal);
        }
        
        // ========================== SCORE BOARD ======================================
        
        // Bật/tắt scoreboard thủ công (ví dụ để test hoặc gọi từ menu).
        public void SetScoreboardVisible(bool visible)
        {
            if (_scoreboard != null) _scoreboard.gameObject.SetActive(visible);
        }
        
        // ========================== CHAT BOX =========================================
        
        public string GetMessage() => _uiChatBox?.GetMessage();

        public void SendChatMessage(string message)
            => _uiChatBox?.SendChatMessage(message);

        public bool SendMessageVisible(bool visible)
        {
            if (_uiChatBox != null)
                return _uiChatBox.SendMessageVisible(visible);
            return false;
        }
        
        public void SetBaloOne(string weapponOne, string weapponTwo)
            => _loadout.SetBaloOne(weapponOne, weapponTwo);
        
        public void SetBaloTwo(string weapponOne, string weapponTwo)
            => _loadout.SetBaloTwo(weapponOne, weapponTwo);
        
        public void SetBaloThree(string weapponOne, string weapponTwo)
            => _loadout.SetBaloThree(weapponOne, weapponTwo);
        
        // ============================ ALL ======================================
        
        // Đặt mục tiêu số mạng cần đạt để thắng.
        public virtual void SetKillTarget(int target) { }
        
        // Đặt tiêu đề scoreboard (tên chế độ).
        public virtual void SetScoreboardTitle(string title) { }

        public void SetDragItemVisible(bool visible, DropItemWeapon item)
        {
            _dragItemImage?.gameObject.SetActive(visible);
            var gm = GameManager.Instance;
            if (gm == null) return;
            gm.SetCanDragItem(visible);
            gm.SetDropItemWeapon(item);
        }
        
        // =========================== SOLO ======================================
        
        // Đặt hạng cá nhân (kèm tiền tố, ví dụ "#2").
        public virtual void SetRank(int rank){}
        
        // Đặt số mạng cá nhân.
        public virtual void SetKills(int kills) { }

        // Đặt dòng mục tiêu / thời gian trên scoreboard.
        public virtual void SetScoreboardObjective(string objective){ }

        // Đổ danh sách người chơi vào các dòng (từ trên xuống); dòng thừa sẽ bị ẩn.
        public virtual void SetScoreboardEntries(IList<ScoreEntry> entries) { }
        
        // =========================== TEAM ======================================
        
        // Cập nhật tên + số mạng đội 1 (và độ đầy thanh điểm theo _killTarget).
        public virtual void SetTeam1(string name, int kills) { }

        // Cập nhật tên + số mạng đội 2.
        public virtual void SetTeam2(string name, int kills) { }
        
        // Đặt tên + điểm + danh sách người chơi cho đội Alpha (đội 1).
        public virtual void SetScoreboardAlpha(string teamName, int teamScore, IList<ScoreEntry> rows) { }

        // Đặt tên + điểm + danh sách người chơi cho đội Bravo (đội 2).
        public virtual void SetScoreboardBravo(string teamName, int teamScore, IList<ScoreEntry> rows) { }
    }
}
