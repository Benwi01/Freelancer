using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Freelancer
{
    public enum GuestMood : byte { Waiting, Happy, Angry }

    public enum GuestShout : byte { None, Thanks, Perfect, NiceSlide, Sticky, Wrong, Mystery, NotEnough, Angry }

    /// <summary>
    /// A wedding guest standing at the bar with an order in a speech bubble and a patience bar.
    /// Server: takes a matching glass put down (or slid) in front of them, sends back wrong ones,
    /// and storms off when patience runs out. Everyone: bubble, colours, bobbing.
    /// </summary>
    public class BarGuest : NetworkBehaviour
    {
        public static readonly List<BarGuest> All = new();

        static readonly Color[] k_Outfits =
        {
            new(0.25f, 0.3f, 0.45f), new(0.55f, 0.25f, 0.35f), new(0.3f, 0.5f, 0.4f), new(0.85f, 0.75f, 0.55f),
            new(0.45f, 0.35f, 0.6f), new(0.2f, 0.2f, 0.22f), new(0.9f, 0.55f, 0.6f), new(0.35f, 0.55f, 0.75f),
        };
        static readonly Color[] k_Skin =
        {
            new(0.98f, 0.84f, 0.7f), new(0.87f, 0.68f, 0.52f), new(0.67f, 0.48f, 0.34f), new(0.45f, 0.31f, 0.22f),
        };

        [Header("Visuals")]
        [SerializeField] Transform body;
        [SerializeField] Renderer[] outfitRenderers;
        [SerializeField] Renderer[] skinRenderers;
        [SerializeField] GameObject[] hats;
        [SerializeField] Transform bubble;
        [SerializeField] TMP_Text bubbleText;
        [SerializeField] Renderer bubblePlate;
        [SerializeField] Transform patienceFill;
        [SerializeField] Renderer patienceRenderer;

        [Header("Serving")]
        [Tooltip("Where drinks get put down, relative to the guest (on the bar in front of them).")]
        [SerializeField] Vector3 serveCenter = new(0f, 1.15f, 0.95f);
        [SerializeField] Vector3 serveSize = new(1.25f, 0.5f, 1.1f);
        [Tooltip("Glasses moving slower than this get caught.")]
        [SerializeField] float catchSpeed = 2.6f;
        [SerializeField] float minFill = 0.5f;

        readonly NetworkVariable<Drink> m_Order = new(Drink.Beer);
        readonly NetworkVariable<GuestMood> m_Mood = new(GuestMood.Waiting);
        readonly NetworkVariable<double> m_Deadline = new(0);
        readonly NetworkVariable<float> m_PatienceSeconds = new(40f);
        readonly NetworkVariable<byte> m_Look = new(0);

        GuestSlot m_Slot;
        float m_LeaveAt;
        float m_NextReject;
        GuestShout m_Shout;
        float m_ShoutUntil;
        float m_MoodChangedAt;
        float m_Seed;
        string m_ShownText;
        MaterialPropertyBlock m_Block;

        public Drink Order => m_Order.Value;
        public GuestMood Mood => m_Mood.Value;
        public float Patience01 => m_Mood.Value != GuestMood.Waiting ? 0f
            : Mathf.Clamp01((float)(m_Deadline.Value - NetworkManager.ServerTime.Time) / Mathf.Max(1f, m_PatienceSeconds.Value));

        /// <summary>Server, right after spawning.</summary>
        public void Init(GuestSlot slot, Drink order, float patienceSeconds)
        {
            m_Slot = slot;
            slot.Occupant = this;
            m_Order.Value = order;
            m_PatienceSeconds.Value = patienceSeconds;
            m_Deadline.Value = NetworkManager.ServerTime.Time + patienceSeconds;
            m_Look.Value = (byte)Random.Range(0, 256);
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            m_Mood.OnValueChanged += OnMoodChanged;
            m_Look.OnValueChanged += (_, _) => ApplyLook();
            m_Seed = Random.value * 10f;
            ApplyLook();
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            m_Mood.OnValueChanged -= OnMoodChanged;
            if (m_Slot != null && m_Slot.Occupant == this) m_Slot.Occupant = null;
        }

        void OnMoodChanged(GuestMood previous, GuestMood current) => m_MoodChangedAt = Time.time;

        void ApplyLook()
        {
            int look = m_Look.Value;
            m_Block ??= new MaterialPropertyBlock();
            m_Block.SetColor("_BaseColor", k_Outfits[look % k_Outfits.Length]);
            foreach (var r in outfitRenderers) if (r != null) r.SetPropertyBlock(m_Block);
            m_Block.SetColor("_BaseColor", k_Skin[(look / 8) % k_Skin.Length]);
            foreach (var r in skinRenderers) if (r != null) r.SetPropertyBlock(m_Block);
            int hat = (look / 32) % (hats.Length + 2) - 2; // most guests have no hat
            for (int i = 0; i < hats.Length; i++) if (hats[i] != null) hats[i].SetActive(i == hat);
        }

        // ---------- Server ----------

        void FixedUpdate()
        {
            if (!IsServer || !IsSpawned) return;

            if (m_Mood.Value != GuestMood.Waiting)
            {
                if (Time.time >= m_LeaveAt) NetworkObject.Despawn();
                return;
            }

            var director = GigDirector.Current;
            if (director != null && director.IsOver) return;

            if (NetworkManager.ServerTime.Time >= m_Deadline.Value)
            {
                Leave(GuestMood.Angry, GuestShout.Angry);
                BarObjective.Current?.OnGuestWalkedOff();
                return;
            }

            foreach (var glass in DrinkGlass.All)
            {
                if (glass == null || !glass.IsSpawned || !InServeZone(glass.transform.position)) continue;
                if (glass.Body.linearVelocity.magnitude > catchSpeed) continue;

                GuestShout problem = glass.Contents == Drink.Mystery ? GuestShout.Mystery
                    : glass.Contents != m_Order.Value ? GuestShout.Wrong
                    : glass.Fill < minFill ? GuestShout.NotEnough
                    : GuestShout.None;

                if (problem == GuestShout.None)
                {
                    bool slid = !glass.IsHeld && Vector3.Distance(glass.ReleasedAt, glass.transform.position) > 1.5f;
                    bool perfect = glass.Fill >= 0.85f && !glass.Sticky;
                    var shout = slid ? GuestShout.NiceSlide : glass.Sticky ? GuestShout.Sticky
                        : perfect ? GuestShout.Perfect : GuestShout.Thanks;
                    glass.NetworkObject.Despawn();
                    BarObjective.Current?.OnGuestServed(slid, perfect);
                    Leave(GuestMood.Happy, shout);
                    return;
                }

                if (Time.time >= m_NextReject && !glass.IsHeld)
                {
                    // Not what I ordered: push it back across the bar.
                    m_NextReject = Time.time + 1.2f;
                    glass.Body.linearVelocity = transform.forward * 1.4f + Vector3.up * 0.4f;
                    ShoutRpc(problem);
                }
                else if (glass.IsHeld && Time.time >= m_NextReject)
                {
                    m_NextReject = Time.time + 1.2f;
                    ShoutRpc(problem);
                }
            }
        }

        bool InServeZone(Vector3 world)
        {
            Vector3 local = transform.InverseTransformPoint(world) - serveCenter;
            return Mathf.Abs(local.x) <= serveSize.x * 0.5f && Mathf.Abs(local.y) <= serveSize.y * 0.5f &&
                   Mathf.Abs(local.z) <= serveSize.z * 0.5f;
        }

        void Leave(GuestMood mood, GuestShout shout)
        {
            m_Mood.Value = mood;
            m_LeaveAt = Time.time + 2.2f;
            if (m_Slot != null && m_Slot.Occupant == this) m_Slot.Occupant = null;
            ShoutRpc(shout);
        }

        [Rpc(SendTo.Everyone)]
        void ShoutRpc(GuestShout shout)
        {
            m_Shout = shout;
            m_ShoutUntil = Time.time + 1.8f;
        }

        // ---------- Everyone: visuals ----------

        static string ShoutText(GuestShout s) => s switch
        {
            GuestShout.Thanks => "Cheers!",
            GuestShout.Perfect => "PERFECT!",
            GuestShout.NiceSlide => "NICE SLIDE!",
            GuestShout.Sticky => "...it's sticky.",
            GuestShout.Wrong => "Not what I ordered!",
            GuestShout.Mystery => "What IS this?!",
            GuestShout.NotEnough => "That's half empty!",
            GuestShout.Angry => "Forget it!",
            _ => "",
        };

        void LateUpdate()
        {
            if (!IsSpawned) return;
            float t = Time.time + m_Seed;
            float since = Time.time - m_MoodChangedAt;

            // Body: idle sway, happy hop, angry stomp-off shake.
            if (body != null)
            {
                Vector3 pos = new Vector3(0f, Mathf.Abs(Mathf.Sin(t * 2.2f)) * 0.03f, 0f);
                Quaternion rot = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.3f) * 3f);
                if (m_Mood.Value == GuestMood.Happy)
                    pos.y += Mathf.Abs(Mathf.Sin(since * 9f)) * 0.18f * Mathf.Clamp01(1.5f - since);
                else if (m_Mood.Value == GuestMood.Angry)
                    rot *= Quaternion.Euler(0f, Mathf.Sin(since * 30f) * 12f * Mathf.Clamp01(1.2f - since) + Mathf.Clamp01(since - 0.8f) * 180f, 0f);
                else if (Patience01 < 0.3f)
                    rot *= Quaternion.Euler(Mathf.Sin(t * 14f) * 3f, 0f, 0f); // tapping their foot, impatiently
                body.localPosition = pos;
                body.localRotation = rot;
            }

            if (bubble == null) return;
            var cam = ViewCamera();
            if (cam != null)
            {
                Vector3 look = bubble.position - cam.transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.001f) bubble.rotation = Quaternion.LookRotation(look);
            }

            bool shouting = Time.time < m_ShoutUntil && m_Shout != GuestShout.None;
            bool waiting = m_Mood.Value == GuestMood.Waiting;
            bubble.gameObject.SetActive(waiting || shouting);
            string say = shouting ? ShoutText(m_Shout) : Drinks.Name(m_Order.Value);
            if (bubbleText != null && say != m_ShownText)
            {
                // Resize the sign to fit what's on it.
                m_ShownText = say;
                bubbleText.text = say;
                bubbleText.fontSize = shouting ? 1.6f : 2.6f;
                bubbleText.ForceMeshUpdate();
                if (bubblePlate != null)
                {
                    Bounds b = bubbleText.textBounds;
                    var plate = bubblePlate.transform;
                    plate.localScale = new Vector3(b.size.x + 0.2f, b.size.y + 0.14f, plate.localScale.z);
                    plate.localPosition = new Vector3(b.center.x, b.center.y, plate.localPosition.z);
                }
            }

            m_Block ??= new MaterialPropertyBlock();
            if (bubblePlate != null)
            {
                Color plate = !shouting ? Drinks.Plate(m_Order.Value)
                    : m_Shout is GuestShout.Thanks or GuestShout.Perfect or GuestShout.NiceSlide ? new Color(0.3f, 0.72f, 0.4f)
                    : new Color(0.85f, 0.3f, 0.25f);
                m_Block.SetColor("_BaseColor", plate);
                bubblePlate.SetPropertyBlock(m_Block);
            }

            if (patienceFill != null)
            {
                float p = Patience01;
                patienceFill.parent.gameObject.SetActive(waiting && !shouting);
                patienceFill.localScale = new Vector3(Mathf.Max(0.001f, p), 1f, 1f);
                if (patienceRenderer != null)
                {
                    m_Block.SetColor("_BaseColor", Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(0.35f, 0.85f, 0.4f), p));
                    patienceRenderer.SetPropertyBlock(m_Block);
                }
            }
        }

        /// <summary>The camera this machine is looking through (the local player's, not a leftover scene camera).</summary>
        static Camera ViewCamera()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && PlayerMotor.ByClientId.TryGetValue(nm.LocalClientId, out var me) && me != null &&
                me.Camera != null && me.Camera.isActiveAndEnabled)
                return me.Camera;
            return Camera.main;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.6f);
            Gizmos.DrawWireCube(serveCenter, serveSize);
        }
    }
}
