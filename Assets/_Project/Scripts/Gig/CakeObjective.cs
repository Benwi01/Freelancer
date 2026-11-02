using Unity.Netcode;
using UnityEngine;

namespace Freelancer
{
    /// <summary>Cake delivery: win when the cake rests upright on the table, untouched; lose on timeout or a ruined cake.</summary>
    public class CakeObjective : GigObjective
    {
        [Tooltip("How long the cake must sit still on the table, untouched, to count as delivered.")]
        [SerializeField] float settleSeconds = 1.5f;
        [SerializeField] float settleMaxSpeed = 0.35f;
        [SerializeField] float uprightDot = 0.8f;

        readonly NetworkVariable<float> m_SettleProgress = new(0f);
        readonly NetworkVariable<float> m_FinalCondition = new(0f);

        float m_SettledFor;

        public override float Progress => m_SettleProgress.Value;
        public override string NoPayLine => "No cake, no cash.";

        public override void ServerTick(GigDirector director)
        {
            var cake = CakeCondition.Current;
            if (cake == null || cake.IsRuined)
            {
                End(director, false, GigFailReason.CakeRuined, 1, cake != null ? cake.Condition01 : 0f);
                return;
            }

            if (IsSettledOnTable(cake))
            {
                m_SettledFor += Time.deltaTime;
                m_SettleProgress.Value = Mathf.Clamp01(m_SettledFor / settleSeconds);
                if (m_SettledFor >= settleSeconds)
                {
                    float condition = cake.Condition01;
                    float timeFraction = director.TimeLeft / director.GigSeconds;
                    // Mostly about the cake; speed is a bonus.
                    float score = 0.65f * condition + 0.35f * timeFraction;
                    int stars = Mathf.Clamp(1 + Mathf.RoundToInt(score * 4f), 1, 5);
                    if (condition < 0.34f) stars = Mathf.Min(stars, 2); // lost two tiers: Brenda is not amused
                    End(director, true, GigFailReason.None, stars, condition);
                    return;
                }
            }
            else if (m_SettledFor > 0f)
            {
                m_SettledFor = 0f;
                m_SettleProgress.Value = 0f;
            }

            if (director.TimeLeft <= 0f) End(director, false, GigFailReason.TimeUp, 1, cake.Condition01);
        }

        void End(GigDirector director, bool delivered, GigFailReason reason, int stars, float condition)
        {
            m_FinalCondition.Value = condition;
            m_SettleProgress.Value = 0f;
            director.Finish(delivered, reason, stars);
        }

        bool IsSettledOnTable(CakeCondition cake)
        {
            var zone = DeliveryZone.Current;
            if (zone == null || !zone.Contains(cake.transform.position)) return false;
            if (Vector3.Dot(cake.transform.up, Vector3.up) < uprightDot) return false;
            if (cake.Body.linearVelocity.magnitude > settleMaxSpeed) return false;
            return !cake.TryGetComponent(out GrabbableObject grab) || !grab.IsHeld;
        }

        public override string ObjectiveText(GigDirector director) => m_SettleProgress.Value > 0f
            ? "Let go... don't touch it... Brenda is watching..."
            : "Get the cake from the van to the table on the top floor. Set it down upright and LET GO.";

        public override string ResultDetail(GigDirector director) => director.FailReason switch
        {
            GigFailReason.TimeUp => "Out of time. The wedding started without the cake.",
            GigFailReason.CakeRuined => "The cake is no longer a cake.",
            _ => $"Delivered with {Mathf.CeilToInt(director.TimeLeft)}s left, cake at {Mathf.CeilToInt(m_FinalCondition.Value * 100)}%.",
        };

        public override void Status(out string title, out float fill, out string text, out Color color)
        {
            var cake = CakeCondition.Current;
            title = "CAKE";
            fill = cake != null ? cake.Condition01 : m_FinalCondition.Value;
            color = Color.Lerp(new Color(0.95f, 0.25f, 0.2f), new Color(0.4f, 0.9f, 0.4f), fill);
            text = cake != null ? $"{Mathf.CeilToInt(fill * 100)}%   ({cake.TiersLeft}/{cake.TierCount} tiers)" : "no cake?!";
        }
    }
}
