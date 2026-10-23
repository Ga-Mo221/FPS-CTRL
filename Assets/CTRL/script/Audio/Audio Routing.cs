using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Đưa AudioSource vào đúng group của AudioMixer chung (Master / Effects / Announcer / Music / UI) để chịu
/// âm lượng trong Cài đặt. AudioSource KHÔNG có outputAudioMixerGroup sẽ đi thẳng ra loa và bỏ qua mọi
/// thanh âm lượng (kể cả Âm lượng tổng) — prefab đã gắn sẵn group, lớp này là lưới an toàn cho nguồn tạo
/// lúc chạy hoặc prefab mới quên gắn.
/// </summary>
public static class AudioRouting
{
    private static AudioMixer _mixer;
    private static AudioMixerGroup _effects, _announcer, _master;

    private static AudioMixer Mixer
    {
        get
        {
            if (_mixer != null) return _mixer;
            if (SettingManager.Instance != null && SettingManager.Instance.Mixer != null) _mixer = SettingManager.Instance.Mixer;
            else
            {
                // Mở thẳng map (không qua menu) vẫn có mixer: thư viện âm UI trỏ cùng mixer.
                var lib = Resources.Load<Game.Audio.UISfxLibrary>("UISfxLibrary");
                if (lib != null && lib.uiGroup != null) _mixer = lib.uiGroup.audioMixer;
            }
            return _mixer;
        }
    }

    private static AudioMixerGroup Group(ref AudioMixerGroup cache, string name)
    {
        if (cache != null) return cache;
        var m = Mixer;
        if (m == null) return null;
        var found = m.FindMatchingGroups(name);
        foreach (var g in found) if (g.name == name) { cache = g; break; }
        return cache;
    }

    public static AudioMixerGroup Effects => Group(ref _effects, "Effects");
    public static AudioMixerGroup Announcer => Group(ref _announcer, "Announcer");
    public static AudioMixerGroup Master => Group(ref _master, "Master");

    /// <summary>Nguồn chưa có group → Effects (súng, lựu đạn, bước chân, trúng đạn...). Nguồn đã có group thì giữ nguyên.</summary>
    public static void EnsureEffects(AudioSource source)
    {
        if (source != null && source.outputAudioMixerGroup == null) source.outputAudioMixerGroup = Effects;
    }
}
