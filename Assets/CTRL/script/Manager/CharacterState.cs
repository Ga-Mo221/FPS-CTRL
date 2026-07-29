using UnityEngine;

[System.Serializable]
public class CharacterState
{
    //---------------------------------------------------
    // Movement
    //---------------------------------------------------

    public void StartRun(ref PlayerStateFlags s, ConstraintManager _c_manager)
    {
        s.IsRun = true;
        StopSprint(ref s, _c_manager);
        ExitADS(ref s);
    }

    public void StopRun(ref PlayerStateFlags s)
    {
        s.IsRun = false;
    }

    public void StartSprint(ref PlayerStateFlags s, ConstraintManager _c_manager)
    {
        s.IsSprint = true;
        StopRun(ref s);
        _c_manager?.TurnOffHand(true);
    }

    public void StopSprint(ref PlayerStateFlags s, ConstraintManager _c_manager)
    {
        s.IsSprint = false;
        _c_manager?.TurnOnHand(true);
    }
    
    //---------------------------------------------------
    // ADS
    //---------------------------------------------------

    public void EnterADS(ref PlayerStateFlags s, ConstraintManager _c_manager)
    {
        s.IsADS = true;
        StopMovement(ref s, _c_manager);
        StopReload(ref s);
    }

    public void ExitADS(ref PlayerStateFlags s)
    {
        s.IsADS = false;
    }
    
    //---------------------------------------------------
    // Movement
    //---------------------------------------------------

    public void ToggleShoot(ref PlayerStateFlags s, ConstraintManager _c_manager)
    {
        StopMovement(ref s, _c_manager);
        StopReload(ref s);
    }

    //---------------------------------------------------
    // Reload
    //---------------------------------------------------

    public void StartReload(ref PlayerStateFlags s, ConstraintManager _c_manager)
    {
        if (s.IsThrow || s.IsSwitching)
            return;

        s.IsReloading = true;
        ExitADS(ref s);
        StopMovement(ref s, _c_manager);
    }

    public void StopReload(ref PlayerStateFlags s)
    {
        s.IsReloading = false;
    }

    //---------------------------------------------------
    // Crouch
    //---------------------------------------------------

    public void ToggleCrouch(ref PlayerStateFlags s, ConstraintManager _c_manager)
    {
        if (s.IsSlide)
            return;

        s.IsCrouch = !s.IsCrouch;
        
        StartSlide(ref s, _c_manager);

        if (s.IsCrouch)
        {
            StopMovement(ref s, _c_manager);
        }
    }

    public void StopCrouch(ref PlayerStateFlags s)
    {
        s.IsCrouch = false;
    }

    //---------------------------------------------------
    // Slide
    //---------------------------------------------------

    private void StartSlide(ref PlayerStateFlags s, ConstraintManager _c_manager)
    {
        if (!s.IsSprint)
            return;
        
        s.IsSlide = true;
        StopMovement(ref s, _c_manager);
        _c_manager.TurnOffHand(true);
    }

    public void StopSlide(ref PlayerStateFlags s, ConstraintManager _c_manager)
    {
        s.IsSlide = false;
        _c_manager.TurnOnHand(true);
        //AudioManager.Instance.Stop(_sound);
    }

    //---------------------------------------------------
    // Throw
    //---------------------------------------------------

    public void StartThrow(ref PlayerStateFlags s, ConstraintManager _c_manager)
    {
        s.IsThrow = true;
        ExitADS(ref s);
        StopCombat(ref s);
        StopMovement(ref s, _c_manager);
    }

    public void StopThrow(ref PlayerStateFlags s)
    {
        s.IsThrow = false;
    }

    //---------------------------------------------------
    // Switch
    //---------------------------------------------------

    public void StartSwitch(ref PlayerStateFlags s)
    {
        s.IsSwitching = true;
        StopCombat(ref s);
    }

    public void StopSwitch(ref PlayerStateFlags s)
    {
        s.IsSwitching = false;
    }

    //---------------------------------------------------
    // Jump
    //---------------------------------------------------

    public void Jump(ref PlayerStateFlags s)
    {
        StopCrouch(ref s);
        StopCombat(ref s);
    }

    //---------------------------------------------------
    // Utility
    //---------------------------------------------------

    public void StopMovement(ref PlayerStateFlags s, ConstraintManager _c_manager)
    {
        StopRun(ref s);
        StopSprint(ref s, _c_manager);
    }

    public void StopCombat(ref PlayerStateFlags s)
    {
        StopReload(ref s);
    }
}
