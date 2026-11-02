using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// Googly eyes: each pupil slides around the front of its eyeball, pulled by gravity and
    /// thrown around when the head moves. Purely visual, runs on every machine.
    /// </summary>
    [DefaultExecutionOrder(70)] // after the body animation and ragdoll have moved the head
    public class PlayerEyes : MonoBehaviour
    {
        [Tooltip("One pivot per eye at the eyeball centre; the pupil is its child, in front along +Z.")]
        [SerializeField] Transform[] pupilPivots;
        [SerializeField] float maxAngle = 32f;
        [SerializeField] float gravityPull = 2.2f;
        [SerializeField] float shakePull = 0.35f;
        [SerializeField] float centering = 6f;
        [SerializeField] float damping = 3f;
        [SerializeField] float bounce = 0.55f;

        Vector2[] m_Offset;
        Vector2[] m_Velocity;
        Vector3 m_LastPos, m_LastVel;
        Transform m_Head;

        void Awake()
        {
            m_Offset = new Vector2[pupilPivots.Length];
            m_Velocity = new Vector2[pupilPivots.Length];
            m_Head = pupilPivots.Length > 0 ? pupilPivots[0].parent : transform;
            m_LastPos = m_Head.position;
            // Start a little off so the two eyes never quite match.
            for (int i = 0; i < m_Offset.Length; i++) m_Offset[i] = Random.insideUnitCircle * 0.4f;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || m_Head == null) return;

            Vector3 vel = (m_Head.position - m_LastPos) / dt;
            m_LastPos = m_Head.position;
            Vector3 accel = (vel - m_LastVel) / dt;
            m_LastVel = vel;
            if (accel.sqrMagnitude > 400f * 400f) accel = Vector3.zero; // teleport

            // What the pupil "feels": gravity, minus how the head is accelerating.
            Vector3 felt = Physics.gravity * gravityPull - accel * shakePull;

            for (int i = 0; i < pupilPivots.Length; i++)
            {
                var pivot = pupilPivots[i];
                Vector3 local = pivot.parent.InverseTransformDirection(felt);
                Vector2 force = new Vector2(local.x, local.y) * 0.1f;

                m_Velocity[i] += (force - m_Offset[i] * centering - m_Velocity[i] * damping) * dt;
                m_Offset[i] += m_Velocity[i] * dt;

                // Rattle against the edge of the eyeball.
                float r = m_Offset[i].magnitude;
                if (r > 1f)
                {
                    Vector2 n = m_Offset[i] / r;
                    m_Offset[i] = n;
                    float into = Vector2.Dot(m_Velocity[i], n);
                    if (into > 0f) m_Velocity[i] -= (1f + bounce) * into * n;
                }

                pivot.localRotation = Quaternion.Euler(-m_Offset[i].y * maxAngle, m_Offset[i].x * maxAngle, 0f);
            }
        }
    }
}
