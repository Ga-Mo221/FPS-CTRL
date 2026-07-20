using UnityEngine;
using UnityEditor;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;

namespace GaMo.BlenderEditorTools
{
    public class BlenderTransformSession
    {
        public enum TransformMode { Move, Rotate, Scale }

        // ================= MULTI-OBJECT STATE =================
        private struct ObjectSnapshot
        {
            public Transform transform;
            public Vector3   startPos;
            public Quaternion startRot;
            public Vector3   startScale;
        }

        public TransformMode Mode { get; private set; }

        // Keep Target for HUD / backward compat (first selected, or active)
        public Transform Target => snapshots.Count > 0 ? snapshots[0].transform : null;

        private List<ObjectSnapshot> snapshots = new();

        // Shared pivot (center of all selected objects)
        private Vector3 sharedPivot;

        bool startIsLocal;

        Vector2 startMousePos;
        Vector3 startHitPoint;
        Vector3 currentHitPoint;
        Plane movePlane;

        Vector3 axisConstraint = Vector3.one;
        bool planeConstraint;

        string numericBuffer = "";
        bool UsingNumeric => numericBuffer.Length > 0;

        // ── Precision Mode ──────────────────────────────────────────────────────
        bool   precisionMode  = false;
        float PrecisionFactor => BlenderEditorPreferences.PrecisionFactor;

        // Move — world-space
        Vector3 accMoveOffset  = Vector3.zero;
        Vector3 segMoveOrigin  = Vector3.zero;
        bool    moveSegInited  = false;

        // Rotate — angle (degrees)
        float   accAngle       = 0f;
        Vector2 segRotOrigin   = Vector2.zero;
        bool    rotSegInited   = false;

        // Scale — multiplicative factor
        float   accScaleFactor = 1f;
        float   segDistAtSeg   = 1f;
        bool    scaleSegInited = false;

        // ── MMB Axis Detection ──────────────────────────────────────────────────
        // Khi đang trong session, nhấn MMB → track delta chuột
        // Thả MMB → detect hướng dominant → auto lock axis (hoặc toggle về free)
        bool    mmbDown           = false;
        Vector2 mmbStartMousePos  = Vector2.zero;
        Vector2 mmbCurrentMousePos = Vector2.zero;

        // Ngưỡng pixel tối thiểu để confirm detect (tránh click nhanh)
        const float MMB_DETECT_THRESHOLD = 8f;

        GUIStyle hudStyle, valueStyle, labelStyle;

        // ================= CONSTRUCTOR =================
        public BlenderTransformSession(TransformMode mode, Transform target, Vector2 mousePos)
        {
            Mode = mode;
            startMousePos = mousePos;
            axisConstraint = Vector3.one;
            planeConstraint = false;
            startIsLocal = Tools.pivotRotation == PivotRotation.Local;

            // Collect ALL selected transforms
            var selected = Selection.transforms;
            if (selected == null || selected.Length == 0)
                selected = new[] { target };

            foreach (var t in selected)
            {
                snapshots.Add(new ObjectSnapshot
                {
                    transform  = t,
                    startPos   = t.position,
                    startRot   = t.rotation,
                    startScale = t.localScale,
                });
            }

            // Shared pivot respects Unity's PivotMode setting:
            //   Center  → average of all selected positions  (Blender: "Median Point")
            //   Pivot   → active object's position           (Blender: "Active Element")
            sharedPivot = CalcSharedPivot();

            switch (mode)
            {
                case TransformMode.Move:   Tools.current = Tool.Move;   break;
                case TransformMode.Rotate: Tools.current = Tool.Rotate; break;
                case TransformMode.Scale:  Tools.current = Tool.Scale;  break;
            }

            Camera cam = SceneView.lastActiveSceneView.camera;
            movePlane    = new Plane(-cam.transform.forward, sharedPivot);
            startHitPoint   = GetWorldPointOnPlane(mousePos, cam);
            currentHitPoint = startHitPoint;

            // Register undo for every selected object at once
            Undo.RegisterCompleteObjectUndo(snapshots.Select(s => s.transform).ToArray<Object>(), "Blender Transform");
        }

        // ================= HELPERS =================

        bool UseIndividualOrigins => snapshots.Count > 1 && Tools.pivotMode == PivotMode.Pivot;

