using UnityEditor;
using UnityEngine;

namespace Freelancer.EditorTools
{
    /// <summary>
    /// Set dressing for the Cake Delivery scene: facades, neighbouring buildings, street furniture,
    /// van details, stairwell and apartment decoration. Purely visual except a few small solid
    /// street objects placed away from walking routes; the gameplay geometry is untouched.
    /// </summary>
    static partial class GigSceneBuilder
    {
        static Material s_Frame, s_GlassPane, s_Stone, s_Metal, s_Light;

        static void Dress()
        {
            s_Frame = SetupUtil.Mat("WindowFrame", Palette.WindowFrame);
            s_GlassPane = SetupUtil.Mat("WindowGlass", Palette.WindowGlass, 0.35f);
            s_Stone = SetupUtil.Mat("Stone", Palette.Stone);
            s_Metal = SetupUtil.Mat("Metal", Palette.Metal, 0.4f);
            s_Light = SetupUtil.Emissive("WarmLight", Palette.WarmLight);

            DressFacade();
            DressNeighbours();
            DressStreet();
            DressVan();
            DressStairwell();
            DressApartment();
        }

        // ---------- Main building front ----------

        static void DressFacade()
        {
            var f = Group("Facade");
            var brick = SetupUtil.Mat("Brick", Palette.Brick);

            // Brick skin over the front wall (front face is z = 4.0), leaving the door open.
            Box("Brick_West", f, new Vector3(-5.9f, 4.5f, 3.98f), new Vector3(0.6f, 9f, 0.04f), brick);
            Box("Brick_OverDoor", f, new Vector3(-4.85f, 5.7f, 3.98f), new Vector3(1.5f, 6.6f, 0.04f), brick);
            Box("Brick_East", f, new Vector3(1.05f, 4.5f, 3.98f), new Vector3(10.3f, 9f, 0.04f), brick);

            // Stone plinth, floor bands and a chunky cornice.
            Box("Plinth_West", f, new Vector3(-5.9f, 0.22f, 3.94f), new Vector3(0.6f, 0.45f, 0.06f), s_Stone);
            Box("Plinth_East", f, new Vector3(1.05f, 0.22f, 3.94f), new Vector3(10.3f, 0.45f, 0.06f), s_Stone);
            foreach (float y in new[] { 3.5f, 6.5f })
                Box("Band", f, new Vector3(0f, y, 3.94f), new Vector3(12.4f, 0.12f, 0.07f), s_Stone);
            Box("Cornice", f, new Vector3(0f, 9.1f, 3.9f), new Vector3(12.8f, 0.35f, 0.5f), s_Stone);

            // Windows: three floors, plus two above the door.
            foreach (float y in new[] { 1.5f, 4.5f, 7.5f })
            foreach (float x in new[] { -2.7f, -0.2f, 2.3f, 4.8f })
                Window(f, new Vector3(x, y, 4f), 0f, 1f, 1.3f);
            foreach (float y in new[] { 4.5f, 7.5f })
                Window(f, new Vector3(-4.85f, y, 4f), 0f, 0.9f, 1.3f);

            // Front door: stone surround, striped awning, doormat, sign plate behind the label.
            Box("DoorJambL", f, new Vector3(-5.63f, 1.25f, 3.92f), new Vector3(0.14f, 2.5f, 0.1f), s_Stone);
            Box("DoorJambR", f, new Vector3(-4.07f, 1.25f, 3.92f), new Vector3(0.14f, 2.5f, 0.1f), s_Stone);
            Box("DoorHead", f, new Vector3(-4.85f, 2.48f, 3.92f), new Vector3(1.9f, 0.16f, 0.1f), s_Stone);

            var awning = SetupUtil.Empty("Awning", f, new Vector3(-4.85f, 2.78f, 3.5f)).transform;
            awning.localRotation = Quaternion.Euler(-16f, 0f, 0f);
            Box("Canvas", awning, Vector3.zero, new Vector3(1.9f, 0.05f, 1f), SetupUtil.Mat("Awning", Palette.Awning));
            for (int i = 0; i < 3; i++)
                Box("Stripe", awning, new Vector3(-0.6f + i * 0.6f, 0.005f, 0f), new Vector3(0.24f, 0.05f, 1.002f),
                    SetupUtil.Mat("AwningStripe", Palette.PartyWhite));

            Box("Doormat", f, new Vector3(-4.85f, 0.106f, 3.55f), new Vector3(1.3f, 0.012f, 0.6f),
                SetupUtil.Mat("Doormat", Palette.Doormat));
            Label("No. 42", f, new Vector3(-3.65f, 2.0f, 3.93f), Quaternion.identity, 0.09f, Palette.Trim, Palette.Stone);
        }

