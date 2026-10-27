// ============================================================
//  TrueInputSystem Tutorials — TISTutMobileHud.cs
//
//  Bộ nút cảm ứng dựng sẵn trong scene Mobile (Editor builder tạo):
//    Nhảy · Tấn công · Lướt (có hồi chiêu) · Cầu lửa (kỹ năng định hướng + vùng huỷ)
//  Mỗi bài học chỉ bật những nút mình cần.
// ============================================================

using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [AddComponentMenu("True Input System/Tutorials/TIS Tut Mobile HUD")]
    public class TISTutMobileHud : MonoBehaviour
    {
        public TISMobileSkillButton      jump;
        public TISMobileSkillButton      attack;
        public TISMobileSkillButton      dash;
        public TISDirectionalSkillButton fireball;

        static TISTutMobileHud _instance;
        public static TISTutMobileHud Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<TISTutMobileHud>(FindObjectsInactive.Include);
                return _instance;
            }
        }

        /// <summary>Chỉ bật các nút được yêu cầu, tắt phần còn lại.</summary>
        public void Show(bool showJump = false, bool showAttack = false, bool showDash = false, bool showFireball = false)
        {
            gameObject.SetActive(true);
            Set(jump, showJump);
            Set(attack, showAttack);
            Set(dash, showDash);
            Set(fireball, showFireball);
        }

        public void HideAll() => Show();

        /// <summary>Ẩn / hiện cả HUD (vd. khi người chơi cầm tay cầm Bluetooth).</summary>
        public void SetVisible(bool visible)
        {
            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.alpha = visible ? 1f : 0f;
            cg.blocksRaycasts = visible;
        }

        static void Set(Component c, bool on)
        {
            if (c != null && c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
        }
    }
}
