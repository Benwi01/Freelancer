using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// Purely visual jelly wobble: each tier's mesh lags behind the cake's acceleration on a spring,
    /// higher tiers more. Runs on every machine from the (interpolated) transform, so it needs no
    /// network traffic and never affects physics.
    /// </summary>
    public class CakeWobble : MonoBehaviour
    {
        [Tooltip("Visual-only children to wobble, bottom tier first.")]
        [SerializeField] Transform[] tierVisuals;
        [SerializeField] float stiffness = 140f;
        [SerializeField] float damping = 7f;
        [SerializeField] float accelerationScale = 0.0035f;
        [SerializeField] float maxOffset = 0.07f;
        [SerializeField] float tiltDegreesPerMetre = 220f;

        Vector3[] m_Offset;
        Vector3[] m_Velocity;
        Vector3[] m_RestPos;
        Quaternion[] m_RestRot;
        Vector3 m_LastPos, m_LastVel;
        bool m_HasHistory;

        void Awake()
        {
            int n = tierVisuals.Length;
            m_Offset = new Vector3[n];
            m_Velocity = new Vector3[n];
            m_RestPos = new Vector3[n];
            m_RestRot = new Quaternion[n];
            for (int i = 0; i < n; i++)
            {
                m_RestPos[i] = tierVisuals[i].localPosition;
                m_RestRot[i] = tierVisuals[i].localRotation;
            }
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 pos = transform.position;
            Vector3 vel = m_HasHistory ? (pos - m_LastPos) / dt : Vector3.zero;
            Vector3 accel = m_HasHistory ? (vel - m_LastVel) / dt : Vector3.zero;
            m_LastPos = pos;
            m_LastVel = vel;
            m_HasHistory = true;

            // Acceleration in the cake's own frame; the jelly lags the opposite way.
            Vector3 localAccel = transform.InverseTransformDirection(Vector3.ClampMagnitude(accel, 80f));
            localAccel.y = 0f;

            for (int i = 0; i < tierVisuals.Length; i++)
            {
                if (tierVisuals[i] == null) continue;
                float heightFactor = 1f + i * 0.8f;
                Vector3 force = -stiffness * m_Offset[i] - damping * m_Velocity[i]
                                - localAccel * accelerationScale * stiffness * heightFactor;
                m_Velocity[i] += force * dt;
                m_Offset[i] = Vector3.ClampMagnitude(m_Offset[i] + m_Velocity[i] * dt, maxOffset * heightFactor);

                Vector3 o = m_Offset[i];
                tierVisuals[i].localPosition = m_RestPos[i] + o;
                tierVisuals[i].localRotation = m_RestRot[i] *
                    Quaternion.Euler(o.z * tiltDegreesPerMetre, 0f, -o.x * tiltDegreesPerMetre);
            }
        }
    }
}
