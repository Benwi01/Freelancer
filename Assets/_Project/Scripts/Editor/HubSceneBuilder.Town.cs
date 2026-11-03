using UnityEditor;
using UnityEngine;
using static Freelancer.EditorTools.SceneKit;

namespace Freelancer.EditorTools
{
    /// <summary>
    /// The town block around the crew's flat: Main Street running east-west in front of the
    /// apartment block, shops either side, a little park, and the gig van at the curb.
    ///
    /// Layout (top view, +z = north; the flat is at the origin, x -5..5, z -4..4):
    ///   north frontage z = -4.2 (facing south): fillers x -32..-6.2, apartment block -6.2..6.2,
    ///     THREADZ clothing 6.2..16, BJÖRKMÖBEL 16..32
    ///   north sidewalk z -4.2..-8.2, road z -8.2..-15.8, south sidewalk z -15.8..-19.8
    ///   south frontage z = -19.8 (facing north): Lucky Lenny's casino x -32..-11, park -11..5,
    ///     Pemberton Properties 5..15, filler 15..32
    ///   road closed at both ends (x ±29), invisible walls at x ±32.5
    /// </summary>
    static partial class HubSceneBuilder
    {
        const float NorthFront = -4.2f;
        const float SouthFront = -19.8f;
        const float RoadN = -8.2f, RoadS = -15.8f;
        const float RoadY = -0.15f;
        const float EndX = 32f;

        static readonly Color k_Threadz = new(0.93f, 0.56f, 0.62f);
        static readonly Color k_Bjork = new(0.2f, 0.42f, 0.36f);
        static readonly Color k_BjorkCream = new(0.98f, 0.93f, 0.78f);
        static readonly Color k_Casino = new(0.3f, 0.19f, 0.4f);
        static readonly Color k_Office = new(0.72f, 0.7f, 0.64f);

        static void BuildTown()
        {
            BuildStreet();
            BuildApartmentBlock();
            BuildThreadz();
            BuildBjorkmobel();
            BuildCasino();
            BuildPark();
            BuildLandlordOffice();
            BuildFillers();
            BuildStreetFurniture();
            BuildVan();
        }

        // ---------- Ground ----------

        static void BuildStreet()
        {
            var g = Group("Street");
            SetupUtil.Block("Asphalt", g, new Vector3(0f, RoadY - 0.5f, -12f), new Vector3(140f, 1f, 120f), M("Asphalt", Palette.Asphalt));
            var walk = M("Sidewalk", Palette.Sidewalk);
            SetupUtil.Block("SidewalkN", g, new Vector3(0f, -0.075f, (NorthFront + RoadN) / 2f), new Vector3(EndX * 2f, 0.15f, NorthFront - RoadN), walk);
            SetupUtil.Block("SidewalkS", g, new Vector3(0f, -0.075f, (RoadS + SouthFront) / 2f), new Vector3(EndX * 2f, 0.15f, RoadS - SouthFront), walk);
            var curb = M("Curb", Palette.Curb);
            Box("CurbN", g, new Vector3(0f, -0.07f, RoadN + 0.1f), new Vector3(EndX * 2f, 0.16f, 0.22f), curb);
            Box("CurbS", g, new Vector3(0f, -0.07f, RoadS - 0.1f), new Vector3(EndX * 2f, 0.16f, 0.22f), curb);
            // Paving joints, so the sidewalk doesn't read as one flat slab.
            var joint = M("PavingJoint", Palette.Curb);
            for (float x = -EndX + 2f; x < EndX; x += 2f)
            {
                Box("Joint", g, new Vector3(x, 0.002f, (NorthFront + RoadN) / 2f), new Vector3(0.04f, 0.004f, NorthFront - RoadN), joint);
                Box("Joint", g, new Vector3(x, 0.002f, (RoadS + SouthFront) / 2f), new Vector3(0.04f, 0.004f, RoadS - SouthFront), joint);
            }

            var paint = M("RoadPaint", Palette.RoadPaint);
            float mid = (RoadN + RoadS) / 2f;
            for (float x = -EndX + 3f; x < EndX - 2f; x += 4f)
                if (Mathf.Abs(x + 3f) > 3f) Box("LaneMark", g, new Vector3(x, RoadY + 0.005f, mid), new Vector3(2f, 0.01f, 0.18f), paint);
            // Zebra crossing from the flat over to the park.
            var white = M("PartyWhite", Palette.PartyWhite);
            for (float z = RoadN - 0.5f; z > RoadS + 0.3f; z -= 1f)
                Box("Zebra", g, new Vector3(-3f, RoadY + 0.006f, z), new Vector3(3f, 0.01f, 0.5f), white);

            // Road closed at both ends, plus invisible walls so nobody wanders off the map.
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * (EndX - 3f);
                for (float z = RoadN - 1f; z > RoadS; z -= 2.4f)
                {
                    Solid(PrimitiveType.Cube, "Barrier", g, new Vector3(x, RoadY + 0.5f, z - 0.8f), new Vector3(0.3f, 1f, 2f), M("Hydrant", Palette.Hydrant));
                    Box("BarrierStripe", g, new Vector3(x, RoadY + 0.6f, z - 0.8f), new Vector3(0.32f, 0.25f, 2.02f), white);
                }
                Label("ROAD CLOSED", g, new Vector3(x - s * 0.17f, RoadY + 1.3f, mid), Quaternion.Euler(0f, s * 90f, 0f), 0.22f,
                    Palette.PartyWhite, Palette.Hydrant);
                var wall = SetupUtil.Empty("EdgeWall", g, new Vector3(s * (EndX + 0.5f), 4f, -12f)).AddComponent<BoxCollider>();
                wall.size = new Vector3(1f, 10f, 60f);
            }
        }

