public static class WeaponID
{
    public const string _9MM_DEMON = "Vesper";
    public const string _M4 = "Umbra";
    public const string _KNIFE = "Knife";
    public const string _REMINGTON = "Comet";
    public const string _M24 = "Dusk";
    public const string _FRAG_M67 = "Frag-M67";
    public const string _FLASH_M84 = "Flash-M84";
    public const string _SMOKE_AN_M8_HC = "Smoke-AN-M8 HC";
}

/// <summary>
/// Bộ trang bị mặc định (giống người chơi mới trên PlayFab: Knife + Vesper, Lựu đạn + Lựu đạn choáng).
/// Dùng cho slot balo bị bỏ trống HOẶC chứa vũ khí không còn tồn tại.
/// </summary>
public static class DefaultLoadout
{
    public const string Weapon_One = WeaponID._KNIFE;
    public const string Weapon_Two = WeaponID._9MM_DEMON;
    public const string Grenade_Q  = WeaponID._FRAG_M67;
    public const string Grenade_E  = WeaponID._FLASH_M84;
}