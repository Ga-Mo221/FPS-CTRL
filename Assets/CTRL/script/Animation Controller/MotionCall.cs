using UnityEngine;
using WebSocketSharp;

public class MotionCall : StateMachineBehaviour
{
    // Char
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

    public void StopSwitch(Animator animator)
    {
        var controller = animator.GetComponentInParent<NCharMovementHandler>();
        controller?.StopSwitch(); // Host tự gọi được, client bị guard bởi HasStateAuthority

        // Client reset local animation state
        var animHandler = animator.GetComponentInParent<NCharacterAnimationHandler>();
        animHandler?.LocalStopSwitch();
    }

    public void DisplayFrag(Animator animator)
    {
        var dis = animator.GetComponent<DisplayObject>();
        if (dis == null) return;
        dis.EnableObj(0);
    }

    public void DisplayFlash(Animator animator)
    {
        var dis = animator.GetComponent<DisplayObject>();
        if (dis == null) return;
        dis.EnableObj(1);
    }

    public void HideFrag(Animator animator)
    {
        var dis = animator.GetComponent<DisplayObject>();
        var shoot = animator.GetComponentInParent<NShootHandler>();
        if (shoot != null)
            shoot.ThrowGrenade(true);
        if (dis == null) return;
        dis.DisableObj(0);
    }

    public void HideFlash(Animator animator)
    {
        var dis = animator.GetComponent<DisplayObject>();
        var shoot = animator.GetComponentInParent<NShootHandler>();
        if (shoot != null)
            shoot.ThrowGrenade(false);
        if (dis == null) return;
        dis.DisableObj(1);
    }

    // Char
    public void ResetReload(Animator animator)
    {
        var controller = animator.GetComponentInParent<NCharMovementHandler>();
        var weaponManager = animator.GetComponentInParent<WeaponManager>();
        
        if (weaponManager != null && weaponManager.Weapon_Active() != null)
            if (weaponManager.Weapon_Active().Reload())
                controller?.StopReload();
    }

    public void DeadVFX(Animator animator)
    {
        var Disso = animator.GetComponent<DissolvingController>();
        var weaponManager = animator.GetComponentInParent<WeaponManager>();
        
        if (Disso == null) return;
        Disso.Dissolve();
        weaponManager?.HideWeaponActive();
    }

    public void ResetDeadVFX(Animator animator)
    {
        var Disso = animator.GetComponent<DissolvingController>();
        
        if (Disso == null) return;
        Disso.ResetMaterials();
    }

    public void MeleeAttackCouldown(Animator animator)
    {
        animator.ResetTrigger("IsShooting");
        var controller = animator.GetComponentInParent<NShootHandler>();
        controller?.TriggerMeleeAttack();
        
        var movement = animator.GetComponentInParent<NCharMovementHandler>();
        movement?.StartCoolDownCombat();
    }

    public void MeleeAttackTrigger(Animator animator)
    {
        var controller = animator.GetComponentInParent<NShootHandler>();
        controller?.OnMeleeHitAnimEvent();
    }

    public void ResetRightPuchMelee(Animator animator)
    {
        var controller = animator.GetComponentInParent<NCharMovementHandler>();
        controller?.ResetRightPuch();
    }

    public void RessetTrigger(Animator animator, string triggerName)
    {
        if (triggerName.IsNullOrEmpty()) return;
        animator.ResetTrigger(triggerName);
    }

    public void RessetCanAttackMelee(Animator animator)
    {
        var controller = animator.GetComponentInParent<NCharMovementHandler>();
        if (controller != null)
            controller.ResetMeleeCombo();
    }

    // ===================================== WEAPON =========================================================
    
    // Gun
    public void AudioWeaponMagazine(Animator animator)
    {
        var controller = animator.GetComponentInParent<WeaponController>();

        if (controller == null)
            return;
        
        AudioManager.Instance.PlayClip(controller.Sound_Slider, 
            controller.Data.MagazineInClip,
            controller.Data.Volume_Magazine, true, 0.5f, 1f);
    }
    
    // Gun
    public void AudioWeaponSlide(Animator animator)
    {
        var controller = animator.GetComponentInParent<WeaponController>();

        if (controller == null)
            return;
        
        AudioManager.Instance.PlayClip(controller.Sound_Slider, 
            controller.Data.SlideClip,
            controller.Data.Volume_Slide, true, 0.5f, 1f);
    }
    
    public void AudioWeaponShoot(Animator animator, int index)
    {
        var controller = animator.GetComponentInParent<WeaponController>();

        if (controller == null)
            return;
        
        if (index == 0)
            AudioManager.Instance.PlayClip(controller.Sound_Slider, 
                controller.Data.FireClip,
                controller.Data.Volume_Fire, 0, 3);
        else
            AudioManager.Instance.PlayClip(controller.Sound_Slider, 
                controller.Data.FireClip,
                controller.Data.Volume_Fire, index, index);
    }
    
    public void AudioWeaponSlider(Animator animator, int index)
    {
        var controller = animator.GetComponentInParent<WeaponController>();

        if (controller == null)
            return;
        
        if (index == 0)
            AudioManager.Instance.PlayClip(controller.Sound_Slider, 
                controller.Data.SlideClip,
                controller.Data.Volume_Slide, 0, 3);
        else
            AudioManager.Instance.PlayClip(controller.Sound_Slider, 
                controller.Data.SlideClip,
                controller.Data.Volume_Slide, index-1, index-1);
    }
    
    public void AudioWeaponMagazine(Animator animator, int index)
    {
        var controller = animator.GetComponentInParent<WeaponController>();

        if (controller == null)
            return;
        
        if (index == 0)
            AudioManager.Instance.PlayClip(controller.Sound_Slider, 
                controller.Data.MagazineInClip,
                controller.Data.Volume_Magazine, 0, 3);
        else
            AudioManager.Instance.PlayClip(controller.Sound_Slider, 
                controller.Data.MagazineInClip,
                controller.Data.Volume_Magazine, index-1, index-1);
    }
    
    // Gun
    public void SetStateWeaponCollision(Animator animator, bool state)
    {
        var component = animator.GetComponentInParent<WeaponCollisionResolver>();
        if (component == null) return;
        component.SetState(state);
    }
    
    // Gun
    public void DropBullet(Animator animator)
    {
        var controller = animator.GetComponentInParent<WeaponController>();
        controller?.DropCaseBullet();
    }
}