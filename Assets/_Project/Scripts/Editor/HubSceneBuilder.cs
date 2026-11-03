using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static Freelancer.EditorTools.SceneKit;

namespace Freelancer.EditorTools
{
    /// <summary>
    /// Builds the Hub scene: the crew's small, slightly run-down ground-floor flat with the
    /// GigGrabber computer, and the town block outside its front door (see HubSceneBuilder.Town).
    /// Room interior is x -5..5, z -4..4, floor at 0, ceiling at 2.9; the front door is in the
    /// south wall at x 2.7..3.7 and opens onto Main Street.
    /// Computer on the north wall, couch on the south wall, kitchenette east, mattress west.
    /// </summary>
    static partial class HubSceneBuilder
    {
        const float Height = 2.9f;
        static Transform s_Root;

        [MenuItem("Freelancer/Setup/Build Hub Scene", priority = 21)]
        public static void BuildHubScene()
        {
            SetupUtil.EnsureFolders();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            LookSetup.ApplyToOpenScene();

            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(12f, 6f, -26f);
                cam.transform.LookAt(new Vector3(0f, 1.5f, -8f));
            }

            s_Root = new GameObject("Apartment").transform;
            BuildShell();
            BuildComputerCorner();
            BuildLounge();
            BuildKitchenette();
            BuildBedCorner();
            BuildWallDecor();
            BuildClutter();
            BuildLights();
            PlaceGameplay();
            BuildTown();

            new GameObject("SessionHud").AddComponent<SessionHud>();
            new GameObject("HubHud").AddComponent<HubHud>();
            new GameObject("GigComputerUI").AddComponent<GigComputerUI>();
            new GameObject("LandlordLetterUI").AddComponent<LandlordLetterUI>();
            new GameObject("RouletteUI").AddComponent<RouletteUI>();

            EditorSceneManager.SaveScene(scene, SetupUtil.HubScenePath);
            Debug.Log("[Freelancer] Built " + SetupUtil.HubScenePath);
        }

        static Transform Group(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(s_Root, false);
            return t;
        }

        static Material M(string name, Color c, float smooth = 0.1f) => SetupUtil.Mat(name, c, smooth);

        // ---------- Room ----------

