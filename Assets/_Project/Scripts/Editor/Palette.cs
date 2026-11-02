using UnityEngine;

namespace Freelancer.EditorTools
{
    /// <summary>
    /// The game's colour palette: warm, slightly saturated flat colours for a cozy low-poly look.
    /// Every level material should pick from here so everything belongs to the same world.
    /// </summary>
    static class Palette
    {
        // Sky and light
        public static readonly Color SkyTop = Hex("3A8BF2");
        public static readonly Color SkyHorizon = Hex("FDEDD8");
        public static readonly Color SkyBelow = Hex("A3958A");
        public static readonly Color Sun = Hex("FFEACB");
        public static readonly Color AmbientSky = Hex("A9C4EA");
        public static readonly Color AmbientEquator = Hex("D9C3AE");
        public static readonly Color AmbientGround = Hex("6E5F55");
        public static readonly Color Fog = Hex("EFE2D2");

        // Street
        public static readonly Color Asphalt = Hex("4A4E5C");
        public static readonly Color Sidewalk = Hex("CFC6B8");
        public static readonly Color RoadPaint = Hex("FFD24A");

        // Building
        public static readonly Color Wall = Hex("F1E2C6");
        public static readonly Color Floor = Hex("B9845A");
        public static readonly Color Stairs = Hex("9DA3B4");
        public static readonly Color Trim = Hex("3E4E7A");
        public static readonly Color Hidden = Hex("7A7068");

        // Van
        public static readonly Color VanWhite = Hex("F4F4F2");
        public static readonly Color Glass = Hex("3C5670");
        public static readonly Color Tyre = Hex("24252B");

        // Furniture and props
        public static readonly Color Wood = Hex("8E5A36");
        public static readonly Color Couch = Hex("E39B3B");
        public static readonly Color Target = Hex("5BD47E");
        public static readonly Color Cardboard = Hex("D29B62");
        public static readonly Color PaintBucket = Hex("3FB5C2");

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