        // ---------- Buildings around it ----------

        static void DressNeighbours()
        {
            var n = Group("Neighbours");
            // Same side of the street (fronts face the street, -z).
            Building(n, "East1", 6.2f, 15.5f, 4.6f, 11.4f, 7f, 1, facesNorth: false);
            Building(n, "West1", -15f, -6.2f, 4.0f, 12f, 11.5f, 0, facesNorth: false);
            Building(n, "East2", 15.5f, 25f, 4.0f, 12f, 6f, 2, facesNorth: false);
            Building(n, "West2", -25f, -15f, 4.6f, 11.4f, 8.5f, 3, facesNorth: false);

            // Across the street (fronts face +z), behind their own sidewalk.
            SetupUtil.Block("FarSidewalk", n, new Vector3(0f, 0.05f, -20f), new Vector3(80f, 0.1f, 3f),
                SetupUtil.Mat("Sidewalk", Palette.Sidewalk));
            float[] edges = { -34f, -22f, -9f, 4f, 16f, 30f };
            float[] heights = { 8f, 6.5f, 10f, 7f, 9f };
            int[] colours = { 4, 2, 0, 5, 1 };
            for (int i = 0; i < heights.Length; i++)
                Building(n, "Across" + (i + 1), edges[i], edges[i + 1], -21.5f, 10f, heights[i], colours[i], facesNorth: true);
        }

        /// <summary>A solid neighbour block with windows, a door, plinth and cornice on its street side.</summary>
        static void Building(Transform parent, string name, float x0, float x1, float zFront, float depth, float height,
            int colour, bool facesNorth)
        {
            var b = SetupUtil.Empty(name, parent, Vector3.zero).transform;
            float outward = facesNorth ? 1f : -1f;
            float zMid = zFront - outward * depth / 2f;
            float w = x1 - x0, xMid = (x0 + x1) / 2f;
            float yaw = facesNorth ? 180f : 0f;

            SetupUtil.Block("Body", b, new Vector3(xMid, height / 2f, zMid), new Vector3(w, height, depth),
                SetupUtil.Mat("Neighbour" + colour, Palette.Neighbours[colour]));
            Box("Cornice", b, new Vector3(xMid, height + 0.12f, zMid), new Vector3(w + 0.1f, 0.25f, depth + 0.3f), s_Stone);
            Box("Plinth", b, new Vector3(xMid, 0.22f, zFront + outward * 0.03f), new Vector3(w, 0.45f, 0.06f), s_Stone);
            Box("Door", b, new Vector3(xMid, 1.05f, zFront + outward * 0.03f), new Vector3(1.1f, 2.1f, 0.06f), s_Trim);

            for (float y = 1.6f; y + 0.8f < height - 0.3f; y += 3f)
            for (float x = x0 + 1.4f; x < x1 - 0.9f; x += 2.4f)
            {
                if (y < 3f && Mathf.Abs(x - xMid) < 1.3f) continue; // door
                Window(b, new Vector3(x, y, zFront), yaw, 1f, 1.3f);
            }
        }

        /// <summary>Framed window with a cross mullion and a sill, sticking out of a wall along local -z.</summary>
        static void Window(Transform parent, Vector3 position, float yaw, float w, float h)
        {
            var p = SetupUtil.Empty("Window", parent, position).transform;
            p.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box("Frame", p, new Vector3(0, 0, -0.03f), new Vector3(w + 0.16f, h + 0.16f, 0.06f), s_Frame);
            Box("Glass", p, new Vector3(0, 0, -0.075f), new Vector3(w, h, 0.02f), s_GlassPane);
            Box("MullionV", p, new Vector3(0, 0, -0.095f), new Vector3(0.05f, h, 0.02f), s_Frame);
            Box("MullionH", p, new Vector3(0, 0.08f, -0.095f), new Vector3(w, 0.05f, 0.02f), s_Frame);
            Box("Sill", p, new Vector3(0, -h / 2f - 0.1f, -0.08f), new Vector3(w + 0.3f, 0.07f, 0.16f), s_Stone);
        }

        // ---------- Street ----------

