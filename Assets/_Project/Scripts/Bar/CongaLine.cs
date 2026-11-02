using Unity.Netcode;
using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// A line of dancing guests. A few times per shift it dances one lap of the scene's
    /// <see cref="CongaPath"/>, bowling over any player in the way and shoving props. The lap is
    /// timed from a shared start time, so every machine moves the dancers identically.
    /// </summary>
    public class CongaLine : NetworkBehaviour
    {
        public static CongaLine Current { get; private set; }

        [SerializeField] Rigidbody[] dancers;
        [SerializeField] float spacing = 0.8f;
        [SerializeField] float speed = 1.8f;
        [SerializeField] float hitRadius = 0.55f;
        [SerializeField] float knockStrength = 5f;
        [Tooltip("When (seconds into the gig) each lap starts.")]
        [SerializeField] float[] lapStarts = { 40f, 105f, 160f };

        readonly NetworkVariable<bool> m_Dancing = new(false);
        readonly NetworkVariable<double> m_StartedAt = new(0);

        int m_NextLap;

        public bool IsDancing => m_Dancing.Value;
        /// <summary>Seconds since the current lap started (any machine).</summary>
        public float LapTime => (float)(NetworkManager.ServerTime.Time - m_StartedAt.Value);

        public override void OnNetworkSpawn() => Current = this;

        public override void OnNetworkDespawn()
        {
            if (Current == this) Current = null;
        }

        void FixedUpdate()
        {
            if (!IsSpawned) return;
            var path = CongaPath.Current;
            if (path == null || dancers == null) return;

            float head = m_Dancing.Value ? Mathf.Min(LapTime * speed, path.Length) : 0f;

            if (IsServer) ServerTick(path, head);

            float time = Time.time;
            for (int i = 0; i < dancers.Length; i++)
            {
                var d = dancers[i];
                if (d == null) continue;
                path.Evaluate(head - i * spacing, out Vector3 pos, out Vector3 fwd);
                // Hip-wiggle: step side to side, bounce on the beat.
                float beat = time * 6f + i * 0.9f;
                Vector3 side = Vector3.Cross(Vector3.up, fwd);
                pos += side * Mathf.Sin(beat) * 0.12f + Vector3.up * Mathf.Abs(Mathf.Sin(beat)) * 0.08f;
                Quaternion rot = Quaternion.LookRotation(fwd) * Quaternion.Euler(0f, 0f, Mathf.Sin(beat) * 8f);
                d.MovePosition(pos);
                d.MoveRotation(rot);
            }
        }

        void ServerTick(CongaPath path, float head)
        {
            var director = GigDirector.Current;
            if (director == null) return;

            if (!m_Dancing.Value)
            {
                if (director.IsRunning && m_NextLap < lapStarts.Length && director.Elapsed >= lapStarts[m_NextLap])
                {
                    m_NextLap++;
                    m_StartedAt.Value = NetworkManager.ServerTime.Time;
                    m_Dancing.Value = true;
                }
                return;
            }

            if (head >= path.Length || director.IsOver)
            {
                m_Dancing.Value = false;
                return;
            }

            // Anyone standing in the way goes flying.
            foreach (var player in PlayerMotor.ByClientId.Values)
            {
                if (player == null) continue;
                foreach (var d in dancers)
                {
                    Vector3 delta = player.transform.position - d.position;
                    delta.y = 0f;
                    if (delta.magnitude > hitRadius) continue;
                    if (player.TryGetComponent(out PlayerKnockdown knock))
                        knock.KnockDown(delta.normalized + d.transform.forward, knockStrength);
                    break;
                }
            }
        }
    }
}
