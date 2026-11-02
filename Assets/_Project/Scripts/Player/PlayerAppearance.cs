using Unity.Netcode;
using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// Shows the player's chosen cosmetics. The owner sends their saved look on spawn; everyone
    /// switches the matching variant objects on. Also works on an unspawned copy (menu preview).
    /// </summary>
    public class PlayerAppearance : NetworkBehaviour
    {
        [Tooltip("One entry per option in PlayerLook; an empty entry means 'nothing'.")]
        [SerializeField] GameObject[] hats;
        [SerializeField] GameObject[] eyes;
        [SerializeField] GameObject[] mouths;
        [SerializeField] GameObject[] outfits;
        [Tooltip("Renderers painted in the player's colour (shirt, sleeves, hats).")]
        [SerializeField] Renderer[] tintRenderers;

        readonly NetworkVariable<PlayerLook> m_Look = new(PlayerLook.Default,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        MaterialPropertyBlock m_Block;

        public PlayerLook Look { get; private set; } = PlayerLook.Default;
        public Color Color => Look.TintColor;

        public override void OnNetworkSpawn()
        {
            if (IsOwner) m_Look.Value = PlayerLook.LoadLocal();
            m_Look.OnValueChanged += OnLookChanged;
            Apply(m_Look.Value);
        }

        public override void OnNetworkDespawn()
        {
            m_Look.OnValueChanged -= OnLookChanged;
        }

        void OnLookChanged(PlayerLook previous, PlayerLook current) => Apply(current);

        /// <summary>Show a look (any machine; also used by the menu preview).</summary>
        public void Apply(PlayerLook look)
        {
            Look = look;
            Show(hats, look.Hat);
            Show(eyes, look.Eyes);
            Show(mouths, look.Mouth);
            Show(outfits, look.Outfit);

            m_Block ??= new MaterialPropertyBlock();
            m_Block.SetColor("_BaseColor", look.TintColor);
            foreach (var r in tintRenderers)
                if (r != null) r.SetPropertyBlock(m_Block);
        }

        static void Show(GameObject[] variants, int index)
        {
            if (variants == null) return;
            for (int i = 0; i < variants.Length; i++)
                if (variants[i] != null) variants[i].SetActive(i == index);
        }
    }
}