        static void DressStreet()
        {
            var s = Group("StreetDressing");
            Box("Curb", s, new Vector3(0f, 0.065f, -1.6f), new Vector3(80f, 0.13f, 0.25f), SetupUtil.Mat("Curb", Palette.Curb));

            foreach (float x in new[] { -9f, 3.5f, 13f }) StreetLamp(s, new Vector3(x, 0.1f, -1.2f));
            foreach (var (x, z) in new[] { (-11f, 1.6f), (9.5f, 2f), (19f, 1.6f), (-19.5f, 2f) }) Tree(s, new Vector3(x, 0.1f, z));
            foreach (var (x, z) in new[] { (-12f, -20.5f), (1f, -20.5f), (14f, -20.5f) }) Tree(s, new Vector3(x, 0.1f, z));

            // Hydrant
            var hydrant = SetupUtil.Mat("Hydrant", Palette.Hydrant);
            Solid(PrimitiveType.Cylinder, "Hydrant", s, new Vector3(5.5f, 0.45f, -1.1f), new Vector3(0.28f, 0.35f, 0.28f), hydrant);
            Vis(LowPolyMeshes.Sphere, "HydrantCap", s, new Vector3(5.5f, 0.8f, -1.1f), Vector3.one * 0.3f, hydrant);
            Vis(LowPolyMeshes.Cylinder, "HydrantNozzle", s, new Vector3(5.5f, 0.55f, -1.1f), new Vector3(0.12f, 0.24f, 0.12f),
                hydrant, Quaternion.Euler(0f, 0f, 90f));

            // Mailbox
            var mail = SetupUtil.Mat("Mailbox", Palette.Mailbox);
            Solid(PrimitiveType.Cube, "Mailbox", s, new Vector3(-7.6f, 0.6f, 3.2f), new Vector3(0.5f, 1f, 0.45f), mail);
            Vis(LowPolyMeshes.Cylinder, "MailboxTop", s, new Vector3(-7.6f, 1.1f, 3.2f), new Vector3(0.5f, 0.225f, 0.45f),
                mail, Quaternion.Euler(0f, 0f, 90f));
            Box("MailSlot", s, new Vector3(-7.6f, 0.9f, 2.97f), new Vector3(0.3f, 0.05f, 0.01f), s_Metal);
        }

        static void StreetLamp(Transform parent, Vector3 foot)
        {
            Solid(PrimitiveType.Cylinder, "LampPost", parent, foot + new Vector3(0, 2f, 0), new Vector3(0.12f, 2f, 0.12f), s_Metal);
            Box("LampArm", parent, foot + new Vector3(0, 3.95f, -0.35f), new Vector3(0.08f, 0.08f, 0.8f), s_Metal);
            Box("LampHead", parent, foot + new Vector3(0, 3.92f, -0.75f), new Vector3(0.4f, 0.12f, 0.28f), s_Metal);
            Vis(LowPolyMeshes.Sphere, "LampBulb", parent, foot + new Vector3(0, 3.83f, -0.75f), new Vector3(0.24f, 0.1f, 0.18f), s_Light);
        }

        static void Tree(Transform parent, Vector3 foot)
        {
            var leaf = SetupUtil.Mat("Leaf", Palette.Leaf);
            var leafDark = SetupUtil.Mat("LeafDark", Palette.LeafDark);
            Solid(PrimitiveType.Cylinder, "Trunk", parent, foot + new Vector3(0, 1.1f, 0), new Vector3(0.25f, 1.1f, 0.25f),
                SetupUtil.Mat("Trunk", Palette.Trunk));
            Vis(LowPolyMeshes.Sphere, "Crown", parent, foot + new Vector3(0, 2.9f, 0), new Vector3(2.3f, 2f, 2.3f), leaf);
            Vis(LowPolyMeshes.Sphere, "CrownTop", parent, foot + new Vector3(0.35f, 3.75f, 0.2f), Vector3.one * 1.5f, leafDark);
            Vis(LowPolyMeshes.Sphere, "CrownSide", parent, foot + new Vector3(-0.6f, 3.3f, -0.4f), Vector3.one * 1.3f, leaf);
        }

        // ---------- Van ----------

