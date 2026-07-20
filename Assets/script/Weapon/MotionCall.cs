using UnityEngine;

public class MotionCall : StateMachineBehaviour
{
    public void Switch(Animator animator)
    {
    
        var Manager = animator.GetComponentInParent<WeaponManager>();

        if (Manager  == null)
        {
            Debug.LogWarning("WeaponManager not found in parent of animator.");
            return;
        }

        Manager.SwitchWeapon();
    }

    public void ResetReload(Animator animator)
    {
        var controller = animator.GetComponentInParent<CharacterHandler>();

        if (controller == null)
            return;

        controller.IsReload = false;
    }
}