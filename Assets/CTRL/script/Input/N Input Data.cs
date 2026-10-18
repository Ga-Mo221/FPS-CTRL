using Fusion;
using UnityEngine;
using UnityEngine.EventSystems;

// [NET-OPT 2026-10-18 Aemy] (bao cao 9.6) Nen input 36B -> 16B moi tick (gui lien tuc tu moi client).
// Fusion weaver cap phat MOI FIELD 1 word (4 byte) nen phai GOP 2 gia tri 16-bit vao 1 field 32-bit.
// Chi FIELD duoc serialize; code cu van dung .MoveDirection / .LookDelta / .AimForward (nay la property) nen khong doi.
// Client predict doc lai dung gia tri DA luong tu hoa -> khong lech voi host.
public struct NInputData : INetworkInput
{
    public int  MovePacked;   // x: bit 0-7, y: bit 8-15 (sbyte -127..127)
    public uint LookPacked;   // x: half-float bit 0-15, y: half-float bit 16-31
    public uint AimPacked;    // yaw: bit 0-15, pitch: bit 16-31 (0..65535 ~ 0.0055 do)
    public NetworkButtons Buttons; // dùng bitmask thay vì nhiều NetworkBool (ZoomDelta -> nut ScrollSwitch)

    public Vector2 MoveDirection
    {
        get
        {
            sbyte x = unchecked((sbyte)(MovePacked & 0xFF));
            sbyte y = unchecked((sbyte)((MovePacked >> 8) & 0xFF));
            return new Vector2(x / 127f, y / 127f);
        }
        set
        {
            int x = Mathf.RoundToInt(Mathf.Clamp(value.x, -1f, 1f) * 127f);
            int y = Mathf.RoundToInt(Mathf.Clamp(value.y, -1f, 1f) * 127f);
            MovePacked = (x & 0xFF) | ((y & 0xFF) << 8);
        }
    }

    public Vector2 LookDelta
    {
        get { return new Vector2(Mathf.HalfToFloat((ushort)(LookPacked & 0xFFFF)), Mathf.HalfToFloat((ushort)(LookPacked >> 16))); }
        set
        {
            uint x = Mathf.FloatToHalf(Mathf.Clamp(value.x, -60000f, 60000f));
            uint y = Mathf.FloatToHalf(Mathf.Clamp(value.y, -60000f, 60000f));
            LookPacked = x | (y << 16);
        }
    }

    public Vector3 AimForward
    {
        get
        {
            if (AimPacked == 0) return Vector3.zero; // chua co input (giu hanh vi cu: Vector3.zero)
            float yaw = (AimPacked & 0xFFFF) / 65535f * 360f;
            float pitch = (AimPacked >> 16) / 65535f * 180f - 90f;
            return Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
        }
        set
        {
            if (value.sqrMagnitude < 1e-8f) { AimPacked = 0; return; }
            value.Normalize();
            float yaw = Mathf.Atan2(value.x, value.z) * Mathf.Rad2Deg;
            if (yaw < 0f) yaw += 360f;
            float pitch = -Mathf.Asin(Mathf.Clamp(value.y, -1f, 1f)) * Mathf.Rad2Deg;
            uint y16 = (uint)Mathf.Clamp(Mathf.RoundToInt(yaw / 360f * 65535f), 0, 65535);
            uint p16 = (uint)Mathf.Clamp(Mathf.RoundToInt((pitch + 90f) / 180f * 65535f), 1, 65535); // >=1 de khac "khong co input"
            AimPacked = y16 | (p16 << 16);
        }
    }
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
    public const int ScrollSwitch = 12; // [NET-OPT 2026-10-18 Aemy] thay ZoomDelta (float) — chi dung "!= 0" de doi sung
}

// [NET-OPT 2026-10-18 Aemy] (bao cao 2.3) 8 NetworkBool (8 word) -> 1 byte bitmask. Code cu "s.IsRun = true" van chay
// vi cac co gio la property; chi field Bits duoc serialize.
[System.Serializable]
public struct PlayerStateFlags : INetworkStruct
{
    public byte Bits;
    private const byte F_ADS = 1, F_RUN = 2, F_SPRINT = 4, F_CROUCH = 8, F_SLIDE = 16, F_RELOAD = 32, F_SWITCH = 64, F_THROW = 128;
    private bool Get(byte f) { return (Bits & f) != 0; }
    private void Set(byte f, bool v) { if (v) Bits |= f; else Bits &= (byte)~f; }

    public bool IsADS       { get { return Get(F_ADS); }    set { Set(F_ADS, value); } }
    public bool IsRun       { get { return Get(F_RUN); }    set { Set(F_RUN, value); } }
    public bool IsSprint    { get { return Get(F_SPRINT); } set { Set(F_SPRINT, value); } }
    public bool IsCrouch    { get { return Get(F_CROUCH); } set { Set(F_CROUCH, value); } }
    public bool IsSlide     { get { return Get(F_SLIDE); }  set { Set(F_SLIDE, value); } }
    public bool IsReloading { get { return Get(F_RELOAD); } set { Set(F_RELOAD, value); } }
    public bool IsSwitching { get { return Get(F_SWITCH); } set { Set(F_SWITCH, value); } }
    public bool IsThrow     { get { return Get(F_THROW); }  set { Set(F_THROW, value); } }
    
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