        static void DressVan()
        {
            var v = Group("VanDressing");
            var stripe = SetupUtil.Mat("GigOrange", Palette.GigOrange);
            var bumper = SetupUtil.Mat("Bumper", Palette.Bumper);
            var van = SetupUtil.Mat("VanWhite", Palette.VanWhite);
            var glass = SetupUtil.Mat("Glass", Palette.Glass, 0.8f);

            // Hood and bumper give the cab a front (solid, so you can't walk into them).
            SetupUtil.Block("Hood", v, new Vector3(0f, 0.72f, -11.45f), new Vector3(2.1f, 0.85f, 0.5f), van);
            SetupUtil.Block("FrontBumper", v, new Vector3(0f, 0.38f, -11.76f), new Vector3(2.2f, 0.2f, 0.16f), bumper);
            Box("Grille", v, new Vector3(0f, 0.85f, -11.71f), new Vector3(0.9f, 0.3f, 0.03f), bumper);
            var headlight = SetupUtil.Emissive("Headlight", Palette.WarmLight, 2.5f);
            foreach (float x in new[] { -0.72f, 0.72f })
                Box("Headlight", v, new Vector3(x, 0.95f, -11.715f), new Vector3(0.36f, 0.16f, 0.03f), headlight);

            var tail = SetupUtil.Emissive("Taillight", Palette.Taillight, 1.6f);
            foreach (float side in new[] { -1f, 1f })
            {
                float x = 1.112f * side;
                Box("Stripe", v, new Vector3(x, 1.1f, -8.9f), new Vector3(0.02f, 0.34f, 4.6f), stripe);
                Box("SideWindow", v, new Vector3(x, 1.65f, -10.45f), new Vector3(0.02f, 0.55f, 0.9f), glass);
                Box("Taillight", v, new Vector3(1.03f * side, 1.0f, -6.48f), new Vector3(0.12f, 0.35f, 0.04f), tail);
                Label("GigGrabber", v, new Vector3(1.125f * side, 1.1f, -10.45f), Quaternion.Euler(0f, -90f * side, 0f),
                    0.1f, Palette.PartyWhite);
                foreach (float z in new[] { -10.4f, -7.0f })
                    Vis(LowPolyMeshes.Cylinder, "Hubcap", v, new Vector3(1.19f * side, 0.35f, z), new Vector3(0.36f, 0.02f, 0.36f),
                        s_Stone, Quaternion.Euler(0f, 0f, 90f));
            }
            Box("RearBumper", v, new Vector3(0f, 0.16f, -6.42f), new Vector3(2.2f, 0.15f, 0.14f), bumper);
        }

        // ---------- Stairwell ----------

        static void DressStairwell()
        {
            var s = Group("StairwellDressing");
            var wood = SetupUtil.Mat("TableWood", Palette.Wood);

            // Handrails along each flight.
            Rail(s, new Vector3(-5.93f, 0.9f, 6.5f), new Vector3(-5.93f, 2.9f, 11.5f), wood);
            Rail(s, new Vector3(-2.07f, 2.9f, 11.5f), new Vector3(-2.07f, 4.9f, 6.5f), wood);
            Rail(s, new Vector3(-5.93f, 4.9f, 6.5f), new Vector3(-5.93f, 6.9f, 11.5f), wood);

            // Cover the bare end of flight 2 (seen from the hallway) in wall colour.
            Box("Flight2EndPanel", s, new Vector3(-2.95f, 1.85f, 6.49f), new Vector3(1.9f, 3.7f, 0.02f), s_StairWall);

            // Wall lights on every level.
            Sconce(s, new Vector3(-6f, 1.9f, 5.2f), 90f);
            Sconce(s, new Vector3(-6f, 3.9f, 13.8f), 90f);
            Sconce(s, new Vector3(-3f, 5.9f, 4.2f), 0f);
            Sconce(s, new Vector3(-6f, 7.9f, 13.8f), 90f);

            // The roof blocks the sun, so each level gets a real (soft, warm) light.
            Lamp(s, new Vector3(-5.4f, 2.2f, 5.4f), 6f, 1.6f);
            Lamp(s, new Vector3(-4.6f, 4.2f, 13.8f), 6f, 1.6f);
            Lamp(s, new Vector3(-3f, 6.2f, 5f), 6f, 1.6f);
            Lamp(s, new Vector3(-4.6f, 8.2f, 13.8f), 6.5f, 1.8f);

            // Floor signs, getting more apologetic.
            Label("FLOOR 1  (keep going)", s, new Vector3(-5.96f, 3.3f, 12.6f), Quaternion.Euler(0f, -90f, 0f), 0.09f, Palette.Trim, Palette.PartyWhite);
            Label("FLOOR 2  (sorry)", s, new Vector3(-4.6f, 5.3f, 4.24f), Quaternion.Euler(0f, 180f, 0f), 0.09f, Palette.Trim, Palette.PartyWhite);
            Label("FLOOR 3  (Brenda)", s, new Vector3(-5.96f, 7.3f, 12.6f), Quaternion.Euler(0f, -90f, 0f), 0.09f, Palette.Trim, Palette.PartyWhite);

            Box("WelcomeMat", s, new Vector3(-2.6f, 6.007f, 13.8f), new Vector3(0.6f, 0.012f, 1f),
                SetupUtil.Mat("Doormat", Palette.Doormat));
        }