        // ---------- North side ----------

        /// <summary>The flat's building: ground-floor flat, three storeys above, brick front with the open door.</summary>
        static void BuildApartmentBlock()
        {
            var g = Group("ApartmentBlock");
            var wall = M("HubWall", Palette.HubWall);
            const float top = 12f, x0 = -6.2f, x1 = 6.2f, back = 5.8f;

            // Solid mass around and above the flat (the flat's own walls are the inner faces).
            SetupUtil.Block("Upper", g, new Vector3(0f, (3.1f + top) / 2f, (NorthFront + back) / 2f), new Vector3(x1 - x0, top - 3.1f, back - NorthFront), wall);
            SetupUtil.Block("SideW", g, new Vector3((x0 - 5.2f) / 2f, 1.55f, 0f), new Vector3(1f, 3.1f, 8.4f), wall);
            SetupUtil.Block("SideE", g, new Vector3((x1 + 5.2f) / 2f, 1.55f, 0f), new Vector3(1f, 3.1f, 8.4f), wall);
            SetupUtil.Block("Back", g, new Vector3(0f, 1.55f, (4.2f + back) / 2f), new Vector3(x1 - x0, 3.1f, back - 4.2f), wall);

            // Brick front, leaving the doorway (x 2.65..3.75, 2.3 high) open.
            var brick = M("Brick", Palette.Brick);
            float face = NorthFront - 0.02f;
            Box("BrickW", g, new Vector3((x0 + 2.65f) / 2f, top / 2f, face), new Vector3(2.65f - x0, top, 0.04f), brick);
            Box("BrickE", g, new Vector3((3.75f + x1) / 2f, top / 2f, face), new Vector3(x1 - 3.75f, top, 0.04f), brick);
            Box("BrickOverDoor", g, new Vector3(3.2f, (2.3f + top) / 2f, face), new Vector3(1.1f, top - 2.3f, 0.04f), brick);
            Box("Plinth", g, new Vector3((x0 + 2.5f) / 2f, 0.22f, face - 0.03f), new Vector3(2.5f - x0, 0.45f, 0.06f), Stone);
            Box("PlinthE", g, new Vector3((3.9f + x1) / 2f, 0.22f, face - 0.03f), new Vector3(x1 - 3.9f, 0.45f, 0.06f), Stone);
            foreach (float y in new[] { 3.1f, 6.1f, 9.1f })
                Box("Band", g, new Vector3(0f, y, face - 0.04f), new Vector3(x1 - x0 + 0.2f, 0.12f, 0.07f), Stone);
            Box("Cornice", g, new Vector3(0f, top + 0.15f, face + 0.2f), new Vector3(x1 - x0 + 0.4f, 0.35f, 0.6f), Stone);
            foreach (float y in new[] { 4.6f, 7.6f, 10.6f })
            foreach (float x in new[] { -4.6f, -2.1f, 0.4f, 3.2f, 5.2f })
                Window(g, new Vector3(x, y, NorthFront), 0f, x > 5f ? 0.7f : 1f, 1.3f);
            foreach (float x in new[] { -3.6f, -0.6f })
                Window(g, new Vector3(x, 1.6f, NorthFront), 0f, 1.1f, 1.2f);

            // Doorway: stone surround, awning, doormat, number and buzzer.
            Box("JambL", g, new Vector3(2.58f, 1.2f, face - 0.05f), new Vector3(0.14f, 2.4f, 0.1f), Stone);
            Box("JambR", g, new Vector3(3.82f, 1.2f, face - 0.05f), new Vector3(0.14f, 2.4f, 0.1f), Stone);
            Box("Head", g, new Vector3(3.2f, 2.38f, face - 0.05f), new Vector3(1.4f, 0.16f, 0.1f), Stone);
            var awning = SetupUtil.Empty("Awning", g, new Vector3(3.2f, 2.7f, NorthFront - 0.45f)).transform;
            awning.localRotation = Quaternion.Euler(-16f, 0f, 0f);
            Box("Canvas", awning, Vector3.zero, new Vector3(1.6f, 0.05f, 0.95f), M("Awning", Palette.Awning));
            for (int i = 0; i < 3; i++)
                Box("Stripe", awning, new Vector3(-0.5f + i * 0.5f, 0.005f, 0f), new Vector3(0.2f, 0.05f, 0.952f), M("AwningStripe", Palette.PartyWhite));
            Box("Doormat", g, new Vector3(3.2f, 0.007f, NorthFront - 0.45f), new Vector3(1.1f, 0.012f, 0.6f), M("Doormat", Palette.Doormat));
            Label("No. 7", g, new Vector3(4.25f, 2.0f, face - 0.01f), Quaternion.identity, 0.09f, Palette.Trim, Palette.Stone);
            Box("Buzzer", g, new Vector3(4.15f, 1.3f, face - 0.02f), new Vector3(0.14f, 0.3f, 0.03f), Metal);
            Label("4C: THE CREW", g, new Vector3(4.15f, 1.55f, face - 0.03f), Quaternion.identity, 0.025f, Palette.Trim, Palette.PartyWhite);
        }

