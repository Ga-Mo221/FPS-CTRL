using UnityEngine;

public class UIPlayerNameBillboard : MonoBehaviour
{
    private Camera targetCamera;

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            if (NPlayer.Local != null)
                targetCamera = NPlayer.Local.ComponentManager.Camera;
            return;
        }

        // ✅ World Space Canvas dùng forward ngược lại vì canvas render mặt trước
        transform.rotation = Quaternion.LookRotation(
            transform.position - targetCamera.transform.position
        );
    }
}