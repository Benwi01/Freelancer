using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// Background wedding guest that never stops: dancing, chatting or leaning on the bar.
    /// Purely cosmetic and local (no networking), so every machine animates its own copy.
    /// </summary>
    public class PartyGuest : MonoBehaviour
    {
        public enum Style { Dance, Chat, Lean }

        [SerializeField] Transform body;
        [SerializeField] Style style = Style.Chat;
        [SerializeField] float speed = 1f;

        float m_Seed;

        void Awake() => m_Seed = Random.value * 20f;

        void LateUpdate()
        {
            if (body == null) return;
            float t = Time.time * speed + m_Seed;
            Vector3 pos = Vector3.zero;
            Quaternion rot;
            switch (style)
            {
                case Style.Dance:
                    // Bounce on the beat, twist the hips, sway side to side.
                    pos.y = Mathf.Abs(Mathf.Sin(t * 5.2f)) * 0.12f;
                    rot = Quaternion.Euler(0f, Mathf.Sin(t * 1.3f) * 35f, Mathf.Sin(t * 2.6f) * 9f);
                    break;
                case Style.Lean:
                    // Propped on the bar, swaying a little more than they should.
                    rot = Quaternion.Euler(8f + Mathf.Sin(t * 0.8f) * 3f, 0f, Mathf.Sin(t * 0.6f) * 5f);
                    break;
                default:
                    // Nodding along, turning to whoever's talking, the odd laugh.
                    float laugh = Mathf.Max(0f, Mathf.Sin(t * 0.45f) - 0.85f) * 6.6f;
                    pos.y = Mathf.Abs(Mathf.Sin(t * 14f)) * 0.03f * laugh;
                    rot = Quaternion.Euler(Mathf.Sin(t * 2.1f) * 4f - laugh * 8f, Mathf.Sin(t * 0.5f) * 18f, Mathf.Sin(t * 0.9f) * 2f);
                    break;
            }
            body.localPosition = pos;
            body.localRotation = rot;
        }
    }
}
