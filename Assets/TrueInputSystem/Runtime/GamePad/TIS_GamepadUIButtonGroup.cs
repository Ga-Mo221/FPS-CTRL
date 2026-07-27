using System.Collections.Generic;
using UnityEngine;

namespace TrueInputSystem
{
    [AddComponentMenu("TIS/Demo/TIS Gamepad UI Button Group")]
    public class TISGamepadUIButtonGroup : MonoBehaviour
    {
        public enum GroupLayoutType
        {
            VerticalList,
            HorizontalRow,
            Grid
        }

        /// <summary>
        /// Chế độ wrap-around theo chiều dọc dành riêng cho Grid.
        /// - ColumnWrap : Mỗi cột tự wrap độc lập (nút đầu cột ↑ → nút cuối cột đó).
        /// - GlobalWrap : Left/Right tuyến tính qua hàng; Up/Down wrap trong cùng cột.
        /// </summary>
        public enum GridVerticalWrapMode
        {
            ColumnWrap,
            GlobalWrap
        }

        [Tooltip("Chọn kiểu sắp xếp hiển thị của nhóm Button này.")]
        public GroupLayoutType layoutType = GroupLayoutType.VerticalList;

        [Tooltip("Số lượng cột trong lưới Button (Chỉ có tác dụng khi chọn kiểu Grid).")] [Min(1)]
        public int gridColumns = 3;

        [Tooltip("Chế độ wrap dọc cho Grid:\n" +
                 "• Column Wrap – mỗi cột tự wrap riêng (nút đầu cột ↑ → nút cuối cột đó).\n" +
                 "• Global Wrap – Left/Right tuyến tính qua hàng; Up/Down wrap trong cùng cột.")]
        public GridVerticalWrapMode gridVerticalWrap = GridVerticalWrapMode.ColumnWrap;

        /// <summary>
        /// Hàm tự động tính toán và gán các mục tiêu Navigation dựa trên cấu trúc hàng/cột được chọn.
        /// </summary>
        public void RebuildNavigation()
        {
            List<TISGamepadUIButton> buttons = new List<TISGamepadUIButton>();
            foreach (Transform child in transform)
            {
                TISGamepadUIButton btn = child.GetComponent<TISGamepadUIButton>();
                if (btn != null && btn.gameObject.activeSelf)
                    buttons.Add(btn);
            }

            int total = buttons.Count;
            if (total == 0) return;

            foreach (var btn in buttons)
            {
                btn.navLeft = null;
                btn.navRight = null;
                btn.navUp = null;
                btn.navDown = null;
            }

            int cols = 1;
            if (layoutType == GroupLayoutType.HorizontalRow)
                cols = total;
            else if (layoutType == GroupLayoutType.Grid)
                cols = Mathf.Max(1, gridColumns);

            int rows = Mathf.CeilToInt((float)total / cols);

            for (int i = 0; i < total; i++)
            {
                int row = i / cols;
                int col = i % cols;

                TISGamepadUIButton current = buttons[i];

                // ── Điều hướng NGANG (HorizontalRow và Grid) ─────────────────────
                if (layoutType == GroupLayoutType.HorizontalRow || layoutType == GroupLayoutType.Grid)
                {
                    bool isGlobalWrap = layoutType == GroupLayoutType.Grid
                                        && gridVerticalWrap == GridVerticalWrapMode.GlobalWrap;

                    if (isGlobalWrap)
                    {
                        current.navRight = (i + 1 < total) ? buttons[i + 1] : buttons[0];
                        current.navLeft = (i - 1 >= 0) ? buttons[i - 1] : buttons[total - 1];
                    }
                    else
                    {
                        int rowStart = row * cols;
                        int rowEnd = Mathf.Min(rowStart + cols, total) - 1;

                        current.navLeft = (col > 0) ? buttons[i - 1] : buttons[rowEnd];

                        bool isRowEnd = (col == cols - 1) || (i == rowEnd);
                        current.navRight = !isRowEnd ? buttons[i + 1] : buttons[rowStart];
                    }
                }

                // ── Điều hướng DỌC (VerticalList và Grid) ────────────────────────
                if (layoutType == GroupLayoutType.VerticalList || layoutType == GroupLayoutType.Grid)
                {
                    if (layoutType == GroupLayoutType.VerticalList)
                    {
                        current.navUp = (row > 0) ? buttons[i - 1] : buttons[total - 1];
                        current.navDown = (i + 1 < total) ? buttons[i + 1] : buttons[0];
                    }
                    else
                    {
                        // Cả ColumnWrap lẫn GlobalWrap đều wrap Up/Down trong cùng cột
                        int prevRowIndex = (row - 1) * cols + col;
                        int nextRowIndex = (row + 1) * cols + col;

                        current.navUp = (row > 0)
                            ? buttons[prevRowIndex]
                            : GetLastInColumn(buttons, total, col, cols, rows);

                        current.navDown = (nextRowIndex < total)
                            ? buttons[nextRowIndex]
                            : buttons[col];
                    }
                }
            }
        }

        private TISGamepadUIButton GetLastInColumn(List<TISGamepadUIButton> buttons, int total, int col, int cols,
            int rows)
        {
            for (int r = rows - 1; r >= 0; r--)
            {
                int idx = r * cols + col;
                if (idx < total)
                    return buttons[idx];
            }

            return buttons[col];
        }
    }
}