        Vector3 CalcSharedPivot()
        {
            if (snapshots.Count == 0) return Vector3.zero;

            switch (Tools.pivotMode)
            {
                case PivotMode.Center:
                default:
                {
                    Vector3 sum = Vector3.zero;
                    foreach (var s in snapshots) sum += s.startPos;
                    return sum / snapshots.Count;
                }

                case PivotMode.Pivot:
                {
                    var active = Selection.activeTransform;
                    var match  = snapshots.FirstOrDefault(s => s.transform == active);
                    return match.transform != null ? match.startPos : snapshots[0].startPos;
                }
            }
        }

        Vector3 GetWorldPointOnPlane(Vector2 screenPos, Camera cam)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(screenPos);
            if (movePlane.Raycast(ray, out float enter))
                return ray.GetPoint(enter);
            return sharedPivot;
        }

        Vector3 GetEffectiveAxisDirection(Vector3 constraint)
        {
            Quaternion primaryRot = snapshots.Count > 0 ? snapshots[0].startRot : Quaternion.identity;
            bool isLocal = Tools.pivotRotation == PivotRotation.Local;
            if (constraint == Vector3.one) return Vector3.zero;

            if (isLocal)
            {
                if (constraint.x > 0.9f) return primaryRot * Vector3.right;
                if (constraint.y > 0.9f) return primaryRot * Vector3.up;
                if (constraint.z > 0.9f) return primaryRot * Vector3.forward;
            }
            else
            {
                if (constraint.x > 0.9f) return Vector3.right;
                if (constraint.y > 0.9f) return Vector3.up;
                if (constraint.z > 0.9f) return Vector3.forward;
            }
            return constraint;
        }

        // ================= INPUT =================
        public void SetAxis(Vector3 axis, bool shift)
        {
            if (shift)
            {
                planeConstraint = true;
                axisConstraint = Vector3.one - axis;
            }
            else
            {
                planeConstraint = false;
                axisConstraint = axis;
            }
        }

        public void AppendNumeric(char c)
        {
            if (char.IsDigit(c) || c == '.' || c == '-') numericBuffer += c;
        }

        public void Backspace()
        {
            if (numericBuffer.Length > 0) numericBuffer = numericBuffer[..^1];
        }