        static void BuildShell()
        {
            var g = Group("Shell");
            var wall = M("HubWall", Palette.HubWall);
            SetupUtil.Block("Floor", g, new Vector3(0f, -0.1f, 0f), new Vector3(10.4f, 0.2f, 8.4f), M("Floor", Palette.Floor));
            SetupUtil.Block("Ceiling", g, new Vector3(0f, Height + 0.1f, 0f), new Vector3(10.4f, 0.2f, 8.4f), M("Ceiling", Palette.Ceiling));
            SetupUtil.Block("WallN", g, new Vector3(0f, Height / 2f, 4.1f), new Vector3(10.4f, Height, 0.2f), wall);
            const float doorW = 2.7f, doorE = 3.7f, doorTop = 2.3f;
            SetupUtil.Block("WallS_W", g, new Vector3((-5.2f + doorW) / 2f, Height / 2f, -4.1f), new Vector3(doorW + 5.2f, Height, 0.2f), wall);
            SetupUtil.Block("WallS_E", g, new Vector3((doorE + 5.2f) / 2f, Height / 2f, -4.1f), new Vector3(5.2f - doorE, Height, 0.2f), wall);
            SetupUtil.Block("WallS_Lintel", g, new Vector3((doorW + doorE) / 2f, (doorTop + Height) / 2f, -4.1f), new Vector3(doorE - doorW, Height - doorTop, 0.2f), wall);
            SetupUtil.Block("WallE", g, new Vector3(5.1f, Height / 2f, 0f), new Vector3(0.2f, Height, 8.4f), wall);
            SetupUtil.Block("WallW", g, new Vector3(-5.1f, Height / 2f, 0f), new Vector3(0.2f, Height, 8.4f), wall);

            // Cream panelling on the lower part of the walls, with a rail on top.
            var panel = M("Wainscot", Palette.Wainscot);
            Box("PanelN", g, new Vector3(0f, 0.5f, 3.985f), new Vector3(10f, 1f, 0.03f), panel);
            Box("PanelS_W", g, new Vector3((-5f + doorW) / 2f, 0.5f, -3.985f), new Vector3(doorW + 5f, 1f, 0.03f), panel);
            Box("PanelS_E", g, new Vector3((doorE + 5f) / 2f, 0.5f, -3.985f), new Vector3(5f - doorE, 1f, 0.03f), panel);
            Box("PanelE", g, new Vector3(4.985f, 0.5f, 0f), new Vector3(0.03f, 1f, 8f), panel);
            Box("PanelW", g, new Vector3(-4.985f, 0.5f, 0f), new Vector3(0.03f, 1f, 8f), panel);
            Box("RailN", g, new Vector3(0f, 1.02f, 3.97f), new Vector3(10f, 0.05f, 0.06f), Stone);
            Box("RailS_W", g, new Vector3((-5f + doorW) / 2f, 1.02f, -3.97f), new Vector3(doorW + 5f, 0.05f, 0.06f), Stone);
            Box("RailS_E", g, new Vector3((doorE + 5f) / 2f, 1.02f, -3.97f), new Vector3(5f - doorE, 0.05f, 0.06f), Stone);
            Box("RailE", g, new Vector3(4.97f, 1.02f, 0f), new Vector3(0.06f, 0.05f, 8f), Stone);
            Box("RailW", g, new Vector3(-4.97f, 1.02f, 0f), new Vector3(0.06f, 0.05f, 8f), Stone);

            // Skirting boards all round, plus a water stain for that lived-in feel.
            Box("SkirtN", g, new Vector3(0f, 0.06f, 3.98f), new Vector3(10f, 0.12f, 0.04f), Stone);
            Box("SkirtS_W", g, new Vector3((-5f + doorW) / 2f, 0.06f, -3.98f), new Vector3(doorW + 5f, 0.12f, 0.04f), Stone);
            Box("SkirtS_E", g, new Vector3((doorE + 5f) / 2f, 0.06f, -3.98f), new Vector3(5f - doorE, 0.12f, 0.04f), Stone);
            Box("SkirtE", g, new Vector3(4.98f, 0.06f, 0f), new Vector3(0.04f, 0.12f, 8f), Stone);
            Box("SkirtW", g, new Vector3(-4.98f, 0.06f, 0f), new Vector3(0.04f, 0.12f, 8f), Stone);
            Box("Stain", g, new Vector3(-3.6f, 2.55f, 3.995f), new Vector3(0.9f, 0.45f, 0.004f), M("Stain", Palette.Stain));
            Box("StainDrip", g, new Vector3(-3.75f, 2.2f, 3.995f), new Vector3(0.18f, 0.4f, 0.004f), M("Stain", Palette.Stain));

            // Windows (onto the back yard, allegedly) and the front door, propped open onto the street.
            Window(g, new Vector3(-2.4f, 1.6f, 4f), 0f, 1.2f, 1.3f);
            Window(g, new Vector3(0.2f, 1.6f, 4f), 0f, 1.2f, 1.3f);
            Box("DoorFrameL", g, new Vector3(doorW - 0.05f, doorTop / 2f, -3.97f), new Vector3(0.1f, doorTop, 0.06f), Stone);
            Box("DoorFrameR", g, new Vector3(doorE + 0.05f, doorTop / 2f, -3.97f), new Vector3(0.1f, doorTop, 0.06f), Stone);
            Box("DoorFrameTop", g, new Vector3((doorW + doorE) / 2f, doorTop + 0.05f, -3.97f), new Vector3(doorE - doorW + 0.2f, 0.1f, 0.06f), Stone);
            var door = SetupUtil.Empty("FrontDoor", g, new Vector3(doorE - 0.03f, 0f, -3.95f)).transform;
            door.localRotation = Quaternion.Euler(0f, 100f, 0f); // swung open into the flat, hinged on the east side
            Box("Panel", door, new Vector3(-0.48f, 1.12f, 0.03f), new Vector3(0.96f, 2.22f, 0.05f), M("Door0", Palette.Doors[0]));
            Vis(LowPolyMeshes.Sphere, "Handle", door, new Vector3(-0.85f, 1f, 0.08f), Vector3.one * 0.07f, M("Brass", Palette.Brass, 0.6f));
            Label("4C", g, door.TransformPoint(new Vector3(-0.48f, 1.95f, 0.065f)), door.rotation * Quaternion.Euler(0f, 180f, 0f), 0.08f, Palette.Trim, Palette.Brass);

            Box("Rug", g, new Vector3(-0.6f, 0.006f, -0.6f), new Vector3(3.4f, 0.012f, 2.4f), M("HubRug", Palette.HubRug));
        }

