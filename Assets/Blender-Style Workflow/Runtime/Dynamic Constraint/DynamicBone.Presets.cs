using System.Collections.Generic;
using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    public partial class DynamicBone
    {
        //  PRESET LOGIC
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Tên hiển thị của từng preset (dùng cho Inspector).
        /// </summary>
        public static string GetPresetLabel(ClothPreset preset)
        {
            switch (preset)
            {
                case ClothPreset.SoftFabric: return "Vải Mềm";
                case ClothPreset.Spring: return "Lò Xo";
                case ClothPreset.RigidForm: return "Áo Cứng";
                case ClothPreset.Custom: return "Tùy Chỉnh";
                default: return preset.ToString();
            }
        }

        /// <summary>
        /// Mô tả ngắn gọn hành vi của từng preset (dùng cho Inspector).
        /// </summary>
        public static string GetPresetDescription(ClothPreset preset)
        {
            switch (preset)
            {
                case ClothPreset.SoftFabric:
                    return
                        "Vải mềm: mượt, chảy tự nhiên. Damping thấp, quán tính cao, spring yếu → xương lắc lơ lửng như lụa.";
                case ClothPreset.Spring:
                    return "Lò xo: nảy và đàn hồi nhanh về rest pose. Stiffness cao, damping trung bình → rung và bật.";
                case ClothPreset.RigidForm:
                    return
                        "Áo cứng giữ form: ít dao động, spring rất mạnh → shape gần như không thay đổi khi di chuyển.";
                case ClothPreset.Custom:
                    return "Chế độ tùy chỉnh: tất cả thông số do bạn chỉnh tay, preset không ghi đè.";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Áp preset hiện tại (bỏ qua nếu đang ở chế độ Custom).
        /// </summary>
        public void ApplyCurrentPreset()
        {
            if (clothPreset != ClothPreset.Custom)
                ApplyPreset(clothPreset);
        }

        public void ApplyPreset(ClothPreset preset)
        {
            switch (preset)
            {
                // ── VẢI MỀM ──────────────────────────────────────
                // Mục tiêu: chảy lơ lửng, giật nhẹ, không nảy
                case ClothPreset.SoftFabric:
                    damping = 0.18f; // cản nhẹ → swing dài
                    elasticity = 0.05f; // đàn hồi rất yếu
                    springStiffness = 0.05f; // spring rất mềm
                    springForceMultiplier = 0.5f;
                    inertia = 0.85f; // quán tính lớn → kéo trễ nhiều
                    useRestPoseGravity = false;
                    gravity = new Vector3(0, -400f, 0);
                    enableClothLinks = true; // vải → nối ngang các dải cho liền mặt
                    clothLinkStiffness = 0.8f;
                    clothLinkIterations = 2;
                    break;

                // ── LÒ XO ────────────────────────────────────────
                // Mục tiêu: nảy về rest pose nhanh, rung đàn hồi
                case ClothPreset.Spring:
                    damping = 0.25f; // cản vừa → có một chút nảy
                    elasticity = 0.35f; // đàn hồi khá mạnh
                    springStiffness = 0.55f; // spring mạnh
                    springForceMultiplier = 20f; // nhân lực lớn → bật về mạnh
                    inertia = 0.5f; // quán tính trung bình
                    useRestPoseGravity = true;
                    restPoseGravityBlend = 0.6f;
                    gravity = new Vector3(0, -9.81f, 0);
                    enableClothLinks = false; // lò xo → mỗi chain dao động độc lập
                    break;

                // ── ÁO CỨNG GIỮ FORM ─────────────────────────────
                // Mục tiêu: giữ nguyên shape, rung tối thiểu
                case ClothPreset.RigidForm:
                    damping = 0.6f; // cản rất mạnh → tắt dao động nhanh
                    elasticity = 0.5f; // đàn hồi cao
                    springStiffness = 0.85f; // spring cứng → shape không đổi
                    springForceMultiplier = 3.0f; // nhân lực rất lớn
                    inertia = 0.2f; // quán tính thấp → ít bị giật
                    useRestPoseGravity = true;
                    restPoseGravityBlend = 1.0f; // hoàn toàn giữ rest pose
                    gravity = new Vector3(0, -2f, 0); // gravity yếu hơn
                    enableClothLinks = true; // giữ form → liên kết ngang rất cứng
                    clothLinkStiffness = 1.0f;
                    clothLinkIterations = 3;
                    break;
            }

            // Reinit trong runtime khi đổi preset
            if (Application.isPlaying) InitializeTrees();
        }


    }
}
