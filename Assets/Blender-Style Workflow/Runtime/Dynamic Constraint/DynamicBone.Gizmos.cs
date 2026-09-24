using UnityEngine;

namespace GaMo.BlenderEditorTools
{
    public partial class DynamicBone
    {
#if UNITY_EDITOR
        // ══════════════════════════════════════════════════════════
        //  GIZMOS (chỉ biên dịch trong Editor)
        // ══════════════════════════════════════════════════════════
        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

            if (Application.isPlaying && _nodeCount > 0)
            {
                bool drawParticleRadius = enableColliders && particleRadius > 0f;

                for (int i = 0; i < _nodeCount; i++)
                {
                    Vector3 pos = _nodes[i].position;

                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(pos, nodeGizmoSize);

                    if (drawParticleRadius)
                    {
                        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
                        Gizmos.DrawWireSphere(pos, particleRadius);
                    }

                    int p = _nodes[i].parent;
                    if (p >= 0)
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawLine(_nodes[p].position, pos);
                        Vector3 restPos = _tf[p].TransformPoint(_nodes[i].localPosition);
                        Gizmos.color = Color.green;
                        Gizmos.DrawWireSphere(restPos, nodeGizmoSize * 0.8f);
                    }
                }

                if (enableClothLinks)
                {
                    Gizmos.color = Color.magenta;
                    for (int i = 0; i < _linkCount; i++)
                        Gizmos.DrawLine(_nodes[_links[i].a].position, _nodes[_links[i].b].position);
                }

                if (dragDirection.sqrMagnitude > 0.00001f)
                {
                    Vector3 origin = transform.position;
                    Vector3 dragVector = dragDirection.normalized * arrowLengthMultiplier;
                    DrawArrowGizmo(origin, origin + dragVector, Color.cyan);
                    DrawArrowGizmo(origin, origin - dragVector * inertia, Color.green);
                }
            }
            else if (rootBones.Count > 0)
            {
                foreach (var rootBone in rootBones)
                    if (rootBone != null)
                        DrawTransformGizmos(rootBone);
            }
        }

        private void DrawTransformGizmos(Transform current)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(current.position, nodeGizmoSize);
            foreach (Transform child in current)
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawLine(current.position, child.position);
                DrawTransformGizmos(child);
            }
        }

        private void DrawArrowGizmo(Vector3 from, Vector3 to, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(from, to);
            Vector3 direction = to - from;
            if (direction.sqrMagnitude < 0.0001f) return;
            Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 200, 0) * Vector3.forward;
            Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 160, 0) * Vector3.forward;
            float arrowHeadSize = Mathf.Clamp(direction.magnitude * 0.2f, 0.05f, 0.5f);
            Gizmos.DrawLine(to, to + right * arrowHeadSize);
            Gizmos.DrawLine(to, to + left * arrowHeadSize);
        }
#endif

    }
}
