using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static Freelancer.EditorTools.SceneKit;

namespace Freelancer.EditorTools
{
    /// <summary>
    /// Builds the Reception Bar gig: Kayleigh &amp; Todd's wedding reception hall with a long bar.
    ///
    /// Layout (top view, +z = north):
    ///   hall interior x -10..10, z -8..8, ceiling at 4.2
    ///   bar counter along the north side: x -10..3.5, z 4.3..5.25, top at 1.05 (slippery)
    ///   bartenders behind it (z 5.25..7.2), back counter with the glass rack at z 7.2..8
    ///   taps at the west end of the bar; guests line up along the south side (z 3.55)
    ///   dance floor south-east, tables south-west, entrance in the south wall
    ///   staff door in the north wall at x 6.5 → stairs down → basement (y -3) with spare kegs
    /// </summary>
    static class ReceptionSceneBuilder
    {
        public const string ScenePath = SetupUtil.ScenesDir + "/ReceptionBar.unity";

        const float Height = 4.2f;
        const float BarTopY = 1.05f;
        const float BarEndX = 3.5f;
        const float BasementY = -3f;

        static readonly float[] k_GuestSlotsX = { -3.6f, -2.2f, -0.8f, 0.6f, 2.0f };
        static readonly Vector3 k_BeerTap = new(-6.8f, BarTopY, 4.75f);
        static readonly Vector3 k_WineBox = new(-5.6f, BarTopY, 4.75f);

        static Transform s_Root;

        [MenuItem("Freelancer/Setup/Build Reception Bar Scene", priority = 23)]
        public static void BuildReceptionScene()
        {
            SetupUtil.EnsureFolders();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            LookSetup.ApplyToOpenScene();

            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(6f, 2.6f, -6f);
                cam.transform.LookAt(new Vector3(-3f, 1f, 5f));
            }

            s_Root = new GameObject("Level").transform;
            BuildShell();
            BuildBar();
            BuildDanceFloor();
            BuildTables();
            BuildDecor();
            BuildStairsAndBasement();
            BuildLights();
            PlaceGameplay();

