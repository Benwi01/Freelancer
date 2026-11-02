using System;
using Unity.Netcode;
using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// A player's chosen cosmetics, as small indices into the option lists below. Saved locally
    /// (PlayerPrefs) and synced to everyone through <see cref="PlayerAppearance"/>.
    /// </summary>
    [Serializable]
    public struct PlayerLook : INetworkSerializable, IEquatable<PlayerLook>
    {
        public byte Color;
        public byte Hat;
        public byte Eyes;
        public byte Mouth;
        public byte Outfit;

        // Option lists. The player prefab builds one variant per entry, in this order.
        public static readonly string[] ColorNames =
            { "Tomato", "Sky", "Lime", "Sunflower", "Grape", "Bubblegum", "Tangerine", "Teal" };
        public static readonly Color[] Colors =
        {
            new(0.95f, 0.35f, 0.3f), new(0.3f, 0.6f, 0.95f), new(0.4f, 0.85f, 0.4f), new(0.97f, 0.82f, 0.25f),
            new(0.6f, 0.42f, 0.88f), new(0.97f, 0.52f, 0.75f), new(1f, 0.6f, 0.2f), new(0.2f, 0.75f, 0.7f),
        };
        public static readonly string[] HatNames = { "Beanie", "GigGrabber cap", "Hard hat", "No hat" };
        public static readonly string[] EyeNames = { "Derpy", "Googly", "Grumpy", "Tired" };
        public static readonly string[] MouthNames = { "Grin", "Ooh", "None" };
        public static readonly string[] OutfitNames = { "Sweater", "Hi-vis vest" };

        public static PlayerLook Default => new() { Color = 0, Hat = 0, Eyes = 0, Mouth = 0, Outfit = 0 };

        public UnityEngine.Color TintColor => Colors[Color % Colors.Length];

        /// <summary>
        /// Where the look is saved. In the Editor each Multiplayer Play Mode player runs from its own
        /// project folder, so the folder is part of the key and every virtual player keeps its own look
        /// (otherwise they all share one save and end up identical).
        /// </summary>
        static string PrefsKey =>
#if UNITY_EDITOR
            "Freelancer.Look." + ((uint)Application.dataPath.GetHashCode()).ToString("x8");
#else
            "Freelancer.Look";
#endif

        /// <summary>This machine's saved look. First time: defaults with a random colour.</summary>
        public static PlayerLook LoadLocal()
        {
            var look = Default;
            string saved = PlayerPrefs.GetString(PrefsKey, "");
            var parts = saved.Split(',');
            if (parts.Length == 5)
            {
                look.Color = Parse(parts[0], Colors.Length);
                look.Hat = Parse(parts[1], HatNames.Length);
                look.Eyes = Parse(parts[2], EyeNames.Length);
                look.Mouth = Parse(parts[3], MouthNames.Length);
                look.Outfit = Parse(parts[4], OutfitNames.Length);
            }
            else
            {
                look.Color = (byte)UnityEngine.Random.Range(0, Colors.Length);
            }
            return look;
        }

        public void SaveLocal()
        {
            PlayerPrefs.SetString(PrefsKey, $"{Color},{Hat},{Eyes},{Mouth},{Outfit}");
            PlayerPrefs.Save();
        }

        static byte Parse(string s, int count) =>
            byte.TryParse(s, out byte b) && b < count ? b : (byte)0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Color);
            serializer.SerializeValue(ref Hat);
            serializer.SerializeValue(ref Eyes);
            serializer.SerializeValue(ref Mouth);
            serializer.SerializeValue(ref Outfit);
        }

        public bool Equals(PlayerLook o) =>
            Color == o.Color && Hat == o.Hat && Eyes == o.Eyes && Mouth == o.Mouth && Outfit == o.Outfit;

        public override bool Equals(object obj) => obj is PlayerLook o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(Color, Hat, Eyes, Mouth, Outfit);
    }
}
