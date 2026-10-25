// ============================================================
//  TrueInputSystem Tutorials — TISTutForceMobile.cs
//
//  Mặc định TIS chỉ giả lập mobile trong Editor khi game chạy ở cửa sổ
//  Device Simulator (hook Editor ghi đè Simulate Mobile In Editor theo
//  TISSimulatorChecker). Scene hướng dẫn Mobile muốn chạy được cả trong
//  Game view thường → component này ép chế độ mobile lúc Play, và trả hook
//  về như cũ khi rời scene. Không ảnh hưởng bản build.
// ============================================================

using System;
using UnityEngine;

namespace GaMo.TrueInputSystem.Tutorials
{
    [DefaultExecutionOrder(-200)]      // trước TISInputBridge (-100) và TISMobileInputSetup (-50)
    [AddComponentMenu("True Input System/Tutorials/TIS Tut Force Mobile (Editor)")]
    public class TISTutForceMobile : MonoBehaviour
    {
        [Tooltip("Bật: chế độ mobile chạy cả trong Game view.\nTắt: theo mặc định của TIS — chỉ khi chạy trong Device Simulator.")]
        // Chi doc trong #if UNITY_EDITOR || DEVELOPMENT_BUILD -> ban release bao CS0414. Giu field (cung layout serialize).
#pragma warning disable CS0414
        [SerializeField] bool _forceInGameView = true;
#pragma warning restore CS0414

#if UNITY_EDITOR
        Func<bool> _original;
        bool _applied;

        void Awake()
        {
            if (!_forceInGameView) return;
            _original = TISInputBridge.SimulatorCheckerOverride;
            TISInputBridge.SimulatorCheckerOverride = () => true;
            _applied = true;
        }

        void OnDestroy()
        {
            if (_applied) TISInputBridge.SimulatorCheckerOverride = _original;
        }
#endif
    }
}