        float GetNumericValue()
        {
            if (float.TryParse(numericBuffer, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) return v;
            return 0f;
        }

        // ── MMB Axis Detection ──────────────────────────────────────────────────

        /// Gọi từ BlenderControls khi MMB được nhấn trong session.
        public void OnMMBDown(Vector2 mousePos)
        {
            mmbDown            = true;
            mmbStartMousePos   = mousePos;
            mmbCurrentMousePos = mousePos;
        }

        /// Gọi từ BlenderControls mỗi frame khi MMB đang giữ (MouseDrag hoặc MouseMove).
        public void OnMMBDrag(Vector2 mousePos)
        {
            if (!mmbDown) return;
            mmbCurrentMousePos = mousePos;
        }

        /// Gọi từ BlenderControls khi MMB được thả.
        /// Detect hướng dominant từ delta chuột rồi lock/toggle axis.
        public void OnMMBUp(Camera cam)
        {
            if (!mmbDown) return;
            mmbDown = false;

            Vector2 delta = mmbCurrentMousePos - mmbStartMousePos;

            // Nếu di chuyển quá ít → bỏ qua (tránh click nhanh vô tình)
            if (delta.magnitude < MMB_DETECT_THRESHOLD) return;

            // Chuyển delta 2D (screen) → hướng 3D dominant trong world/local space
            Vector3 detectedAxis = DetectAxisFromScreenDelta(delta, cam);

            if (detectedAxis == Vector3.zero) return;

            // Toggle: nếu đang lock đúng axis này rồi → về free; ngược lại → lock
            bool isLocal = Tools.pivotRotation == PivotRotation.Local;
            Vector3 currentAxisDir = GetEffectiveAxisDirection(axisConstraint);

            bool alreadyLocked = !planeConstraint
                && axisConstraint != Vector3.one
                && Vector3.Dot(currentAxisDir.normalized, GetEffectiveAxisDirection(detectedAxis).normalized) > 0.99f;

            if (alreadyLocked)
            {
                // Toggle về free
                axisConstraint  = Vector3.one;
                planeConstraint = false;
            }
            else
            {
                // Lock vào axis detect được
                planeConstraint = false;
                axisConstraint  = detectedAxis;
            }

            // Reset segment move/rotate/scale origin để tránh jump
            ResetSegmentOrigins();
        }

        /// Chiếu delta 2D màn hình lên các trục world/local, chọn axis gần nhất.
        Vector3 DetectAxisFromScreenDelta(Vector2 screenDelta, Camera cam)
        {
            // Danh sách axis ứng viên
            bool isLocal = Tools.pivotRotation == PivotRotation.Local;
            Quaternion primaryRot = snapshots.Count > 0 ? snapshots[0].startRot : Quaternion.identity;

            Vector3[] axes3D = isLocal
                ? new[] { primaryRot * Vector3.right, primaryRot * Vector3.up, primaryRot * Vector3.forward }
                : new[] { Vector3.right, Vector3.up, Vector3.forward };

            // Canonical constraints tương ứng (dùng để SetAxis sau này)
            Vector3[] constraints = { Vector3.right, Vector3.up, Vector3.forward };

            float bestScore = -1f;
            int   bestIdx   = -1;

            for (int i = 0; i < axes3D.Length; i++)
            {
                // Project axis 3D → 2D screen (dùng điểm pivot + axis direction)
                Vector3 pivotScreen = cam.WorldToScreenPoint(sharedPivot);
                Vector3 tipScreen   = cam.WorldToScreenPoint(sharedPivot + axes3D[i]);

                // WorldToScreenPoint dùng bottom-left origin → cần flip Y như GUI
                float svH = SceneView.lastActiveSceneView.position.height;
                Vector2 pivotGUI = new Vector2(pivotScreen.x, svH - pivotScreen.y);
                Vector2 tipGUI   = new Vector2(tipScreen.x,   svH - tipScreen.y);

                Vector2 axis2D = (tipGUI - pivotGUI);
                if (axis2D.magnitude < 0.01f) continue;
                axis2D.Normalize();

                // Độ tương đồng hướng: |dot| (cả 2 chiều dương/âm đều hợp lệ)
                float score = Mathf.Abs(Vector2.Dot(screenDelta.normalized, axis2D));

                if (score > bestScore)
                {
                    bestScore = score;
                    bestIdx   = i;
                }
            }

            if (bestIdx < 0) return Vector3.zero;
            return constraints[bestIdx];
        }

        /// Reset origin của các segment để tránh jump sau khi đổi axis giữa chừng.
        void ResetSegmentOrigins()
        {
            Camera cam = SceneView.lastActiveSceneView?.camera;
            if (cam == null) return;

            // Chốt accumulated trước, rồi reset neo
            switch (Mode)
            {
                case TransformMode.Move:
                {
                    Vector3 segDelta = currentHitPoint - segMoveOrigin;
                    float   mult     = precisionMode ? PrecisionFactor : 1f;
                    accMoveOffset   += ApplyAxisConstraint(segDelta) * mult;
                    segMoveOrigin    = currentHitPoint;
                    break;
                }
                case TransformMode.Rotate:
                {
                    accAngle    += CalcAngleDelta(mmbCurrentMousePos, cam, precisionMode);
                    segRotOrigin = mmbCurrentMousePos;
                    break;
                }
                case TransformMode.Scale:
                {
                    accScaleFactor *= CalcScaleDelta(mmbCurrentMousePos, cam, precisionMode);
                    InitScaleSeg(mmbCurrentMousePos, cam);
                    break;
                }
            }
        }

        // ================= UPDATE =================
        public void Update(Vector2 mousePos, Camera cam, bool shiftHeld = false)
        {
            bool modeChanged = shiftHeld != precisionMode;

            if (modeChanged && !UsingNumeric)
            {
                CommitSegment(mousePos, cam);
            }

            precisionMode   = shiftHeld;
            currentHitPoint = GetWorldPointOnPlane(mousePos, cam);

            if (!moveSegInited)  { segMoveOrigin  = currentHitPoint; moveSegInited  = true; }
            if (!rotSegInited)   { segRotOrigin   = mousePos;        rotSegInited   = true; }
            if (!scaleSegInited) { InitScaleSeg(mousePos, cam);      scaleSegInited = true; }

            switch (Mode)
            {
                case TransformMode.Move:   ApplyMove(mousePos, cam); break;
                case TransformMode.Rotate: ApplyRotate(mousePos, cam); break;
                case TransformMode.Scale:  ApplyScale(mousePos, cam);  break;
            }
        }

        void CommitSegment(Vector2 mousePos, Camera cam)
        {
            bool oldPrecision = precisionMode;
            Vector3 hitNow    = GetWorldPointOnPlane(mousePos, cam);

            switch (Mode)
            {
                case TransformMode.Move:
                {
                    Vector3 segDelta  = hitNow - segMoveOrigin;
                    float   mult      = oldPrecision ? PrecisionFactor : 1f;
                    accMoveOffset    += ApplyAxisConstraint(segDelta) * mult;
                    segMoveOrigin     = hitNow;
                    break;
                }
                case TransformMode.Rotate:
                {
                    accAngle    += CalcAngleDelta(mousePos, cam, oldPrecision);
                    segRotOrigin = mousePos;
                    break;
                }
                case TransformMode.Scale:
                {
                    accScaleFactor *= CalcScaleDelta(mousePos, cam, oldPrecision);
                    InitScaleSeg(mousePos, cam);
                    break;
                }
            }
        }

        void InitScaleSeg(Vector2 mousePos, Camera cam)
        {
            Camera c = cam ?? SceneView.lastActiveSceneView.camera;
            Vector3 sc = c.WorldToScreenPoint(sharedPivot);
            Vector2 c2 = new Vector2(sc.x, SceneView.lastActiveSceneView.position.height - sc.y);
            segDistAtSeg   = Mathf.Max(Vector2.Distance(mousePos, c2), 1f);
            scaleSegInited = true;
        }

        Vector3 ApplyAxisConstraint(Vector3 rawDelta)
        {
            if (axisConstraint == Vector3.one) return rawDelta;
            Vector3 dir = GetEffectiveAxisDirection(axisConstraint);
            if (planeConstraint)
                return Vector3.ProjectOnPlane(rawDelta, GetEffectiveAxisDirection(Vector3.one - axisConstraint));
            return Vector3.Project(rawDelta, dir);
        }

        // -------- MOVE --------
        void ApplyMove(Vector2 mousePos, Camera cam)
        {
            if (UsingNumeric)
            {
                Vector3 direction = GetEffectiveAxisDirection(axisConstraint);
                Vector3 mv = direction == Vector3.zero ? Vector3.zero : direction * GetNumericValue();
                foreach (var s in snapshots) s.transform.position = s.startPos + mv;
                return;
            }

            Vector3 segDelta = currentHitPoint - segMoveOrigin;
            segDelta = ApplyAxisConstraint(segDelta);

            float mult = precisionMode ? PrecisionFactor : 1f;
            Vector3 totalOffset = accMoveOffset + segDelta * mult;

            foreach (var s in snapshots)
                s.transform.position = s.startPos + totalOffset;
        }

        float CalcAngleDelta(Vector2 currentMousePos, Camera cam, bool isPrecision)
        {
            Vector3 screenCenter   = cam.WorldToScreenPoint(sharedPivot);
            Vector2 screenCenter2D = new Vector2(screenCenter.x,
                SceneView.lastActiveSceneView.position.height - screenCenter.y);

            Vector2 dirStart   = (segRotOrigin      - screenCenter2D).normalized;
            Vector2 dirCurrent = (currentMousePos   - screenCenter2D).normalized;

            float angleStart   = Mathf.Atan2(dirStart.y,   dirStart.x)   * Mathf.Rad2Deg;
            float angleCurrent = Mathf.Atan2(dirCurrent.y, dirCurrent.x) * Mathf.Rad2Deg;

            float delta = -Mathf.DeltaAngle(angleStart, angleCurrent);

            if (Event.current != null && Event.current.control)
                delta = Mathf.Round(delta / 5f) * 5f;

            return isPrecision ? delta * PrecisionFactor : delta;
        }

        // -------- ROTATE --------
        void ApplyRotate(Vector2 currentMousePos, Camera cam)
        {
            Vector3 axis;
            if (axisConstraint == Vector3.one)
                axis = cam.transform.forward;
            else if (planeConstraint)
                axis = GetEffectiveAxisDirection(Vector3.one - axisConstraint);
            else
                axis = GetEffectiveAxisDirection(axisConstraint);

            float totalAngle;
            if (UsingNumeric)
            {
                totalAngle = GetNumericValue();
            }
            else
            {
                float segAngle = CalcAngleDelta(currentMousePos, cam, precisionMode);
                totalAngle = accAngle + segAngle;

                if (axisConstraint != Vector3.one && Vector3.Dot(cam.transform.forward, axis) < 0)
                    totalAngle = -totalAngle;
            }

            Quaternion deltaRot = Quaternion.AngleAxis(totalAngle, axis);

            if (UseIndividualOrigins)
            {
                foreach (var s in snapshots)
                    s.transform.rotation = deltaRot * s.startRot;
            }
            else
            {
                foreach (var s in snapshots)
                {
                    Vector3 offset = s.startPos - sharedPivot;
                    s.transform.position = sharedPivot + deltaRot * offset;
                    s.transform.rotation = deltaRot * s.startRot;
                }
            }
        }

        float CalcScaleDelta(Vector2 currentMousePos, Camera cam, bool isPrecision)
        {
            Camera c = cam ?? SceneView.lastActiveSceneView.camera;
            Vector3 sc  = c.WorldToScreenPoint(sharedPivot);
            Vector2 c2d = new Vector2(sc.x, SceneView.lastActiveSceneView.position.height - sc.y);

            float distCurrent = Mathf.Max(Vector2.Distance(currentMousePos, c2d), 1f);
            float rawFactor   = distCurrent / segDistAtSeg;

            if (isPrecision)
                return 1f + (rawFactor - 1f) * PrecisionFactor;
            return rawFactor;
        }

        // -------- SCALE --------
        void ApplyScale(Vector2 currentMousePos, Camera cam)
        {
            float totalFactor;

            if (UsingNumeric)
            {
                totalFactor = GetNumericValue();
            }
            else
            {
                float segFactor = CalcScaleDelta(currentMousePos, cam, precisionMode);
                totalFactor = accScaleFactor * segFactor;
            }

            if (UseIndividualOrigins)
            {
                foreach (var s in snapshots)
                {
                    Vector3 sc = s.startScale;
                    if (axisConstraint == Vector3.one)
                        s.transform.localScale = sc * totalFactor;
                    else
                    {
                        if (axisConstraint.x > 0.1f) sc.x *= totalFactor;
                        if (axisConstraint.y > 0.1f) sc.y *= totalFactor;
                        if (axisConstraint.z > 0.1f) sc.z *= totalFactor;
                        s.transform.localScale = sc;
                    }
                }
            }
            else
            {
                foreach (var s in snapshots)
                {
                    Vector3 offset = s.startPos - sharedPivot;
                    s.transform.position = sharedPivot + offset * totalFactor;

                    Vector3 sc = s.startScale;
                    if (axisConstraint == Vector3.one)
                        s.transform.localScale = sc * totalFactor;
                    else
                    {
                        if (axisConstraint.x > 0.1f) sc.x *= totalFactor;
                        if (axisConstraint.y > 0.1f) sc.y *= totalFactor;
                        if (axisConstraint.z > 0.1f) sc.z *= totalFactor;
                        s.transform.localScale = sc;
                    }
                }
            }
        }

        // ================= CANCEL =================
        /// Gọi sau khi confirm session (từ BlenderControls) để đồng bộ Tools.hidden.
        public void OnConfirm()
        {
            Tools.hidden = !GizmoVisible;
        }

        public void Cancel()
        {
            foreach (var s in snapshots)
            {
                s.transform.position   = s.startPos;
                s.transform.rotation   = s.startRot;
                s.transform.localScale = s.startScale;
            }
            Tools.pivotRotation = startIsLocal ? PivotRotation.Local : PivotRotation.Global;
            // Restore Tools.hidden theo preference (tránh bị kẹt ẩn sau khi hủy session)
            Tools.hidden = !GizmoVisible;
        }

        // ================= VISUALS & HUD =================
        public void DrawHUD()
        {
            // Đảm bảo Tools.hidden luôn đúng (Unity có thể reset giữa frame)
            Tools.hidden = !GizmoVisible;

            SetupStyles();
            DrawGuideLines();
            DrawInfoPanel();
            if (Target != null) DrawContextButtons(Target);
        }

        void SetupStyles()
        {
            if (hudStyle == null) hudStyle = new GUIStyle(EditorStyles.helpBox);
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(EditorStyles.label);
                labelStyle.fontSize = 12;
                labelStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            }
            if (valueStyle == null)
            {
                valueStyle = new GUIStyle(EditorStyles.boldLabel);
                valueStyle.fontSize = 14;
                valueStyle.normal.textColor = new Color(1f, 0.8f, 0.4f);
            }
        }

