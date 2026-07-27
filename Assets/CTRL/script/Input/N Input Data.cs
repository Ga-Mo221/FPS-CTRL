using Fusion;
using UnityEngine;
using UnityEngine.EventSystems;

public struct NInputData : INetworkInput
{
    public Vector2 MoveDirection;
    public Vector2 LookDelta;
    public float ZoomDelta;
    public Vector3 AimForward;
    
    public NetworkButtons Buttons; // dùng bitmask thay vì nhiều NetworkBool
}

public static class InputButtons
{
    public const int Jump    = 0;
    public const int Run     = 1;   // Sprint toggle
    public const int Crouch  = 2;
    public const int ADS     = 3;
    public const int Attack  = 4;
    public const int Reload  = 5;
    public const int Switch  = 6;
    public const int ThrowFrag  = 7;
    public const int ThrowFlash = 8;
    public const int Mode    = 9;
}

[System.Serializable]
public struct PlayerStateFlags : INetworkStruct
{
    public NetworkBool IsADS;
    public NetworkBool IsRun;
    public NetworkBool IsSprint;
    public NetworkBool IsCrouch;
    public NetworkBool IsSlide;
    public NetworkBool IsReloading;
    public NetworkBool IsSwitching;
    public NetworkBool IsThrow;
    
    public Vector2 Move(Vector2 moveDirection)
    {
        Vector2 move = Vector2.zero;

        if (Mathf.Abs(moveDirection.x) > 0.3f)
        {
            move.x = 0.4f * Mathf.Sign(moveDirection.x);
            if (IsADS || IsThrow) 
                move.x = 0.2f * Mathf.Sign(moveDirection.x);
            if (IsRun && !IsCrouch && moveDirection.y > 0.5f) 
                move.x = 0.6f * Mathf.Sign(moveDirection.x);
        }

        if (Mathf.Abs(moveDirection.y) > 0.3f)
        {
            move.y = 0.4f * Mathf.Sign(moveDirection.y);
            if (IsADS || IsThrow) 
                move.y = 0.2f * Mathf.Sign(moveDirection.y);
            if (IsRun && !IsCrouch && moveDirection.y > 0.5f) 
                move.y = 0.6f;
            if (IsSprint && !IsCrouch && moveDirection.y > 0.88f)
                move.y = 1f;
        }
        
        return move;
    }
}

[System.Serializable]
public struct ShotInfo : INetworkStruct
{
    public Vector3 HitPosition;
    public Vector3 HitNormal;
    public NetworkBool ShowEffect;
}