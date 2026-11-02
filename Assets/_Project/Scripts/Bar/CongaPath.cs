using UnityEngine;

namespace Freelancer
{
    /// <summary>Scene marker: a closed loop (its children, in order) that the conga line dances along.</summary>
    public class CongaPath : MonoBehaviour
    {
        public static CongaPath Current { get; private set; }

        Vector3[] m_Points;
        float[] m_Cumulative;

        public float Length { get; private set; }

        void OnEnable()
        {
            Current = this;
            Build();
        }

        void OnDisable()
        {
            if (Current == this) Current = null;
        }

        void Build()
        {
            int n = transform.childCount;
            m_Points = new Vector3[n];
            m_Cumulative = new float[n + 1];
            for (int i = 0; i < n; i++) m_Points[i] = transform.GetChild(i).position;
            Length = 0f;
            for (int i = 0; i < n; i++)
            {
                m_Cumulative[i] = Length;
                Length += Vector3.Distance(m_Points[i], m_Points[(i + 1) % n]);
            }
            m_Cumulative[n] = Length;
        }

        /// <summary>Point and direction at a distance along the loop (wraps both ways).</summary>
        public void Evaluate(float distance, out Vector3 position, out Vector3 forward)
        {
            if (m_Points == null || m_Points.Length < 2) Build();
            int n = m_Points.Length;
            if (n < 2 || Length <= 0f)
            {
                position = transform.position;
                forward = transform.forward;
                return;
            }
            distance = Mathf.Repeat(distance, Length);
            int i = 0;
            while (i < n - 1 && m_Cumulative[i + 1] < distance) i++;
            Vector3 a = m_Points[i], b = m_Points[(i + 1) % n];
            float segment = m_Cumulative[i + 1] - m_Cumulative[i];
            float t = segment > 0f ? (distance - m_Cumulative[i]) / segment : 0f;
            position = Vector3.Lerp(a, b, t);
            forward = (b - a).normalized;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.9f, 0.9f);
            int n = transform.childCount;
            for (int i = 0; i < n; i++)
                Gizmos.DrawLine(transform.GetChild(i).position + Vector3.up * 0.1f,
                    transform.GetChild((i + 1) % n).position + Vector3.up * 0.1f);
        }
    }
}