        static void Rail(Transform parent, Vector3 from, Vector3 to, Material mat)
        {
            Vector3 d = to - from;
            Vis(LowPolyMeshes.Cylinder, "Handrail", parent, (from + to) / 2f, new Vector3(0.07f, d.magnitude / 2f, 0.07f), mat,
                Quaternion.FromToRotation(Vector3.up, d));
        }

        /// <summary>Wall light; yaw turns local +z (out of the wall) toward the room.</summary>
        static void Sconce(Transform parent, Vector3 wallPoint, float yaw)
        {
            var p = SetupUtil.Empty("Sconce", parent, wallPoint).transform;
            p.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box("Plate", p, new Vector3(0, 0, 0.03f), new Vector3(0.22f, 0.3f, 0.05f), s_Metal);
            Vis(LowPolyMeshes.Sphere, "Glow", p, new Vector3(0, 0.02f, 0.13f), Vector3.one * 0.2f, s_Light);
        }

        // ---------- Brenda's apartment ----------

        static void DressApartment()
        {
            var a = Group("ApartmentDressing");
            const float floor = 6f;

            // Ceiling lights (the roof blocks the sun).
            foreach (var spot in new[] { new Vector3(1.2f, 8.98f, 8f), new Vector3(3.2f, 8.98f, 13f) })
            {
                Vis(LowPolyMeshes.Cylinder, "CeilingCanopy", a, spot, new Vector3(0.18f, 0.02f, 0.18f), s_Metal);
                Vis(LowPolyMeshes.Sphere, "CeilingLamp", a, spot + Vector3.down * 0.1f, new Vector3(0.55f, 0.22f, 0.55f), s_Light);
                Lamp(a, spot + Vector3.down * 0.4f, 9f, 2.2f);
            }

            // Rug under the table.
            Box("RugBorder", a, new Vector3(3.4f, floor + 0.004f, 12f), new Vector3(3.8f, 0.008f, 2.8f),
                SetupUtil.Mat("PartyWhite", Palette.PartyWhite));
            Box("Rug", a, new Vector3(3.4f, floor + 0.007f, 12f), new Vector3(3.5f, 0.01f, 2.5f), SetupUtil.Mat("Rug", Palette.Rug));

            // Windows on the inside of the front wall (matching the facade).
            foreach (float x in new[] { -0.2f, 2.3f, 4.8f })
                Window(a, new Vector3(x, 7.5f, 4.2f), 180f, 1f, 1.3f);

            // Couch cushions.
            var cushion = SetupUtil.Mat("Cushion", Palette.Cushion);
            foreach (float x in new[] { 4f, 5.2f })
                Box("Cushion", a, new Vector3(x, 6.87f, 6.3f), new Vector3(1.05f, 0.15f, 0.6f), cushion);

            // Pictures on the west wall (inside face x = -1.8).
            WallArt(a, new Vector3(-1.8f, 7.5f, 7f), SetupUtil.Mat("PartyPink", Palette.PartyPink));
            WallArt(a, new Vector3(-1.8f, 7.6f, 9.6f), SetupUtil.Mat("Leaf", Palette.Leaf));

            // Floor lamp and a plant (solid, out of the way in the corners).
            Solid(PrimitiveType.Cylinder, "LampPole", a, new Vector3(5.5f, floor + 0.8f, 15.4f), new Vector3(0.06f, 0.8f, 0.06f), s_Metal);
            Vis(LowPolyMeshes.Cylinder, "LampBase", a, new Vector3(5.5f, floor + 0.03f, 15.4f), new Vector3(0.4f, 0.03f, 0.4f), s_Metal);
            Vis(LowPolyMeshes.Cylinder, "LampShade", a, new Vector3(5.5f, floor + 1.62f, 15.4f), new Vector3(0.5f, 0.17f, 0.5f),
                SetupUtil.Mat("PartyWhite", Palette.PartyWhite));
            Vis(LowPolyMeshes.Sphere, "LampBulb", a, new Vector3(5.5f, floor + 1.5f, 15.4f), Vector3.one * 0.16f, s_Light);

            Solid(PrimitiveType.Cylinder, "PlantPot", a, new Vector3(-1.3f, floor + 0.22f, 15.4f), new Vector3(0.45f, 0.22f, 0.45f),
                SetupUtil.Mat("Terracotta", Palette.Terracotta));
            Vis(LowPolyMeshes.Sphere, "PlantLeaves", a, new Vector3(-1.3f, floor + 0.85f, 15.4f), new Vector3(0.7f, 0.9f, 0.7f),
                SetupUtil.Mat("Leaf", Palette.Leaf));
            Vis(LowPolyMeshes.Sphere, "PlantTop", a, new Vector3(-1.2f, floor + 1.25f, 15.35f), Vector3.one * 0.45f,
                SetupUtil.Mat("LeafDark", Palette.LeafDark));

            // The wedding: bunting, balloons, banner.
            var party = new[]
            {
                SetupUtil.Mat("PartyPink", Palette.PartyPink), SetupUtil.Mat("PartyWhite", Palette.PartyWhite),
                SetupUtil.Mat("PartyGold", Palette.PartyGold, 0.5f),
            };
            foreach (float z in new[] { 9.3f, 13.6f })
            {
                Box("BuntingString", a, new Vector3(2.1f, 8.55f, z), new Vector3(7.8f, 0.015f, 0.015f), s_Frame);
                int i = 0;
                for (float x = -1.5f; x < 5.8f; x += 0.45f, i++)
                    Box("Pennant", a, new Vector3(x, 8.45f, z), new Vector3(0.2f, 0.2f, 0.012f), party[i % party.Length],
                        Quaternion.Euler(0f, 0f, 45f));
            }

            var balloonSpots = new[] { new Vector3(5.2f, 7.9f, 13.5f), new Vector3(5.55f, 8.15f, 13.1f), new Vector3(5.0f, 8.25f, 13.0f) };
            for (int i = 0; i < balloonSpots.Length; i++)
            {
                Vector3 b = balloonSpots[i];
                Vis(LowPolyMeshes.Sphere, "Balloon", a, b, new Vector3(0.34f, 0.42f, 0.34f), party[i % party.Length]);
                float len = b.y - 0.21f - floor;
                Box("BalloonString", a, new Vector3(b.x, floor + len / 2f, b.z), new Vector3(0.01f, len, 0.01f), s_Frame);
            }
            Box("BalloonWeight", a, new Vector3(5.25f, floor + 0.05f, 13.2f), new Vector3(0.12f, 0.1f, 0.12f), party[2]);

            Label("CONGRATS KAYLEIGH & TODD!", a, new Vector3(3.2f, 8.2f, 15.96f), Quaternion.identity, 0.15f, Palette.PartyWhite,
                Palette.PartyPink);
        }