        void DrawGuideLines()
        {
            bool isLocal = Tools.pivotRotation == PivotRotation.Local;
            Handles.color = new Color(1, 1, 1, 0.5f);

            if (Target != null)
            {
                if (Mode == TransformMode.Move)
                    Handles.DrawDottedLine(snapshots[0].startPos, Target.position, 4f);
                else
                    Handles.DrawDottedLine(sharedPivot, currentHitPoint, 4f);
            }

            Quaternion primaryRot = snapshots.Count > 0 ? snapshots[0].startRot : Quaternion.identity;
            Vector3 right   = isLocal ? primaryRot * Vector3.right   : Vector3.right;
            Vector3 up      = isLocal ? primaryRot * Vector3.up      : Vector3.up;
            Vector3 forward = isLocal ? primaryRot * Vector3.forward  : Vector3.forward;

            if (axisConstraint != Vector3.one)
            {
                if (planeConstraint)
                {
                    if (axisConstraint.x > 0.1f) DrawInfiniteAxis(right,   new Color(1f, 0.2f, 0.2f));
                    if (axisConstraint.y > 0.1f) DrawInfiniteAxis(up,      new Color(0.2f, 1f, 0.2f));
                    if (axisConstraint.z > 0.1f) DrawInfiniteAxis(forward, new Color(0.2f, 0.4f, 1f));
                }
                else
                {
                    if (axisConstraint.x > 0.9f) DrawInfiniteAxis(right,   new Color(1f, 0.2f, 0.2f));
                    else if (axisConstraint.y > 0.9f) DrawInfiniteAxis(up, new Color(0.2f, 1f, 0.2f));
                    else if (axisConstraint.z > 0.9f) DrawInfiniteAxis(forward, new Color(0.2f, 0.4f, 1f));
                }
            }

            // Vẽ preview hướng drag MMB đang theo dõi (feedback trực quan khi giữ MMB)
            if (mmbDown)
            {
                DrawMMBPreview();
            }
        }

