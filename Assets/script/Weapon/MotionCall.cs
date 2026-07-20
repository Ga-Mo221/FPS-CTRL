using UnityEngine;

public class MotionCall : StateMachineBehaviour
{
    public void Switch(Animator animator)
    {
        var controller = animator.GetComponentInParent<WeaponController>();

        if (controller == null)
            return;

        controller.Manager.SwitchWeapon();
    }
}