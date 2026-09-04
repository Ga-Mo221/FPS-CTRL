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
    public const int Weapon1 = 10;
    public const int Weapon2  = 11;
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
    // ── Pellet 0 (Rifle / Sniper dùng slot này, Shotgun dùng làm pellet đầu) ──
    public Vector3    HitPosition;
    public Vector3    HitNormal;
    public NetworkBool ShowEffect;

    // ── Shotgun pellets 1-7 ───────────────────────────────────────────────────
    // PelletCount = 0  →  Rifle/Sniper (chỉ đọc slot 0)
    // PelletCount = N  →  Shotgun, đọc slot 0..N-1
    public int PelletCount;

    public Vector3    HitPosition1; public Vector3    HitNormal1; public NetworkBool ShowEffect1;
    public Vector3    HitPosition2; public Vector3    HitNormal2; public NetworkBool ShowEffect2;
    public Vector3    HitPosition3; public Vector3    HitNormal3; public NetworkBool ShowEffect3;
    public Vector3    HitPosition4; public Vector3    HitNormal4; public NetworkBool ShowEffect4;
    public Vector3    HitPosition5; public Vector3    HitNormal5; public NetworkBool ShowEffect5;
    public Vector3    HitPosition6; public Vector3    HitNormal6; public NetworkBool ShowEffect6;
    public Vector3    HitPosition7; public Vector3    HitNormal7; public NetworkBool ShowEffect7;

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Lấy HitPosition của pellet thứ i (0-based).</summary>
    public Vector3 GetHitPosition(int i) => i switch
    {
        0 => HitPosition,  1 => HitPosition1, 2 => HitPosition2,
        3 => HitPosition3, 4 => HitPosition4, 5 => HitPosition5,
        6 => HitPosition6, 7 => HitPosition7,
        _ => HitPosition
    };

    /// <summary>Lấy HitNormal của pellet thứ i (0-based).</summary>
    public Vector3 GetHitNormal(int i) => i switch
    {
        0 => HitNormal,  1 => HitNormal1, 2 => HitNormal2,
        3 => HitNormal3, 4 => HitNormal4, 5 => HitNormal5,
        6 => HitNormal6, 7 => HitNormal7,
        _ => HitNormal
    };

    /// <summary>Lấy ShowEffect của pellet thứ i (0-based).</summary>
    public bool GetShowEffect(int i) => i switch
    {
        0 => ShowEffect,  1 => ShowEffect1, 2 => ShowEffect2,
        3 => ShowEffect3, 4 => ShowEffect4, 5 => ShowEffect5,
        6 => ShowEffect6, 7 => ShowEffect7,
        _ => ShowEffect
    };

    /// <summary>
    /// Ghi dữ liệu một pellet vào đúng slot.
    /// Trả về struct đã cập nhật (vì INetworkStruct là value type).
    /// </summary>
    public ShotInfo SetPellet(int i, Vector3 pos, Vector3 normal, bool showEffect)
    {
        switch (i)
        {
            case 0: HitPosition  = pos; HitNormal  = normal; ShowEffect  = showEffect; break;
            case 1: HitPosition1 = pos; HitNormal1 = normal; ShowEffect1 = showEffect; break;
            case 2: HitPosition2 = pos; HitNormal2 = normal; ShowEffect2 = showEffect; break;
            case 3: HitPosition3 = pos; HitNormal3 = normal; ShowEffect3 = showEffect; break;
            case 4: HitPosition4 = pos; HitNormal4 = normal; ShowEffect4 = showEffect; break;
            case 5: HitPosition5 = pos; HitNormal5 = normal; ShowEffect5 = showEffect; break;
            case 6: HitPosition6 = pos; HitNormal6 = normal; ShowEffect6 = showEffect; break;
            case 7: HitPosition7 = pos; HitNormal7 = normal; ShowEffect7 = showEffect; break;
        }
        return this;
    }
}