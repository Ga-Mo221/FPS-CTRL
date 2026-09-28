// ============================================================
//  TrueInputSystem — TIS_DocWindow_Palette.cs
//  Centralized color palette for TISDocWindow (all partial files).
//
//  Usage: reference any property directly from TISDocWindow partial class.
//         All colors are computed properties → they respond to
//         EditorGUIUtility.isProSkin at runtime (Light ↔ Dark skin).
//
//  Dark  = Unity Pro / Dark skin  (isProSkin == true)
//          → Neutral grey ramp matching Unity Editor's own chrome.
//            BgBase = #383838 (Unity main window bg)
//            BgSurface = #3C3C3C (panel/inspector bg)
//            BgElevated = #4A4A4A (toolbar / header)
//            BgDeep = #2B2B2B (sidebar / deepest trough)
//  Light = Unity Personal / Light skin
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GaMo.TrueInputSystem.Editor
{
    public partial class TISDocWindow : EditorWindow
    {
        // ══════════════════════════════════════════════════════
        //  SKIN HELPER
        // ══════════════════════════════════════════════════════

        private static bool Dark => EditorGUIUtility.isProSkin;

        // ══════════════════════════════════════════════════════
        //  BACKGROUND TONES
        //  Dark : neutral grey ramp — matches Unity Editor chrome
        //         #2B2B2B → #383838 → #3C3C3C → #4A4A4A
        //  Light: Unity's standard grey ramp (#C8C8C8 base)
        // ══════════════════════════════════════════════════════

        private static Color BgDeep     => Dark ? new Color(0.169f, 0.169f, 0.169f)  // #2B2B2B  sidebar trough
                                                 : new Color(0.761f, 0.761f, 0.761f); // #C2C2C2

        private static Color BgBase     => Dark ? new Color(0.220f, 0.220f, 0.220f)  // #383838  main window bg
                                                 : new Color(0.800f, 0.800f, 0.800f); // #CCCCCC

        private static Color BgSurface  => Dark ? new Color(0.235f, 0.235f, 0.235f)  // #3C3C3C  panel / inspector
                                                 : new Color(0.839f, 0.839f, 0.839f); // #D6D6D6

        private static Color BgElevated => Dark ? new Color(0.290f, 0.290f, 0.290f)  // #4A4A4A  toolbar / header
                                                 : new Color(0.878f, 0.878f, 0.878f); // #E0E0E0

        // ══════════════════════════════════════════════════════
        //  ACCENT PALETTE
        //  Blue calibrated to Unity's selection / focus colour.
        //  Other accents kept vivid but slightly warmer to sit
        //  well on neutral-grey rather than navy.
        // ══════════════════════════════════════════════════════

        private static Color AccentBlue   => Dark ? new Color(0.298f, 0.494f, 1.000f)  // #4C7EFF  Unity selection blue
                                                   : new Color(0.153f, 0.392f, 0.863f); // #2764DC

        private static Color AccentPurple => Dark ? new Color(0.729f, 0.502f, 0.996f)  // #BA80FE
                                                   : new Color(0.510f, 0.255f, 0.824f); // #8241D2

        private static Color AccentCyan   => Dark ? new Color(0.341f, 0.863f, 0.863f)  // #57DCDC
                                                   : new Color(0.059f, 0.596f, 0.596f); // #0F9898

        private static Color AccentGreen  => Dark ? new Color(0.302f, 0.863f, 0.600f)  // #4DDC99
                                                   : new Color(0.106f, 0.588f, 0.337f); // #1B9656

        private static Color AccentAmber  => Dark ? new Color(0.996f, 0.729f, 0.247f)  // #FEBA3F
                                                   : new Color(0.737f, 0.451f, 0.000f); // #BC7300

        private static Color AccentRed    => Dark ? new Color(0.996f, 0.400f, 0.400f)  // #FE6666
                                                   : new Color(0.780f, 0.141f, 0.141f); // #C72424

        private static Color AccentPink   => Dark ? new Color(0.996f, 0.400f, 0.729f)  // #FE66BA
                                                   : new Color(0.780f, 0.137f, 0.510f); // #C72382

        // ══════════════════════════════════════════════════════
        //  TEXT TONES
        //  Dark : Unity Editor label colours
        //         Primary #EEEEEE · Secondary #B4B4B4 · Muted #888888
        //  Light: near-black on light bg (Unity standard text)
        // ══════════════════════════════════════════════════════

        private static Color TxtPrimary => Dark ? new Color(0.933f, 0.933f, 0.933f)   // #EEEEEE  Unity label text
                                                : new Color(0.063f, 0.063f, 0.063f);  // #101010

        private static Color TxtSecond  => Dark ? new Color(0.706f, 0.706f, 0.706f)   // #B4B4B4  Unity secondary label
                                                : new Color(0.243f, 0.243f, 0.243f);  // #3E3E3E

        private static Color TxtMuted   => Dark ? new Color(0.533f, 0.533f, 0.533f)   // #888888  Unity muted / hint
                                                : new Color(0.431f, 0.431f, 0.431f);  // #6E6E6E

        // ══════════════════════════════════════════════════════
        //  BORDERS
        //  Dark : black overlay — Unity uses dark separators on grey
        //  Light: black overlay (subtle dividers on light)
        // ══════════════════════════════════════════════════════

        private static Color BorderSub   => Dark ? new Color(0f, 0f, 0f, 0.25f)
                                                  : new Color(0f, 0f, 0f, 0.10f);

        private static Color BorderNorm  => Dark ? new Color(0f, 0f, 0f, 0.40f)
                                                  : new Color(0f, 0f, 0f, 0.18f);

        private static Color BorderFocus => Dark ? new Color(0.298f, 0.494f, 1.000f, 0.60f)
                                                  : new Color(0.153f, 0.392f, 0.863f, 0.50f);

        // ══════════════════════════════════════════════════════
        //  SYNTAX HIGHLIGHTING
        //  Token colours unchanged — they read well on both
        //  the old navy CodeBg and the new #1E1E1E CodeBg.
        // ══════════════════════════════════════════════════════

        private static Color SynKeyword  => Dark ? new Color(0.796f, 0.482f, 0.863f)  // #CB7BDC
                                                  : new Color(0.533f, 0.059f, 0.702f); // #880FB3

        private static Color SynType     => Dark ? new Color(0.918f, 0.769f, 0.455f)  // #EAC474
                                                  : new Color(0.576f, 0.380f, 0.000f); // #936100

        private static Color SynString   => Dark ? new Color(0.608f, 0.796f, 0.506f)  // #9BCB81
                                                  : new Color(0.122f, 0.490f, 0.082f); // #1F7D15

        private static Color SynComment  => Dark ? new Color(0.400f, 0.435f, 0.537f)  // #666F89
                                                  : new Color(0.384f, 0.439f, 0.384f); // #627063

        private static Color SynFunction => Dark ? new Color(0.424f, 0.718f, 0.988f)  // #6CB7FC
                                                  : new Color(0.082f, 0.369f, 0.706f); // #155EB4

        private static Color SynNumber   => Dark ? new Color(0.996f, 0.729f, 0.490f)  // #FEBA7D
                                                  : new Color(0.667f, 0.310f, 0.000f); // #AA4F00

        private static Color SynVar      => Dark ? new Color(0.937f, 0.502f, 0.490f)  // #EF807D
                                                  : new Color(0.663f, 0.129f, 0.118f); // #A9211E

        private static Color SynOperator => Dark ? new Color(0.369f, 0.761f, 0.761f)  // #5EC2C2
                                                  : new Color(0.020f, 0.490f, 0.490f); // #057D7D

        private static Color SynPlain    => Dark ? new Color(0.839f, 0.839f, 0.839f)  // #D6D6D6  matches TxtSecond+ on dark
                                                  : new Color(0.118f, 0.118f, 0.118f); // #1E1E1E

        // ══════════════════════════════════════════════════════
        //  CODE BLOCK BACKGROUND
        //  Dark : #1E1E1E — VS Code / Rider dark IDE standard,
        //         noticeably darker than #383838 panel bg so code
        //         blocks are visually distinct without being navy.
        // ══════════════════════════════════════════════════════

        private static Color CodeBg      => Dark ? new Color(0.118f, 0.118f, 0.118f)  // #1E1E1E
                                                  : new Color(0.922f, 0.922f, 0.922f); // #EBEBEB

        // ══════════════════════════════════════════════════════
        //  CALLOUT BACKGROUNDS
        //  Hues calibrated to be readable on #3C3C3C surface.
        //  Saturation reduced vs old navy palette so they don't
        //  pop too hard on the neutral grey base.
        // ══════════════════════════════════════════════════════

        private static Color CalloutNoteBg => Dark ? new Color(0.102f, 0.176f, 0.290f)  // #1A2D4A
                                                    : new Color(0.816f, 0.878f, 0.976f);

        private static Color CalloutTipBg  => Dark ? new Color(0.102f, 0.188f, 0.125f)  // #1A3020
                                                    : new Color(0.804f, 0.929f, 0.863f);

        private static Color CalloutWarnBg => Dark ? new Color(0.239f, 0.169f, 0.051f)  // #3D2B0D
                                                    : new Color(0.996f, 0.941f, 0.800f);

        // ══════════════════════════════════════════════════════
        //  CALLOUT TEXT
        // ══════════════════════════════════════════════════════

        private static Color CalloutNoteTxt => Dark ? new Color(0.686f, 0.812f, 0.996f)
                                                     : new Color(0.082f, 0.255f, 0.600f);

        private static Color CalloutTipTxt  => Dark ? new Color(0.502f, 0.937f, 0.722f)
                                                     : new Color(0.059f, 0.392f, 0.196f);

        private static Color CalloutWarnTxt => Dark ? new Color(0.996f, 0.835f, 0.502f)
                                                     : new Color(0.502f, 0.290f, 0.000f);

        // ══════════════════════════════════════════════════════
        //  NAV / SIDEBAR
        // ══════════════════════════════════════════════════════

        private static Color NavActiveBg => Dark ? new Color(0.298f, 0.494f, 1.000f, 0.16f)
                                                  : new Color(0.153f, 0.392f, 0.863f, 0.14f);

        private static Color NavHoverBg  => Dark ? new Color(1f, 1f, 1f, 0.06f)
                                                  : new Color(0f, 0f, 0f, 0.05f);

        // ══════════════════════════════════════════════════════
        //  LOGO / SIDEBAR HEADER BACKGROUND
        //  Dark: #2B2B2B — one step deeper than BgBase so the
        //        logo zone is visually distinct from the nav list.
        // ══════════════════════════════════════════════════════

        private static Color SidebarHeaderBg => Dark ? new Color(0.169f, 0.169f, 0.169f)  // #2B2B2B
                                                      : new Color(0.714f, 0.714f, 0.714f);

        // ══════════════════════════════════════════════════════
        //  SEARCH FIELD BACKGROUND
        //  Dark: slightly darker than BgBase so the input recedes.
        // ══════════════════════════════════════════════════════

        private static Color SearchBg => Dark ? new Color(0.153f, 0.153f, 0.153f)  // #272727
                                               : new Color(0.910f, 0.910f, 0.910f);

        // ══════════════════════════════════════════════════════
        //  BADGE BACKGROUNDS
        //  Dark: desaturated, slightly darker than BgSurface.
        // ══════════════════════════════════════════════════════

        private static Color BadgeBlueBg   => Dark ? new Color(0.102f, 0.180f, 0.345f) : new Color(0.718f, 0.804f, 0.976f);  // #1A2E58
        private static Color BadgeGreenBg  => Dark ? new Color(0.082f, 0.200f, 0.118f) : new Color(0.718f, 0.890f, 0.773f);  // #15331E
        private static Color BadgeAmberBg  => Dark ? new Color(0.235f, 0.157f, 0.031f) : new Color(0.976f, 0.906f, 0.718f);  // #3C2808
        private static Color BadgeRedBg    => Dark ? new Color(0.275f, 0.078f, 0.078f) : new Color(0.976f, 0.769f, 0.769f);  // #461414
        private static Color BadgePurpleBg => Dark ? new Color(0.176f, 0.098f, 0.298f) : new Color(0.878f, 0.773f, 0.976f);  // #2D194C

        // ══════════════════════════════════════════════════════
        //  STEP NUMBER BADGE
        //  Text on the coloured circle — always dark for contrast
        // ══════════════════════════════════════════════════════

        private static Color StepNumTxt => new Color(0.06f, 0.06f, 0.09f);

        // ══════════════════════════════════════════════════════
        //  SIDEBAR — CODE / FAVORITES SUBTAB
        // ══════════════════════════════════════════════════════

        private static Color SidebarActiveBg  => Dark ? new Color(0.298f, 0.494f, 1.000f, 0.14f)
                                                       : new Color(0.153f, 0.392f, 0.863f, 0.12f);

        private static Color SidebarHoverBg   => Dark ? new Color(1f, 1f, 1f, 0.05f)
                                                       : new Color(0f, 0f, 0f, 0.04f);

        private static Color SidebarRowSep    => Dark ? new Color(0f, 0f, 0f, 0.25f)
                                                       : new Color(0f, 0f, 0f, 0.06f);

        // ══════════════════════════════════════════════════════
        //  FAQ — ANSWER BLOCK BACKGROUND
        //  Dark: #252525 — just below CodeBg, clearly darker than
        //        BgSurface so the answer area is visually sunken.
        // ══════════════════════════════════════════════════════

        private static Color FaqAnswerBg      => Dark ? new Color(0.145f, 0.145f, 0.145f)  // #252525
                                                       : new Color(0.910f, 0.910f, 0.910f);

        // ══════════════════════════════════════════════════════
        //  CODE BLOCK HEADER — TRAFFIC-LIGHT DOTS (macOS style)
        //  Fixed vivid hues — intentionally theme-invariant so
        //  the dots always read as red / yellow / green signals.
        // ══════════════════════════════════════════════════════

        private static readonly Color DotRed    = new Color(0.996f, 0.361f, 0.329f); // #FE5C54
        private static readonly Color DotYellow = new Color(0.996f, 0.745f, 0.176f); // #FEBF2D
        private static readonly Color DotGreen  = new Color(0.157f, 0.784f, 0.251f); // #28C840

        // ══════════════════════════════════════════════════════
        //  SEARCH MODAL — BACKDROP & DROP SHADOW
        //  Pure-black semi-transparent — intentionally the same
        //  in both themes (shadow/dim always reads as darkness).
        // ══════════════════════════════════════════════════════

        private static readonly Color ModalBackdrop   = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color ModalDropShadow = new Color(0f, 0f, 0f, 0.50f);

    }  // partial class TISDocWindow
}
#endif