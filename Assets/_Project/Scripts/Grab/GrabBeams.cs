using System.Collections.Generic;
using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// Draws a coloured line from each holder's hand to their grip point, on every machine,
    /// so you can see who is (mis)handling the cake.
    /// </summary>
    [RequireComponent(typeof(GrabbableObject))]
    public class GrabBeams : MonoBehaviour
    {
        static Material s_LineMaterial;

        [Tooltip("Unlit material for the beams (assigned by the setup menu).")]
        [SerializeField] Material lineMaterial;

        readonly List<LineRenderer> m_Lines = new();
        GrabbableObject m_Grab;

        void Awake()
        {
            m_Grab = GetComponent<GrabbableObject>();
        }

        void LateUpdate()
        {
            int used = 0;
            if (m_Grab.IsSpawned)
            {
                foreach (var holder in m_Grab.Holders)
                {
                    if (!PlayerMotor.ByClientId.TryGetValue(holder.ClientId, out var player) || player == null) continue;
                    if (!GrabbableObject.TryGetHandPoint(holder, out Vector3 hand)) continue;

                    // Start the line a little below the eyes so it doesn't cover your own view.
                    Vector3 from = player.EyePosition - Vector3.up * 0.35f + player.transform.right * 0.25f;
                    Vector3 anchor = transform.TransformPoint(holder.LocalAnchor);

                    var line = GetLine(used++);
                    line.positionCount = 3;
                    line.SetPosition(0, from);
                    line.SetPosition(1, Vector3.Lerp(from, hand, 0.8f));
                    line.SetPosition(2, anchor);
                    line.startColor = line.endColor = player.PlayerColor;
                }
            }

            for (int i = used; i < m_Lines.Count; i++)
                m_Lines[i].enabled = false;
        }

        LineRenderer GetLine(int index)
        {
            while (m_Lines.Count <= index)
            {
                var go = new GameObject("GrabBeam");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.widthMultiplier = 0.035f;
                lr.numCapVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.sharedMaterial = lineMaterial != null ? lineMaterial : FallbackMaterial;
                m_Lines.Add(lr);
            }
            m_Lines[index].enabled = true;
            return m_Lines[index];
        }

        static Material FallbackMaterial
        {
            get
            {
                if (s_LineMaterial == null)
                {
                    var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                    s_LineMaterial = new Material(shader);
                }
                return s_LineMaterial;
            }
        }
    }
}
