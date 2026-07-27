using UnityEngine;

public class MotionCall : StateMachineBehaviour
{
    public void Switch(Animator animator)
    {
    
        var Manager = animator.GetComponentInParent<NWeaponManager>();

        if (Manager  == null)
        {
            Debug.LogWarning("WeaponManager not found in parent of animator.");
            return;
        }

        Manager.SwitchWeapon();
    }

    public void ResetReload(Animator animator)
    {
        var controller = animator.GetComponentInParent<NCharMovementHandler>();

        if (controller == null)
            return;

        controller.StopReload();
    }

    public void WeaponMagazine(Animator animator)
    {
        var controller = animator.GetComponentInParent<WeaponController>();

        if (controller == null)
            return;
        
        AudioManager.Instance.PlayClip(controller.Sound, 
            controller.Data.MagazineInClip,
            controller.Data.Volume_Magazine);
    }
    
    public void WeaponSlide(Animator animator)
    {
        var controller = animator.GetComponentInParent<WeaponController>();

        if (controller == null)
            return;
        
        AudioManager.Instance.PlayClip(controller.Sound, 
            controller.Data.SlideClip,
            controller.Data.Volume_Slide);
    }
}