using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static Freelancer.EditorTools.SceneKit;

namespace Freelancer.EditorTools
{
    /// <summary>
    /// Builds the Reception Bar gig: Kayleigh &amp; Todd's wedding reception hall.
    ///
    /// Layout (top view, +z = north):
    ///   hall interior x -10..10, z -8..8, ceiling at 4.2
    ///   bar counter along the north side: x -6..3, z 4.3..5.25, top at 1.05 (slippery)
    ///   two drink stations in the middle of the bar (beer, then wine), each with its own
    ///   glass rack right beside the tap; guests order from two spots either side of them
    ///   bartenders behind the bar (z 5.25..7.2), back counter with bottles at z 7.2..8
    ///   staff door in the corner behind the bar (x -5) → stairs down → basement with spare kegs
    ///   dance floor south-east, tables south-west, head table and photo booth on the east side
    /// </summary>
    static class ReceptionSceneBuilder
    {
        public const string ScenePath = SetupUtil.ScenesDir + "/ReceptionBar.unity";

        const float Height = 4.2f;
        const float BarTopY = 1.05f;
        const float BarStartX = -6f;
        const float BarEndX = 3f;
        const float BasementY = -3f;
        /// <summary>Centre of the staff door and the stairs behind it.</summary>
        public const float StairX = -5f;
        /// <summary>The basement was first laid out around a stair at x 6.5; everything down there shifts by this.</summary>
        const float Dx = StairX - 6.5f;

        static readonly float[] k_GuestSlotsX = { -5.2f, -3.8f, 0.8f, 2.2f };
        public static readonly Vector3 BeerTap = new(-2.05f, BarTopY, 4.75f);
        public static readonly Vector3 WineBox = new(-0.95f, BarTopY, 4.75f);
        static readonly Vector3 k_BeerRack = new(-2.75f, BarTopY, 4.95f);
        static readonly Vector3 k_WineRack = new(-0.25f, BarTopY, 4.95f);
        static readonly Color k_BeerColor = new(0.93f, 0.62f, 0.18f);
        static readonly Color k_WineColor = new(0.55f, 0.15f, 0.28f);

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
                cam.transform.LookAt(new Vector3(-2f, 1f, 5f));
            }

            s_Root = new GameObject("Level").transform;
            BuildShell();
            BuildBar();
            BuildDanceFloor();
            BuildTables();
            BuildHeadTableAndPhotoBooth();
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
        static Material Chrome() => SetupUtil.Mat("Chrome", new Color(0.78f, 0.8f, 0.84f), 0.75f);

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
            // North wall with the staff door gap behind the bar (StairX ± 0.9).
            float gapW = StairX - 0.9f, gapE = StairX + 0.9f;
            SetupUtil.Block("WallN_West", g, new Vector3((gapW - 10.2f) / 2f, Height / 2f, 8.1f), new Vector3(gapW + 10.2f, Height, 0.2f), wall);
            SetupUtil.Block("WallN_East", g, new Vector3((gapE + 10.2f) / 2f, Height / 2f, 8.1f), new Vector3(10.2f - gapE, Height, 0.2f), wall);
            SetupUtil.Block("WallN_Lintel", g, new Vector3(StairX, 3.3f, 8.1f), new Vector3(1.8f, 1.8f, 0.2f), wall);

            // Gold trim along the top and a skirting board.
            Box("CrownS", g, new Vector3(0f, Height - 0.1f, -7.98f), new Vector3(20f, 0.12f, 0.06f), trim);
            Box("CrownE", g, new Vector3(9.98f, Height - 0.1f, 0f), new Vector3(0.06f, 0.12f, 16f), trim);
            Box("CrownW", g, new Vector3(-9.98f, Height - 0.1f, 0f), new Vector3(0.06f, 0.12f, 16f), trim);
            Box("CrownN", g, new Vector3(0f, Height - 0.1f, 7.98f), new Vector3(20f, 0.12f, 0.06f), trim);
            Box("SkirtS", g, new Vector3(0f, 0.07f, -7.98f), new Vector3(20f, 0.14f, 0.05f), Stone);
            Box("SkirtE", g, new Vector3(9.98f, 0.07f, 0f), new Vector3(0.05f, 0.14f, 16f), Stone);
            Box("SkirtW", g, new Vector3(-9.98f, 0.07f, 0f), new Vector3(0.05f, 0.14f, 16f), Stone);
            // Dado rail: a thin gold line round the room at hand height.
            Box("DadoS", g, new Vector3(0f, 1.0f, -7.98f), new Vector3(20f, 0.05f, 0.04f), trim);
            Box("DadoE", g, new Vector3(9.98f, 1.0f, 0f), new Vector3(0.04f, 0.05f, 16f), trim);
            Box("DadoW", g, new Vector3(-9.98f, 1.0f, 0f), new Vector3(0.04f, 0.05f, 16f), trim);