        /// Vẽ overlay nhỏ cho thấy axis nào sẽ được lock khi thả MMB.
        void DrawMMBPreview()
        {
            Camera cam = SceneView.lastActiveSceneView?.camera;
            if (cam == null) return;

            Vector2 delta = mmbCurrentMousePos - mmbStartMousePos;
            if (delta.magnitude < MMB_DETECT_THRESHOLD) return;

            Vector3 previewConstraint = DetectAxisFromScreenDelta(delta, cam);
            if (previewConstraint == Vector3.zero) return;

            bool isLocal = Tools.pivotRotation == PivotRotation.Local;
            Quaternion primaryRot = snapshots.Count > 0 ? snapshots[0].startRot : Quaternion.identity;

            Vector3 axisDir;
            Color   axisColor;

            if (previewConstraint.x > 0.9f)
            {
                axisDir   = isLocal ? primaryRot * Vector3.right : Vector3.right;
                axisColor = new Color(1f, 0.2f, 0.2f, 0.7f);
            }
            else if (previewConstraint.y > 0.9f)
            {
                axisDir   = isLocal ? primaryRot * Vector3.up : Vector3.up;
                axisColor = new Color(0.2f, 1f, 0.2f, 0.7f);
            }
            else
            {
                axisDir   = isLocal ? primaryRot * Vector3.forward : Vector3.forward;
                axisColor = new Color(0.2f, 0.4f, 1f, 0.7f);
            }

            // Vẽ axis preview với màu nhạt hơn / nét đứt
            Handles.color = axisColor;
            Handles.DrawDottedLine(sharedPivot - axisDir * 1000f, sharedPivot + axisDir * 1000f, 6f);
        }

