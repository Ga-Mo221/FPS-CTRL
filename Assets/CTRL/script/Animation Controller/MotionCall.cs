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
            shoot.ThrowFrag();
        if (dis == null) return;
        dis.DisableObj(0);
    }

    public void HideFlash(Animator animator)
    {
        var dis = animator.GetComponent<DisplayObject>();
        var shoot = animator.GetComponentInParent<NShootHandler>();
        if (shoot != null)
            shoot.ThrowFlash();
        if (dis == null) return;
        dis.DisableObj(1);
    }

    // Char
    public void ResetReload(Animator animator)
    {
        var controller = animator.GetComponentInParent<NCharMovementHandler>();
        var weaponManager = animator.GetComponentInParent<WeaponManager>();
        
        controller?.StopReload();
        weaponManager?.Weapon_Active()?.Reload();
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

    public void MeleeAttack(Animator animator)
    {
        animator.ResetTrigger("IsShooting");
        var controller = animator.GetComponentInParent<NShootHandler>();
        controller?.TriggerMeleeAttack();
        
        var movement = animator.GetComponentInParent<NCharMovementHandler>();
        movement?.StartCoolDownCombat();
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

    public void RessetShootIndex(Animator animator)
    {
        var controller = animator.GetComponentInParent<NCharMovementHandler>();
        if (controller != null)
        {
            Debug.Log("reset motion");
            controller.ResetMeleeCombo();
        }
        else
        {
            Debug.Log("reset motion null");
        }
    }

    // ===================================== WEAPON =========================================================
    
    // Gun
    public void WeaponMagazine(Animator animator)
    {
        var controller = animator.GetComponentInParent<WeaponController>();

        if (controller == null)
            return;
        
        AudioManager.Instance.PlayClip(controller.Sound, 
            controller.Data.MagazineInClip,
            controller.Data.Volume_Magazine);
    }
    
    // Gun
    public void WeaponSlide(Animator animator)
    {
        var controller = animator.GetComponentInParent<WeaponController>();

        if (controller == null)
            return;
        
        AudioManager.Instance.PlayClip(controller.Sound, 
            controller.Data.SlideClip,
            controller.Data.Volume_Slide);
    }
    
    // Gun
    public void SetStateWeaponCollision(Animator animator, bool state)
    {
        var component = animator.GetComponentInParent<WeaponCollisionResolver>();
        if (component == null) return;
        component.SetState(state);
    }
}