            // Tall windows (evening sky) with curtains, and the main entrance.
            var curtain = M("Curtain", new Color(0.86f, 0.42f, 0.52f));
            foreach (float x in new[] { -7f, -3.5f, 3.5f, 7f })
            {
                Window(g, new Vector3(x, 2.1f, -8f), 180f, 1.3f, 2f);
                foreach (float s in new[] { -1f, 1f })
                    Box("Curtain", g, new Vector3(x + s * 0.85f, 2.05f, -7.9f), new Vector3(0.32f, 2.7f, 0.06f), curtain);
                Box("Pelmet", g, new Vector3(x, 3.45f, -7.9f), new Vector3(2.1f, 0.18f, 0.08f), trim);
            }
            foreach (float z in new[] { -4.5f, -0.5f })
            {
                Window(g, new Vector3(10f, 2.1f, z), -90f, 1.3f, 2f);
                foreach (float s in new[] { -1f, 1f })
                    Box("Curtain", g, new Vector3(9.9f, 2.05f, z + s * 0.85f), new Vector3(0.06f, 2.7f, 0.32f), curtain);
                Box("Pelmet", g, new Vector3(9.9f, 3.45f, z), new Vector3(0.08f, 0.18f, 2.1f), trim);
            }

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
            // Welcome mat and a little easel sign by the door.
            Box("Runner", g, new Vector3(0f, 0.004f, -6.2f), new Vector3(1.6f, 0.008f, 3.4f), M("Runner", new Color(0.78f, 0.25f, 0.32f)));
            Box("EaselLeg", g, new Vector3(1.75f, 0.7f, -7.1f), new Vector3(0.04f, 1.4f, 0.04f), M("BarWood", Palette.BarWood), Quaternion.Euler(-12, 0, 0));
            Box("EaselBoard", g, new Vector3(1.75f, 1.15f, -7.25f), new Vector3(0.7f, 0.9f, 0.03f), M("Tablecloth", Palette.Tablecloth), Quaternion.Euler(-12, 0, 0));
            Label("WELCOME", g, new Vector3(1.75f, 1.38f, -7.21f), Quaternion.Euler(-12, 180, 0), 0.06f, Palette.Trim);
            Label("Kayleigh & Todd", g, new Vector3(1.75f, 1.2f, -7.25f), Quaternion.Euler(-12, 180, 0), 0.05f, new Color(0.8f, 0.35f, 0.5f));
            Label("open bar til late", g, new Vector3(1.75f, 0.98f, -7.3f), Quaternion.Euler(-12, 180, 0), 0.035f, Palette.Trim);
        }

        // ---------- The bar ----------

        static void BuildBar()
        {
            var g = Group("Bar");
            var wood = M("BarWood", Palette.BarWood);
            var darkWood = M("BarWoodDark", Palette.BarWood * 0.75f);
            var panelWood = M("BarPanel", Palette.BarWood * 1.15f);
            var top = M("BarTop", Palette.BarTop, 0.6f);
            var trim = M("HallTrim", Palette.HallTrim, 0.4f);
            var white = M("PartyWhite", Palette.PartyWhite);

            float length = BarEndX - BarStartX, centerX = (BarEndX + BarStartX) / 2f;
            SetupUtil.Block("BarFront", g, new Vector3(centerX, 0.5f, 4.75f), new Vector3(length, 1f, 0.8f), wood);
            var counter = SetupUtil.Block("BarTop", g, new Vector3(centerX, BarTopY - 0.025f, 4.78f), new Vector3(length + 0.1f, 0.05f, 0.95f), top);
            counter.GetComponent<Collider>().sharedMaterial = PrefabBuilder.Slippery();
            Box("TopEdge", g, new Vector3(centerX, BarTopY - 0.04f, 4.3f), new Vector3(length + 0.1f, 0.06f, 0.04f), trim);

            // Front: raised panels between dark pilasters, gold band, brass foot rail.
            Box("Kick", g, new Vector3(centerX, 0.06f, 4.33f), new Vector3(length, 0.12f, 0.04f), darkWood);
            Box("TrimBand", g, new Vector3(centerX, 0.9f, 4.33f), new Vector3(length, 0.05f, 0.02f), trim);
            int panels = 6;
            float pw = length / panels;
            for (int i = 0; i <= panels; i++)
                Box("Pilaster", g, new Vector3(BarStartX + i * pw, 0.48f, 4.33f), new Vector3(0.1f, 0.74f, 0.04f), darkWood);
            for (int i = 0; i < panels; i++)
            {
                float x = BarStartX + (i + 0.5f) * pw;
                Box("Panel", g, new Vector3(x, 0.48f, 4.335f), new Vector3(pw - 0.3f, 0.56f, 0.02f), panelWood);
                Box("PanelInset", g, new Vector3(x, 0.48f, 4.325f), new Vector3(pw - 0.5f, 0.38f, 0.01f), wood);
            }
            Vis(LowPolyMeshes.Cylinder, "FootRail", g, new Vector3(centerX, 0.22f, 4.18f), new Vector3(0.05f, length / 2f - 0.2f, 0.05f), trim,
                Quaternion.Euler(0, 0, 90));
            for (int i = 1; i < panels; i++)
                Box("RailBracket", g, new Vector3(BarStartX + i * pw, 0.22f, 4.25f), new Vector3(0.04f, 0.04f, 0.14f), trim);

            // Flap counter closing off the west end behind the bar.
            SetupUtil.Block("EndCounter", g, new Vector3(BarStartX - 0.2f, 0.5f, 6.18f), new Vector3(0.4f, 1f, 2.05f), wood);
            SetupUtil.Block("EndCounterTop", g, new Vector3(BarStartX - 0.2f, BarTopY - 0.025f, 6.18f), new Vector3(0.46f, 0.05f, 2.1f), top);

            // Back counter, mirror, lit shelves and bottles.
            float backW = 3f - (StairX + 0.95f), backX = (3f + StairX + 0.95f) / 2f;
            SetupUtil.Block("BackCounter", g, new Vector3(backX, 0.47f, 7.6f), new Vector3(backW, 0.94f, 0.8f), wood);
            SetupUtil.Block("BackCounterTop", g, new Vector3(backX, 0.96f, 7.6f), new Vector3(backW, 0.04f, 0.84f), top);
            for (float x = backX - backW / 2f + 0.6f; x < backX + backW / 2f; x += 1.2f)
                Box("Drawer", g, new Vector3(x, 0.6f, 7.19f), new Vector3(1.0f, 0.3f, 0.02f), panelWood);
            float shelfW = backW - 0.6f;
            Box("Mirror", g, new Vector3(backX, 1.95f, 7.99f), new Vector3(shelfW + 0.2f, 1.1f, 0.01f),
                SetupUtil.Mat("BarMirror", new Color(0.72f, 0.78f, 0.84f), 0.95f));
            Box("MirrorFrame", g, new Vector3(backX, 2.52f, 7.98f), new Vector3(shelfW + 0.3f, 0.06f, 0.03f), trim);
            Box("MirrorFrameLow", g, new Vector3(backX, 1.38f, 7.98f), new Vector3(shelfW + 0.3f, 0.06f, 0.03f), trim);
            var shelfMat = M("Shelf", Palette.BarWood * 0.9f);
            var glow = SetupUtil.Emissive("ShelfGlow", Palette.WarmLight, 1.8f);
            var bottleColors = new[] { new Color(0.25f, 0.5f, 0.3f), new Color(0.55f, 0.35f, 0.15f), new Color(0.75f, 0.85f, 0.9f),
                new Color(0.45f, 0.12f, 0.2f), new Color(0.85f, 0.7f, 0.3f) };
            foreach (float y in new[] { 1.65f, 2.15f })
            {
                Box("Shelf", g, new Vector3(backX, y, 7.85f), new Vector3(shelfW, 0.04f, 0.28f), shelfMat);
                Box("ShelfLight", g, new Vector3(backX, y - 0.03f, 7.93f), new Vector3(shelfW, 0.015f, 0.03f), glow);
                int i = 0;
                for (float x = backX - shelfW / 2f + 0.2f; x < backX + shelfW / 2f - 0.1f; x += 0.3f, i++)
                {
                    var mat = M("Bottle" + i % bottleColors.Length, bottleColors[i % bottleColors.Length], 0.7f);
                    float h = 0.24f + (i * 37 % 7) * 0.012f;
                    Vis(LowPolyMeshes.Cylinder, "Bottle", g, new Vector3(x, y + 0.02f + h / 2f, 7.85f), new Vector3(0.08f, h / 2f, 0.08f), mat);
                    Vis(LowPolyMeshes.Cylinder, "Neck", g, new Vector3(x, y + 0.02f + h + 0.05f, 7.85f), new Vector3(0.035f, 0.05f, 0.035f), mat);
                }
            }
            var neon = SetupUtil.Emissive("Neon", Palette.Neon, 3f);
            // Off to the side, clear of the hanging station signs.
            Label("OPEN BAR", g, new Vector3(1.4f, 3.05f, 7.95f), Quaternion.identity, 0.32f, Palette.Neon);
            Box("NeonUnderline", g, new Vector3(1.4f, 2.72f, 7.96f), new Vector3(2.6f, 0.04f, 0.02f), neon);

            // Back counter clutter: till, ice bucket, lemons, napkins, a champagne tower nobody's touching.
            Box("Till", g, new Vector3(2.4f, 1.1f, 7.6f), new Vector3(0.42f, 0.24f, 0.36f), M("Ink", new Color(0.1f, 0.1f, 0.12f)), Quaternion.Euler(-10, 0, 0));
            Box("TillScreen", g, new Vector3(2.4f, 1.27f, 7.72f), new Vector3(0.3f, 0.16f, 0.02f), SetupUtil.Emissive("TillScreen", new Color(0.4f, 0.9f, 0.6f), 1.2f),
                Quaternion.Euler(-25, 0, 0));
            Vis(LowPolyMeshes.Cylinder, "IceBucket", g, new Vector3(-3.6f, 1.1f, 7.55f), new Vector3(0.28f, 0.12f, 0.28f), Chrome());
            Vis(LowPolyMeshes.Sphere, "Ice", g, new Vector3(-3.6f, 1.2f, 7.55f), new Vector3(0.24f, 0.06f, 0.24f), SetupUtil.Mat("Ice", new Color(0.88f, 0.95f, 1f), 0.8f));
            Vis(LowPolyMeshes.Cylinder, "Bottle", g, new Vector3(-3.55f, 1.3f, 7.58f), new Vector3(0.08f, 0.16f, 0.08f), M("Bottle0", bottleColors[0], 0.7f),
                Quaternion.Euler(12, 0, 8));
            for (int t = 0; t < 3; t++)
                for (int k = 0; k < 3 - t; k++)
                {
                    float y = 0.98f + 0.07f + t * 0.14f;
                    float x = 0.9f + (k - (2 - t) / 2f) * 0.14f;
                    Vis(LowPolyMeshes.Cylinder, "Flute", g, new Vector3(x, y, 7.55f), new Vector3(0.06f, 0.07f, 0.06f), SetupUtil.Mat("Champagne", new Color(0.98f, 0.88f, 0.55f), 0.8f));
                }
            Label("FOR THE TOAST", g, new Vector3(0.9f, 1.02f, 7.18f), Quaternion.identity, 0.03f, Palette.Trim, Palette.PartyWhite);

            // The two drink stations: tap + its own little rack of clean glasses, a sign overhead.
            Station(g, "BEER", BeerTap, k_BeerRack, k_BeerColor);
            Station(g, "WINE", WineBox, k_WineRack, k_WineColor);

            // A bowl of lemons between the stations, napkins, beer mats in front of every guest spot, a tip jar.
            Vis(LowPolyMeshes.Cylinder, "Bowl", g, new Vector3(-1.5f, BarTopY + 0.03f, 5.05f), new Vector3(0.24f, 0.03f, 0.24f), white);
            var lemon = M("Lemon", new Color(1f, 0.86f, 0.25f), 0.4f);
            for (int i = 0; i < 4; i++)
                Vis(LowPolyMeshes.Sphere, "Lemon", g, new Vector3(-1.5f + Mathf.Sin(i * 1.7f) * 0.06f, BarTopY + 0.07f + (i == 3 ? 0.04f : 0f), 5.05f + Mathf.Cos(i * 1.7f) * 0.06f),
                    new Vector3(0.08f, 0.07f, 0.07f), lemon);
            Box("Napkins", g, new Vector3(-1.5f, BarTopY + 0.025f, 4.55f), new Vector3(0.16f, 0.05f, 0.16f), white, Quaternion.Euler(0, 20, 0));
            var mat1 = M("BeerMat", new Color(0.72f, 0.2f, 0.25f));
            foreach (float x in k_GuestSlotsX)
                Box("BeerMat", g, new Vector3(x, BarTopY + 0.002f, 4.55f), new Vector3(0.11f, 0.004f, 0.11f), mat1, Quaternion.Euler(0, x * 40f, 0));
            Vis(LowPolyMeshes.Cylinder, "TipJar", g, new Vector3(2.8f, BarTopY + 0.09f, 4.5f), new Vector3(0.14f, 0.09f, 0.14f), GlassPane);
            Vis(LowPolyMeshes.Cylinder, "Tips", g, new Vector3(2.8f, BarTopY + 0.03f, 4.5f), new Vector3(0.12f, 0.03f, 0.12f), M("PartyGold", Palette.PartyGold));
            Label("TIPS :)", g, new Vector3(2.8f, BarTopY + 0.1f, 4.42f), Quaternion.identity, 0.03f, Palette.Trim, Palette.PartyWhite);

            // Keg spot under the beer tap: drag the spare keg here.
            var kegSpot = new Vector3(BeerTap.x, 0f, BeerTap.z + 0.75f);
            Box("KegSpot", g, kegSpot + new Vector3(0, 0.006f, 0), new Vector3(0.9f, 0.012f, 0.9f), M("Target", Palette.Target));
            Label("KEG", g, kegSpot + new Vector3(0, 0.015f, 0), Quaternion.Euler(90, 0, 0), 0.18f, Palette.PartyWhite);
            Vis(LowPolyMeshes.Cylinder, "Hose", g, new Vector3(BeerTap.x - 0.1f, 0.5f, 5.2f), new Vector3(0.04f, 0.5f, 0.04f), Metal);
            Label("BEER KEG GOES HERE", g, new Vector3(BeerTap.x - 0.75f, 0.72f, 5.215f), Quaternion.Euler(0, 180, 0), 0.06f,
                Palette.PartyWhite, Palette.Trim);
            // Bin and a sink behind the bar.
            Vis(LowPolyMeshes.Cylinder, "Bin", g, new Vector3(2.5f, 0.3f, 5.6f), new Vector3(0.36f, 0.3f, 0.36f), M("Ink", new Color(0.1f, 0.1f, 0.12f)));
            Box("Sink", g, new Vector3(1.2f, 0.98f, 7.6f), new Vector3(0.6f, 0.02f, 0.4f), Chrome());

            // Bar stools on the guest side, between the guest spots.
            var seat = M("StoolSeat", Palette.Couch);
            foreach (float x in new[] { -4.5f, 1.5f, 2.85f })
            {
                Vis(LowPolyMeshes.Cylinder, "StoolLeg", g, new Vector3(x, 0.36f, 3.75f), new Vector3(0.06f, 0.36f, 0.06f), Metal);
                Vis(LowPolyMeshes.Cylinder, "StoolRing", g, new Vector3(x, 0.28f, 3.75f), new Vector3(0.28f, 0.015f, 0.28f), Metal);
                Vis(LowPolyMeshes.Cylinder, "StoolSeat", g, new Vector3(x, 0.74f, 3.75f), new Vector3(0.38f, 0.04f, 0.38f), seat);
            }

            // A regular propped on the end of the bar.
            Npc(g, new Vector3(BarStartX - 0.45f, 0f, 4.7f), 90f, new Color(0.55f, 0.45f, 0.3f), 1, hat: -1, PartyGuest.Style.Lean, 0.8f);
        }

        /// <summary>A tap's surroundings: a coloured mat, a rack of glasses with a lip, a hanging sign.</summary>
        static void Station(Transform g, string name, Vector3 tap, Vector3 rack, Color color)
        {
            var mat = M("Station" + name, color);
            var chrome = Chrome();
            Box(name + "Mat", g, new Vector3((tap.x + rack.x) / 2f, BarTopY + 0.002f, 4.93f), new Vector3(Mathf.Abs(tap.x - rack.x) + 0.6f, 0.004f, 0.42f), mat);

            // Rack: a chrome tray with a low lip so glasses don't skate off.
            Box(name + "Rack", g, rack + new Vector3(0, 0.006f, 0), new Vector3(0.5f, 0.008f, 0.34f), chrome);
            foreach (var (pos, size) in new[]
                     {
                         (new Vector3(0, 0.02f, 0.17f), new Vector3(0.52f, 0.03f, 0.015f)),
                         (new Vector3(0, 0.02f, -0.17f), new Vector3(0.52f, 0.03f, 0.015f)),
                         (new Vector3(0.25f, 0.02f, 0), new Vector3(0.015f, 0.03f, 0.34f)),
                         (new Vector3(-0.25f, 0.02f, 0), new Vector3(0.015f, 0.03f, 0.34f)),
                     })
                Solid(PrimitiveType.Cube, name + "RackLip", g, rack + pos, size, chrome);
            Label("GLASSES", g, rack + new Vector3(0, 0.07f, -0.19f), Quaternion.Euler(0, 180, 0), 0.03f, Palette.PartyWhite, color);

            // Sign hanging over the station, readable from both sides of the bar.
            var sign = new Vector3(tap.x, 3.1f, 4.8f);
            foreach (float s in new[] { -0.45f, 0.45f })
                Box(name + "Chain", g, new Vector3(sign.x + s, (Height + sign.y) / 2f, sign.z), new Vector3(0.015f, Height - sign.y, 0.015f), Metal);
            Label(name, g, sign + new Vector3(0, 0, -0.03f), Quaternion.identity, 0.2f, Palette.PartyWhite, color);
            Label(name, g, sign + new Vector3(0, 0, 0.03f), Quaternion.Euler(0, 180, 0), 0.2f, Palette.PartyWhite, color);
        }

        // ---------- Dance floor ----------

        static void BuildDanceFloor()
        {
            var g = Group("DanceFloor");
            var light = M("DanceLight", Palette.DanceLight, 0.5f);
            var dark = M("DanceDark", Palette.DanceDark, 0.5f);
            for (int x = 0; x < 7; x++)
            for (int z = 0; z < 6; z++)
                Box("Tile", g, new Vector3(1.5f + x + 0.5f, 0.004f, -6.5f + z + 0.5f), new Vector3(1f, 0.008f, 1f), (x + z) % 2 == 0 ? light : dark);
            Box("FloorEdge", g, new Vector3(5f, 0.006f, -0.48f), new Vector3(7.04f, 0.012f, 0.04f), M("HallTrim", Palette.HallTrim, 0.4f));
            Box("FloorEdgeW", g, new Vector3(1.48f, 0.006f, -3.5f), new Vector3(0.04f, 0.012f, 6f), M("HallTrim", Palette.HallTrim, 0.4f));

            // DJ booth against the east wall.
            var booth = M("DJBooth", Palette.DanceDark);
            SetupUtil.Block("DJBooth", g, new Vector3(9.3f, 0.55f, -3.2f), new Vector3(1.2f, 1.1f, 2.2f), booth);
            Box("Decks", g, new Vector3(9.3f, 1.13f, -3.2f), new Vector3(0.7f, 0.06f, 1.4f), Metal);
            foreach (float z in new[] { -3.6f, -2.8f })
                Vis(LowPolyMeshes.Cylinder, "Record", g, new Vector3(9.3f, 1.17f, z), new Vector3(0.32f, 0.01f, 0.32f), M("Ink", new Color(0.1f, 0.1f, 0.12f)));
            Box("BoothStripe", g, new Vector3(8.69f, 0.85f, -3.2f), new Vector3(0.02f, 0.06f, 2.2f), SetupUtil.Emissive("Neon", Palette.Neon, 3f));
            Label("DJ GAZ", g, new Vector3(8.68f, 0.5f, -3.2f), Quaternion.Euler(0, 90, 0), 0.16f, Palette.Neon);
            foreach (float z in new[] { -5f, -1.4f })
            {
                SetupUtil.Block("Speaker", g, new Vector3(9.5f, 0.8f, z), new Vector3(0.7f, 1.6f, 0.7f), M("Speaker", new Color(0.15f, 0.15f, 0.17f)));
                Vis(LowPolyMeshes.Cylinder, "Woofer", g, new Vector3(9.14f, 0.55f, z), new Vector3(0.42f, 0.01f, 0.42f), Metal, Quaternion.Euler(0, 0, 90));
                Vis(LowPolyMeshes.Cylinder, "Tweeter", g, new Vector3(9.14f, 1.2f, z), new Vector3(0.2f, 0.01f, 0.2f), Metal, Quaternion.Euler(0, 0, 90));
            }
            // DJ Gaz himself, nodding along behind the decks.
            Npc(g, new Vector3(9.75f, 0f, -3.2f), -90f, new Color(0.15f, 0.15f, 0.18f), 2, hat: -1, PartyGuest.Style.Chat, 2.2f);

            // Disco ball.
            Vis(LowPolyMeshes.Sphere, "DiscoBall", g, new Vector3(5f, 3.4f, -3.5f), Vector3.one * 0.5f, SetupUtil.Mat("Mirror", new Color(0.85f, 0.88f, 0.95f), 0.95f));
            Box("DiscoString", g, new Vector3(5f, 3.85f, -3.5f), new Vector3(0.01f, 0.5f, 0.01f), Metal);

            // Guests who just never stop dancing (kept clear of the conga route).
            Npc(g, new Vector3(3.2f, 0f, -4.4f), 40f, new Color(0.35f, 0.55f, 0.75f), 0, hat: 1, PartyGuest.Style.Dance);
            Npc(g, new Vector3(6.4f, 0f, -2.1f), -120f, new Color(0.9f, 0.55f, 0.6f), 1, hat: 2, PartyGuest.Style.Dance, 1.1f);
            Npc(g, new Vector3(7.6f, 0f, -5.3f), 200f, new Color(0.45f, 0.35f, 0.6f), 2, hat: -1, PartyGuest.Style.Dance, 0.9f);
            Npc(g, new Vector3(4.4f, 0f, -2.0f), 160f, new Color(0.95f, 0.75f, 0.3f), 0, hat: -1, PartyGuest.Style.Dance, 1.2f);
            Npc(g, new Vector3(2.6f, 0f, -2.0f), 100f, new Color(0.3f, 0.6f, 0.45f), 1, hat: 0, PartyGuest.Style.Dance, 0.8f);
            Npc(g, new Vector3(4.6f, 0f, -4.3f), -40f, new Color(0.85f, 0.85f, 0.88f), 2, hat: -1, PartyGuest.Style.Dance, 1.05f);
        }

        // ---------- Tables ----------

        static void BuildTables()
        {
            var g = Group("Tables");
            var cloth = M("Tablecloth", Palette.Tablecloth);
            var chair = M("Chair", Palette.Chair);
            var pink = M("PartyPink", Palette.PartyPink);
            var gold = M("PartyGold", Palette.PartyGold);
            var white = M("PartyWhite", Palette.PartyWhite);
            var candle = SetupUtil.Emissive("Candle", Palette.WarmLight, 2.5f);
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
                Vis(LowPolyMeshes.Cylinder, "Runner", tg, new Vector3(0, 0.765f, 0), new Vector3(0.9f, 0.005f, 0.9f), n % 2 == 0 ? pink : gold);
                Vis(LowPolyMeshes.Cylinder, "Vase", tg, new Vector3(0, 0.88f, 0), new Vector3(0.12f, 0.11f, 0.12f), GlassPane);
                Vis(LowPolyMeshes.Sphere, "Flowers", tg, new Vector3(0, 1.05f, 0), Vector3.one * 0.26f, n % 2 == 0 ? pink : gold);
                Box("TableNumber", tg, new Vector3(0.3f, 0.86f, -0.2f), new Vector3(0.12f, 0.16f, 0.01f), white, Quaternion.Euler(-10, 20, 0));
                for (int c = 0; c < 6; c++)
                {
                    float a = c * 60f + 30f;
                    var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                    var cp = dir * 1.15f;
                    Box("Seat", tg, cp + new Vector3(0, 0.45f, 0), new Vector3(0.42f, 0.05f, 0.42f), chair, Quaternion.Euler(0, a, 0));
                    Box("Back", tg, cp + dir * 0.2f + new Vector3(0, 0.75f, 0), new Vector3(0.42f, 0.55f, 0.04f), chair, Quaternion.Euler(0, a, 0));
                    Box("Bow", tg, cp + dir * 0.23f + new Vector3(0, 0.75f, 0), new Vector3(0.18f, 0.12f, 0.03f), pink, Quaternion.Euler(0, a, 0));
                    // Place setting and a tea light at every seat.
                    Vis(LowPolyMeshes.Cylinder, "Plate", tg, dir * 0.55f + new Vector3(0, 0.77f, 0), new Vector3(0.22f, 0.006f, 0.22f), white);
                    if (c % 2 == 0)
                        Vis(LowPolyMeshes.Cylinder, "Candle", tg, dir * 0.3f + new Vector3(0, 0.79f, 0), new Vector3(0.04f, 0.02f, 0.04f), candle);
                }
                // Somebody's still sitting there, chatting away.
                if (n % 2 == 1)
                    Npc(tg, Quaternion.Euler(0, 30f, 0) * Vector3.forward * 1.25f, 210f, new Color(0.3f, 0.5f, 0.4f), n, hat: -1, PartyGuest.Style.Chat);
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

        // ---------- Head table and photo booth (east side) ----------

        static void BuildHeadTableAndPhotoBooth()
        {
            var g = Group("HeadTable");
            var cloth = M("Tablecloth", Palette.Tablecloth);
            var pink = M("PartyPink", Palette.PartyPink);
            var gold = M("PartyGold", Palette.PartyGold);
            var white = M("PartyWhite", Palette.PartyWhite);
            var chair = M("Chair", Palette.Chair);

            // Long table along the east wall, the couple and their best man / bridesmaid behind it.
            const float tx = 8.9f, tz = 3.2f, len = 4.4f;
            SetupUtil.Block("HeadTable", g, new Vector3(tx, 0.38f, tz), new Vector3(0.8f, 0.76f, len), cloth);
            Box("Swag", g, new Vector3(tx - 0.41f, 0.62f, tz), new Vector3(0.02f, 0.22f, len), pink);
            for (int i = 0; i < 12; i++)
            {
                float z = tz - len / 2f + 0.2f + i * (len - 0.4f) / 11f;
                Vis(LowPolyMeshes.Sphere, "Rose", g, new Vector3(tx - 0.43f, 0.74f, z), Vector3.one * 0.11f, i % 2 == 0 ? pink : gold);
            }
            Label("MR & MRS", g, new Vector3(tx - 0.43f, 0.45f, tz), Quaternion.Euler(0, 90, 0), 0.12f, Palette.Trim);
            var seats = new[] { tz - 1.5f, tz - 0.5f, tz + 0.5f, tz + 1.5f };
            var outfits = new[] { new Color(0.55f, 0.6f, 0.75f), new Color(0.97f, 0.96f, 0.94f), new Color(0.15f, 0.15f, 0.2f), new Color(0.9f, 0.55f, 0.65f) };
            var hats = new[] { -1, 2, 0, -1 };
            for (int i = 0; i < seats.Length; i++)
            {
                Vis(LowPolyMeshes.Cylinder, "Plate", g, new Vector3(tx - 0.12f, 0.77f, seats[i]), new Vector3(0.24f, 0.006f, 0.24f), white);
                Vis(LowPolyMeshes.Cylinder, "Flute", g, new Vector3(tx - 0.28f, 0.84f, seats[i] + 0.18f), new Vector3(0.05f, 0.08f, 0.05f), GlassPane);
                Box("ChairBack", g, new Vector3(9.85f, 0.75f, seats[i]), new Vector3(0.04f, 0.6f, 0.44f), chair);
                Npc(g, new Vector3(9.55f, 0f, seats[i]), -90f, outfits[i], i, hats[i], PartyGuest.Style.Chat, 0.8f + i * 0.15f);
            }
            // Bunting on the wall behind them.
            for (int i = 0; i < 14; i++)
            {
                float z = tz - 2.6f + i * 0.4f;
                float y = 3.0f - Mathf.Sin(i / 13f * Mathf.PI) * 0.35f;
                Box("Flag", g, new Vector3(9.97f, y, z), new Vector3(0.01f, 0.22f, 0.22f), i % 3 == 0 ? pink : i % 3 == 1 ? gold : white,
                    Quaternion.Euler(45, 0, 0));
            }

            // Photo booth in the north-east corner: curtain backdrop, prop box and a camera on a tripod.
            var b = Group("PhotoBooth");
            const float bx = 8.4f;
            SetupUtil.Block("Backdrop", b, new Vector3(bx, 1.25f, 7.88f), new Vector3(2.4f, 2.5f, 0.06f), M("Curtain", new Color(0.86f, 0.42f, 0.52f)));
            for (int i = 0; i < 8; i++)
                Box("Fold", b, new Vector3(bx - 1.05f + i * 0.3f, 1.25f, 7.84f), new Vector3(0.1f, 2.5f, 0.03f), M("CurtainFold", new Color(0.78f, 0.35f, 0.45f)));
            foreach (float s in new[] { -1.25f, 1.25f })
                Box("Post", b, new Vector3(bx + s, 1.35f, 7.85f), new Vector3(0.08f, 2.7f, 0.08f), gold);
            Box("Header", b, new Vector3(bx, 2.72f, 7.85f), new Vector3(2.6f, 0.3f, 0.08f), gold);
            Label("SAY CHEESE!", b, new Vector3(bx, 2.72f, 7.8f), Quaternion.identity, 0.12f, Palette.Trim);
            SetupUtil.Block("PropBox", b, new Vector3(9.6f, 0.3f, 6.4f), new Vector3(0.6f, 0.6f, 0.6f), M("Gift0", Palette.PartyPink));
            Label("PROPS", b, new Vector3(9.29f, 0.35f, 6.4f), Quaternion.Euler(0, 90, 0), 0.08f, Palette.PartyWhite);
            Vis(LowPolyMeshes.Sphere, "Moustache", b, new Vector3(9.55f, 0.66f, 6.3f), new Vector3(0.18f, 0.05f, 0.08f), M("Ink", new Color(0.1f, 0.1f, 0.12f)));
            Box("Sunglasses", b, new Vector3(9.65f, 0.63f, 6.5f), new Vector3(0.2f, 0.05f, 0.06f), M("Ink", new Color(0.1f, 0.1f, 0.12f)), Quaternion.Euler(0, 30, 0));
            for (int i = 0; i < 3; i++)
                Box("TripodLeg", b, new Vector3(bx + Mathf.Sin(i * 2.09f) * 0.12f, 0.7f, 6.1f + Mathf.Cos(i * 2.09f) * 0.12f), new Vector3(0.03f, 1.4f, 0.03f), Metal,
                    Quaternion.Euler(Mathf.Cos(i * 2.09f) * -8f, 0, Mathf.Sin(i * 2.09f) * 8f));
            Box("Camera", b, new Vector3(bx, 1.5f, 6.1f), new Vector3(0.3f, 0.2f, 0.18f), M("Ink", new Color(0.1f, 0.1f, 0.12f)));
            Vis(LowPolyMeshes.Cylinder, "Lens", b, new Vector3(bx, 1.5f, 6.22f), new Vector3(0.1f, 0.05f, 0.1f), Metal, Quaternion.Euler(90, 0, 0));
            // Two guests mid-pose, one in a moustache.
            Npc(b, new Vector3(bx - 0.4f, 0f, 7.3f), 180f, new Color(0.35f, 0.55f, 0.75f), 0, hat: 1, PartyGuest.Style.Dance, 0.6f);
            Npc(b, new Vector3(bx + 0.45f, 0f, 7.3f), 180f, new Color(0.95f, 0.75f, 0.3f), 2, hat: -1, PartyGuest.Style.Chat, 1.4f);
        }

        // ---------- Wedding decorations ----------

        static void BuildDecor()
        {
            var g = Group("Decor");
            Label("KAYLEIGH & TODD", g, new Vector3(4.5f, 3.3f, -7.95f), Quaternion.Euler(0, 180, 0), 0.4f,
                Palette.PartyWhite, Palette.PartyPink);
            Label("JUST MARRIED (TODAY!)", g, new Vector3(-9.95f, 3.1f, -3f), Quaternion.Euler(0, -90, 0), 0.22f,
                Palette.Trim, Palette.PartyGold);

            // Fabric swags draped across the ceiling, pink and cream.
            var cream = M("Drape", new Color(0.99f, 0.96f, 0.9f));
            var rose = M("DrapePink", new Color(0.97f, 0.78f, 0.84f));
            int row = 0;
            foreach (float z in new[] { -5.3f, -1.9f, 1.5f, 4.9f })
            {
                const int segs = 12;
                Vector3 Point(int i)
                {
                    float x = -10f + 20f * i / segs, u = x / 10f;
                    return new Vector3(x, Height - 0.05f - 0.5f * (1f - u * u), z);
                }
                for (int i = 0; i < segs; i++)
                {
                    Vector3 a = Point(i), c = Point(i + 1), d = c - a;
                    Box("Swag", g, (a + c) / 2f, new Vector3(d.magnitude + 0.02f, 0.02f, 1.1f), row % 2 == 0 ? cream : rose,
                        Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg));
                }
                row++;
            }

            // String lights zig-zagging under the ceiling.
            var bulb = SetupUtil.Emissive("FairyLight", Palette.PartyGold, 1.4f);
            for (int r = 0; r < 4; r++)
            {
                float z = -7f + r * 3.4f;
                for (float x = -9.5f; x <= 9.5f; x += 0.7f)
                {
                    float sag = Mathf.Sin((x + 9.5f) / 19f * Mathf.PI * 3f);
                    Vis(LowPolyMeshes.Sphere, "Bulb", g, new Vector3(x, 3.85f - Mathf.Abs(sag) * 0.35f, z), Vector3.one * 0.08f, bulb);
                }
            }

            // Balloons in clumps.
            var colors = new[] { Palette.PartyPink, Palette.PartyGold, Palette.PartyWhite };
            var clumps = new[] { new Vector3(-9.4f, 0, -7.4f), new Vector3(9.4f, 0, -7.4f), new Vector3(-1.8f, 0, -7.4f), new Vector3(9.4f, 0, 0.3f),
                new Vector3(3.6f, 0, 7.4f) };
            int k = 0;
            foreach (var c in clumps)
                for (int b = 0; b < 4; b++, k++)
                {
                    var off = new Vector3(Mathf.Sin(b * 2.1f) * 0.25f, 1.7f + b * 0.22f, Mathf.Cos(b * 2.1f) * 0.2f);
                    Vis(LowPolyMeshes.Sphere, "Balloon", g, c + off, new Vector3(0.36f, 0.42f, 0.36f), M("Balloon" + k % 3, colors[k % 3], 0.5f));
                    Box("BalloonString", g, c + new Vector3(off.x * 0.5f, off.y * 0.5f, off.z * 0.5f), new Vector3(0.008f, off.y, 0.008f), Metal);
                }

            // Gift table near the entrance.
            SetupUtil.Block("GiftTable", g, new Vector3(-8.6f, 0.38f, 1.6f), new Vector3(1.2f, 0.76f, 2.2f), M("Tablecloth", Palette.Tablecloth));
            var giftColors = new[] { Palette.PartyPink, Palette.Mailbox, Palette.PartyGold, Palette.Target };
            for (int i = 0; i < 6; i++)
            {
                float s = 0.22f + (i % 3) * 0.08f;
                var pos = new Vector3(-8.6f + (i % 2 - 0.5f) * 0.5f, 0.76f + s / 2f, 0.8f + i * 0.3f);
                Box("Gift", g, pos, Vector3.one * s, M("Gift" + i % 4, giftColors[i % 4]));
                Box("Ribbon", g, pos, new Vector3(s + 0.01f, s + 0.01f, 0.04f), M("PartyWhite", Palette.PartyWhite));
            }
            Label("GIFTS", g, new Vector3(-7.99f, 0.5f, 1.6f), Quaternion.Euler(0, -90, 0), 0.1f, Palette.Trim);

            // Little knots of guests chatting.
            Chatters(g, new Vector3(-8.1f, 0f, 3.75f), new[] { new Color(0.55f, 0.25f, 0.35f), new Color(0.35f, 0.55f, 0.75f), new Color(0.85f, 0.75f, 0.55f) });
            Chatters(g, new Vector3(-2.45f, 0f, -4.4f), new[] { new Color(0.45f, 0.35f, 0.6f), new Color(0.3f, 0.5f, 0.4f) });
        }

        /// <summary>A small circle of guests facing each other.</summary>
        static void Chatters(Transform g, Vector3 center, Color[] outfits)
        {
            for (int i = 0; i < outfits.Length; i++)
            {
                float a = (i * 360f / outfits.Length + 20f) * Mathf.Deg2Rad;
                var pos = center + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 0.5f;
                Vector3 toCenter = center - pos;
                float yaw = Mathf.Atan2(toCenter.x, toCenter.z) * Mathf.Rad2Deg;
                Npc(g, pos, yaw, outfits[i], i, i == 1 ? 1 : -1, PartyGuest.Style.Chat, 0.8f + i * 0.25f);
            }
        }

        // ---------- Staff stairs and basement ----------

        static void BuildStairsAndBasement()
        {
            var g = Group("Basement");
            var wall = M("BasementWall", Palette.Basement);
            var floor = M("BasementFloor", Palette.BasementFloor);
            var stairs = M("Stairs", Palette.Stairs);
            float sx = StairX;

            // Staff door frame and signs (read from inside the bar, looking north).
            foreach (float x in new[] { sx - 0.95f, sx + 0.95f })
                Box("StaffJamb", g, new Vector3(x, 1.2f, 7.97f), new Vector3(0.12f, 2.4f, 0.08f), Stone);
            Box("StaffHead", g, new Vector3(sx, 2.43f, 7.97f), new Vector3(2.02f, 0.1f, 0.08f), Stone);
            Label("STAFF ONLY", g, new Vector3(sx, 2.7f, 7.94f), Quaternion.identity, 0.12f, Palette.PartyWhite, Palette.Awning);
            Label("SPARE KEGS DOWNSTAIRS", g, new Vector3(sx, 2.95f, 7.94f), Quaternion.identity, 0.07f, Palette.Trim, Palette.PartyGold);

            // Stair corridor from z 8 down to z 13 (y 0 → -3): visual steps on a smooth ramp.
            const int steps = 15;
            float run = 5f / steps, rise = 3f / steps;
            for (int i = 0; i < steps; i++)
            {
                float topY = -rise * (i + 1);
                float z = 8.2f + run * i + run / 2f;
                Box("Step", g, new Vector3(sx, (topY + BasementY - 0.2f) / 2f, z), new Vector3(1.8f, topY - BasementY + 0.2f, run), i % 2 == 0 ? stairs : floor);
                Box("Nosing", g, new Vector3(sx, topY + 0.005f, z - run / 2f + 0.03f), new Vector3(1.8f, 0.012f, 0.05f), M("Target", Palette.Target));
            }
            float angle = Mathf.Atan2(3f, 5f) * Mathf.Rad2Deg;
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "StairRamp";
            ramp.transform.SetParent(g, false);
            var normal = Quaternion.Euler(angle, 0, 0) * Vector3.up;
            ramp.transform.localPosition = new Vector3(sx, -1.5f + 0.05f, 10.7f) - normal * 0.1f;
            ramp.transform.localRotation = Quaternion.Euler(angle, 0, 0);
            ramp.transform.localScale = new Vector3(1.8f, 0.2f, Mathf.Sqrt(25f + 9f));
            Object.DestroyImmediate(ramp.GetComponent<MeshRenderer>());
            ramp.isStatic = true;

            SetupUtil.Block("StairWallW", g, new Vector3(sx - 1f, -0.3f, 10.6f), new Vector3(0.2f, 5.4f, 5f), wall);
            SetupUtil.Block("StairWallE", g, new Vector3(sx + 1f, -0.3f, 10.6f), new Vector3(0.2f, 5.4f, 5f), wall);
            SetupUtil.Block("StairCeiling", g, new Vector3(sx, 2.5f, 10.6f), new Vector3(2.2f, 0.2f, 5f), M("Ceiling", Palette.Ceiling));
            SetupUtil.Block("StairDrop", g, new Vector3(sx, 1.2f, 13.1f), new Vector3(2.2f, 2.4f, 0.2f), wall);
            Box("Handrail", g, new Vector3(sx - 0.87f, -0.6f, 10.6f), new Vector3(0.05f, 0.05f, 5.8f), Metal, Quaternion.Euler(angle, 0, 0));
            Box("Handrail", g, new Vector3(sx + 0.87f, -0.6f, 10.6f), new Vector3(0.05f, 0.05f, 5.8f), Metal, Quaternion.Euler(angle, 0, 0));
            Label("MIND THE STEP", g, new Vector3(sx - 0.89f, 0.6f, 9.6f), Quaternion.Euler(0, -90, 0), 0.07f, Palette.Trim, Palette.PartyGold);
            Lamp(g, new Vector3(sx, 1.8f, 9.5f), 5f, 1.1f);

            // Basement room x 2..10, z 13..19 (shifted along with the stairs).
            float cy = BasementY;
            SetupUtil.Block("BasementFloor", g, new Vector3(6f + Dx, cy - 0.1f, 16f), new Vector3(8.4f, 0.2f, 6.4f), floor);
            SetupUtil.Block("BasementCeiling", g, new Vector3(6f + Dx, 0.1f, 16f), new Vector3(8.4f, 0.2f, 6.4f), M("Ceiling", Palette.Ceiling));
            SetupUtil.Block("BasementN", g, new Vector3(6f + Dx, cy + 1.5f, 19.1f), new Vector3(8.4f, 3f, 0.2f), wall);
            SetupUtil.Block("BasementE", g, new Vector3(10.1f + Dx, cy + 1.5f, 16f), new Vector3(0.2f, 3f, 6.4f), wall);
            SetupUtil.Block("BasementW", g, new Vector3(1.9f + Dx, cy + 1.5f, 16f), new Vector3(0.2f, 3f, 6.4f), wall);
            SetupUtil.Block("BasementS_W", g, new Vector3(3.7f + Dx, cy + 1.5f, 12.9f), new Vector3(3.8f, 3f, 0.2f), wall);
            SetupUtil.Block("BasementS_E", g, new Vector3(8.8f + Dx, cy + 1.5f, 12.9f), new Vector3(2.8f, 3f, 0.2f), wall);
            Vis(LowPolyMeshes.Cylinder, "Pipe", g, new Vector3(6f + Dx, -0.25f, 18.8f), new Vector3(0.12f, 4f, 0.12f), Metal, Quaternion.Euler(0, 0, 90));
            Vis(LowPolyMeshes.Cylinder, "Pipe", g, new Vector3(2.2f + Dx, -0.4f, 16f), new Vector3(0.08f, 3f, 0.08f), Metal, Quaternion.Euler(90, 0, 0));
            Box("Puddle", g, new Vector3(7.2f + Dx, cy + 0.003f, 15.2f), new Vector3(0.9f, 0.004f, 0.6f), SetupUtil.Mat("Puddle", new Color(0.45f, 0.5f, 0.55f), 0.9f));

            // One bare bulb that doesn't quite work.
            Box("Flex", g, new Vector3(6f + Dx, -0.15f, 16f), new Vector3(0.01f, 0.3f, 0.01f), M("Ink", new Color(0.1f, 0.1f, 0.12f)));
            var bulbGo = Vis(LowPolyMeshes.Sphere, "Bulb", g, new Vector3(6f + Dx, -0.35f, 16f), Vector3.one * 0.12f, SetupUtil.Emissive("WarmLight", Palette.WarmLight));
            bulbGo.isStatic = false;
            var lightGo = SetupUtil.Empty("FlickerLight", g, new Vector3(6f + Dx, -0.6f, 16f));
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Palette.WarmLight;
            light.range = 8f;
            light.intensity = 1.5f;
            light.shadows = LightShadows.None;
            var flicker = new SerializedObject(lightGo.AddComponent<FlickerLight>());
            flicker.FindProperty("bulb").objectReferenceValue = bulbGo.GetComponent<Renderer>();
            flicker.ApplyModifiedPropertiesWithoutUndo();

            // Shelves of stuff, wine boxes, stacked crates.
            var shelf = M("Shelf", Palette.BarWood * 0.9f);
            foreach (float y in new[] { 0.6f, 1.3f, 2.0f })
                SetupUtil.Block("Shelf", g, new Vector3(9.6f + Dx, cy + y, 16f), new Vector3(0.7f, 0.05f, 5f), shelf);
            for (int i = 0; i < 6; i++)
                Box("StoredWine", g, new Vector3(9.6f + Dx, cy + 0.82f + (i % 3) * 0.7f, 14.4f + i * 0.55f), new Vector3(0.34f, 0.34f, 0.28f),
                    M("WineBox", k_WineColor));
            var crate = M("Crate", Palette.Cardboard);
            var slat = M("CrateSlat", Palette.Cardboard * 0.8f);
            foreach (var (pos, yaw) in new[] { (new Vector3(7.4f, 0.25f, 18.5f), 0f), (new Vector3(8.0f, 0.25f, 18.5f), 8f), (new Vector3(7.7f, 0.75f, 18.5f), -5f),
                         (new Vector3(2.5f, 0.25f, 17.6f), 15f) })
            {
                var c = SetupUtil.Block("Crate", g, new Vector3(pos.x + Dx, cy + pos.y, pos.z), Vector3.one * 0.5f, crate, yaw);
                Box("Slat", c.transform, new Vector3(0, 0, -0.51f), new Vector3(1.02f, 0.15f, 0.02f), slat);
            }
            Label("KEGS", g, new Vector3(4.6f + Dx, cy + 2.1f, 18.98f), Quaternion.identity, 0.2f, Palette.PartyWhite, Palette.GigOrange);
            Label("(heavy - lift with a friend)", g, new Vector3(4.6f + Dx, cy + 1.75f, 18.98f), Quaternion.identity, 0.07f, Palette.Trim, Palette.PartyWhite);
            Label("NO PARTYING IN THE BASEMENT", g, new Vector3(1.99f + Dx, cy + 1.6f, 16f), Quaternion.Euler(0, -90, 0), 0.07f,
                Palette.PartyWhite, Palette.Awning);
        }

        // ---------- Lights ----------

        static void BuildLights()
        {
            var g = Group("Lights");
            Lamp(g, new Vector3(-1.5f, 3.4f, 6.2f), 9f, 1.6f);  // behind the bar
            Lamp(g, new Vector3(-5f, 3.4f, 6.4f), 6f, 1.0f);
            Lamp(g, new Vector3(-1.5f, 3.4f, 2.5f), 9f, 1.2f);  // guest side
            Lamp(g, new Vector3(5f, 3.2f, -3.5f), 10f, 1.4f);   // dance floor
            Lamp(g, new Vector3(-5.5f, 3.4f, -4f), 10f, 1.2f);  // tables
            Lamp(g, new Vector3(8f, 3.2f, 3.2f), 7f, 1.1f);     // head table
            Lamp(g, new Vector3(8.4f, 2.8f, 6.4f), 4.5f, 1.0f); // photo booth
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
            Spawner(gameplay, PrefabBuilder.BeerTapPath, BeerTap, 0f, "BeerTapSpawner");
            Spawner(gameplay, PrefabBuilder.WineBoxPath, WineBox, 0f, "WineBoxSpawner");
            Spawner(gameplay, PrefabBuilder.KegPath, new Vector3(3.6f + Dx, BasementY + 0.31f, 18.2f), 0f, "KegSpawner1");
            Spawner(gameplay, PrefabBuilder.KegPath, new Vector3(4.4f + Dx, BasementY + 0.31f, 18.4f), 0f, "KegSpawner2");
            Spawner(gameplay, PrefabBuilder.KegPath, new Vector3(5.4f + Dx, BasementY + 0.31f, 18.1f), 0f, "KegSpawner3");
            Spawner(gameplay, PrefabBuilder.CongaPath, new Vector3(1.5f, 0f, -1f), 0f, "CongaSpawner");

            foreach (var (name, pos) in new[] { ("BeerGlassRack", k_BeerRack), ("WineGlassRack", k_WineRack) })
            {
                var rack = SetupUtil.Empty(name, gameplay, pos + new Vector3(0, 0.01f, 0)).AddComponent<GlassRack>();
                var rso = new SerializedObject(rack);
                rso.FindProperty("glassPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<NetworkObject>(PrefabBuilder.GlassPath);
                rso.FindProperty("keepOnRack").intValue = 3;
                rso.FindProperty("rackSize").vector2Value = new Vector2(0.2f, 0.12f);
                rso.ApplyModifiedPropertiesWithoutUndo();
            }

            var slots = SetupUtil.Empty("GuestSlots", gameplay, Vector3.zero).transform;
            foreach (float x in k_GuestSlotsX)
                SetupUtil.Empty("Slot", slots, new Vector3(x, 0f, 3.55f)).AddComponent<GuestSlot>();

            // Conga route: round the dance floor, up past the head table, along the end of the bar,
            // across in front of the guests, down past the cake, and back.
            var path = SetupUtil.Empty("CongaPath", gameplay, Vector3.zero).transform;
            var points = new[]
            {
                new Vector3(1.5f, 0, -1f), new Vector3(7f, 0, -1f), new Vector3(7f, 0, 5.8f), new Vector3(4f, 0, 5.8f),
                new Vector3(4f, 0, 2.2f), new Vector3(-1f, 0, 2.2f), new Vector3(-1f, 0, -5.5f), new Vector3(6f, 0, -5.5f),
                new Vector3(6f, 0, -3f), new Vector3(1.5f, 0, -3f),
            };
            for (int i = 0; i < points.Length; i++) SetupUtil.Empty("P" + i, path, points[i]);
            path.gameObject.AddComponent<CongaPath>();

            var players = new GameObject("PlayerSpawner");
            players.transform.SetParent(gameplay, false);
            players.transform.position = new Vector3(-1.5f, 0.1f, 6.3f);
            players.transform.rotation = Quaternion.Euler(0, 180, 0); // facing the guests
            for (int i = 0; i < SessionManager.MaxPlayers; i++)
                SetupUtil.Empty("Spawn" + (i + 1), players.transform, new Vector3(-1.8f + i * 1.2f, 0f, 0));
            var spawner = players.AddComponent<PlayerSpawner>();
            var so = new SerializedObject(spawner);
            so.FindProperty("playerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<NetworkObject>(PrefabBuilder.PlayerPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            // A few boxes in the basement to trip over.
            Spawner(gameplay, PrefabBuilder.BoxPath, new Vector3(3f + Dx, BasementY + 0.4f, 14.2f), 20f);
            Spawner(gameplay, PrefabBuilder.BoxPath, new Vector3(3.2f + Dx, BasementY + 0.95f, 14.3f), 50f);
            Spawner(gameplay, PrefabBuilder.BucketPath, new Vector3(8.6f + Dx, BasementY + 0.3f, 13.8f), 0f);
        }

        // ---------- Helpers ----------

        /// <summary>A decorative guest. With a style it dances / chats / leans; without one it stands still.</summary>
        static void Npc(Transform parent, Vector3 localPos, float yaw, Color outfit, int skin, int hat,
            PartyGuest.Style? style = null, float speed = 1f)
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

            bool animated = style.HasValue;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = !animated;
            if (!animated) return;
            var guest = new SerializedObject(go.AddComponent<PartyGuest>());
            guest.FindProperty("body").objectReferenceValue = parts.Body;
            guest.FindProperty("style").enumValueIndex = (int)style.Value;
            guest.FindProperty("speed").floatValue = speed;
            guest.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