        // ---------- The GigGrabber computer ----------

        static void BuildComputerCorner()
        {
            var g = Group("ComputerCorner");
            var wood = M("TableWood", Palette.Wood);
            var desk = new Vector3(3.3f, 0f, 3.5f);
            SetupUtil.Block("DeskTop", g, desk + new Vector3(0f, 0.76f, 0f), new Vector3(1.7f, 0.06f, 0.8f), wood);
            foreach (float x in new[] { -0.78f, 0.78f })
            foreach (float z in new[] { -0.33f, 0.33f })
                SetupUtil.Block("DeskLeg", g, desk + new Vector3(x, 0.365f, z), new Vector3(0.06f, 0.73f, 0.06f), wood);

            // Monitor facing the room (-z), showing the app.
            Box("MonitorStand", g, desk + new Vector3(0f, 0.94f, 0.2f), new Vector3(0.1f, 0.3f, 0.1f), Metal);
            Box("MonitorFoot", g, desk + new Vector3(0f, 0.8f, 0.2f), new Vector3(0.35f, 0.02f, 0.25f), Metal);
            Box("Bezel", g, desk + new Vector3(0f, 1.32f, 0.22f), new Vector3(1.02f, 0.62f, 0.06f), Metal);
            Box("Screen", g, desk + new Vector3(0f, 1.32f, 0.188f), new Vector3(0.94f, 0.54f, 0.01f),
                SetupUtil.Emissive("Screen", Palette.Screen, 1.1f));
            Box("ScreenBar", g, desk + new Vector3(0f, 1.545f, 0.186f), new Vector3(0.94f, 0.09f, 0.008f),
                SetupUtil.Emissive("ScreenBar", Palette.GigOrange, 0.55f));
            Label("GigGrabber", g, desk + new Vector3(-0.3f, 1.545f, 0.18f), Quaternion.identity, 0.045f, Palette.PartyWhite);
            Label("1 new gig near you!", g, desk + new Vector3(0f, 1.36f, 0.18f), Quaternion.identity, 0.05f, Palette.Trim);
            Label("Wedding Cake Delivery  -  $120", g, desk + new Vector3(0f, 1.22f, 0.18f), Quaternion.identity, 0.032f, Palette.Trim);

            Box("Keyboard", g, desk + new Vector3(0f, 0.805f, -0.15f), new Vector3(0.6f, 0.03f, 0.2f), Metal);
            Box("Mouse", g, desk + new Vector3(0.45f, 0.8f, -0.15f), new Vector3(0.07f, 0.03f, 0.11f), Metal);
            Vis(LowPolyMeshes.Cylinder, "Mug", g, desk + new Vector3(-0.6f, 0.84f, -0.1f), new Vector3(0.1f, 0.05f, 0.1f),
                M("GigOrange", Palette.GigOrange));

            // Desk lamp
            Vis(LowPolyMeshes.Cylinder, "LampBase", g, desk + new Vector3(0.7f, 0.8f, 0.2f), new Vector3(0.16f, 0.01f, 0.16f), Metal);
            Box("LampArm", g, desk + new Vector3(0.7f, 1f, 0.2f), new Vector3(0.03f, 0.4f, 0.03f), Metal);
            Vis(LowPolyMeshes.Sphere, "LampBulb", g, desk + new Vector3(0.7f, 1.2f, 0.12f), Vector3.one * 0.12f, WarmLight);

            // Office chair
            var chair = desk + new Vector3(0f, 0f, -0.75f);
            Solid(PrimitiveType.Cylinder, "ChairSeat", g, chair + new Vector3(0f, 0.48f, 0f), new Vector3(0.5f, 0.04f, 0.5f), Metal);
            Box("ChairPole", g, chair + new Vector3(0f, 0.24f, 0f), new Vector3(0.06f, 0.44f, 0.06f), Metal);
            Box("ChairBack", g, chair + new Vector3(0f, 0.85f, -0.24f), new Vector3(0.48f, 0.55f, 0.06f), M("Blanket", Palette.Blanket));

            // What the E key talks to: a trigger around the desk and monitor.
            var computer = new GameObject("GigComputer");
            computer.transform.SetParent(g, false);
            computer.transform.position = desk + new Vector3(0f, 1.05f, 0.05f);
            var trigger = computer.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1.7f, 0.75f, 0.9f);
            computer.AddComponent<GigComputer>();
        }

        // ---------- Couch and coffee table ----------

        static void BuildLounge()
        {
            var g = Group("Lounge");
            var couch = M("Couch", Palette.Couch);
            var c = new Vector3(-2f, 0f, -3.45f);
            SetupUtil.Block("CouchSeat", g, c + new Vector3(0f, 0.22f, 0f), new Vector3(2.6f, 0.44f, 0.9f), couch);
            SetupUtil.Block("CouchBack", g, c + new Vector3(0f, 0.5f, -0.35f), new Vector3(2.6f, 1f, 0.22f), couch);
            foreach (float x in new[] { -1.3f, 1.3f })
                SetupUtil.Block("CouchArm", g, c + new Vector3(x, 0.32f, 0f), new Vector3(0.24f, 0.64f, 0.9f), couch);
            Box("Cushion", g, c + new Vector3(-0.6f, 0.5f, 0.05f), new Vector3(1.1f, 0.12f, 0.65f), M("Cushion", Palette.Cushion));
            Box("CushionPatched", g, c + new Vector3(0.6f, 0.5f, 0.05f), new Vector3(1.1f, 0.12f, 0.65f), M("Cushion", Palette.Cushion));
            Box("Patch", g, c + new Vector3(0.75f, 0.565f, 0.1f), new Vector3(0.35f, 0.01f, 0.3f), M("CouchPatch", Palette.CouchPatch));

            var wood = M("TableWood", Palette.Wood);
            var t = new Vector3(-2f, 0f, -2.2f);
            SetupUtil.Block("CoffeeTable", g, t + new Vector3(0f, 0.4f, 0f), new Vector3(1.2f, 0.06f, 0.6f), wood);
            foreach (float x in new[] { -0.54f, 0.54f })
            foreach (float z in new[] { -0.24f, 0.24f })
                SetupUtil.Block("TableLeg", g, t + new Vector3(x, 0.19f, z), new Vector3(0.05f, 0.38f, 0.05f), wood);
            Box("PizzaBox", g, t + new Vector3(-0.2f, 0.455f, 0f), new Vector3(0.5f, 0.05f, 0.5f), M("Cardboard", Palette.Cardboard));
            Label("PIZZA", g, t + new Vector3(-0.2f, 0.482f, 0f), Quaternion.Euler(90f, 0f, 0f), 0.06f, Palette.Hydrant);
            var can = M("Hydrant", Palette.Hydrant);
            Vis(LowPolyMeshes.Cylinder, "Can", g, t + new Vector3(0.3f, 0.49f, 0.1f), new Vector3(0.07f, 0.06f, 0.07f), can);
            Vis(LowPolyMeshes.Cylinder, "CanTipped", g, t + new Vector3(0.42f, 0.465f, -0.12f), new Vector3(0.07f, 0.06f, 0.07f), can,
                Quaternion.Euler(0f, 30f, 90f));
        }

        // ---------- Kitchenette ----------

        static void BuildKitchenette()
        {
            var g = Group("Kitchenette");
            var white = M("PartyWhite", Palette.PartyWhite);
            SetupUtil.Block("Counter", g, new Vector3(4.66f, 0.45f, -1.8f), new Vector3(0.64f, 0.9f, 2.4f), M("TableWood", Palette.Wood));
            Box("CounterTop", g, new Vector3(4.63f, 0.92f, -1.8f), new Vector3(0.72f, 0.04f, 2.46f), Stone);
            Box("Sink", g, new Vector3(4.62f, 0.945f, -1.5f), new Vector3(0.48f, 0.012f, 0.4f), Metal);
            Box("Tap", g, new Vector3(4.86f, 1.05f, -1.5f), new Vector3(0.04f, 0.22f, 0.04f), Metal);
            Box("Microwave", g, new Vector3(4.66f, 1.09f, -2.6f), new Vector3(0.45f, 0.3f, 0.36f), Metal);
            Box("Cupboard", g, new Vector3(4.8f, 2.1f, -1.8f), new Vector3(0.38f, 0.6f, 2.2f), M("TableWood", Palette.Wood));

            SetupUtil.Block("Fridge", g, new Vector3(4.6f, 0.95f, 0.25f), new Vector3(0.75f, 1.9f, 0.75f), white);
            Box("FridgeHandle", g, new Vector3(4.2f, 1.2f, 0.5f), new Vector3(0.03f, 0.5f, 0.04f), Metal);
            Box("FridgeLine", g, new Vector3(4.222f, 1.3f, 0.25f), new Vector3(0.004f, 0.01f, 0.74f), Metal);
            Box("StickyNote", g, new Vector3(4.22f, 1.55f, 0.15f), new Vector3(0.006f, 0.22f, 0.22f), M("StickyNote", Palette.StickyNote));
            Label("RENT DUE\nFRIDAY!!", g, new Vector3(4.215f, 1.55f, 0.15f), Quaternion.Euler(0f, 90f, 0f), 0.028f, Palette.Trim);
        }

        // ---------- Mattress corner ----------

        static void BuildBedCorner()
        {
            var g = Group("BedCorner");
            var b = new Vector3(-4.15f, 0f, 1.6f);
            SetupUtil.Block("Mattress", g, b + new Vector3(0f, 0.11f, 0f), new Vector3(1.4f, 0.22f, 2f), M("PartyWhite", Palette.PartyWhite));
            Box("Blanket", g, b + new Vector3(0f, 0.245f, 0.3f), new Vector3(1.42f, 0.05f, 1.35f), M("Blanket", Palette.Blanket));
            Box("Pillow", g, b + new Vector3(0f, 0.28f, -0.72f), new Vector3(0.62f, 0.12f, 0.36f), M("PartyWhite", Palette.PartyWhite));

            // A plant that has given up.
            var pot = new Vector3(-4.5f, 0f, -3.5f);
            Solid(PrimitiveType.Cylinder, "Pot", g, pot + new Vector3(0f, 0.2f, 0f), new Vector3(0.4f, 0.2f, 0.4f), M("Terracotta", Palette.Terracotta));
            var dead = M("DeadLeaf", Palette.DeadLeaf);
            Vis(LowPolyMeshes.Sphere, "Droop", g, pot + new Vector3(0.1f, 0.5f, 0f), new Vector3(0.5f, 0.35f, 0.45f), dead);
            Box("Stalk", g, pot + new Vector3(-0.05f, 0.65f, 0f), new Vector3(0.03f, 0.5f, 0.03f), dead, Quaternion.Euler(0f, 0f, 25f));
        }

        // ---------- Clutter ----------

        static void BuildClutter()
        {
            var g = Group("Clutter");
            // Laundry pile next to the mattress.
            var colours = new[] { Palette.Blanket, Palette.PartyPink, Palette.PartyWhite, Palette.Leaf };
            var spots = new[] { new Vector3(-3.1f, 0.12f, 3.2f), new Vector3(-2.75f, 0.1f, 3.4f), new Vector3(-3f, 0.24f, 3.35f), new Vector3(-3.35f, 0.09f, 3.45f) };
            for (int i = 0; i < spots.Length; i++)
                Vis(LowPolyMeshes.Sphere, "Laundry", g, spots[i], new Vector3(0.55f, 0.22f, 0.45f),
                    M("Laundry" + i, colours[i]), Quaternion.Euler(0f, i * 47f, 0f));

            // Stack of pizza boxes at the end of the couch.
            var cardboard = M("Cardboard", Palette.Cardboard);
            for (int i = 0; i < 4; i++)
                Box("PizzaStack", g, new Vector3(-0.35f, 0.03f + i * 0.055f, -3.6f), new Vector3(0.5f, 0.05f, 0.5f), cardboard,
                    Quaternion.Euler(0f, i * 9f - 12f, 0f));

            // Shoes by the door.
            var shoes = M("Shoes", new Color(0.18f, 0.14f, 0.13f));
            Box("Shoe", g, new Vector3(1.9f, 0.045f, -3.6f), new Vector3(0.12f, 0.09f, 0.28f), shoes, Quaternion.Euler(0f, 12f, 0f));
            Box("Shoe", g, new Vector3(2.1f, 0.045f, -3.55f), new Vector3(0.12f, 0.09f, 0.28f), shoes, Quaternion.Euler(0f, -20f, 0f));

            // Floor lamp by the couch.
            Solid(PrimitiveType.Cylinder, "FloorLampPole", g, new Vector3(-3.75f, 0.75f, -3.5f), new Vector3(0.05f, 0.75f, 0.05f), Metal);
            Vis(LowPolyMeshes.Cylinder, "FloorLampShade", g, new Vector3(-3.75f, 1.55f, -3.5f), new Vector3(0.45f, 0.16f, 0.45f),
                M("PartyWhite", Palette.PartyWhite));
            Vis(LowPolyMeshes.Sphere, "FloorLampBulb", g, new Vector3(-3.75f, 1.45f, -3.5f), Vector3.one * 0.14f, WarmLight);
            Lamp(g, new Vector3(-3.75f, 1.3f, -3.2f), 3.5f, 0.9f);

            // Wall clock (always late).
            Vis(LowPolyMeshes.Cylinder, "Clock", g, new Vector3(1.6f, 2.2f, 3.98f), new Vector3(0.42f, 0.02f, 0.42f),
                M("PartyWhite", Palette.PartyWhite), Quaternion.Euler(90f, 0f, 0f));
            Box("ClockHandH", g, new Vector3(1.6f, 2.24f, 3.955f), new Vector3(0.02f, 0.1f, 0.005f), Metal);
            Box("ClockHandM", g, new Vector3(1.66f, 2.2f, 3.955f), new Vector3(0.13f, 0.015f, 0.005f), Metal);
        }

        // ---------- Posters ----------

        static void BuildWallDecor()
        {
            var g = Group("WallDecor");
            // GigGrabber "motivational" poster on the west wall (inside face x = -5).
            var face = Quaternion.Euler(0f, -90f, 0f); // readable from the room
            Box("HustlePoster", g, new Vector3(-4.99f, 1.7f, -1.4f), new Vector3(0.01f, 1.05f, 0.75f), M("GigOrange", Palette.GigOrange));
            Label("HUSTLE.", g, new Vector3(-4.98f, 1.85f, -1.4f), face, 0.13f, Palette.PartyWhite);
            Label("GigGrabber\nbe your own boss*", g, new Vector3(-4.98f, 1.45f, -1.4f), face, 0.04f, Palette.PartyWhite);

            // A wonky framed photo above the mattress.
            Box("PhotoFrame", g, new Vector3(-4.985f, 1.6f, 1.6f), new Vector3(0.02f, 0.5f, 0.65f), Metal, Quaternion.Euler(4f, 0f, 0f));
            Box("Photo", g, new Vector3(-4.97f, 1.6f, 1.6f), new Vector3(0.005f, 0.4f, 0.55f), M("Leaf", Palette.Leaf), Quaternion.Euler(4f, 0f, 0f));
        }

        // ---------- Lights ----------

        static void BuildLights()
        {
            var g = Group("Lights");
            Vis(LowPolyMeshes.Cylinder, "CeilingCanopy", g, new Vector3(0f, 2.88f, 0f), new Vector3(0.18f, 0.02f, 0.18f), Metal);
            Vis(LowPolyMeshes.Sphere, "CeilingLamp", g, new Vector3(0f, 2.78f, 0f), new Vector3(0.6f, 0.22f, 0.6f), WarmLight);
            Lamp(g, new Vector3(0f, 2.4f, 0f), 10f, 1.6f);
            Lamp(g, new Vector3(3.9f, 1.3f, 3.3f), 3.5f, 1.2f);   // desk lamp
            Lamp(g, new Vector3(3.9f, 2.3f, -1.8f), 4.5f, 1f);    // kitchen
        }

        // ---------- Spawns and props ----------

        static void PlaceGameplay()
        {
            var g = Group("Gameplay");
            var players = new GameObject("PlayerSpawner");
            players.transform.SetParent(g, false);
            players.transform.position = new Vector3(0f, 0.1f, -0.8f);
            players.transform.rotation = Quaternion.Euler(0f, 35f, 0f); // turned toward the computer
            for (int i = 0; i < SessionManager.MaxPlayers; i++)
                SetupUtil.Empty("Spawn" + (i + 1), players.transform, new Vector3(-1.5f + i * 1f, 0f, 0f));
            var spawner = players.AddComponent<PlayerSpawner>();
            var so = new SerializedObject(spawner);
            so.FindProperty("playerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<NetworkObject>(PrefabBuilder.PlayerPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Moving boxes nobody unpacked: something to throw while you wait.
            Spawner(g, PrefabBuilder.BoxPath, new Vector3(0.6f, 0.4f, 2.6f), 15f);
            Spawner(g, PrefabBuilder.BoxPath, new Vector3(1.2f, 0.4f, 3.1f), -10f);
            Spawner(g, PrefabBuilder.BoxPath, new Vector3(0.9f, 0.95f, 2.85f), 40f);
            Spawner(g, PrefabBuilder.BucketPath, new Vector3(2.4f, 0.4f, -2.6f), 0f);
        }
    }
}