        static void BuildThreadz()
        {
            var g = Group("Threadz");
            var b = Shop(g, "THREADZ", "clothes for people who work", 6.2f, 16f, NorthFront, -1f, 6.5f, k_Threadz, Palette.PartyWhite,
                bayFrom: 7f, bayTo: 12.6f, doorX: 14.3f);

            // Window display: three mannequins in this season's workwear.
            var skin = M("Mannequin", new Color(0.92f, 0.9f, 0.86f));
            var outfits = new[] { Palette.GigOrange, new Color(0.25f, 0.4f, 0.7f), Palette.PartyPink };
            for (int i = 0; i < 3; i++)
            {
                var m = SetupUtil.Empty("Mannequin", b, new Vector3(8.1f + i * 1.7f, 0.05f, NorthFront + 0.75f));
                m.transform.localRotation = Quaternion.Euler(0f, 180f + (i - 1) * 20f, 0f);
                var parts = PrefabBuilder.BuildNpc(m.transform, M("Outfit" + i, outfits[i]), skin, armsForward: false);
                parts.Hats[i].SetActive(true);
                foreach (var t in m.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
                Vis(LowPolyMeshes.Cylinder, "Stand", b, new Vector3(8.1f + i * 1.7f, 0.06f, NorthFront + 0.75f), new Vector3(0.5f, 0.03f, 0.5f), Metal);
            }
            Label("NEW IN: HI-VIS", g, new Vector3(9.8f, 2.35f, NorthFront - 0.02f), Quaternion.identity, 0.08f, Palette.PartyWhite, Palette.Hydrant);
        }

        static void BuildBjorkmobel()
        {
            var g = Group("Bjorkmobel");
            var b = Shop(g, "BJÖRKMÖBEL", "furniture, some assembly required", 16f, EndX, NorthFront, -1f, 9f, k_Bjork, k_BjorkCream,
                bayFrom: 17.2f, bayTo: 26.5f, doorX: 29f);

            // Window display: a stack of flat-pack boxes, a sofa and a lamp.
            var card = M("Cardboard", Palette.Cardboard);
            var bay = NorthFront + 0.7f;
            for (int i = 0; i < 5; i++)
                Box("FlatPack", b, new Vector3(17.9f + (i % 3) * 0.15f, 0.12f + i * 0.17f, bay), new Vector3(1.3f, 0.16f, 0.7f), card,
                    Quaternion.Euler(0f, i * 7f - 10f, 0f));
            Label("SNÖRK", g, new Vector3(17.95f, 0.65f, bay - 0.36f), Quaternion.identity, 0.06f, Palette.Trim);
            var sofa = M("SofaGreen", new Color(0.55f, 0.7f, 0.55f));
            Box("SofaSeat", b, new Vector3(21.6f, 0.3f, bay), new Vector3(2.4f, 0.5f, 0.85f), sofa);
            Box("SofaBack", b, new Vector3(21.6f, 0.7f, bay + 0.32f), new Vector3(2.4f, 0.75f, 0.22f), sofa);
            foreach (float s in new[] { -1.1f, 1.1f })
                Box("SofaArm", b, new Vector3(21.6f + s, 0.45f, bay), new Vector3(0.22f, 0.55f, 0.85f), sofa);
            Box("PriceTag", b, new Vector3(22.4f, 0.75f, bay - 0.45f), new Vector3(0.5f, 0.3f, 0.01f), M("PartyWhite", Palette.PartyWhite));
            Label("KLÖMP\n$499", g, new Vector3(22.4f, 0.75f, bay - 0.46f), Quaternion.identity, 0.05f, Palette.Trim);
            Solid(PrimitiveType.Cylinder, "LampPole", b, new Vector3(24.8f, 0.9f, bay), new Vector3(0.05f, 0.85f, 0.05f), Metal);
            Vis(LowPolyMeshes.Cylinder, "LampShade", b, new Vector3(24.8f, 1.8f, bay), new Vector3(0.5f, 0.2f, 0.5f), M("PartyWhite", Palette.PartyWhite));
            Label("Assembly: 12 minutes*", g, new Vector3(21.8f, 2.35f, NorthFront - 0.02f), Quaternion.identity, 0.07f, k_Bjork, k_BjorkCream);
        }

        // ---------- South side ----------

        static void BuildCasino()
        {
            var g = Group("Casino");
            float o = 1f, front = SouthFront;
            var face = Quaternion.Euler(0f, 180f, 0f);
            const float x0 = -EndX, x1 = -11f, h = 8f;
            Body(g, x0, x1, front, o, 10f, h, k_Casino);

            // Dark tinted windows and a gold trim band.
            var tint = M("CasinoGlass", new Color(0.12f, 0.1f, 0.18f), 0.9f);
            var gold = M("PartyGold", Palette.PartyGold);
            for (float x = x0 + 2.5f; x < x1 - 1f; x += 3f)
            {
                if (Mathf.Abs(x - -21.5f) < 2.5f) continue; // entrance
                Box("TintedWindow", g, new Vector3(x, 1.7f, front + 0.03f), new Vector3(2f, 2.2f, 0.04f), tint);
                Box("WindowTrim", g, new Vector3(x, 2.85f, front + 0.05f), new Vector3(2.2f, 0.08f, 0.06f), gold);
            }
            Box("GoldBand", g, new Vector3((x0 + x1) / 2f, 3.4f, front + 0.05f), new Vector3(x1 - x0, 0.15f, 0.08f), gold);

            // The big sign: name on a board ringed with bulbs.
            var sign = new Vector3(-21.5f, 5.2f, front + 0.08f);
            Box("SignBoard", g, sign, new Vector3(9f, 2.2f, 0.1f), M("CasinoSign", new Color(0.55f, 0.1f, 0.2f)));
            Label("LUCKY LENNY'S", g, sign + new Vector3(0f, 0.3f, 0.07f), face, 0.6f, Palette.PartyGold);
            Label("C A S I N O", g, sign + new Vector3(0f, -0.6f, 0.07f), face, 0.25f, Palette.PartyWhite);
            var bulb = SetupUtil.Emissive("CasinoBulb", Palette.PartyGold, 2.4f);
            for (float x = -4.3f; x <= 4.31f; x += 0.43f)
                foreach (float y in new[] { -1.05f, 1.05f })
                    Vis(LowPolyMeshes.Sphere, "Bulb", g, sign + new Vector3(x, y, 0.08f), Vector3.one * 0.13f, bulb);
            for (float y = -0.62f; y <= 0.63f; y += 0.42f)
                foreach (float x in new[] { -4.45f, 4.45f })
                    Vis(LowPolyMeshes.Sphere, "Bulb", g, sign + new Vector3(x, y, 0.08f), Vector3.one * 0.13f, bulb);

            // Entrance: gold doors (shut for now), red carpet and a velvet rope.
            Box("DoorFrame", g, new Vector3(-21.5f, 1.3f, front + 0.04f), new Vector3(2.8f, 2.6f, 0.06f), gold);
            Box("Doors", g, new Vector3(-21.5f, 1.2f, front + 0.07f), new Vector3(2.4f, 2.4f, 0.04f), tint);
            Box("Carpet", g, new Vector3(-21.5f, 0.006f, front + 2f), new Vector3(2f, 0.012f, 4f), M("Runner", new Color(0.78f, 0.25f, 0.32f)));
            foreach (float x in new[] { -23f, -20f })
            {
                Solid(PrimitiveType.Cylinder, "RopePost", g, new Vector3(x, 0.5f, front + 2.2f), new Vector3(0.12f, 0.5f, 0.12f), gold);
                Vis(LowPolyMeshes.Sphere, "PostTop", g, new Vector3(x, 1.05f, front + 2.2f), Vector3.one * 0.16f, gold);
            }
            Vis(LowPolyMeshes.Cylinder, "Rope", g, new Vector3(-21.5f, 0.85f, front + 2.2f), new Vector3(0.05f, 1.5f, 0.05f), M("CasinoSign", new Color(0.55f, 0.1f, 0.2f)),
                Quaternion.Euler(0f, 0f, 90f));
            OpeningSoon(g, new Vector3(-21.5f, 1.4f, front + 0.12f), face, "Feeling lucky? Soon.");
        }

        static void BuildPark()
        {
            var g = Group("Park");
            const float x0 = -11f, x1 = 5f, back = -30f;
            float cx = (x0 + x1) / 2f, cz = (SouthFront + back) / 2f;
            SetupUtil.Block("Grass", g, new Vector3(cx, -0.07f, cz), new Vector3(x1 - x0, 0.14f, SouthFront - back), M("Grass", Palette.Leaf * 0.85f));
            var path = M("Gravel", Palette.Sidewalk * 0.92f);
            Box("Path", g, new Vector3(-3f, 0.004f, cz), new Vector3(1.8f, 0.01f, SouthFront - back), path);
            Box("PathCross", g, new Vector3(cx, 0.004f, -25f), new Vector3(x1 - x0, 0.01f, 1.6f), path);

            // Low hedges along the street, gap for the path.
            var hedge = M("LeafDark", Palette.LeafDark);
            Solid(PrimitiveType.Cube, "Hedge", g, new Vector3((x0 + -4.2f) / 2f, 0.4f, SouthFront - 0.5f), new Vector3(-4.2f - x0, 0.8f, 0.7f), hedge);
            Solid(PrimitiveType.Cube, "Hedge", g, new Vector3((-1.8f + x1) / 2f, 0.4f, SouthFront - 0.5f), new Vector3(x1 + 1.8f, 0.8f, 0.7f), hedge);
            Solid(PrimitiveType.Cube, "HedgeBack", g, new Vector3(cx, 0.4f, back + 0.4f), new Vector3(x1 - x0, 0.8f, 0.7f), hedge);

            // Fountain in the middle (solid, so it's something to stand around or fall into... no, around).
            var stone = Stone;
            var water = M("Water", new Color(0.45f, 0.7f, 0.9f), 0.9f);
            Solid(PrimitiveType.Cylinder, "Basin", g, new Vector3(-3f, 0.3f, -25f), new Vector3(3.4f, 0.3f, 3.4f), stone);
            Vis(LowPolyMeshes.Cylinder, "Water", g, new Vector3(-3f, 0.58f, -25f), new Vector3(3.1f, 0.03f, 3.1f), water);
            Solid(PrimitiveType.Cylinder, "Column", g, new Vector3(-3f, 0.9f, -25f), new Vector3(0.35f, 0.6f, 0.35f), stone);
            Vis(LowPolyMeshes.Cylinder, "Bowl", g, new Vector3(-3f, 1.55f, -25f), new Vector3(1.2f, 0.1f, 1.2f), stone);
            Vis(LowPolyMeshes.Cylinder, "BowlWater", g, new Vector3(-3f, 1.64f, -25f), new Vector3(1.05f, 0.02f, 1.05f), water);
            Vis(LowPolyMeshes.Sphere, "Spout", g, new Vector3(-3f, 1.9f, -25f), new Vector3(0.25f, 0.5f, 0.25f), water);
            Label("IN MEMORY OF NOBODY IN PARTICULAR", g, new Vector3(-3f, 0.35f, -23.28f), Quaternion.Euler(0f, 180f, 0f), 0.05f, Palette.Trim);

            // Benches facing the fountain, trees, a lamp and a bin.
            Bench(g, new Vector3(-7.5f, 0f, -25f), -90f);
            Bench(g, new Vector3(1.5f, 0f, -25f), 90f);
            Bench(g, new Vector3(-6f, 0f, -21.6f), 0f);
            foreach (var p in new[] { new Vector3(-9f, 0f, -28f), new Vector3(3f, 0f, -28.2f), new Vector3(2.8f, 0f, -21.8f), new Vector3(-9.2f, 0f, -21.8f) })
                Tree(g, p);
            StreetLamp(g, new Vector3(-1.3f, 0f, -22.8f), 0f);
            Vis(LowPolyMeshes.Cylinder, "Bin", g, new Vector3(-4.6f, 0.35f, -22.4f), new Vector3(0.45f, 0.35f, 0.45f), M("LeafDark", Palette.LeafDark));
            Label("LITTLE PARK", g, new Vector3(-3f, 1.15f, SouthFront - 0.12f), Quaternion.Euler(0f, 180f, 0f), 0.12f, Palette.PartyWhite, Palette.LeafDark);
            Solid(PrimitiveType.Cube, "SignPost", g, new Vector3(-3f, 0.5f, SouthFront - 0.1f), new Vector3(0.08f, 1f, 0.08f), Metal);
        }

        static void BuildLandlordOffice()
        {
            var g = Group("PembertonProperties");
            float o = 1f, front = SouthFront;
            var face = Quaternion.Euler(0f, 180f, 0f);
            const float x0 = 5f, x1 = 15f, h = 7f;
            Body(g, x0, x1, front, o, 10f, h, k_Office);
            Box("Cornice", g, new Vector3((x0 + x1) / 2f, h + 0.12f, front - 5f), new Vector3(x1 - x0 + 0.1f, 0.25f, 10.3f), Stone);
            foreach (float y in new[] { 1.6f, 4.6f })
            foreach (float x in new[] { 6.5f, 8.7f, 13.3f })
                Window(g, new Vector3(x, y, front), 180f, 1.2f, 1.3f);
            Window(g, new Vector3(11f, 4.6f, front), 180f, 1.2f, 1.3f);

            Box("Door", g, new Vector3(11f, 1.1f, front + 0.05f), new Vector3(1.1f, 2.2f, 0.06f), M("Door2", Palette.Doors[2]));
            Box("DoorHead", g, new Vector3(11f, 2.3f, front + 0.06f), new Vector3(1.4f, 0.15f, 0.1f), Stone);
            Box("Sign", g, new Vector3(10f, 3.15f, front + 0.06f), new Vector3(6.6f, 0.7f, 0.06f), M("Navy", new Color(0.18f, 0.23f, 0.37f)));
            Label("PEMBERTON PROPERTIES LTD.", g, new Vector3(10f, 3.2f, front + 0.1f), face, 0.2f, Palette.PartyGold);
            Label("Est. 1987  ·  No pets  ·  No refunds", g, new Vector3(10f, 2.95f, front + 0.1f), face, 0.07f, Palette.PartyWhite);
            Box("Note", g, new Vector3(11f, 1.5f, front + 0.09f), new Vector3(0.45f, 0.35f, 0.01f), M("StickyNote", Palette.StickyNote));
            Label("BACK IN\n5 MINUTES", g, new Vector3(11f, 1.5f, front + 0.1f), face, 0.045f, Palette.Trim);
            Box("ToLet", g, new Vector3(8.7f, 1.6f, front + 0.12f), new Vector3(0.8f, 0.55f, 0.02f), M("PartyWhite", Palette.PartyWhite));
            Label("TO LET\n(again)", g, new Vector3(8.7f, 1.6f, front + 0.14f), face, 0.07f, Palette.Hydrant);
        }

        static void BuildFillers()
        {
            var g = Group("Neighbours");
            Filler(g, "Filler_NW", -EndX, -20f, NorthFront, -1f, 10f, 4);
            Filler(g, "Filler_N", -20f, -6.2f, NorthFront, -1f, 8f, 2);
            Filler(g, "Filler_SE", 15f, EndX, SouthFront, 1f, 10f, 1);
            Filler(g, "Filler_ParkBack", -11f, 5f, -30.2f, 1f, 7f, 0);
            // Buildings across both ends of the street, so the view stops at a wall, not the void.
            foreach (float s in new[] { -1f, 1f })
            {
                float face = s * (EndX + 0.6f);
                var end = SetupUtil.Empty(s < 0 ? "EndCap_W" : "EndCap_E", g, Vector3.zero).transform;
                SetupUtil.Block("Body", end, new Vector3(face + s * 6f, 5f, -12f), new Vector3(12f, 10f, 56f), M("Neighbour3", Palette.Neighbours[3]));
                Box("Cornice", end, new Vector3(face + s * 6f, 10.12f, -12f), new Vector3(12.3f, 0.25f, 56.1f), Stone);
                for (float y = 1.6f; y < 9f; y += 3f)
                for (float z = -30f; z < 6f; z += 2.6f)
                    Window(end, new Vector3(face, y, z), s > 0 ? 90f : -90f, 1f, 1.3f);
            }
            Label("LAUNDRETTE", g, new Vector3(-13f, 3.1f, NorthFront - 0.06f), Quaternion.identity, 0.24f, Palette.PartyWhite, new Color(0.3f, 0.55f, 0.75f));
        }

        // ---------- Street furniture and the van ----------

        static void BuildStreetFurniture()
        {
            var g = Group("StreetFurniture");
            foreach (float x in new[] { -24f, -12f, 9f, 21f }) StreetLamp(g, new Vector3(x, 0f, RoadN + 0.5f), 0f);
            foreach (float x in new[] { -18f, 12.5f, 26f }) StreetLamp(g, new Vector3(x, 0f, RoadS - 0.5f), 180f);
            foreach (float x in new[] { -27f, 19f, 29f }) Tree(g, new Vector3(x, 0f, RoadS - 1.2f));
            foreach (float x in new[] { -16f, -9f }) Tree(g, new Vector3(x, 0f, RoadN + 1.1f));

            Bench(g, new Vector3(-4f, 0f, NorthFront - 0.7f), 0f);
            var hydrant = M("Hydrant", Palette.Hydrant);
            Solid(PrimitiveType.Cylinder, "Hydrant", g, new Vector3(5.6f, 0.35f, RoadN + 0.5f), new Vector3(0.28f, 0.35f, 0.28f), hydrant);
            Vis(LowPolyMeshes.Sphere, "HydrantCap", g, new Vector3(5.6f, 0.7f, RoadN + 0.5f), Vector3.one * 0.3f, hydrant);
            var mail = M("Mailbox", Palette.Mailbox);
            Solid(PrimitiveType.Cube, "Mailbox", g, new Vector3(-7f, 0.5f, NorthFront - 0.5f), new Vector3(0.5f, 1f, 0.45f), mail);
            Vis(LowPolyMeshes.Cylinder, "MailboxTop", g, new Vector3(-7f, 1f, NorthFront - 0.5f), new Vector3(0.5f, 0.225f, 0.45f), mail, Quaternion.Euler(0f, 0f, 90f));
            Box("MailSlot", g, new Vector3(-7f, 0.8f, NorthFront - 0.73f), new Vector3(0.3f, 0.05f, 0.01f), Metal);

            // Street name sign on the corner lamp.
            Box("StreetSign", g, new Vector3(9f, 2.6f, RoadN + 0.5f), new Vector3(1.4f, 0.32f, 0.04f), M("RoadSign", new Color(0.2f, 0.45f, 0.3f)));
            Label("MAIN ST", g, new Vector3(9f, 2.6f, RoadN + 0.47f), Quaternion.identity, 0.12f, Palette.PartyWhite);
            Label("MAIN ST", g, new Vector3(9f, 2.6f, RoadN + 0.53f), Quaternion.Euler(0f, 180f, 0f), 0.12f, Palette.PartyWhite);
        }

        /// <summary>The crew van, parked at the curb outside the flat facing east. E opens GigGrabber.</summary>
        static void BuildVan()
        {
            var g = Group("GigVan");
            var white = M("VanWhite", Palette.VanWhite);
            var glass = M("VanGlass", Palette.Glass, 0.8f);
            var tyre = M("Tyre", Palette.Tyre);
            var bumper = M("Bumper", Palette.Bumper);
            var orange = M("GigOrange", Palette.GigOrange);
            const float z = -9.75f, bottom = RoadY + 0.5f;
            const float n = z + 1.1f, s = z - 1.1f; // north (sidewalk) and south sides

            SetupUtil.Block("Cargo", g, new Vector3(-1.15f, bottom + 1f, z), new Vector3(3.7f, 2f, 2.2f), white);
            SetupUtil.Block("Cab", g, new Vector3(1.55f, bottom + 0.75f, z), new Vector3(1.7f, 1.5f, 2.2f), white);
            SetupUtil.Block("Hood", g, new Vector3(2.65f, bottom + 0.35f, z), new Vector3(0.5f, 0.7f, 2.1f), white);
            SetupUtil.Block("BumperFront", g, new Vector3(2.95f, bottom + 0.05f, z), new Vector3(0.16f, 0.2f, 2.2f), bumper);
            SetupUtil.Block("BumperRear", g, new Vector3(-3.05f, bottom + 0.05f, z), new Vector3(0.14f, 0.18f, 2.2f), bumper);
            Box("Windshield", g, new Vector3(2.41f, bottom + 1.1f, z), new Vector3(0.02f, 0.6f, 1.9f), glass);
            var headlight = SetupUtil.Emissive("Headlight", Palette.WarmLight, 2.5f);
            foreach (float dz in new[] { -0.75f, 0.75f })
            {
                Box("Headlight", g, new Vector3(2.91f, bottom + 0.45f, z + dz), new Vector3(0.03f, 0.16f, 0.36f), headlight);
                Box("Taillight", g, new Vector3(-3.01f, bottom + 0.6f, z + dz * 1.2f), new Vector3(0.04f, 0.35f, 0.12f), SetupUtil.Emissive("Taillight", Palette.Taillight, 1.6f));
            }
            foreach (float x in new[] { -2.2f, 1.6f })
            foreach (float side in new[] { n + 0.01f, s - 0.01f })
            {
                Vis(LowPolyMeshes.Cylinder, "Wheel", g, new Vector3(x, RoadY + 0.35f, side - Mathf.Sign(side - z) * 0.06f), new Vector3(0.7f, 0.12f, 0.7f), tyre,
                    Quaternion.Euler(90f, 0f, 0f));
                Vis(LowPolyMeshes.Cylinder, "Hubcap", g, new Vector3(x, RoadY + 0.35f, side + Mathf.Sign(side - z) * 0.02f), new Vector3(0.36f, 0.02f, 0.36f), Stone,
                    Quaternion.Euler(90f, 0f, 0f));
            }
            // Both sides: orange stripe, cab window, logo. The sidewalk side also gets the sliding door.
            foreach (float side in new[] { n, s })
            {
                float o = Mathf.Sign(side - z);
                var read = o > 0 ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
                Box("Stripe", g, new Vector3(-0.2f, bottom + 0.75f, side + o * 0.01f), new Vector3(5.6f, 0.3f, 0.02f), orange);
                Box("CabWindow", g, new Vector3(1.6f, bottom + 1.15f, side + o * 0.01f), new Vector3(1f, 0.5f, 0.02f), glass);
                Label("GigGrabber", g, new Vector3(-1.4f, bottom + 1.45f, side + o * 0.02f), read, 0.3f, Palette.GigOrange);
                Label("be your own boss*", g, new Vector3(-1.4f, bottom + 1.1f, side + o * 0.02f), read, 0.09f, Palette.Trim);
            }
            Box("SlidingDoor", g, new Vector3(0.15f, bottom + 1f, n + 0.012f), new Vector3(1.1f, 1.8f, 0.01f), M("VanSeam", Palette.VanWhite * 0.85f));
            Box("DoorHandle", g, new Vector3(-0.3f, bottom + 1f, n + 0.03f), new Vector3(0.2f, 0.05f, 0.04f), bumper);

            // E on the van: the GigGrabber tablet on the dashboard.
            var van = SetupUtil.Empty("GigVanTablet", g, new Vector3(-0.2f, bottom + 1f, z));
            var trigger = van.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(6.2f, 2.4f, 2.8f);
            van.AddComponent<GigVan>();
        }

        // ---------- Helpers ----------

        /// <summary>
        /// A shop: solid body behind a recessed display window, a sign board over the front and a
        /// shut door with an "opening soon" sign. Returns the group to add the display to.
        /// <paramref name="o"/> is the outward direction in z (-1 = faces south).
        /// </summary>
        static Transform Shop(Transform g, string name, string slogan, float x0, float x1, float front, float o, float h, Color body, Color text,
            float bayFrom, float bayTo, float doorX)
        {
            const float bayDepth = 1.4f, bayTop = 2.7f;
            var read = o < 0 ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
            var mat = M("Shop_" + name, body);
            float inward = -o;

            // Body behind the display bay, plus the wall around the bay.
            float zBack = front + inward * bayDepth;
            SetupUtil.Block("Body", g, new Vector3((x0 + x1) / 2f, h / 2f, zBack + inward * 5f), new Vector3(x1 - x0, h, 10f), mat);
            SetupUtil.Block("Upper", g, new Vector3((x0 + x1) / 2f, (bayTop + h) / 2f, front + inward * bayDepth / 2f), new Vector3(x1 - x0, h - bayTop, bayDepth), mat);
            SetupUtil.Block("PierW", g, new Vector3((x0 + bayFrom) / 2f, bayTop / 2f, front + inward * bayDepth / 2f), new Vector3(bayFrom - x0, bayTop, bayDepth), mat);
            SetupUtil.Block("PierE", g, new Vector3((bayTo + x1) / 2f, bayTop / 2f, front + inward * bayDepth / 2f), new Vector3(x1 - bayTo, bayTop, bayDepth), mat);

            // The bay: floor, back wall in a lighter shade, glass you can't walk through.
            var bay = SetupUtil.Empty("Display", g, Vector3.zero).transform;
            Box("BayFloor", bay, new Vector3((bayFrom + bayTo) / 2f, 0.03f, front + inward * bayDepth / 2f), new Vector3(bayTo - bayFrom, 0.06f, bayDepth),
                M("BayFloor", new Color(0.95f, 0.93f, 0.88f)));
            Box("BayBack", bay, new Vector3((bayFrom + bayTo) / 2f, bayTop / 2f, zBack - inward * 0.01f), new Vector3(bayTo - bayFrom, bayTop, 0.02f),
                M("Shop_" + name + "_Light", Color.Lerp(body, Color.white, 0.55f)));
            var glass = Solid(PrimitiveType.Cube, "ShopGlass", bay, new Vector3((bayFrom + bayTo) / 2f, bayTop / 2f, front), new Vector3(bayTo - bayFrom, bayTop, 0.04f),
                SetupUtil.Transparent("ShopWindow", new Color(0.75f, 0.88f, 0.95f, 0.18f)));
            glass.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Box("WindowFrameTop", g, new Vector3((bayFrom + bayTo) / 2f, bayTop, front + o * 0.02f), new Vector3(bayTo - bayFrom + 0.1f, 0.1f, 0.06f), Trim);
            Box("WindowFrameLow", g, new Vector3((bayFrom + bayTo) / 2f, 0.1f, front + o * 0.02f), new Vector3(bayTo - bayFrom + 0.1f, 0.2f, 0.06f), Trim);
            Lamp(g, new Vector3((bayFrom + bayTo) / 2f, bayTop - 0.3f, front + inward * bayDepth * 0.5f), 4f, 0.8f);

            // Sign board across the front.
            var sign = new Vector3((x0 + x1) / 2f, bayTop + 0.75f, front + o * 0.06f);
            Box("SignBoard", g, sign, new Vector3(Mathf.Min(x1 - x0 - 1f, 10f), 1.1f, 0.08f), M("Shop_" + name + "_Sign", Color.Lerp(body, Color.black, 0.35f)));
            Label(name, g, sign + new Vector3(0f, 0.12f, o * 0.06f), read, 0.5f, text);
            Label(slogan, g, sign + new Vector3(0f, -0.35f, o * 0.06f), read, 0.1f, text);

            // Shut door with a sign on it.
            Box("Door", g, new Vector3(doorX, 1.1f, front + o * 0.03f), new Vector3(1.2f, 2.2f, 0.06f), M("Shop_" + name + "_Door", Color.Lerp(body, Color.black, 0.5f)));
            OpeningSoon(g, new Vector3(doorX, 1.35f, front + o * 0.08f), read, "Grand opening soon!");

            // Upper floor windows above the sign.
            for (float y = bayTop + 2.4f; y < h - 0.9f; y += 2.6f)
            for (float x = x0 + 1.4f; x < x1 - 0.9f; x += 2.4f)
                Window(g, new Vector3(x, y, front), o < 0 ? 0f : 180f, 1f, 1.2f);
            Box("Cornice", g, new Vector3((x0 + x1) / 2f, h + 0.12f, front + inward * 5f), new Vector3(x1 - x0 + 0.1f, 0.25f, 10.4f), Stone);
            return bay;
        }

        /// <summary>A plain solid block with its front at <paramref name="front"/>.</summary>
        static void Body(Transform g, float x0, float x1, float front, float o, float depth, float h, Color color)
        {
            SetupUtil.Block("Body", g, new Vector3((x0 + x1) / 2f, h / 2f, front - o * depth / 2f), new Vector3(x1 - x0, h, depth), M("Body_" + ColorUtility.ToHtmlStringRGB(color), color));
            Box("Cornice", g, new Vector3((x0 + x1) / 2f, h + 0.12f, front - o * depth / 2f), new Vector3(x1 - x0 + 0.1f, 0.25f, depth + 0.3f), Stone);
            Box("Plinth", g, new Vector3((x0 + x1) / 2f, 0.22f, front + o * 0.03f), new Vector3(x1 - x0, 0.45f, 0.06f), Stone);
        }

        /// <summary>A neighbour block with windows and a door.</summary>
        static void Filler(Transform g, string name, float x0, float x1, float front, float o, float h, int colour)
        {
            var b = SetupUtil.Empty(name, g, Vector3.zero).transform;
            Body(b, x0, x1, front, o, 10f, h, Palette.Neighbours[colour]);
            float mid = (x0 + x1) / 2f;
            Box("Door", b, new Vector3(mid, 1.05f, front + o * 0.05f), new Vector3(1.1f, 2.1f, 0.06f), Trim);
            for (float y = 1.6f; y + 0.8f < h - 0.3f; y += 3f)
            for (float x = x0 + 1.4f; x < x1 - 0.9f; x += 2.4f)
            {
                if (y < 3f && Mathf.Abs(x - mid) < 1.3f) continue;
                Window(b, new Vector3(x, y, front), o < 0 ? 0f : 180f, 1f, 1.3f);
            }
        }

        static void OpeningSoon(Transform g, Vector3 pos, Quaternion read, string line)
        {
            var board = SetupUtil.Empty("OpeningSoon", g, pos).transform;
            board.rotation = read * Quaternion.Euler(0f, 0f, -4f);
            Box("Board", board, Vector3.zero, new Vector3(0.95f, 0.6f, 0.02f), M("PartyWhite", Palette.PartyWhite));
            Label("OPENING SOON", g, board.TransformPoint(new Vector3(0f, 0.1f, -0.015f)), board.rotation, 0.08f, Palette.Hydrant);
            Label(line, g, board.TransformPoint(new Vector3(0f, -0.13f, -0.015f)), board.rotation, 0.04f, Palette.Trim);
        }

        static void Bench(Transform g, Vector3 foot, float yaw)
        {
            var b = SetupUtil.Empty("Bench", g, foot).transform;
            b.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var wood = M("TableWood", Palette.Wood);
            var seat = Solid(PrimitiveType.Cube, "Seat", b, new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.08f, 0.45f), wood);
            seat.transform.localRotation = Quaternion.identity;
            Box("Backrest", b, new Vector3(0f, 0.8f, 0.2f), new Vector3(1.6f, 0.35f, 0.06f), wood);
            foreach (float x in new[] { -0.7f, 0.7f })
                Box("Leg", b, new Vector3(x, 0.22f, 0f), new Vector3(0.06f, 0.44f, 0.4f), Metal);
        }