        void DrawInfiniteAxis(Vector3 dir, Color color)
        {
            Handles.color = color;
            Handles.DrawLine(sharedPivot - dir * 1000f, sharedPivot + dir * 1000f);
        }

        void DrawInfoPanel()
        {
            Handles.BeginGUI();

            bool showPrecision = precisionMode && !UsingNumeric;
            bool showMMBHint   = mmbDown;

            float width   = showPrecision ? 270 : 240;
            float height  = showPrecision ? 72  : (showMMBHint ? 72 : 55);
            float padding = 10;
            Rect containerRect = new Rect(
                padding,
                SceneView.lastActiveSceneView.position.height - height - padding - 20,
                width, height);

            GUI.Box(containerRect, GUIContent.none, hudStyle);

            string countLabel = snapshots.Count > 1 ? $" [{snapshots.Count} objects]" : "";
            GUI.Label(
                new Rect(containerRect.x + 10, containerRect.y + 5, containerRect.width - 20, 20),
                $"{Mode} | {GetAxisName()}{countLabel}", labelStyle);

            string displayValue = UsingNumeric ? numericBuffer : GetCurrentDeltaDisplay();
            GUI.Label(
                new Rect(containerRect.x + 10, containerRect.y + 25, containerRect.width - 20, 25),
                $"Value: {displayValue}", valueStyle);

            // Badge Precision Mode
            if (showPrecision)
            {
                if (precisionBadgeStyle == null)
                {
                    precisionBadgeStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        fontSize  = 10,
                        fontStyle = FontStyle.Bold,
                        normal    = { textColor = new Color(0.4f, 0.85f, 1.0f) }
                    };
                }
                GUI.Label(
                    new Rect(containerRect.x + 10, containerRect.y + 50, containerRect.width - 20, 18),
                    $"[SHIFT] PRECISE  ×{PrecisionFactor:F2}  — thả Shift để thoát",
                    precisionBadgeStyle);
            }

