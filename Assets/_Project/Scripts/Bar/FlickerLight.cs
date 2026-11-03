using UnityEngine;

namespace Freelancer
{
    /// <summary>A dodgy bulb: mostly on, now and then it stutters. Cosmetic, local only.</summary>
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        [SerializeField] Renderer bulb;
        [SerializeField] float minGap = 2f;
        [SerializeField] float maxGap = 7f;

        Light m_Light;
        float m_On;
        float m_NextBurst;
        float m_BurstUntil;

        void Awake()
        {
            m_Light = GetComponent<Light>();
            m_On = m_Light.intensity;
            m_NextBurst = Time.time + Random.Range(minGap, maxGap);
        }

        void Update()
        {
            bool lit = true;
            if (Time.time >= m_NextBurst)
            {
                m_BurstUntil = Time.time + Random.Range(0.2f, 0.7f);
                m_NextBurst = m_BurstUntil + Random.Range(minGap, maxGap);
            }
            if (Time.time < m_BurstUntil) lit = Random.value > 0.5f;

            m_Light.intensity = lit ? m_On : m_On * 0.15f;
            if (bulb != null && bulb.enabled != lit) bulb.enabled = lit;
        }
    }
}