        /// <summary>Street lamp; yaw 0 leans the head toward -z.</summary>
        static void StreetLamp(Transform g, Vector3 foot, float yaw)
        {
            var l = SetupUtil.Empty("StreetLamp", g, foot).transform;
            l.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Solid(PrimitiveType.Cylinder, "Post", l, new Vector3(0f, 2f, 0f), new Vector3(0.12f, 2f, 0.12f), Metal);
            Box("Arm", l, new Vector3(0f, 3.95f, -0.35f), new Vector3(0.08f, 0.08f, 0.8f), Metal);
            Box("Head", l, new Vector3(0f, 3.92f, -0.75f), new Vector3(0.4f, 0.12f, 0.28f), Metal);
            Vis(LowPolyMeshes.Sphere, "Bulb", l, new Vector3(0f, 3.83f, -0.75f), new Vector3(0.24f, 0.1f, 0.18f), WarmLight);
        }

        static void Tree(Transform g, Vector3 foot)
        {
            var leaf = M("Leaf", Palette.Leaf);
            Solid(PrimitiveType.Cylinder, "Trunk", g, foot + new Vector3(0f, 1.1f, 0f), new Vector3(0.25f, 1.1f, 0.25f), M("Trunk", Palette.Trunk));
            Vis(LowPolyMeshes.Sphere, "Crown", g, foot + new Vector3(0f, 2.9f, 0f), new Vector3(2.3f, 2f, 2.3f), leaf);
            Vis(LowPolyMeshes.Sphere, "CrownTop", g, foot + new Vector3(0.35f, 3.75f, 0.2f), Vector3.one * 1.5f, M("LeafDark", Palette.LeafDark));
            Vis(LowPolyMeshes.Sphere, "CrownSide", g, foot + new Vector3(-0.6f, 3.3f, -0.4f), Vector3.one * 1.3f, leaf);
        }
    }
}
