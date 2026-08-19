using System.Collections.Generic;
using UnityEngine;

public class CameraObjectDetector : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private float checkDistance = 10f;
    [SerializeField] private Vector2 boxSize = new Vector2(5f, 5f);
    
    [Header("Wall Occlusion Settings")]
    [SerializeField] private LayerMask wallLayer; // Chỉ chọn Layer của Tường/Vật cản

    [Header("UI Overlay Settings (Game View)")]
    [SerializeField] private bool showScanBox = true;
    [SerializeField] private Color noTargetColor = new Color(0f, 0.85f, 1f, 0.6f);
    [SerializeField] private Color targetDetectedColor = new Color(1f, 0.2f, 0.2f, 0.85f);

    [Header("Gizmos Debug")]
    [SerializeField] private bool showGizmo = true;
    [SerializeField] private Color boxColor = Color.green;
    [SerializeField] private Color detectedLineColor = Color.cyan;
    [SerializeField] private Color blockedLineColor = Color.red;

    [SerializeField] private List<UIPlayerNickName> detectedObjects = new List<UIPlayerNickName>();
    private Camera _camera;
    private Texture2D _whiteTexture;

    public UIPlayerNickName CurrentTarget => detectedObjects.Count > 0 ? detectedObjects[0] : null;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        if (_camera == null)
            _camera = Camera.main;

        _whiteTexture = new Texture2D(1, 1);
        _whiteTexture.SetPixel(0, 0, Color.white);
        _whiteTexture.Apply();
    }

    private void OnDestroy()
    {
        if (_whiteTexture != null)
        {
            Destroy(_whiteTexture);
        }
    }

    private void Update()
    {
        ScanObjectsInFront();
    }

    private void OnGUI()
    {
        if (showScanBox)
        {
            DrawScanBox();
        }
    }

    private void ClearList()
    {
        foreach (UIPlayerNickName ui in detectedObjects)
        {
            if (ui != null)
            {
                ui.SetActive(false);
            }
        }
        detectedObjects.Clear();
    }

    public List<UIPlayerNickName> ScanObjectsInFront()
    {
        ClearList();

        // 1. Lấy toàn bộ danh sách Target trong Scene (Target hoàn toàn KHÔNG CẦN Collider)
        UIPlayerNickName[] allTargets = FindObjectsByType<UIPlayerNickName>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (UIPlayerNickName targetUI in allTargets)
        {
            Vector3 targetPos = targetUI.transform.position;

            // 2. Chuyển vị trí Target sang Local Space của Camera để kiểm tra hình học 3D
            Vector3 localPos = transform.InverseTransformPoint(targetPos);

            bool isInDepth  = localPos.z >= 0f && localPos.z <= checkDistance;
            bool isInWidth  = Mathf.Abs(localPos.x) <= (boxSize.x * 0.5f);
            bool isInHeight = Mathf.Abs(localPos.y) <= (boxSize.y * 0.5f);

            // 3. Nếu Target nằm trong phạm vi Box quét
            if (isInDepth && isInWidth && isInHeight)
            {
                Vector3 dirToTarget = targetPos - transform.position;
                float distToTarget = dirToTarget.magnitude;

                // 4. Giải pháp 1: Bắn Raycast kiểm tra xem có Tường (wallLayer) cản giữa Camera và Target không
                bool isBlocked = Physics.Raycast(transform.position, dirToTarget.normalized, distToTarget, wallLayer);

                if (!isBlocked)
                {
                    targetUI.SetActive(true);
                    detectedObjects.Add(targetUI);
                    Debug.DrawLine(transform.position, targetPos, detectedLineColor);
                }
                else
                {
                    Debug.DrawLine(transform.position, targetPos, blockedLineColor);
                }
            }
        }

        return detectedObjects;
    }

    private void DrawScanBox()
    {
        if (_camera == null) return;

        float screenH = Screen.height;
        float screenW = Screen.width;
        float centerX = screenW * 0.5f;
        float centerY = screenH * 0.5f;

        float aspect = _camera.aspect;
        float halfFovRad = _camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        
        float frustumHeightAtOne = 2f * Mathf.Tan(halfFovRad);
        float frustumWidthAtOne = frustumHeightAtOne * aspect;

        float boxWidthPx = (boxSize.x / frustumWidthAtOne) * screenW;
        float boxHeightPx = (boxSize.y / frustumHeightAtOne) * screenH;

        Rect boxRect = new Rect(
            centerX - (boxWidthPx * 0.5f),
            centerY - (boxHeightPx * 0.5f),
            boxWidthPx,
            boxHeightPx
        );

        Color displayColor = CurrentTarget != null ? targetDetectedColor : noTargetColor;

        DrawRectOutline(boxRect, displayColor, 2f);

        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(centerX - 2f, centerY - 2f, 4f, 4f), _whiteTexture);
    }

    private void DrawRectOutline(Rect rect, Color color, float thickness)
    {
        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), _whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), _whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), _whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), _whiteTexture);
        GUI.color = Color.white;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmo) return;

        Gizmos.color = boxColor;
        Vector3 boxCenter = transform.position + transform.forward * (checkDistance / 2f);
        Vector3 fullSize = new Vector3(boxSize.x, boxSize.y, checkDistance);

        Gizmos.matrix = Matrix4x4.TRS(boxCenter, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, fullSize);
        Gizmos.matrix = Matrix4x4.identity;
    }
}