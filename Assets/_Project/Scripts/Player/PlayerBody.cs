using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// Code-driven animation of the jointed body: walk cycle, wobbly lean, head follows the aim,
    /// arms reach for whatever the player is holding. Runs on every machine from replicated data
    /// (position, pitch, grab holders), so no animation state needs to be sent over the network.
    /// Paused while the ragdoll is active.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class PlayerBody : MonoBehaviour
    {
        [Header("Bones (parent first)")]
        [SerializeField] Transform hips;
        [SerializeField] Transform torso;
        [SerializeField] Transform head;
        [SerializeField] Transform upperArmL, lowerArmL, upperArmR, lowerArmR;
        [SerializeField] Transform upperLegL, lowerLegL, upperLegR, lowerLegR;

        [Header("Walk")]
        [SerializeField] float stepLength = 0.75f;
        [SerializeField] float legSwing = 38f;
        [SerializeField] float armSwing = 32f;
        [SerializeField] float bobHeight = 0.05f;

        [Header("Wobble")]
        [SerializeField] float leanStiffness = 110f;
        [SerializeField] float leanDamping = 9f;

        PlayerMotor m_Motor;
        PlayerRagdoll m_Ragdoll;
        Transform[] m_Bones;
        Vector3[] m_RestPositions;

        Vector3 m_LastPos;
        Vector3 m_Velocity;
        Vector3 m_LastVelocity;
        float m_Phase;
        float m_Air;
        Vector2 m_Lean, m_LeanVel;
        float m_Grab;
        Vector3 m_GripPoint;

        /// <summary>Smoothed world velocity of the player (works on every machine).</summary>
        public Vector3 Velocity => m_Velocity;

        void Awake()
        {
            m_Motor = GetComponent<PlayerMotor>();
            m_Ragdoll = GetComponent<PlayerRagdoll>();
            m_Bones = new[] { hips, torso, head, upperArmL, lowerArmL, upperArmR, lowerArmR,
                upperLegL, lowerLegL, upperLegR, lowerLegR };
            m_RestPositions = new Vector3[m_Bones.Length];
            for (int i = 0; i < m_Bones.Length; i++) m_RestPositions[i] = m_Bones[i].localPosition;
            m_LastPos = transform.position;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Velocity from movement, so remote players animate from their synced position.
            Vector3 raw = (transform.position - m_LastPos) / dt;
            m_LastPos = transform.position;
            if (raw.sqrMagnitude > 30f * 30f) raw = Vector3.zero; // teleport
            m_LastVelocity = m_Velocity;
            m_Velocity = Vector3.Lerp(m_Velocity, raw, 1f - Mathf.Exp(-12f * dt));

            if (m_Ragdoll != null && m_Ragdoll.IsActive) return;

            for (int i = 0; i < m_Bones.Length; i++) m_Bones[i].localPosition = m_RestPositions[i];

            Vector3 local = transform.InverseTransformDirection(m_Velocity);
            Vector3 planar = new Vector3(local.x, 0f, local.z);
            float speed = planar.magnitude;
            float move = Mathf.Clamp(speed / 4.2f, 0f, 1.5f);
            Vector3 accel = transform.InverseTransformDirection((m_Velocity - m_LastVelocity) / dt);

            m_Air = Mathf.MoveTowards(m_Air, Mathf.Abs(m_Velocity.y) > 1.5f ? 1f : 0f, dt * 5f);
            m_Phase += speed / (2f * stepLength) * Mathf.PI * 2f * dt;
            float s = Mathf.Sin(m_Phase), c = Mathf.Cos(m_Phase);

            // Swing around the axis that is sideways to the direction of travel (handles strafing).
            Vector3 swingAxis = speed > 0.05f ? Vector3.Cross(Vector3.up, planar / speed) : Vector3.right;

            // Wobbly lean: a spring pulled toward a target that depends on speed and acceleration.
            Vector2 leanTarget = new Vector2(
                Mathf.Clamp(local.z * 2.2f + accel.z * 0.9f, -15f, 20f),
                Mathf.Clamp(local.x * 1.8f + accel.x * 0.8f, -15f, 15f));
            m_LeanVel += ((leanTarget - m_Lean) * leanStiffness - m_LeanVel * leanDamping) * dt;
            m_Lean += m_LeanVel * dt;
            m_Lean = Vector2.ClampMagnitude(m_Lean, 35f);

            float pitch = m_Motor != null ? m_Motor.Pitch : 0f;

            // Hips: bounce and a little twist with each step.
            hips.localPosition += Vector3.up * (Mathf.Abs(s) * bobHeight * move - 0.03f * move - 0.06f * m_Air);
            hips.localRotation = Quaternion.Euler(0f, s * 7f * move, 0f);

            // Torso: lean, plus a bit of the look pitch.
            torso.localRotation = Quaternion.Euler(m_Lean.x + pitch * 0.25f, -s * 9f * move, -m_Lean.y);

            // Head: rest of the pitch, lagging the lean so it bobbles.
            head.localRotation = Quaternion.Euler(Mathf.Clamp(pitch * 0.75f - m_Lean.x * 0.6f, -70f, 70f),
                0f, m_Lean.y * 0.7f + m_LeanVel.y * 0.02f);

            // Legs.
            float legL = -s * legSwing * Mathf.Min(move, 1.2f);
            float legR = s * legSwing * Mathf.Min(move, 1.2f);
            upperLegL.localRotation = Quaternion.AngleAxis(legL - 20f * m_Air, swingAxis);
            upperLegR.localRotation = Quaternion.AngleAxis(legR + 10f * m_Air, swingAxis);
            lowerLegL.localRotation = Quaternion.Euler(Mathf.Max(0f, c) * 55f * move + 45f * m_Air, 0f, 0f);
            lowerLegR.localRotation = Quaternion.Euler(Mathf.Max(0f, -c) * 55f * move + 25f * m_Air, 0f, 0f);

            // Arms: swing opposite the legs, flail when airborne, reach when grabbing.
            bool holding = m_Motor != null && GrabbableObject.TryFindGrip(m_Motor.OwnerClientId, out m_GripPoint);
            m_Grab = Mathf.MoveTowards(m_Grab, holding ? 1f : 0f, dt * 7f);

            float flail = Mathf.Sin(Time.time * 18f) * 25f * m_Air;
            Quaternion swingL = Quaternion.AngleAxis(-legL * armSwing / legSwing, swingAxis) *
                                Quaternion.Euler(0f, 0f, -8f - 110f * m_Air + flail);
            Quaternion swingR = Quaternion.AngleAxis(-legR * armSwing / legSwing, swingAxis) *
                                Quaternion.Euler(0f, 0f, 8f + 110f * m_Air + flail);
            Quaternion elbow = Quaternion.Euler(-15f - 25f * move, 0f, 0f);

            upperArmL.localRotation = Quaternion.Slerp(swingL, ReachRotation(upperArmL, -1f), m_Grab);
            upperArmR.localRotation = Quaternion.Slerp(swingR, ReachRotation(upperArmR, 1f), m_Grab);
            Quaternion straight = Quaternion.Euler(-8f, 0f, 0f);
            lowerArmL.localRotation = Quaternion.Slerp(elbow, straight, m_Grab);
            lowerArmR.localRotation = Quaternion.Slerp(elbow, straight, m_Grab);
        }

        /// <summary>Local rotation that points a hanging arm at the grip point.</summary>
        Quaternion ReachRotation(Transform upperArm, float side)
        {
            Vector3 target = m_GripPoint + transform.right * (0.1f * side);
            Vector3 dir = upperArm.parent.InverseTransformDirection(target - upperArm.position);
            if (dir.sqrMagnitude < 1e-4f) return Quaternion.identity;
            return Quaternion.FromToRotation(Vector3.down, dir.normalized);
        }
    }
}
