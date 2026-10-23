using System.Collections.Generic;
using GaMo.BlenderEditorTools;
using UnityEngine;

/// <summary>
/// Bước tính gimbal của <see cref="CameraStabilizer"/>, chạy TRONG ConstraintScheduler để có pose của frame
/// hiện tại: đọc RAW_Camera của thân (sau Animator) → ghi Root Gimbal (object này) + Camera Gimbal
/// → constraint Root và Camera-Root đọc chúng sau đó. Tự gắn lúc chạy, không đặt tay.
/// </summary>
public class CameraGimbalSolver : ConstraintBehaviour
{
    public CameraStabilizer Owner;
    public Transform Input;
    public Transform SecondOutput;

    public override int DefaultTypeOrder => TypeOrderTransform;

    public override void Solve()
    {
        if (Owner != null) Owner.SolveGimbal();
    }

    public override void CollectDependencies(List<Transform> inputs, List<Transform> outputs)
    {
        inputs.Add(Input);
        outputs.Add(transform);
        outputs.Add(SecondOutput);
    }
}
