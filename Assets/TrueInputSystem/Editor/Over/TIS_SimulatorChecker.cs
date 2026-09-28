#if UNITY_EDITOR
using UnityEditor;
using GaMo.TrueInputSystem;
using UnityEngine;

[InitializeOnLoad]
public static class TISSimulatorCheckerInjector
{
    static TISSimulatorCheckerInjector()
    {
        TISInputBridge.SimulatorCheckerOverride = () =>
            SystemInfo.deviceType != UnityEngine.Device.SystemInfo.deviceType;
    }
}
#endif