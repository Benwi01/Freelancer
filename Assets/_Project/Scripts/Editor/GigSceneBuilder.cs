using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Freelancer.EditorTools
{
    /// <summary>
    /// Builds the Cake Delivery gig scene from primitives:
    /// street + van (south), a building whose front door opens onto a 3-flight switchback stairwell,
    /// and a top-floor apartment with the "place cake here" table.
    ///
    /// Layout (top view, +z = north, into the building):
    ///   x -6..-4.1  lane A: flight 1 (y 0→2) and flight 3 (y 4→6), both going north
    ///   x -3.9..-2  lane B: flight 2 (y 2→4) going south
    ///   z 11.5..16  landing 1 (y 2) and top landing (y 6); z 4.2..6.5 landing 2 (y 4)
    ///   x -1.8..6   apartment at y 6, door in its west wall at z 13..14.6
    /// </summary>
    static partial class GigSceneBuilder
    {
        const float StepRise = 0.2f;
        const float StepRun = 0.5f;

        static Transform s_Level;
        static Material s_Wall, s_Floor, s_Stairs, s_Trim, s_StairWall;

        [MenuItem("Freelancer/Setup/Build Cake Delivery Scene", priority = 22)]
        public static void BuildCakeDeliveryScene()
        {
            SetupUtil.EnsureFolders();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(14, 14, -16);
                cam.transform.LookAt(new Vector3(0, 3, 4));
            }
            LookSetup.ApplyToOpenScene();

            s_Level = new GameObject("Level").transform;
            s_Wall = SetupUtil.Mat("Wall", Palette.Wall);
            s_Floor = SetupUtil.Mat("Floor", Palette.Floor);
            s_Stairs = SetupUtil.Mat("Stairs", Palette.Stairs);
            s_Trim = SetupUtil.Mat("Trim", Palette.Trim);
            s_StairWall = SetupUtil.Mat("StairwellWall", Palette.StairwellWall);

            BuildStreet();
            BuildVan();
            BuildBuildingShell();
            BuildStairwell();
            BuildApartment();
            Dress(); // decoration only: see GigSceneBuilder.Dressing.cs
            PlaceGameplay();

            new GameObject("SessionHud").AddComponent<SessionHud>();
            new GameObject("GigHud").AddComponent<GigHud>();

            EditorSceneManager.SaveScene(scene, SetupUtil.GigScenePath);
            Debug.Log("[Freelancer] Built " + SetupUtil.GigScenePath);
        }

        // ---------- Street & van ----------

        static void BuildStreet()
        {
            var street = Group("Street");
            SetupUtil.Block("Asphalt", street, new Vector3(0, -0.5f, 0), new Vector3(400, 1, 400),
                SetupUtil.Mat("Asphalt", Palette.Asphalt));
            SetupUtil.Block("Sidewalk", street, new Vector3(0, 0.05f, 1.2f), new Vector3(80, 0.1f, 5.6f),
                SetupUtil.Mat("Sidewalk", Palette.Sidewalk));
            var paint = SetupUtil.Mat("RoadPaint", Palette.RoadPaint);
            for (int i = -4; i <= 4; i++)
                Deco(PrimitiveType.Cube, "LaneMark", street, new Vector3(i * 4f + 3.5f, 0.005f, -13), new Vector3(2f, 0.01f, 0.2f), paint);
        }

        static void BuildVan()
        {
            var van = Group("Van");
            var white = SetupUtil.Mat("VanWhite", Palette.VanWhite);
            var glass = SetupUtil.Mat("Glass", Palette.Glass, 0.8f);
            var tyre = SetupUtil.Mat("Tyre", Palette.Tyre);

            // Cargo bay: open at the back (+z), floor top at y 0.6.
            SetupUtil.Block("CargoFloor", van, new Vector3(0, 0.3f, -8f), new Vector3(2.2f, 0.6f, 3f), white);
            SetupUtil.Block("WallLeft", van, new Vector3(-1.05f, 1.5f, -8f), new Vector3(0.1f, 1.8f, 3f), white);
            SetupUtil.Block("WallRight", van, new Vector3(1.05f, 1.5f, -8f), new Vector3(0.1f, 1.8f, 3f), white);
            SetupUtil.Block("Roof", van, new Vector3(0, 2.45f, -8f), new Vector3(2.2f, 0.1f, 3f), white);
            SetupUtil.Block("Bulkhead", van, new Vector3(0, 1.5f, -9.45f), new Vector3(2.2f, 1.8f, 0.1f), white);
            SetupUtil.Block("Cab", van, new Vector3(0, 1.2f, -10.35f), new Vector3(2.2f, 1.8f, 1.7f), white);
            Deco(PrimitiveType.Cube, "Windshield", van, new Vector3(0, 1.6f, -11.21f), new Vector3(1.9f, 0.7f, 0.02f), glass);
            foreach (var x in new[] { -1.12f, 1.12f })
            foreach (var z in new[] { -10.4f, -7.0f })
            {
                var wheel = Deco(PrimitiveType.Cylinder, "Wheel", van, new Vector3(x, 0.35f, z), new Vector3(0.7f, 0.12f, 0.7f), tyre);
                wheel.transform.localRotation = Quaternion.Euler(0, 0, 90);
            }

            // Ramp from the cargo floor down to the street.
            const float rampAngle = 15.3f;
            var ramp = SetupUtil.Block("Ramp", van, new Vector3(0, 0.25f, -5.4f), new Vector3(1.6f, 0.1f, 2.3f), s_Stairs);
            ramp.transform.localRotation = Quaternion.Euler(rampAngle, 0, 0);

            Label("FRESH CAKES (MOSTLY)", van, new Vector3(1.125f, 1.6f, -8f), Quaternion.Euler(0, -90, 0), 0.17f, s_Trim.color);
        }

        // ---------- Building ----------

        static void BuildBuildingShell()
        {
            var shell = Group("BuildingShell");
            const float top = 9f;

            // Front wall with the door (x -5.6..-4.1, 2.4 high).
            SetupUtil.Block("Front_WestJamb", shell, new Vector3(-5.8f, top / 2, 4.1f), new Vector3(0.4f, top, 0.2f), s_Wall);
            SetupUtil.Block("Front_East", shell, new Vector3(0.95f, top / 2, 4.1f), new Vector3(10.1f, top, 0.2f), s_Wall);
            SetupUtil.Block("Front_Lintel", shell, new Vector3(-4.85f, (top + 2.4f) / 2, 4.1f), new Vector3(1.5f, top - 2.4f, 0.2f), s_Wall);
            Label("DELIVERIES  >  TOP FLOOR", shell, new Vector3(-4.85f, 3.15f, 3.9f), Quaternion.identity, 0.13f, Palette.Wall, Palette.Trim);

            // Roof over the stairwell and the apartment (its underside is the ceiling).
            SetupUtil.Block("Roof", shell, new Vector3(0f, 9.1f, 10.1f), new Vector3(12.4f, 0.2f, 12.2f), s_Wall);
            Deco(PrimitiveType.Cube, "RoofTop", shell, new Vector3(0f, 9.205f, 10.1f), new Vector3(12.4f, 0.01f, 12.2f),
                SetupUtil.Mat("Roof", Palette.Roof));

            SetupUtil.Block("Back", shell, new Vector3(0, top / 2, 16.1f), new Vector3(12.4f, top, 0.2f), s_Wall);
            SetupUtil.Block("West", shell, new Vector3(-6.1f, top / 2, 10.1f), new Vector3(0.2f, top, 12.2f), s_StairWall);
            SetupUtil.Block("East", shell, new Vector3(6.1f, top / 2, 10.1f), new Vector3(0.2f, top, 12.2f), s_Wall);

            // Ground floor of the stairwell (hallway).
            SetupUtil.Block("HallFloor", shell, new Vector3(-4f, 0.02f, 10.1f), new Vector3(4f, 0.04f, 11.8f), s_Floor);

            // Wall between the stairwell and the apartment block (x -2..-1.8).
            SetupUtil.Block("Inner_Lower", shell, new Vector3(-1.9f, 3f, 10.1f), new Vector3(0.2f, 6f, 11.8f), s_StairWall);
            SetupUtil.Block("Inner_Upper_South", shell, new Vector3(-1.9f, 7.5f, 8.6f), new Vector3(0.2f, 3f, 8.8f), s_Wall);
            SetupUtil.Block("Inner_Upper_North", shell, new Vector3(-1.9f, 7.5f, 15.3f), new Vector3(0.2f, 3f, 1.4f), s_Wall);
            SetupUtil.Block("Inner_Lintel", shell, new Vector3(-1.9f, 8.7f, 13.8f), new Vector3(0.2f, 0.6f, 1.6f), s_Wall);
        }

        static void BuildStairwell()
        {
            var stairs = Group("Stairwell");

            // Flight 1: lane A, north, y 0 -> 2 (solid to the ground).
            Flight(stairs, "Flight1", laneX: -5.05f, startZ: 6.5f, dirZ: +1, startY: 0f, steps: 10, slab: 0f);
            // Landing 1 (y 2), full width: the first tight U-turn.
            SetupUtil.Block("Landing1", stairs, new Vector3(-4f, 1f, 13.75f), new Vector3(4f, 2f, 4.5f), s_Floor);
            // Flight 2: lane B, south, y 2 -> 4.
            Flight(stairs, "Flight2", laneX: -2.95f, startZ: 11.5f, dirZ: -1, startY: 2f, steps: 10, slab: 0f);
            // Landing 2 (y 4) above the hallway: second U-turn.
            SetupUtil.Block("Landing2", stairs, new Vector3(-4f, 3.85f, 5.35f), new Vector3(4f, 0.3f, 2.3f), s_Floor);
            // Flight 3: lane A again, north, y 4 -> 6, as thin slabs over flight 1.
            Flight(stairs, "Flight3", laneX: -5.05f, startZ: 6.5f, dirZ: +1, startY: 4f, steps: 10, slab: 0.35f);
            // Top landing (y 6).
            SetupUtil.Block("TopLanding", stairs, new Vector3(-4f, 5.85f, 13.75f), new Vector3(4f, 0.3f, 4.5f), s_Floor);

            // Centre wall between the lanes, up past the top floor so it doubles as a railing.
            SetupUtil.Block("CentreWall", stairs, new Vector3(-4f, 3.5f, 9f), new Vector3(0.2f, 7f, 5f), s_StairWall);
            // Railing so you can't step off the top landing into flight 2 (you can still drop a cake over it).
            SetupUtil.Block("TopRailing", stairs, new Vector3(-2.95f, 6.5f, 11.45f), new Vector3(1.9f, 1f, 0.1f), s_Trim);
        }

        /// <summary>A straight flight of 0.2 m x 0.5 m steps in a 1.9 m wide lane.</summary>
        static void Flight(Transform parent, string name, float laneX, float startZ, int dirZ, float startY, int steps, float slab)
        {
            var flight = new GameObject(name).transform;
            flight.SetParent(parent, false);
            for (int i = 1; i <= steps; i++)
            {
                float topY = startY + i * StepRise;
                float zMid = startZ + dirZ * (i - 0.5f) * StepRun;
                float height = slab > 0f ? slab : topY;
                SetupUtil.Block("Step" + i, flight, new Vector3(laneX, topY - height / 2f, zMid),
                    new Vector3(1.9f, height, StepRun), i % 2 == 0 ? s_Stairs : s_Floor);
            }
        }

        static void BuildApartment()
        {
            var apt = Group("Apartment");
            SetupUtil.Block("AptFloor", apt, new Vector3(2.1f, 5.85f, 10.1f), new Vector3(7.8f, 0.3f, 11.8f), s_Floor);
            // Fill under the apartment so nothing falls into the void.
            SetupUtil.Block("GroundFloorFill", apt, new Vector3(2.1f, 2.85f, 10.1f), new Vector3(7.8f, 5.7f, 11.8f),
                SetupUtil.Mat("Hidden", Palette.Hidden));

            // The table.
            var wood = SetupUtil.Mat("TableWood", Palette.Wood);
            var tablePos = new Vector3(3.5f, 6f, 12f);
            SetupUtil.Block("TableTop", apt, tablePos + new Vector3(0, 0.8f, 0), new Vector3(1.6f, 0.08f, 1.1f), wood);
            foreach (var x in new[] { -0.7f, 0.7f })
            foreach (var z in new[] { -0.45f, 0.45f })
                SetupUtil.Block("TableLeg", apt, tablePos + new Vector3(x, 0.38f, z), new Vector3(0.08f, 0.76f, 0.08f), wood);
            Deco(PrimitiveType.Cube, "TargetMat", apt, tablePos + new Vector3(0, 0.845f, 0), new Vector3(1.3f, 0.01f, 0.9f),
                SetupUtil.Mat("Target", Palette.Target));
            // Hanging sign over the table (strings up to the ceiling).
            Label("PLACE CAKE HERE", apt, tablePos + new Vector3(0, 2.05f, 0), Quaternion.Euler(0, 90, 0), 0.16f,
                Palette.PartyWhite, Palette.Target);
            foreach (float z in new[] { -0.5f, 0.5f })
                Deco(PrimitiveType.Cube, "SignString", apt, tablePos + new Vector3(0.02f, 2.62f, z), new Vector3(0.01f, 0.76f, 0.01f),
                    SetupUtil.Mat("WindowFrame", Palette.WindowFrame));

            // Furniture to bump into.
            var couch = SetupUtil.Mat("Couch", Palette.Couch);
            SetupUtil.Block("Couch", apt, new Vector3(4.6f, 6.4f, 6.2f), new Vector3(2.4f, 0.8f, 0.9f), couch);
            SetupUtil.Block("CouchBack", apt, new Vector3(4.6f, 6.8f, 5.75f), new Vector3(2.4f, 1.2f, 0.25f), couch);
            SetupUtil.Block("Bookshelf", apt, new Vector3(0.2f, 7f, 15.7f), new Vector3(1.6f, 2f, 0.4f), wood);

            Label("Brenda's Apartment", apt, new Vector3(-2.04f, 8.68f, 13.8f), Quaternion.Euler(0, 90, 0), 0.09f, Palette.PartyWhite, Palette.Trim);
        }

        // ---------- Gameplay objects ----------

        static void PlaceGameplay()
        {
            var gameplay = Group("Gameplay");

            Spawner(gameplay, PrefabBuilder.GigDirectorPath, Vector3.zero, 0f, "GigDirectorSpawner");
            Spawner(gameplay, PrefabBuilder.CakePath, new Vector3(0, 0.62f, -8.4f), 0f, "CakeSpawner");

            var zone = new GameObject("DeliveryZone");
            zone.transform.SetParent(gameplay, false);
            zone.transform.position = new Vector3(3.5f, 6.84f + 0.45f, 12f);
            zone.AddComponent<DeliveryZone>();

            var players = new GameObject("PlayerSpawner");
            players.transform.SetParent(gameplay, false);
            players.transform.position = new Vector3(0, 0.1f, -3.2f);
            players.transform.rotation = Quaternion.Euler(0, 180, 0); // facing the van
            for (int i = 0; i < SessionManager.MaxPlayers; i++)
                SetupUtil.Empty("Spawn" + (i + 1), players.transform, new Vector3(-1.8f + i * 1.2f, 0f, 0));
            var spawner = players.AddComponent<PlayerSpawner>();
            var so = new SerializedObject(spawner);
            so.FindProperty("playerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<NetworkObject>(PrefabBuilder.PlayerPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Clutter. Boxes and buckets are grabbable, throwable, and in the way.
            var props = new GameObject("Props").transform;
            props.SetParent(gameplay, false);
            Spawner(props, PrefabBuilder.BoxPath, new Vector3(-3.5f, 0.4f, 1.5f), 20f);
            Spawner(props, PrefabBuilder.BoxPath, new Vector3(-3.0f, 0.95f, 1.6f), 50f);
            Spawner(props, PrefabBuilder.BucketPath, new Vector3(2.5f, 0.4f, -1f), 0f);
            Spawner(props, PrefabBuilder.BoxPath, new Vector3(-3.0f, 0.4f, 5.4f), 0f);
            Spawner(props, PrefabBuilder.BucketPath, new Vector3(-4.6f, 2.4f, 14.5f), 0f);
            Spawner(props, PrefabBuilder.BucketPath, new Vector3(-3.0f, 2.4f, 15.0f), 0f);
            Spawner(props, PrefabBuilder.BoxPath, new Vector3(-5.2f, 4.35f, 5.2f), 10f);
            Spawner(props, PrefabBuilder.BoxPath, new Vector3(1.0f, 6.4f, 9f), 30f);
            Spawner(props, PrefabBuilder.BucketPath, new Vector3(2.0f, 6.4f, 14.8f), 0f);
        }

        // ---------- Helpers ----------

        static Transform Group(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(s_Level, false);
            return t;
        }

        static GameObject Deco(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat) =>
            SetupUtil.Visual(type, name, parent, pos, scale, mat);

        static void Spawner(Transform parent, string prefabPath, Vector3 position, float yaw, string name = null)
        {
            var go = new GameObject(name ?? "Spawn_" + System.IO.Path.GetFileNameWithoutExtension(prefabPath));
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var spawner = go.AddComponent<ServerSpawner>();
            var so = new SerializedObject(spawner);
            so.FindProperty("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<NetworkObject>(prefabPath);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Crisp world-space text (TextMesh Pro). Readable when looking along the rotation's +z;
        /// with a plate colour it sits on a sign board sized to fit the text.
        /// </summary>
        static GameObject Label(string text, Transform parent, Vector3 position, Quaternion rotation, float letterHeight,
            Color color, Color? plate = null)
        {
            var go = new GameObject("Label_" + text);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);

            var tmp = go.AddComponent<TMPro.TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = letterHeight * 13f;
            tmp.fontStyle = TMPro.FontStyles.Bold;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            tmp.color = color;
            tmp.rectTransform.sizeDelta = new Vector2(10f, 2f);

            if (plate.HasValue)
            {
                tmp.ForceMeshUpdate();
                Bounds b = tmp.textBounds;
                float pad = letterHeight * 0.55f;
                var board = SetupUtil.Visual(PrimitiveType.Cube, "Plate", go.transform,
                    new Vector3(b.center.x, b.center.y, 0.015f),
                    new Vector3(b.size.x + pad * 2f, b.size.y + pad * 2f, 0.02f),
                    SetupUtil.Mat("Sign_" + ColorUtility.ToHtmlStringRGB(plate.Value), plate.Value));
                board.isStatic = true;
            }
            return go;
        }
    }
}
