using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace Freelancer.EditorTools
{
    /// <summary>Builds the networked prefabs (player, props, cake) from primitives.</summary>
    static class PrefabBuilder
    {
        public const string PlayerPath = SetupUtil.PrefabsDir + "/Player.prefab";
        public const string BoxPath = SetupUtil.PrefabsDir + "/Prop_Box.prefab";
        public const string BucketPath = SetupUtil.PrefabsDir + "/Prop_PaintBucket.prefab";

        [MenuItem("Freelancer/Setup/Build Prefabs", priority = 21)]
        public static void BuildAllPrefabs()
        {
            SetupUtil.EnsureFolders();
            var list = SetupMenu.GetOrCreatePrefabList();

            Register(BuildPlayer());
            Register(BuildProp(BoxPath, "box", PrimitiveType.Cube, new Vector3(0.6f, 0.5f, 0.6f), 4f,
                SetupUtil.Mat("Cardboard", new Color(0.72f, 0.55f, 0.35f))));
            Register(BuildProp(BucketPath, "paint bucket", PrimitiveType.Cylinder, new Vector3(0.35f, 0.22f, 0.35f), 3f,
                SetupUtil.Mat("PaintBucket", new Color(0.3f, 0.75f, 0.85f))));

            var topDebris = BuildTierDebris(TopTierPath, "cake tier (top)", CakeTiers[2], 4f);
            var midDebris = BuildTierDebris(MidTierPath, "cake tier (middle)", CakeTiers[1], 8f);
            Register(topDebris);
            Register(midDebris);
            Register(BuildCake(topDebris, midDebris));
            Register(BuildGigDirector());

            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
            Debug.Log("[Freelancer] Prefabs built.");
        }

        // ---------- Cake ----------

        public const string CakePath = SetupUtil.PrefabsDir + "/WeddingCake.prefab";
        public const string TopTierPath = SetupUtil.PrefabsDir + "/CakeTier_Top.prefab";
        public const string MidTierPath = SetupUtil.PrefabsDir + "/CakeTier_Middle.prefab";
        public const string GigDirectorPath = SetupUtil.PrefabsDir + "/GigDirector.prefab";

        /// <summary>Radius and height of each tier, bottom first.</summary>
        static readonly (float radius, float height)[] CakeTiers = { (0.55f, 0.4f), (0.4f, 0.34f), (0.27f, 0.3f) };
        const float BoardHeight = 0.06f;

        static Material Frosting => SetupUtil.Mat("Frosting", new Color(0.98f, 0.96f, 0.9f), 0.5f);
        static Material Icing => SetupUtil.Mat("PinkIcing", new Color(0.97f, 0.6f, 0.72f), 0.5f);

        static GameObject BuildCake(GameObject topDebris, GameObject midDebris)
        {
            var root = new GameObject("WeddingCake");

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            board.transform.SetParent(root.transform, false);
            board.transform.localPosition = new Vector3(0, BoardHeight / 2f, 0);
            board.transform.localScale = new Vector3(1.3f, BoardHeight, 1.3f);
            board.GetComponent<Renderer>().sharedMaterial = SetupUtil.Mat("CakeBoard", new Color(0.8f, 0.82f, 0.86f), 0.8f);

            float y = BoardHeight;
            var tiers = new GameObject[CakeTiers.Length];
            var visuals = new Transform[CakeTiers.Length];
            for (int i = 0; i < CakeTiers.Length; i++)
            {
                var (r, h) = CakeTiers[i];
                var tier = new GameObject("Tier" + (i + 1));
                tier.transform.SetParent(root.transform, false);
                tier.transform.localPosition = new Vector3(0, y + h / 2f, 0);
                AddCylinderCollider(tier, r, h);
                visuals[i] = BuildTierVisual(tier.transform, r, h, topper: i == CakeTiers.Length - 1);
                tiers[i] = tier;
                y += h;
            }

            MakeNetworkedPhysics(root, 45f, "wedding cake");
            var rb = root.GetComponent<Rigidbody>();
            rb.angularDamping = 0.8f;

            // Heavy: one player can drag it, two can carry it.
            var grab = new SerializedObject(root.GetComponent<GrabbableObject>());
            grab.FindProperty("maxForcePerHolder").floatValue = 380f;
            grab.FindProperty("maxThrowImpulse").floatValue = 60f;
            grab.ApplyModifiedPropertiesWithoutUndo();

            var knock = new SerializedObject(root.GetComponent<ImpactKnockdown>());
            knock.FindProperty("minSpeed").floatValue = 4.5f;
            knock.ApplyModifiedPropertiesWithoutUndo();

            var cond = root.AddComponent<CakeCondition>();
            var so = new SerializedObject(cond);
            SetObjects(so.FindProperty("detachableTiers"), tiers[2], tiers[1]);
            SetObjects(so.FindProperty("tierDebrisPrefabs"),
                topDebris.GetComponent<NetworkObject>(), midDebris.GetComponent<NetworkObject>());
            so.ApplyModifiedPropertiesWithoutUndo();

            var wobble = root.AddComponent<CakeWobble>();
            so = new SerializedObject(wobble);
            SetObjects(so.FindProperty("tierVisuals"), visuals[0], visuals[1], visuals[2]);
            so.ApplyModifiedPropertiesWithoutUndo();

            return Save(root, CakePath);
        }

        static GameObject BuildTierDebris(string path, string displayName, (float radius, float height) size, float mass)
        {
            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
            AddCylinderCollider(root, size.radius, size.height);
            BuildTierVisual(root.transform, size.radius, size.height, topper: path == TopTierPath);
            MakeNetworkedPhysics(root, mass, displayName);
            return Save(root, path);
        }

        /// <summary>Frosted cylinder with a pink ring; optional bride-and-groom topper.</summary>
        static Transform BuildTierVisual(Transform parent, float r, float h, bool topper)
        {
            var visual = SetupUtil.Empty("Visual", parent, Vector3.zero).transform;
            SetupUtil.Visual(PrimitiveType.Cylinder, "Sponge", visual, Vector3.zero,
                new Vector3(r * 2f, h / 2f, r * 2f), Frosting);
            SetupUtil.Visual(PrimitiveType.Cylinder, "Ring", visual, new Vector3(0, -h / 2f + 0.04f, 0),
                new Vector3(r * 2f + 0.04f, 0.03f, r * 2f + 0.04f), Icing);
            SetupUtil.Visual(PrimitiveType.Cylinder, "TopRing", visual, new Vector3(0, h / 2f - 0.01f, 0),
                new Vector3(r * 2f + 0.02f, 0.015f, r * 2f + 0.02f), Icing);
            if (topper)
            {
                var suit = SetupUtil.Mat("TopperSuit", new Color(0.12f, 0.12f, 0.15f));
                SetupUtil.Visual(PrimitiveType.Capsule, "Groom", visual, new Vector3(-0.07f, h / 2f + 0.09f, 0),
                    new Vector3(0.07f, 0.09f, 0.07f), suit);
                SetupUtil.Visual(PrimitiveType.Capsule, "Bride", visual, new Vector3(0.07f, h / 2f + 0.09f, 0),
                    new Vector3(0.08f, 0.09f, 0.08f), Frosting);
            }
            return visual;
        }

        static void AddCylinderCollider(GameObject go, float r, float h)
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);

            // Cylinder primitive is 2 units tall and 1 wide; use a scaled child to fit.
            var col = new GameObject("Collider");
            col.transform.SetParent(go.transform, false);
            col.transform.localScale = new Vector3(r * 2f, h / 2f, r * 2f);
            var mc = col.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.convex = true;
        }

        static void SetObjects(SerializedProperty array, params Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // ---------- Gig ----------

        static GameObject BuildGigDirector()
        {
            var root = new GameObject("GigDirector");
            root.AddComponent<NetworkObject>();
            root.AddComponent<GigDirector>();
            return Save(root, GigDirectorPath);
        }

        static void Register(GameObject prefab) => SetupMenu.RegisterNetworkPrefab(prefab);

        // ---------- Player ----------

        static GameObject BuildPlayer()
        {
            var root = new GameObject("Player");
            root.AddComponent<NetworkObject>();

            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0, 0.9f, 0);
            cc.stepOffset = 0.45f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.04f;

            // Kinematic body so the server copy shoves physics props around properly.
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var nt = root.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;

            var shirt = SetupUtil.Mat("PlayerShirt", Color.white);
            var skin = SetupUtil.Mat("Skin", new Color(0.95f, 0.78f, 0.62f));
            var dark = SetupUtil.Mat("Visor", new Color(0.1f, 0.1f, 0.12f), 0.8f);

            // Body pivots at the feet so it tips over nicely.
            var body = SetupUtil.Empty("Body", root.transform, Vector3.zero).transform;
            var torso = SetupUtil.Visual(PrimitiveType.Capsule, "Torso", body, new Vector3(0, 0.75f, 0),
                new Vector3(0.7f, 0.75f, 0.7f), shirt);
            var head = SetupUtil.Empty("Head", body, new Vector3(0, 1.6f, 0)).transform;
            SetupUtil.Visual(PrimitiveType.Sphere, "Skull", head, Vector3.zero, Vector3.one * 0.42f, skin);
            SetupUtil.Visual(PrimitiveType.Cube, "Visor", head, new Vector3(0, 0.03f, 0.18f),
                new Vector3(0.32f, 0.1f, 0.1f), dark);
            // A cap so the joke reads from far away.
            var cap = SetupUtil.Visual(PrimitiveType.Cylinder, "Cap", head, new Vector3(0, 0.2f, 0.02f),
                new Vector3(0.44f, 0.05f, 0.44f), shirt);
            SetupUtil.Visual(PrimitiveType.Cube, "Brim", head, new Vector3(0, 0.18f, 0.24f),
                new Vector3(0.3f, 0.03f, 0.18f), shirt);

            var cameraPivot = SetupUtil.Empty("CameraPivot", root.transform, new Vector3(0, PlayerMotor.EyeHeight, 0)).transform;
            var camGo = new GameObject("PlayerCamera", typeof(Camera), typeof(AudioListener));
            camGo.transform.SetParent(cameraPivot, false);
            camGo.transform.localPosition = new Vector3(0, 0, 0.1f);
            var cam = camGo.GetComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 75f;
            cam.enabled = false;
            camGo.GetComponent<AudioListener>().enabled = false;

            var motor = root.AddComponent<PlayerMotor>();
            var knock = root.AddComponent<PlayerKnockdown>();
            root.AddComponent<PlayerGrabber>();

            var so = new SerializedObject(motor);
            so.FindProperty("cameraPivot").objectReferenceValue = cameraPivot;
            so.FindProperty("playerCamera").objectReferenceValue = cam;
            so.FindProperty("headVisual").objectReferenceValue = head;
            var tint = so.FindProperty("tintRenderers");
            var tinted = new[] { torso.GetComponent<Renderer>(), cap.GetComponent<Renderer>(),
                head.Find("Brim").GetComponent<Renderer>() };
            tint.arraySize = tinted.Length;
            for (int i = 0; i < tinted.Length; i++) tint.GetArrayElementAtIndex(i).objectReferenceValue = tinted[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            so = new SerializedObject(knock);
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("cameraPivot").objectReferenceValue = cameraPivot;
            so.ApplyModifiedPropertiesWithoutUndo();

            return Save(root, PlayerPath);
        }

        // ---------- Props ----------

        public static GameObject BuildProp(string path, string displayName, PrimitiveType shape, Vector3 scale,
            float mass, Material mat)
        {
            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
            var visual = GameObject.CreatePrimitive(shape);
            visual.name = "Mesh";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = scale;
            visual.GetComponent<Renderer>().sharedMaterial = mat;
            // Cylinders come with a capsule collider; swap for a mesh-ish box that matches.
            if (shape == PrimitiveType.Cylinder)
            {
                Object.DestroyImmediate(visual.GetComponent<Collider>());
                var box = root.AddComponent<BoxCollider>();
                box.size = new Vector3(scale.x, scale.y * 2f, scale.z);
            }

            MakeNetworkedPhysics(root, mass, displayName);
            return Save(root, path);
        }

        /// <summary>Adds the standard server-owned physics + grab components.</summary>
        public static void MakeNetworkedPhysics(GameObject root, float mass, string displayName)
        {
            root.AddComponent<NetworkObject>();
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.angularDamping = 0.3f;

            var nt = root.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;
            root.AddComponent<NetworkRigidbody>();

            var grab = root.AddComponent<GrabbableObject>();
            var so = new SerializedObject(grab);
            so.FindProperty("displayName").stringValue = displayName;
            so.ApplyModifiedPropertiesWithoutUndo();

            var beams = root.AddComponent<GrabBeams>();
            so = new SerializedObject(beams);
            so.FindProperty("lineMaterial").objectReferenceValue = BeamMaterial();
            so.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<ImpactKnockdown>();
        }

        static Material BeamMaterial()
        {
            string path = SetupUtil.MaterialsDir + "/M_GrabBeam.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        public static GameObject Save(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            // NetworkObject computes its GlobalObjectIdHash in OnValidate. When saved from a temporary
            // scene object it keeps the scene-object hash (and prefabs can collide), so re-validate
            // the saved asset to get the asset-based hash.
            var netObj = prefab.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                typeof(NetworkObject).GetMethod("OnValidate",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Public)
                    ?.Invoke(netObj, null);
                EditorUtility.SetDirty(prefab);
                AssetDatabase.SaveAssetIfDirty(prefab);
            }
            return prefab;
        }
    }
}
