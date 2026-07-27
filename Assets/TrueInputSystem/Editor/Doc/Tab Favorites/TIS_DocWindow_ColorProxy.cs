// ============================================================
//  TrueInputSystem — TIS_DocWindow_ColorProxy.cs
//  Expose palette colors as internal properties so module files
//  (IFavSubTabModule, ICodeSubTabModule implementations) can
//  access them via  win.AccentBlue,  win.AccentGreen, etc.
//
//  Palette gốc (TIS_DocWindow_Palette.cs) GIỮ NGUYÊN private.
//  File này chỉ forward — không định nghĩa màu mới.
// ============================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace TrueInputSystem.Editor
{
    public partial class TISDocWindow : EditorWindow
    {
        // ── Accent ───────────────────────────────────────────
        internal Color C_AccentBlue   => AccentBlue;
        internal Color C_AccentGreen  => AccentGreen;
        internal Color C_AccentAmber  => AccentAmber;
        internal Color C_AccentRed    => AccentRed;
        internal Color C_AccentPurple => AccentPurple;
        internal Color C_AccentCyan   => AccentCyan;
        internal Color C_AccentPink   => AccentPink;

        // ── Background ───────────────────────────────────────
        internal Color C_BgBase      => BgBase;
        internal Color C_BgSurface   => BgSurface;
        internal Color C_BgElevated  => BgElevated;
        internal Color C_BgDeep      => BgDeep;

        // ── Text ─────────────────────────────────────────────
        internal Color C_TxtPrimary  => TxtPrimary;
        internal Color C_TxtSecond   => TxtSecond;
        internal Color C_TxtMuted    => TxtMuted;
    }
}
#endif