            // Badge MMB Axis Hint
            if (showMMBHint && !showPrecision)
            {
                if (mmbBadgeStyle == null)
                {
                    mmbBadgeStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        fontSize  = 10,
                        fontStyle = FontStyle.Bold,
                        normal    = { textColor = new Color(1.0f, 0.75f, 0.2f) }
                    };
                }

                Camera cam = SceneView.lastActiveSceneView?.camera;
                string axisHint = "...";
                if (cam != null)
                {
                    Vector2 delta = mmbCurrentMousePos - mmbStartMousePos;
                    if (delta.magnitude >= MMB_DETECT_THRESHOLD)
                    {
                        Vector3 detected = DetectAxisFromScreenDelta(delta, cam);
                        if (detected.x > 0.9f) axisHint = "X";
                        else if (detected.y > 0.9f) axisHint = "Y";
                        else if (detected.z > 0.9f) axisHint = "Z";
                    }
                }

                GUI.Label(
                    new Rect(containerRect.x + 10, containerRect.y + 50, containerRect.width - 20, 18),
                    $"[MMB] Thả để lock trục  →  {axisHint}",
                    mmbBadgeStyle);
            }

            Handles.EndGUI();
        }

        GUIStyle precisionBadgeStyle;
        GUIStyle mmbBadgeStyle;

        // ================= GIZMO VISIBILITY =================
        private const string K_GIZMO_VISIBLE = "BlenderEditor_GizmoVisible";

        /// Trạng thái hiển thị Gizmo (move/rotate/scale handle + Global/Local buttons).
        /// Lưu vào EditorPrefs để nhớ giữa các session.
        public static bool GizmoVisible
        {
            get => EditorPrefs.GetBool(K_GIZMO_VISIBLE, true);
            set
            {
                EditorPrefs.SetBool(K_GIZMO_VISIBLE, value);
                // Ẩn/hiện Unity built-in transform handle ngay lập tức
                Tools.hidden = !value;
                SceneView.RepaintAll();
            }
        }

        // ================= STATIC CONTEXT BUTTONS =================
        public static void DrawContextButtons(Transform target)
        {
            float distanceFromCenter = 110f;
            float eyeButtonWidth  = 24f;   // nút mắt nhỏ hình vuông
            float buttonWidth     = 60f;
            float buttonHeight    = 20f;
            float buttonGap       = 5f;

            Handles.BeginGUI();

            // ── Styles ──────────────────────────────────────────────────────────
            GUIStyle btnStyle = new GUIStyle(EditorStyles.miniButton) { fontSize = 10 };

            GUIStyle activeBtnStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize   = 10,
                fontStyle  = FontStyle.Bold,
            };
            activeBtnStyle.normal.textColor = Color.yellow;

            GUIStyle eyeStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize  = 11,
                alignment = TextAnchor.MiddleCenter,
            };

            // ── Layout ──────────────────────────────────────────────────────────
            Vector2 screenPos = HandleUtility.WorldToGUIPoint(target.position);

            // Tổng chiều rộng của nhóm đầy đủ (mắt + gap + Global + gap + Local)
            float fullGroupWidth = eyeButtonWidth + buttonGap + (buttonWidth * 2) + buttonGap;
            float startY = screenPos.y + distanceFromCenter;

            bool visible = GizmoVisible;

            // Khi ẩn: nút mắt căn giữa theo tâm của nhóm đầy đủ → cân bằng
            // Khi hiện: layout bình thường từ trái sang phải
            float eyeStartX = visible
                ? screenPos.x - (fullGroupWidth / 2)
                : screenPos.x - (eyeButtonWidth / 2);

            float groupStartX = screenPos.x - (fullGroupWidth / 2);

            Rect rectEye    = new Rect(eyeStartX, startY, eyeButtonWidth, buttonHeight);
            Rect rectGlobal = new Rect(groupStartX + eyeButtonWidth + buttonGap, startY, buttonWidth, buttonHeight);
            Rect rectLocal  = new Rect(groupStartX + eyeButtonWidth + buttonGap + buttonWidth + buttonGap, startY, buttonWidth, buttonHeight);

            // Block Unity controls khi hover lên bất kỳ button nào
            if (rectEye.Contains(Event.current.mousePosition)
             || rectGlobal.Contains(Event.current.mousePosition)
             || rectLocal.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.Layout)
                    HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }

            bool isLocal = Tools.pivotRotation == PivotRotation.Local;

            // ── Nút Mắt ─────────────────────────────────────────────────────────
            // Mở = 👁  Nhắm = 🚫  (dùng Unicode cho gọn, fallback text nếu font không hỗ trợ)
            string eyeLabel = visible ? "👁" : "⊘";
            GUIStyle currentEyeStyle = new GUIStyle(eyeStyle);
            currentEyeStyle.normal.textColor = visible ? new Color(0.6f, 0.9f, 1f) : new Color(0.55f, 0.55f, 0.55f);

            if (GUI.Button(rectEye, eyeLabel, currentEyeStyle))
            {
                GizmoVisible = !visible;
                SceneView.RepaintAll();
            }

            // ── Global / Local — chỉ hiện khi Gizmo đang bật ───────────────────
            if (visible)
            {
                if (GUI.Button(rectGlobal, "Global", !isLocal ? activeBtnStyle : btnStyle))
                {
                    Tools.pivotRotation = PivotRotation.Global;
                    SceneView.RepaintAll();
                }

                if (GUI.Button(rectLocal, "Local", isLocal ? activeBtnStyle : btnStyle))
                {
                    Tools.pivotRotation = PivotRotation.Local;
                    SceneView.RepaintAll();
                }
            }

            Handles.EndGUI();
        }

        // ================= HUD HELPERS =================
        string GetAxisName()
        {
            bool isLocal = Tools.pivotRotation == PivotRotation.Local;
            string space = isLocal ? "(Local)" : "(Global)";
            if (axisConstraint == Vector3.one) return $"Free {space}";

            string axes = "";
            if (planeConstraint) axes += "Shift + ";
            if (axisConstraint.x > 0.1f) axes += "X ";
            if (axisConstraint.y > 0.1f) axes += "Y ";
            if (axisConstraint.z > 0.1f) axes += "Z ";

            return $"{axes.Trim()} {space}";
        }

        string GetCurrentDeltaDisplay()
        {
            if (snapshots.Count == 0) return "";
            var s = snapshots[0];
            switch (Mode)
            {
                case TransformMode.Move:
                    return (s.transform.position - s.startPos).magnitude.ToString("F3") + " m";
                case TransformMode.Rotate:
                    return Quaternion.Angle(s.startRot, s.transform.rotation).ToString("F1") + " °";
                case TransformMode.Scale:
                    return "×" + (s.transform.localScale.x / s.startScale.x).ToString("F3");
                default: return "";
            }
        }
    }
}