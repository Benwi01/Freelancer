using UnityEngine;

namespace Freelancer
{
    public enum Drink : byte
    {
        None,
        Beer,
        Wine,
        Mystery, // two drinks mixed in one glass. Nobody wants this.
    }

    public static class Drinks
    {
        public static string Name(Drink d) => d switch
        {
            Drink.Beer => "BEER",
            Drink.Wine => "WINE",
            Drink.Mystery => "???",
            _ => "",
        };

        public static Color Liquid(Drink d) => d switch
        {
            Drink.Beer => new Color(0.98f, 0.68f, 0.16f),
            Drink.Wine => new Color(0.55f, 0.08f, 0.22f),
            Drink.Mystery => new Color(0.45f, 0.5f, 0.2f),
            _ => Color.clear,
        };

        /// <summary>Sign colour behind the order in a guest's speech bubble.</summary>
        public static Color Plate(Drink d) => d switch
        {
            Drink.Beer => new Color(0.95f, 0.62f, 0.12f),
            Drink.Wine => new Color(0.62f, 0.12f, 0.3f),
            _ => new Color(0.3f, 0.3f, 0.35f),
        };
    }
}
