using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using GaMo.TrueInputSystem;

namespace Game.UI
{
    /// <summary>
    /// Bao dam LUON co 1 TISGamepadUIButton duoc focus khi nguoi choi dung tay cam/arrow.
    /// Khac phuc goc: chuoi focus cua TIS mong manh (chuot xoa selection, selection tro vao button panel da an,
    /// EventSystem chua san luc set) -> khong button nao duoc chon -> bam tay cam vo tac dung.
    /// Dat len GOC cua menu (MenuShell). Chi set focus khi KHONG co button hop le dang chon -> khong dung voi TIS.
    /// </summary>
    public class MenuGamepadFocusGuard : MonoBehaviour
    {
        [Tooltip("Chi tac dong khi co input tay cam/arrow (tranh gianh focus voi chuot).")]
        [SerializeField] private bool _onlyWhenNavInput = true;

        private void OnEnable()  { StartCoroutine(InitFocus()); }

        // Trễ vài frame cho EventSystem + panel active xong rồi set focus ban đầu (button mặc định được chọn sẵn).
        private System.Collections.IEnumerator InitFocus()
        {
            yield return null; yield return null;
            Reassert();
        }

private void Update()
        {
            // overlay dang mo -> khong gianh focus; controller-first -> chi khi dang dung tay cam.
            if (TISGamepadUIButton.ExternalSuppress > 0) return;
            var _br = TISInputBridge.Instance; if (_br != null && !_br.IsGamepad) return;

            if (_onlyWhenNavInput && !HasNavInput()) return;

            var es = EventSystem.current;
            if (es == null) return;

            var cur = es.currentSelectedGameObject;
            if (IsValidFocus(cur)) return;   // da co button hop le -> de yen

            Reassert();
        }

        private void Reassert()
        {
            var es = EventSystem.current;
            if (es == null) return;
            var pick = PickDefault();
            if (pick != null) es.SetSelectedGameObject(pick.gameObject);
        }

        private bool IsValidFocus(GameObject go)
        {
            if (go == null || !go.activeInHierarchy) return false;
            var b = go.GetComponent<TISGamepadUIButton>();
            // Bo dieu kien interactable: o "khoa"/"da so huu" (interactable=false) van la cho dung hop le
            // cho tay cam (di qua duoc). Truoc day guard keo focus di moi lan bam D-pad -> ket.
            return b != null && b.enabled;
        }

        // Chon button mac dinh cua panel DANG HIEN: uu tien isFirstSelected, roi button active dau tien.
        private TISGamepadUIButton PickDefault()
        {
            var all = GetComponentsInChildren<TISGamepadUIButton>(false); // chi active
            TISGamepadUIButton first = null;
            foreach (var b in all)
            {
                if (b == null || !b.isActiveAndEnabled || !b.interactable) continue;
                if (b.isFirstSelected) return b;
                if (first == null) first = b;
            }
            return first;
        }

        private bool HasNavInput()
        {
            var gp = Gamepad.current;
            if (gp != null && (gp.dpad.ReadValue().sqrMagnitude > 0.25f || gp.leftStick.ReadValue().sqrMagnitude > 0.25f)) return true;
            var kb = Keyboard.current;
            if (kb != null && (kb.upArrowKey.isPressed || kb.downArrowKey.isPressed || kb.leftArrowKey.isPressed || kb.rightArrowKey.isPressed
                              || kb.wKey.isPressed || kb.sKey.isPressed || kb.aKey.isPressed || kb.dKey.isPressed)) return true;
            return false;
        }
    }
}
