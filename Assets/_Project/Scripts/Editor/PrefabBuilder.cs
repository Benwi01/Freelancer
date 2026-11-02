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

            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
            Debug.Log("[Freelancer] Prefabs built.");
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
