using System;

/// <summary>
/// Su kien kill CHI CUA LOCAL PLAYER (chi chay tren may nguoi giet, khong RPC, khong bien networked rieng).
/// Nguon duy nhat: NInGameMessagesHandler (headshot, tu RPC kill feed co san) + NShootHandler (OnKillChange).
/// Nguoi nghe: GameplayAudio (announcer), DisplayKillState (thong bao giua man hinh)...
/// </summary>
public static class LocalKillEvents
{
    /// <summary>Local player vua ha dich bang headshot.</summary>
    public static event Action Headshot;

    /// <summary>So kill cua local player vua tang; tham so = chuoi kill hien tai (reset khi chet).</summary>
    public static event Action<int> Kill;

    public static void RaiseHeadshot() => Headshot?.Invoke();

    public static void RaiseKill(int streak) => Kill?.Invoke(streak);
}