            new GameObject("SessionHud").AddComponent<SessionHud>();
            new GameObject("GigHud").AddComponent<GigHud>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Freelancer] Built " + ScenePath);
        }

        static Transform Group(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(s_Root, false);
            return t;
        }

        static Material M(string name, Color c, float smooth = 0.1f) => SetupUtil.Mat(name, c, smooth);

        // ---------- Hall ----------

        static void BuildShell()
        {
            var g = Group("Hall");
            var wall = M("HallWall", Palette.HallWall);
            var trim = M("HallTrim", Palette.HallTrim, 0.4f);
            SetupUtil.Block("Floor", g, new Vector3(0f, -0.1f, 0f), new Vector3(20.4f, 0.2f, 16.4f), M("HallFloor", Palette.HallFloor));
            SetupUtil.Block("Ceiling", g, new Vector3(0f, Height + 0.1f, 0f), new Vector3(20.4f, 0.2f, 16.4f), M("Ceiling", Palette.Ceiling));
            SetupUtil.Block("WallS", g, new Vector3(0f, Height / 2f, -8.1f), new Vector3(20.4f, Height, 0.2f), wall);
            SetupUtil.Block("WallE", g, new Vector3(10.1f, Height / 2f, 0f), new Vector3(0.2f, Height, 16.4f), wall);
            SetupUtil.Block("WallW", g, new Vector3(-10.1f, Height / 2f, 0f), new Vector3(0.2f, Height, 16.4f), wall);
            // North wall with the staff door gap at x 5.6..7.4.
            SetupUtil.Block("WallN_West", g, new Vector3(-2.2f, Height / 2f, 8.1f), new Vector3(15.6f, Height, 0.2f), wall);
            SetupUtil.Block("WallN_East", g, new Vector3(8.75f, Height / 2f, 8.1f), new Vector3(2.7f, Height, 0.2f), wall);
            SetupUtil.Block("WallN_Lintel", g, new Vector3(6.5f, 3.3f, 8.1f), new Vector3(1.8f, 1.8f, 0.2f), wall);

            // Gold trim along the top and a skirting board.
            Box("CrownS", g, new Vector3(0f, Height - 0.1f, -7.98f), new Vector3(20f, 0.12f, 0.06f), trim);
            Box("CrownE", g, new Vector3(9.98f, Height - 0.1f, 0f), new Vector3(0.06f, 0.12f, 16f), trim);
            Box("CrownW", g, new Vector3(-9.98f, Height - 0.1f, 0f), new Vector3(0.06f, 0.12f, 16f), trim);
            Box("CrownN", g, new Vector3(0f, Height - 0.1f, 7.98f), new Vector3(20f, 0.12f, 0.06f), trim);
            Box("SkirtS", g, new Vector3(0f, 0.07f, -7.98f), new Vector3(20f, 0.14f, 0.05f), Stone);
            Box("SkirtE", g, new Vector3(9.98f, 0.07f, 0f), new Vector3(0.05f, 0.14f, 16f), Stone);
            Box("SkirtW", g, new Vector3(-9.98f, 0.07f, 0f), new Vector3(0.05f, 0.14f, 16f), Stone);

            // Tall windows (evening sky) and the main entrance.
            foreach (float x in new[] { -7f, -3.5f, 3.5f, 7f })
                Window(g, new Vector3(x, 2.1f, -8f), 180f, 1.3f, 2f);
            foreach (float z in new[] { -4.5f, -0.5f })
                Window(g, new Vector3(10f, 2.1f, z), -90f, 1.3f, 2f);

            var door = M("HallDoor", new Color(0.95f, 0.93f, 0.88f));
            Box("EntranceFrame", g, new Vector3(0f, 1.3f, -7.97f), new Vector3(2.4f, 2.6f, 0.06f), trim);
            foreach (float x in new[] { -0.56f, 0.56f })
            {
                Box("EntranceDoor", g, new Vector3(x, 1.24f, -7.93f), new Vector3(1.06f, 2.44f, 0.05f), door);
                Box("DoorWindow", g, new Vector3(x, 1.7f, -7.9f), new Vector3(0.5f, 0.8f, 0.01f), GlassPane);
                Box("PushBar", g, new Vector3(x, 1.05f, -7.88f), new Vector3(0.8f, 0.05f, 0.05f), Metal);
            }
            Label("EXIT", g, new Vector3(0f, 2.85f, -7.95f), Quaternion.Euler(0, 180, 0), 0.12f,
                Color.white, new Color(0.2f, 0.65f, 0.35f));
        }

        // ---------- The bar ----------

        static void BuildBar()
        {
            var g = Group("Bar");
            var wood = M("BarWood", Palette.BarWood);
            var top = M("BarTop", Palette.BarTop, 0.6f);
            var trim = M("HallTrim", Palette.HallTrim, 0.4f);

            float length = BarEndX + 10f, centerX = (BarEndX - 10f) / 2f;
            SetupUtil.Block("BarFront", g, new Vector3(centerX, 0.5f, 4.75f), new Vector3(length, 1f, 0.8f), wood);
            var counter = SetupUtil.Block("BarTop", g, new Vector3(centerX, BarTopY - 0.025f, 4.78f), new Vector3(length, 0.05f, 0.95f), top);
            counter.GetComponent<Collider>().sharedMaterial = PrefabBuilder.Slippery();
            // Low lip at the far end so slid glasses have a chance (it's still a long way down).
            Box("FootRail", g, new Vector3(centerX, 0.2f, 4.25f), new Vector3(length - 0.4f, 0.05f, 0.05f), trim);
            Box("TrimBand", g, new Vector3(centerX, 0.9f, 4.34f), new Vector3(length, 0.05f, 0.02f), trim);
            for (float x = -9f; x < BarEndX - 0.5f; x += 1.6f)
                Box("Panel", g, new Vector3(x + 0.8f, 0.5f, 4.345f), new Vector3(1.3f, 0.6f, 0.01f), M("BarPanel", Palette.BarWood * 1.15f));

            // Back counter, shelves and bottles.
            SetupUtil.Block("BackCounter", g, new Vector3(-4f, 0.47f, 7.6f), new Vector3(12f, 0.94f, 0.8f), wood);
            SetupUtil.Block("BackCounterTop", g, new Vector3(-4f, 0.96f, 7.6f), new Vector3(12f, 0.04f, 0.84f), top);
            var shelfMat = M("Shelf", Palette.BarWood * 0.9f);
            var bottleColors = new[] { new Color(0.25f, 0.5f, 0.3f), new Color(0.55f, 0.35f, 0.15f), new Color(0.75f, 0.85f, 0.9f),
                new Color(0.45f, 0.12f, 0.2f), new Color(0.85f, 0.7f, 0.3f) };
            foreach (float y in new[] { 1.65f, 2.15f })
            {
                Box("Shelf", g, new Vector3(-4f, y, 7.85f), new Vector3(9f, 0.04f, 0.28f), shelfMat);
                int i = 0;
                for (float x = -8.2f; x < 0.3f; x += 0.32f, i++)
                {
                    var mat = M("Bottle" + i % bottleColors.Length, bottleColors[i % bottleColors.Length], 0.7f);
                    float h = 0.24f + (i * 37 % 7) * 0.012f;
                    Vis(LowPolyMeshes.Cylinder, "Bottle", g, new Vector3(x, y + 0.02f + h / 2f, 7.85f), new Vector3(0.08f, h / 2f, 0.08f), mat);
                    Vis(LowPolyMeshes.Cylinder, "Neck", g, new Vector3(x, y + 0.02f + h + 0.05f, 7.85f), new Vector3(0.035f, 0.05f, 0.035f), mat);
                }
            }
            var neon = SetupUtil.Emissive("Neon", Palette.Neon, 3f);
            Label("OPEN BAR", g, new Vector3(-4f, 3.05f, 7.95f), Quaternion.identity, 0.32f, Palette.Neon);
            Box("NeonUnderline", g, new Vector3(-4f, 2.72f, 7.96f), new Vector3(2.6f, 0.04f, 0.02f), neon);

            // Glass rack and sign.
            Box("Rack", g, new Vector3(-4.4f, 0.985f, 7.55f), new Vector3(1.3f, 0.01f, 0.7f), Chrome());
            Label("CLEAN GLASSES", g, new Vector3(-4.4f, 1.3f, 7.98f), Quaternion.identity, 0.08f, Palette.PartyWhite, Palette.Trim);

            // Keg spot under the beer tap: drag the spare keg here.
            var kegSpot = new Vector3(k_BeerTap.x, 0f, k_BeerTap.z + 0.75f);
            Box("KegSpot", g, kegSpot + new Vector3(0, 0.006f, 0), new Vector3(0.9f, 0.012f, 0.9f), M("Target", Palette.Target));
            Label("KEG", g, kegSpot + new Vector3(0, 0.015f, 0), Quaternion.Euler(90, 0, 0), 0.18f, Palette.PartyWhite);
            Vis(LowPolyMeshes.Cylinder, "Hose", g, new Vector3(k_BeerTap.x - 0.1f, 0.5f, 5.2f), new Vector3(0.04f, 0.5f, 0.04f), Metal);
            Label("BEER KEG GOES HERE", g, new Vector3(k_BeerTap.x + 0.6f, 0.72f, 5.215f), Quaternion.Euler(0, 180, 0), 0.06f,
                Palette.PartyWhite, Palette.Trim);

            // Bar stools on the guest side (between the guest spots).
            var seat = M("StoolSeat", Palette.Couch);
            foreach (float x in new[] { -4.4f, 2.9f, -6.2f })
            {
                Vis(LowPolyMeshes.Cylinder, "StoolLeg", g, new Vector3(x, 0.36f, 3.7f), new Vector3(0.06f, 0.36f, 0.06f), Metal);
                Vis(LowPolyMeshes.Cylinder, "StoolSeat", g, new Vector3(x, 0.74f, 3.7f), new Vector3(0.38f, 0.04f, 0.38f), seat);
            }
        }

        static Material Chrome() => SetupUtil.Mat("Chrome", new Color(0.78f, 0.8f, 0.84f), 0.75f);

        // ---------- Dance floor ----------

        static void BuildDanceFloor()
        {
            var g = Group("DanceFloor");
            var light = M("DanceLight", Palette.DanceLight, 0.5f);
            var dark = M("DanceDark", Palette.DanceDark, 0.5f);
            for (int x = 0; x < 7; x++)
            for (int z = 0; z < 6; z++)
                Box("Tile", g, new Vector3(1.5f + x + 0.5f, 0.004f, -6.5f + z + 0.5f), new Vector3(1f, 0.008f, 1f), (x + z) % 2 == 0 ? light : dark);

            // DJ booth against the east wall.
            var booth = M("DJBooth", Palette.DanceDark);
            SetupUtil.Block("DJBooth", g, new Vector3(9.3f, 0.55f, -3.2f), new Vector3(1.2f, 1.1f, 2.2f), booth);
            Box("Decks", g, new Vector3(9.3f, 1.13f, -3.2f), new Vector3(0.7f, 0.06f, 1.4f), Metal);
            foreach (float z in new[] { -3.6f, -2.8f })
                Vis(LowPolyMeshes.Cylinder, "Record", g, new Vector3(9.3f, 1.17f, z), new Vector3(0.32f, 0.01f, 0.32f), M("Ink", new Color(0.1f, 0.1f, 0.12f)));
            foreach (float z in new[] { -5f, -1.4f })
            {
                SetupUtil.Block("Speaker", g, new Vector3(9.5f, 0.8f, z), new Vector3(0.7f, 1.6f, 0.7f), M("Speaker", new Color(0.15f, 0.15f, 0.17f)));
                Vis(LowPolyMeshes.Cylinder, "Woofer", g, new Vector3(9.14f, 0.55f, z), new Vector3(0.42f, 0.01f, 0.42f), Metal, Quaternion.Euler(0, 0, 90));
                Vis(LowPolyMeshes.Cylinder, "Tweeter", g, new Vector3(9.14f, 1.2f, z), new Vector3(0.2f, 0.01f, 0.2f), Metal, Quaternion.Euler(0, 0, 90));
            }
            // Disco ball.
            Vis(LowPolyMeshes.Sphere, "DiscoBall", g, new Vector3(5f, 3.4f, -3.5f), Vector3.one * 0.5f, SetupUtil.Mat("Mirror", new Color(0.85f, 0.88f, 0.95f), 0.95f));
            Box("DiscoString", g, new Vector3(5f, 3.85f, -3.5f), new Vector3(0.01f, 0.5f, 0.01f), Metal);

            // A couple of guests who just never stop dancing.
            Npc(g, new Vector3(3.2f, 0f, -4.5f), 40f, new Color(0.35f, 0.55f, 0.75f), 0, hat: 1);
            Npc(g, new Vector3(6.4f, 0f, -2.2f), -120f, new Color(0.9f, 0.55f, 0.6f), 1, hat: 2);
            Npc(g, new Vector3(7.2f, 0f, -5.4f), 200f, new Color(0.45f, 0.35f, 0.6f), 2, hat: -1);
        }

        // ---------- Tables ----------

        static void BuildTables()
        {
            var g = Group("Tables");
            var cloth = M("Tablecloth", Palette.Tablecloth);
            var chair = M("Chair", Palette.Chair);
            var pink = M("PartyPink", Palette.PartyPink);
            var gold = M("PartyGold", Palette.PartyGold);
            var tables = new[] { new Vector3(-7.4f, 0, -5.6f), new Vector3(-4.2f, 0, -6f), new Vector3(-7.4f, 0, -1.8f), new Vector3(-4.4f, 0, -2.6f) };
            int n = 0;
            foreach (var t in tables)
            {
                var tg = SetupUtil.Empty("Table", g, t).transform;
                Vis(LowPolyMeshes.Cylinder, "Top", tg, new Vector3(0, 0.75f, 0), new Vector3(1.6f, 0.02f, 1.6f), cloth);
                var bump = tg.gameObject.AddComponent<BoxCollider>();
                bump.center = new Vector3(0, 0.38f, 0);
                bump.size = new Vector3(1.3f, 0.77f, 1.3f);
                Vis(LowPolyMeshes.Cylinder, "Cloth", tg, new Vector3(0, 0.45f, 0), new Vector3(1.62f, 0.3f, 1.62f), cloth);
                Vis(LowPolyMeshes.Cylinder, "Vase", tg, new Vector3(0, 0.88f, 0), new Vector3(0.12f, 0.11f, 0.12f), GlassPane);
                Vis(LowPolyMeshes.Sphere, "Flowers", tg, new Vector3(0, 1.05f, 0), Vector3.one * 0.26f, n % 2 == 0 ? pink : gold);
                for (int c = 0; c < 6; c++)
                {
                    float a = c * 60f + 30f;
                    var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                    var cp = dir * 1.15f;
                    Box("Seat", tg, cp + new Vector3(0, 0.45f, 0), new Vector3(0.42f, 0.05f, 0.42f), chair, Quaternion.Euler(0, a, 0));
                    Box("Back", tg, cp + dir * 0.2f + new Vector3(0, 0.75f, 0), new Vector3(0.42f, 0.55f, 0.04f), chair, Quaternion.Euler(0, a, 0));
                    Box("Bow", tg, cp + dir * 0.23f + new Vector3(0, 0.75f, 0), new Vector3(0.18f, 0.12f, 0.03f), pink, Quaternion.Euler(0, a, 0));
                }
                // Somebody's still sitting there.
                if (n % 2 == 1) Npc(tg, Quaternion.Euler(0, 30f, 0) * Vector3.forward * 1.25f, 210f, new Color(0.3f, 0.5f, 0.4f), n, hat: -1);
                n++;
            }

            // The cake table: the cake survived (mostly).
            var cake = SetupUtil.Empty("CakeTable", g, new Vector3(-1.4f, 0, -6.8f)).transform;
            Solid(PrimitiveType.Cube, "Top", cake, new Vector3(0, 0.75f, 0), new Vector3(1.6f, 0.05f, 0.9f), cloth);
            Vis(LowPolyMeshes.Cylinder, "Skirt", cake, new Vector3(0, 0.37f, 0), new Vector3(1.55f, 0.37f, 0.85f), pink);
            var frosting = SetupUtil.Mat("Frosting", new Color(0.98f, 0.96f, 0.9f), 0.5f);
            Vis(LowPolyMeshes.Cylinder, "CakeBottom", cake, new Vector3(0, 0.98f, 0), new Vector3(1.0f, 0.2f, 1.0f), frosting);
            Vis(LowPolyMeshes.Cylinder, "CakeMiddle", cake, new Vector3(0.05f, 1.3f, 0), new Vector3(0.72f, 0.12f, 0.72f), frosting,
                Quaternion.Euler(0, 0, 6));
            Vis(LowPolyMeshes.Cylinder, "Ring", cake, new Vector3(0, 0.82f, 0), new Vector3(1.04f, 0.02f, 1.04f), M("PinkIcing", new Color(0.97f, 0.6f, 0.72f), 0.5f));
            Label("THE CAKE (mostly)", cake, cake.position + new Vector3(0, 0.79f, -0.46f), Quaternion.identity, 0.05f, Palette.Trim, Palette.PartyWhite);
        }

        // ---------- Wedding decorations ----------

        static void BuildDecor()
        {
            var g = Group("Decor");
            Label("KAYLEIGH & TODD", g, new Vector3(4.5f, 3.3f, -7.95f), Quaternion.Euler(0, 180, 0), 0.4f,
                Palette.PartyWhite, Palette.PartyPink);
            Label("JUST MARRIED (TODAY!)", g, new Vector3(-9.95f, 3.1f, -3f), Quaternion.Euler(0, -90, 0), 0.22f,
                Palette.Trim, Palette.PartyGold);

            // String lights zig-zagging under the ceiling.
            var bulb = SetupUtil.Emissive("FairyLight", Palette.PartyGold, 1.4f);
            for (int row = 0; row < 4; row++)
            {
                float z = -7f + row * 3.4f;
                for (float x = -9.5f; x <= 9.5f; x += 0.7f)
                {
                    float sag = Mathf.Sin((x + 9.5f) / 19f * Mathf.PI * 3f);
                    Vis(LowPolyMeshes.Sphere, "Bulb", g, new Vector3(x, 3.85f - Mathf.Abs(sag) * 0.35f, z), Vector3.one * 0.08f, bulb);
                }
            }

            // Balloons in clumps.
            var colors = new[] { Palette.PartyPink, Palette.PartyGold, Palette.PartyWhite };
            var clumps = new[] { new Vector3(-9.4f, 0, -7.4f), new Vector3(9.4f, 0, -7.4f), new Vector3(-1.8f, 0, -7.4f), new Vector3(9.4f, 0, 2f) };
            int i = 0;
            foreach (var c in clumps)
                for (int b = 0; b < 4; b++, i++)
                {
                    var off = new Vector3(Mathf.Sin(b * 2.1f) * 0.25f, 1.7f + b * 0.22f, Mathf.Cos(b * 2.1f) * 0.2f);
                    Vis(LowPolyMeshes.Sphere, "Balloon", g, c + off, new Vector3(0.36f, 0.42f, 0.36f), M("Balloon" + i % 3, colors[i % 3], 0.5f));
                    Box("BalloonString", g, c + new Vector3(off.x * 0.5f, off.y * 0.5f, off.z * 0.5f), new Vector3(0.008f, off.y, 0.008f), Metal);
                }

            // Gift table near the entrance.
            SetupUtil.Block("GiftTable", g, new Vector3(-8.6f, 0.38f, 1.6f), new Vector3(1.2f, 0.76f, 2.2f), M("Tablecloth", Palette.Tablecloth));
            var giftColors = new[] { Palette.PartyPink, Palette.Mailbox, Palette.PartyGold, Palette.Target };
            for (int k = 0; k < 6; k++)
            {
                float s = 0.22f + (k % 3) * 0.08f;
                var pos = new Vector3(-8.6f + (k % 2 - 0.5f) * 0.5f, 0.76f + s / 2f, 0.8f + k * 0.3f);
                Box("Gift", g, pos, Vector3.one * s, M("Gift" + k % 4, giftColors[k % 4]));
                Box("Ribbon", g, pos, new Vector3(s + 0.01f, s + 0.01f, 0.04f), M("PartyWhite", Palette.PartyWhite));
            }
        }

        // ---------- Staff stairs and basement ----------

        static void BuildStairsAndBasement()
        {
            var g = Group("Basement");
            var wall = M("BasementWall", Palette.Basement);
            var floor = M("BasementFloor", Palette.BasementFloor);
            var stairs = M("Stairs", Palette.Stairs);

            // Staff door frame and signs.
            foreach (float x in new[] { 5.55f, 7.45f })
                Box("StaffJamb", g, new Vector3(x, 1.2f, 7.97f), new Vector3(0.12f, 2.4f, 0.08f), Stone);
            Box("StaffHead", g, new Vector3(6.5f, 2.43f, 7.97f), new Vector3(2.02f, 0.1f, 0.08f), Stone);
            Label("STAFF ONLY", g, new Vector3(6.5f, 2.7f, 7.94f), Quaternion.Euler(0, 180, 0), 0.12f, Palette.PartyWhite, Palette.Awning);
            Label("SPARE KEGS: BASEMENT", g, new Vector3(6.5f, 2.95f, 7.94f), Quaternion.Euler(0, 180, 0), 0.07f, Palette.Trim, Palette.PartyGold);

            // Stair corridor x 5.6..7.4 from z 8 down to z 13 (y 0 → -3): visual steps on a smooth ramp.
            const int steps = 15;
            float run = 5f / steps, rise = 3f / steps;
            for (int i = 0; i < steps; i++)
            {
                float topY = -rise * (i + 1);
                float z = 8.2f + run * i + run / 2f;
                Box("Step", g, new Vector3(6.5f, (topY + BasementY - 0.2f) / 2f, z), new Vector3(1.8f, topY - BasementY + 0.2f, run), i % 2 == 0 ? stairs : floor);
            }
            float angle = Mathf.Atan2(3f, 5f) * Mathf.Rad2Deg;
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "StairRamp";
            ramp.transform.SetParent(g, false);
            var normal = Quaternion.Euler(angle, 0, 0) * Vector3.up;
            ramp.transform.localPosition = new Vector3(6.5f, -1.5f + 0.05f, 10.7f) - normal * 0.1f;
            ramp.transform.localRotation = Quaternion.Euler(angle, 0, 0);
            ramp.transform.localScale = new Vector3(1.8f, 0.2f, Mathf.Sqrt(25f + 9f));
            Object.DestroyImmediate(ramp.GetComponent<MeshRenderer>());
            ramp.isStatic = true;

            SetupUtil.Block("StairWallW", g, new Vector3(5.5f, -0.3f, 10.6f), new Vector3(0.2f, 5.4f, 5f), wall);
            SetupUtil.Block("StairWallE", g, new Vector3(7.5f, -0.3f, 10.6f), new Vector3(0.2f, 5.4f, 5f), wall);
            SetupUtil.Block("StairCeiling", g, new Vector3(6.5f, 2.5f, 10.6f), new Vector3(2.2f, 0.2f, 5f), M("Ceiling", Palette.Ceiling));
            SetupUtil.Block("StairDrop", g, new Vector3(6.5f, 1.2f, 13.1f), new Vector3(2.2f, 2.4f, 0.2f), wall);
            Box("Handrail", g, new Vector3(5.63f, -0.6f, 10.6f), new Vector3(0.05f, 0.05f, 5.8f), Metal, Quaternion.Euler(angle, 0, 0));
            Lamp(g, new Vector3(6.5f, 1.8f, 9.5f), 5f, 1.1f);

            // Basement room x 2..10, z 13..19.
            float cy = BasementY;
            SetupUtil.Block("BasementFloor", g, new Vector3(6f, cy - 0.1f, 16f), new Vector3(8.4f, 0.2f, 6.4f), floor);
            SetupUtil.Block("BasementCeiling", g, new Vector3(6f, 0.1f, 16f), new Vector3(8.4f, 0.2f, 6.4f), M("Ceiling", Palette.Ceiling));
            SetupUtil.Block("BasementN", g, new Vector3(6f, cy + 1.5f, 19.1f), new Vector3(8.4f, 3f, 0.2f), wall);
            SetupUtil.Block("BasementE", g, new Vector3(10.1f, cy + 1.5f, 16f), new Vector3(0.2f, 3f, 6.4f), wall);
            SetupUtil.Block("BasementW", g, new Vector3(1.9f, cy + 1.5f, 16f), new Vector3(0.2f, 3f, 6.4f), wall);
            SetupUtil.Block("BasementS_W", g, new Vector3(3.7f, cy + 1.5f, 12.9f), new Vector3(3.8f, 3f, 0.2f), wall);
            SetupUtil.Block("BasementS_E", g, new Vector3(8.8f, cy + 1.5f, 12.9f), new Vector3(2.8f, 3f, 0.2f), wall);
            Vis(LowPolyMeshes.Cylinder, "Pipe", g, new Vector3(6f, -0.25f, 18.8f), new Vector3(0.12f, 4f, 0.12f), Metal, Quaternion.Euler(0, 0, 90));
            Vis(LowPolyMeshes.Sphere, "Bulb", g, new Vector3(6f, -0.35f, 16f), Vector3.one * 0.12f, SetupUtil.Emissive("WarmLight", Palette.WarmLight));
            Lamp(g, new Vector3(6f, -0.6f, 16f), 8f, 1.4f);

            // Shelves of stuff, a dead keg, wine boxes.
            var shelf = M("Shelf", Palette.BarWood * 0.9f);
            foreach (float y in new[] { 0.6f, 1.3f, 2.0f })
                SetupUtil.Block("Shelf", g, new Vector3(9.6f, cy + y, 16f), new Vector3(0.7f, 0.05f, 5f), shelf);
            for (int i = 0; i < 6; i++)
                Box("StoredWine", g, new Vector3(9.6f, cy + 0.82f + (i % 3) * 0.7f, 14.4f + i * 0.55f), new Vector3(0.34f, 0.34f, 0.28f),
                    M("WineBox", new Color(0.55f, 0.15f, 0.28f)));
            Label("KEGS", g, new Vector3(4.6f, cy + 2.1f, 18.98f), Quaternion.identity, 0.2f, Palette.PartyWhite, Palette.GigOrange);
            Label("(heavy - lift with a friend)", g, new Vector3(4.6f, cy + 1.75f, 18.98f), Quaternion.identity, 0.07f, Palette.Trim, Palette.PartyWhite);
        }

        // ---------- Lights ----------

        static void BuildLights()
        {
            var g = Group("Lights");
            Lamp(g, new Vector3(-3f, 3.4f, 6.2f), 9f, 1.6f);   // behind the bar
            Lamp(g, new Vector3(-7f, 3.4f, 6.2f), 7f, 1.2f);
            Lamp(g, new Vector3(-0.5f, 3.4f, 2.5f), 9f, 1.2f);  // guest side
            Lamp(g, new Vector3(5f, 3.2f, -3.5f), 10f, 1.4f);   // dance floor
            Lamp(g, new Vector3(-5.5f, 3.4f, -4f), 10f, 1.2f);  // tables
            Lamp(g, new Vector3(7f, 3.2f, 5.5f), 7f, 1f);       // by the staff door
            // A coloured party light over the dance floor.
            var party = SetupUtil.Empty("PartyLight", g, new Vector3(5f, 3f, -3.5f)).AddComponent<Light>();
            party.type = LightType.Point;
            party.color = Palette.Neon;
            party.range = 7f;
            party.intensity = 1.2f;
            party.shadows = LightShadows.None;
        }

        // ---------- Gameplay ----------

        static void PlaceGameplay()
        {
            var gameplay = new GameObject("Gameplay").transform;

            Spawner(gameplay, PrefabBuilder.BarDirectorPath, Vector3.zero, 0f, "GigDirectorSpawner");
            Spawner(gameplay, PrefabBuilder.BeerTapPath, k_BeerTap, 0f, "BeerTapSpawner");
            Spawner(gameplay, PrefabBuilder.WineBoxPath, k_WineBox, 0f, "WineBoxSpawner");
            Spawner(gameplay, PrefabBuilder.KegPath, new Vector3(3.6f, BasementY + 0.31f, 18.2f), 0f, "KegSpawner1");
            Spawner(gameplay, PrefabBuilder.KegPath, new Vector3(4.4f, BasementY + 0.31f, 18.4f), 0f, "KegSpawner2");
            Spawner(gameplay, PrefabBuilder.KegPath, new Vector3(5.4f, BasementY + 0.31f, 18.1f), 0f, "KegSpawner3");
            Spawner(gameplay, PrefabBuilder.CongaPath, new Vector3(1.5f, 0f, -1f), 0f, "CongaSpawner");

            var rack = SetupUtil.Empty("GlassRack", gameplay, new Vector3(-4.4f, 0.99f, 7.55f)).AddComponent<GlassRack>();
            var so = new SerializedObject(rack);
            so.FindProperty("glassPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<NetworkObject>(PrefabBuilder.GlassPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            var slots = SetupUtil.Empty("GuestSlots", gameplay, Vector3.zero).transform;
            foreach (float x in k_GuestSlotsX)
                SetupUtil.Empty("Slot", slots, new Vector3(x, 0f, 3.55f)).AddComponent<GuestSlot>();

            var path = SetupUtil.Empty("CongaPath", gameplay, Vector3.zero).transform;
            var points = new[]
            {
                new Vector3(1.5f, 0, -1f), new Vector3(8.4f, 0, -1f), new Vector3(8.4f, 0, 6.6f), new Vector3(4.3f, 0, 6.6f),
                new Vector3(4.3f, 0, 2.2f), new Vector3(-1f, 0, 2.2f), new Vector3(-1f, 0, -5.5f), new Vector3(6f, 0, -5.5f),
                new Vector3(6f, 0, -3f), new Vector3(1.5f, 0, -3f),
            };
            for (int i = 0; i < points.Length; i++) SetupUtil.Empty("P" + i, path, points[i]);
            path.gameObject.AddComponent<CongaPath>();

            var players = new GameObject("PlayerSpawner");
            players.transform.SetParent(gameplay, false);
            players.transform.position = new Vector3(-2f, 0.1f, 6.3f);
            players.transform.rotation = Quaternion.Euler(0, 180, 0); // facing the guests
            for (int i = 0; i < SessionManager.MaxPlayers; i++)
                SetupUtil.Empty("Spawn" + (i + 1), players.transform, new Vector3(-1.8f + i * 1.2f, 0f, 0));
            var spawner = players.AddComponent<PlayerSpawner>();
            so = new SerializedObject(spawner);
            so.FindProperty("playerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<NetworkObject>(PrefabBuilder.PlayerPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            // A few boxes in the basement to trip over.
            Spawner(gameplay, PrefabBuilder.BoxPath, new Vector3(3f, BasementY + 0.4f, 14.2f), 20f);
            Spawner(gameplay, PrefabBuilder.BoxPath, new Vector3(3.2f, BasementY + 0.95f, 14.3f), 50f);
            Spawner(gameplay, PrefabBuilder.BucketPath, new Vector3(8.6f, BasementY + 0.3f, 13.8f), 0f);
        }

        // ---------- Helpers ----------

        /// <summary>A static decorative guest.</summary>
        static void Npc(Transform parent, Vector3 localPos, float yaw, Color outfit, int skin, int hat)
        {
            var skins = new[] { new Color(0.98f, 0.84f, 0.7f), new Color(0.75f, 0.55f, 0.4f), new Color(0.5f, 0.35f, 0.25f) };
            var go = SetupUtil.Empty("Guest", parent, localPos);
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var parts = PrefabBuilder.BuildNpc(go.transform,
                SetupUtil.Mat("DecoGuest_" + ColorUtility.ToHtmlStringRGB(outfit), outfit),
                SetupUtil.Mat("DancerSkin" + skin % 3, skins[skin % 3]), armsForward: false);
            if (hat >= 0 && hat < parts.Hats.Count) parts.Hats[hat].SetActive(true);
            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.95f, 0);
            col.height = 1.9f;
            col.radius = 0.3f;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
        }
    }
}