        /// <summary>A warm point light (no shadows, cheap).</summary>
        static void Lamp(Transform parent, Vector3 position, float range, float intensity)
        {
            var go = SetupUtil.Empty("Light", parent, position);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Palette.WarmLight;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        static void WallArt(Transform parent, Vector3 wallPoint, Material canvas)
        {
            var p = SetupUtil.Empty("WallArt", parent, wallPoint).transform;
            p.localRotation = Quaternion.Euler(0f, -90f, 0f); // local -z points into the room (+x)
            Box("Frame", p, new Vector3(0, 0, -0.02f), new Vector3(1f, 0.75f, 0.04f), s_Metal);
            Box("Canvas", p, new Vector3(0, 0, -0.042f), new Vector3(0.84f, 0.6f, 0.01f), canvas);
        }

        // ---------- Helpers ----------

        /// <summary>Visual-only box (no collider).</summary>
        static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat, Quaternion? rot = null)
        {
            var go = SetupUtil.Visual(PrimitiveType.Cube, name, parent, localPos, size, mat);
            if (rot.HasValue) go.transform.localRotation = rot.Value;
            go.isStatic = true;
            return go;
        }

        /// <summary>Visual-only low-poly mesh.</summary>
        static GameObject Vis(Mesh mesh, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat,
            Quaternion? rot = null)
        {
            var go = LowPolyMeshes.Visual(mesh, name, parent, localPos, scale, mat, rot);
            go.isStatic = true;
            return go;
        }

        /// <summary>A primitive you can bump into (keeps its collider).</summary>
        static GameObject Solid(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }
    }
}
