using System.Collections.Generic;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace Freelancer.EditorTools
{
    /// <summary>Prefabs for the Reception Bar gig: glass, taps, keg, guests, conga line and the director.</summary>
    static partial class PrefabBuilder
    {
        public const string GlassPath = SetupUtil.PrefabsDir + "/Bar_Glass.prefab";
        public const string BeerTapPath = SetupUtil.PrefabsDir + "/Bar_BeerTap.prefab";
        public const string WineBoxPath = SetupUtil.PrefabsDir + "/Bar_WineBox.prefab";
        public const string KegPath = SetupUtil.PrefabsDir + "/Bar_Keg.prefab";
        public const string GuestPath = SetupUtil.PrefabsDir + "/Bar_Guest.prefab";
        public const string CongaPath = SetupUtil.PrefabsDir + "/Bar_CongaLine.prefab";
        public const string BarDirectorPath = SetupUtil.PrefabsDir + "/Bar_GigDirector.prefab";
        const string PhysicsDir = SetupUtil.Root + "/Art/Physics";

        static Material Chrome => SetupUtil.Mat("Chrome", new Color(0.78f, 0.8f, 0.84f), 0.75f);
        static Material Liquid => SetupUtil.Mat("Liquid", Color.white, 0.6f);
        static Material Ink => SetupUtil.Mat("Ink", new Color(0.1f, 0.1f, 0.12f));

        public static void BuildBarPrefabs()
        {
            Register(BuildGlass());
            Register(BuildTap(BeerTapPath, Drink.Beer, "beer", wineBox: false));
            Register(BuildTap(WineBoxPath, Drink.Wine, "wine", wineBox: true));
            Register(BuildKeg());
            var guest = BuildGuest();
            Register(guest);
            Register(BuildConga());
            Register(BuildBarDirector(guest));
        }

        /// <summary>Low friction for things that should slide along the bar.</summary>
        public static PhysicsMaterial Slippery()
        {
            SetupUtil.EnsureFolder(PhysicsDir);
            string path = PhysicsDir + "/PM_Slippery.asset";
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (pm == null)
            {
                pm = new PhysicsMaterial("Slippery");
                AssetDatabase.CreateAsset(pm, path);
            }
            pm.dynamicFriction = 0.14f;
            pm.staticFriction = 0.2f;
            pm.bounciness = 0f;
            pm.frictionCombine = PhysicsMaterialCombine.Minimum;
            pm.bounceCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(pm);
            return pm;
        }

        // ---------- Glass ----------

        static GameObject BuildGlass()
        {
            var root = new GameObject("Bar_Glass");
            var glassMat = SetupUtil.Transparent("Glass", new Color(0.85f, 0.95f, 1f, 0.35f));
            Mesh cyl = LowPolyMeshes.Cylinder;

            var wall = LowPolyMeshes.Visual(cyl, "Glass", root.transform, new Vector3(0, 0.085f, 0), new Vector3(0.13f, 0.085f, 0.13f), glassMat);
            LowPolyMeshes.Visual(cyl, "Base", root.transform, new Vector3(0, 0.008f, 0), new Vector3(0.12f, 0.008f, 0.12f), glassMat);

            var liquid = SetupUtil.Empty("Liquid", root.transform, new Vector3(0, 0.016f, 0)).transform;
            var liquidMesh = LowPolyMeshes.Visual(cyl, "Drink", liquid, new Vector3(0, 0.5f, 0), new Vector3(0.115f, 0.5f, 0.115f), Liquid);
            liquid.localScale = new Vector3(1f, 0.001f, 1f);
            liquid.gameObject.SetActive(false);

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 0.085f, 0);
            box.size = new Vector3(0.12f, 0.17f, 0.12f);
            box.sharedMaterial = Slippery();

            MakeNetworkedPhysics(root, 0.35f, "glass");
            Object.DestroyImmediate(root.GetComponent<ImpactKnockdown>());
            var rb = root.GetComponent<Rigidbody>();
            rb.angularDamping = 0.6f;
            rb.centerOfMass = new Vector3(0, 0.05f, 0); // bottom-heavy, so slides don't tip it

            var grab = new SerializedObject(root.GetComponent<GrabbableObject>());
            grab.FindProperty("throwSpeed").floatValue = 5f;
            grab.FindProperty("heldAngularDamping").floatValue = 8f;
            grab.ApplyModifiedPropertiesWithoutUndo();

            var drink = root.AddComponent<DrinkGlass>();
            var so = new SerializedObject(drink);
            so.FindProperty("liquid").objectReferenceValue = liquid;
            so.FindProperty("liquidRenderer").objectReferenceValue = liquidMesh.GetComponent<Renderer>();
            so.FindProperty("glassRenderer").objectReferenceValue = wall.GetComponent<Renderer>();
            so.FindProperty("maxLiquidHeight").floatValue = 0.14f;
            so.ApplyModifiedPropertiesWithoutUndo();

            return Save(root, GlassPath);
        }

        // ---------- Taps ----------

        /// <summary>
        /// Root sits on the bar top; local +z points at the bartender. Glasses go on the green
        /// drip tray under the spout.
        /// </summary>
        static GameObject BuildTap(string path, Drink drink, string drinkName, bool wineBox)
        {
            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
            root.AddComponent<NetworkObject>();
            var t = root.transform;
            Mesh cyl = LowPolyMeshes.Cylinder;
            var drinkMat = SetupUtil.Mat(wineBox ? "WineStream" : "BeerStream", Drinks.Liquid(drink), 0.6f);

            SceneKit.Box("DripTray", t, new Vector3(0, 0.01f, 0.15f), new Vector3(0.34f, 0.02f, 0.26f), SetupUtil.Mat("DripTray", Palette.Target));
            SceneKit.Box("Grate", t, new Vector3(0, 0.022f, 0.15f), new Vector3(0.28f, 0.006f, 0.2f), Chrome);

            Transform handle;
            Vector3 spoutPos;
            if (!wineBox)
            {
                LowPolyMeshes.Visual(cyl, "Tower", t, new Vector3(0, 0.24f, -0.04f), new Vector3(0.1f, 0.24f, 0.1f), Chrome);
                SceneKit.Box("Arm", t, new Vector3(0, 0.42f, 0.06f), new Vector3(0.07f, 0.07f, 0.24f), Chrome);
                spoutPos = new Vector3(0, 0.36f, 0.15f);
                LowPolyMeshes.Visual(cyl, "Spout", t, spoutPos + new Vector3(0, 0.03f, 0), new Vector3(0.035f, 0.03f, 0.035f), Chrome);
                handle = SetupUtil.Empty("Handle", t, new Vector3(0, 0.46f, 0.1f)).transform;
                SceneKit.Box("Grip", handle, new Vector3(0, 0.1f, 0), new Vector3(0.045f, 0.2f, 0.045f), SetupUtil.Mat("TapHandle", Palette.GigOrange));
                SceneKit.Box("Badge", handle, new Vector3(0, 0.14f, 0.026f), new Vector3(0.05f, 0.07f, 0.008f), SetupUtil.Mat("TapBadge", Palette.PartyGold));
                SceneKit.Label("BEER", t, t.TransformPoint(new Vector3(0, 0.2f, 0.012f)), Quaternion.Euler(0, 180, 0), 0.05f,
                    Palette.PartyWhite, new Color(0.2f, 0.2f, 0.24f));
            }
            else
            {
                // Bag-in-box wine on a little crate, spout at the front.
                SceneKit.Box("Crate", t, new Vector3(0, 0.1f, -0.06f), new Vector3(0.36f, 0.2f, 0.3f), SetupUtil.Mat("Crate", Palette.Cardboard));
                SceneKit.Box("WineBox", t, new Vector3(0, 0.37f, -0.06f), new Vector3(0.34f, 0.34f, 0.28f), SetupUtil.Mat("WineBox", new Color(0.55f, 0.15f, 0.28f)));
                SceneKit.Box("Grapes", t, new Vector3(0, 0.44f, 0.081f), new Vector3(0.12f, 0.12f, 0.004f), SetupUtil.Mat("Grapes", new Color(0.42f, 0.2f, 0.5f)));
                SceneKit.Label("CHÂTEAU BRENDA", t, t.TransformPoint(new Vector3(0, 0.31f, 0.084f)), Quaternion.Euler(0, 180, 0), 0.03f,
                    Palette.PartyWhite);
                spoutPos = new Vector3(0, 0.27f, 0.13f);
                LowPolyMeshes.Visual(cyl, "Spout", t, new Vector3(0, 0.27f, 0.1f), new Vector3(0.04f, 0.04f, 0.04f), Ink,
                    Quaternion.Euler(90, 0, 0));
                handle = SetupUtil.Empty("Handle", t, new Vector3(0, 0.3f, 0.13f)).transform;
                SceneKit.Box("Knob", handle, new Vector3(0, 0.02f, 0), new Vector3(0.05f, 0.04f, 0.03f), Ink);
            }

            var spout = SetupUtil.Empty("SpoutPoint", t, spoutPos).transform;
            float streamLen = spoutPos.y - 0.03f;
            var stream = LowPolyMeshes.Visual(cyl, "Stream", t, new Vector3(0, 0.03f + streamLen / 2f, 0.15f),
                new Vector3(0.022f, streamLen / 2f, 0.022f), drinkMat);
            stream.SetActive(false);

            // Look at it and press E (trigger, so grabs and glasses ignore it).
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0, 0.3f, 0f);
            trigger.size = new Vector3(0.4f, 0.6f, 0.36f);
            // Something solid for glasses to bump into.
            var solid = SetupUtil.Empty("Solid", t, Vector3.zero).AddComponent<BoxCollider>();
            solid.center = wineBox ? new Vector3(0, 0.27f, -0.06f) : new Vector3(0, 0.24f, -0.04f);
            solid.size = wineBox ? new Vector3(0.36f, 0.54f, 0.3f) : new Vector3(0.1f, 0.48f, 0.1f);

            var tap = root.AddComponent<DrinkTap>();
            var so = new SerializedObject(tap);
            so.FindProperty("drink").enumValueIndex = (int)drink;
            so.FindProperty("drinkName").stringValue = drinkName;
            so.FindProperty("spout").objectReferenceValue = spout;
            so.FindProperty("stream").objectReferenceValue = stream;
            so.FindProperty("handle").objectReferenceValue = handle;
            so.FindProperty("usesKeg").boolValue = !wineBox;
            if (!wineBox)
                so.FindProperty("kegSpot").objectReferenceValue =
                    SetupUtil.Empty("KegSpot", t, new Vector3(0, -1.05f, 0.75f)).transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.isStatic = false;
            return Save(root, path);
        }

        // ---------- Keg ----------

        static GameObject BuildKeg()
        {
            var root = new GameObject("Bar_Keg");
            Mesh cyl = LowPolyMeshes.Cylinder;
            var steel = SetupUtil.Mat("KegSteel", new Color(0.7f, 0.73f, 0.78f), 0.6f);
            LowPolyMeshes.Visual(cyl, "Body", root.transform, Vector3.zero, new Vector3(0.48f, 0.3f, 0.48f), steel);
            foreach (float y in new[] { -0.2f, 0.2f })
                LowPolyMeshes.Visual(cyl, "Ring", root.transform, new Vector3(0, y, 0), new Vector3(0.5f, 0.025f, 0.5f), Chrome);
            LowPolyMeshes.Visual(cyl, "Valve", root.transform, new Vector3(0, 0.32f, 0), new Vector3(0.1f, 0.03f, 0.1f), Ink);
            SceneKit.Box("Label", root.transform, new Vector3(0, 0f, 0.243f), new Vector3(0.2f, 0.14f, 0.004f), SetupUtil.Mat("TapHandle", Palette.GigOrange));
            AddCylinderCollider(root, 0.24f, 0.6f);

            MakeNetworkedPhysics(root, 50f, "beer keg");
            root.GetComponent<Rigidbody>().angularDamping = 1.2f;
            var grab = new SerializedObject(root.GetComponent<GrabbableObject>());
            grab.FindProperty("maxForcePerHolder").floatValue = 380f; // one player drags it, two carry it
            grab.FindProperty("maxThrowImpulse").floatValue = 60f;
            grab.ApplyModifiedPropertiesWithoutUndo();
            var knock = new SerializedObject(root.GetComponent<ImpactKnockdown>());
            knock.FindProperty("minSpeed").floatValue = 4f;
            knock.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<BeerKeg>();
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.isStatic = false;
            return Save(root, KegPath);
        }

        // ---------- People ----------

        public sealed class NpcParts
        {
            public Transform Body;
            public readonly List<Renderer> Outfit = new();
            public readonly List<Renderer> Skin = new();
            public readonly List<GameObject> Hats = new();
        }

        /// <summary>A simple chunky person (feet at the parent's origin, facing +z).</summary>
        public static NpcParts BuildNpc(Transform parent, Material outfit, Material skin, bool armsForward)
        {
            var parts = new NpcParts { Body = SetupUtil.Empty("Body", parent, Vector3.zero).transform };
            var b = parts.Body;
            Mesh capsule = LowPolyMeshes.Capsule, sphere = LowPolyMeshes.Sphere, cyl = LowPolyMeshes.Cylinder;
            var pants = SetupUtil.Mat("GuestPants", new Color(0.2f, 0.21f, 0.26f));
            var shoes = SetupUtil.Mat("Shoes", new Color(0.18f, 0.14f, 0.13f));

            Renderer V(Mesh m, string n, Transform p, Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null) =>
                LowPolyMeshes.Visual(m, n, p, pos, scale, mat, rot).GetComponent<Renderer>();

            foreach (float side in new[] { -1f, 1f })
            {
                V(capsule, "Leg", b, new Vector3(0.12f * side, 0.42f, 0), new Vector3(0.17f, 0.38f, 0.17f), pants);
                V(capsule, "Shoe", b, new Vector3(0.12f * side, 0.05f, 0.05f), new Vector3(0.15f, 0.06f, 0.24f), shoes);
            }
            parts.Outfit.Add(V(capsule, "Torso", b, new Vector3(0, 1.12f, 0), new Vector3(0.54f, 0.33f, 0.38f), outfit));
            foreach (float side in new[] { -1f, 1f })
            {
                if (armsForward)
                {
                    // Hands on the shoulders of whoever's in front.
                    parts.Outfit.Add(V(capsule, "Arm", b, new Vector3(0.26f * side, 1.32f, 0.3f), new Vector3(0.13f, 0.28f, 0.13f), outfit,
                        Quaternion.Euler(80, 0, 0)));
                    parts.Skin.Add(V(sphere, "Hand", b, new Vector3(0.26f * side, 1.36f, 0.6f), Vector3.one * 0.12f, skin));
                }
                else
                {
                    parts.Outfit.Add(V(capsule, "Arm", b, new Vector3(0.34f * side, 1.08f, 0.02f), new Vector3(0.13f, 0.27f, 0.13f), outfit,
                        Quaternion.Euler(0, 0, 10 * side)));
                    parts.Skin.Add(V(sphere, "Hand", b, new Vector3(0.39f * side, 0.8f, 0.04f), Vector3.one * 0.12f, skin));
                }
            }
            parts.Skin.Add(V(sphere, "Head", b, new Vector3(0, 1.66f, 0), Vector3.one * 0.44f, skin));

            var white = SetupUtil.Mat("EyeWhite", Color.white, 0.4f);
            foreach (float side in new[] { -1f, 1f })
            {
                V(sphere, "Eye", b, new Vector3(0.09f * side, 1.71f, 0.18f), new Vector3(0.11f, 0.12f, 0.08f), white);
                V(sphere, "Pupil", b, new Vector3(0.09f * side + 0.012f, 1.7f, 0.22f), new Vector3(0.05f, 0.05f, 0.03f), Ink);
            }
            V(capsule, "Mouth", b, new Vector3(0, 1.56f, 0.2f), new Vector3(0.1f, 0.018f, 0.03f), Ink, Quaternion.Euler(0, 0, 90));

            // Hats (the guest picks one, or none).
            var topHat = SetupUtil.Empty("Hat_TopHat", b, new Vector3(0, 1.84f, 0));
            V(cyl, "Brim", topHat.transform, Vector3.zero, new Vector3(0.38f, 0.01f, 0.38f), Ink);
            V(cyl, "Crown", topHat.transform, new Vector3(0, 0.11f, 0), new Vector3(0.26f, 0.11f, 0.26f), Ink);
            parts.Hats.Add(topHat);

            var partyHat = SetupUtil.Empty("Hat_Party", b, new Vector3(0.04f, 1.84f, 0));
            partyHat.transform.localRotation = Quaternion.Euler(0, 0, -12);
            V(cyl, "Cone", partyHat.transform, new Vector3(0, 0.09f, 0), new Vector3(0.14f, 0.09f, 0.14f), SetupUtil.Mat("PartyPink", Palette.PartyPink));
            V(sphere, "Pom", partyHat.transform, new Vector3(0, 0.2f, 0), Vector3.one * 0.07f, SetupUtil.Mat("PartyGold", Palette.PartyGold));
            parts.Hats.Add(partyHat);

            var bow = SetupUtil.Empty("Hat_Bow", b, new Vector3(0.12f, 1.85f, 0.04f));
            V(sphere, "BowL", bow.transform, new Vector3(-0.05f, 0, 0), new Vector3(0.1f, 0.07f, 0.04f), SetupUtil.Mat("PartyPink", Palette.PartyPink));
            V(sphere, "BowR", bow.transform, new Vector3(0.05f, 0, 0), new Vector3(0.1f, 0.07f, 0.04f), SetupUtil.Mat("PartyPink", Palette.PartyPink));
            parts.Hats.Add(bow);

            foreach (var hat in parts.Hats) hat.SetActive(false);
            return parts;
        }

        static GameObject BuildGuest()
        {
            var root = new GameObject("Bar_Guest");
            root.AddComponent<NetworkObject>();
            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.95f, 0);
            col.height = 1.9f;
            col.radius = 0.3f;

            var parts = BuildNpc(root.transform, SetupUtil.Mat("GuestOutfit", Color.white), SetupUtil.Mat("GuestSkin", Color.white), false);

            // Speech bubble: order sign + patience bar. Turned toward the camera at runtime.
            var bubble = SetupUtil.Empty("Bubble", root.transform, new Vector3(0, 2.55f, 0)).transform;
            var text = new GameObject("Text").AddComponent<TMPro.TextMeshPro>();
            text.transform.SetParent(bubble, false);
            text.text = "BEER";
            text.fontSize = 2.6f;
            text.fontStyle = TMPro.FontStyles.Bold;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            text.color = Color.white;
            text.rectTransform.sizeDelta = new Vector2(3f, 0.6f);
            var plate = SceneKit.Box("Plate", bubble, new Vector3(0, 0, 0.02f), new Vector3(0.8f, 0.34f, 0.02f), SetupUtil.Mat("BubblePlate", Color.white));
            plate.isStatic = false;
            var tail = SceneKit.Box("Tail", bubble, new Vector3(0, -0.2f, 0.02f), new Vector3(0.12f, 0.12f, 0.02f), SetupUtil.Mat("BubblePlate", Color.white),
                Quaternion.Euler(0, 0, 45));
            tail.isStatic = false;

            var patience = SetupUtil.Empty("Patience", bubble, new Vector3(0, -0.3f, 0.02f)).transform;
            SceneKit.Box("Back", patience, Vector3.zero, new Vector3(0.74f, 0.08f, 0.01f), Ink).isStatic = false;
            var fill = SetupUtil.Empty("Fill", patience, new Vector3(-0.35f, 0, -0.008f)).transform;
            var fillBox = SceneKit.Box("Bar", fill, new Vector3(0.35f, 0, 0), new Vector3(0.7f, 0.05f, 0.01f), SetupUtil.Mat("BubblePlate", Color.white));
            fillBox.isStatic = false;
            foreach (var r in root.GetComponentsInChildren<Transform>(true)) r.gameObject.isStatic = false;

            var guest = root.AddComponent<BarGuest>();
            var so = new SerializedObject(guest);
            so.FindProperty("body").objectReferenceValue = parts.Body;
            SetObjects(so.FindProperty("outfitRenderers"), parts.Outfit.ToArray());
            SetObjects(so.FindProperty("skinRenderers"), parts.Skin.ToArray());
            SetObjects(so.FindProperty("hats"), parts.Hats.ToArray());
            so.FindProperty("bubble").objectReferenceValue = bubble;
            so.FindProperty("bubbleText").objectReferenceValue = text;
            so.FindProperty("bubblePlate").objectReferenceValue = plate.GetComponent<Renderer>();
            so.FindProperty("patienceFill").objectReferenceValue = fill;
            so.FindProperty("patienceRenderer").objectReferenceValue = fillBox.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();

            return Save(root, GuestPath);
        }

        static readonly Color[] k_DancerOutfits =
        {
            new(0.9f, 0.45f, 0.55f), new(0.3f, 0.5f, 0.8f), new(0.95f, 0.75f, 0.3f),
            new(0.4f, 0.7f, 0.45f), new(0.6f, 0.4f, 0.75f), new(0.85f, 0.85f, 0.88f),
        };

        static GameObject BuildConga()
        {
            var root = new GameObject("Bar_CongaLine");
            root.AddComponent<NetworkObject>();
            var dancers = new List<Rigidbody>();
            for (int i = 0; i < k_DancerOutfits.Length; i++)
            {
                var d = SetupUtil.Empty("Dancer" + (i + 1), root.transform, new Vector3(0, 0, -0.8f * i));
                var parts = BuildNpc(d.transform, SetupUtil.Mat("Dancer" + i, k_DancerOutfits[i]),
                    SetupUtil.Mat("DancerSkin" + i % 3, new[] { new Color(0.98f, 0.84f, 0.7f), new Color(0.75f, 0.55f, 0.4f), new Color(0.5f, 0.35f, 0.25f) }[i % 3]),
                    armsForward: i > 0);
                if (i == 0) parts.Hats[1].SetActive(true);   // the leader wears the party hat
                if (i == 3) parts.Hats[0].SetActive(true);
                var col = d.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0, 0.95f, 0);
                col.height = 1.9f;
                col.radius = 0.3f;
                var rb = d.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                dancers.Add(rb);
            }

            var conga = root.AddComponent<CongaLine>();
            var so = new SerializedObject(conga);
            SetObjects(so.FindProperty("dancers"), dancers.ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();
            return Save(root, CongaPath);
        }

        // ---------- Director ----------

        static GameObject BuildBarDirector(GameObject guestPrefab)
        {
            var root = new GameObject("Bar_GigDirector");
            root.AddComponent<NetworkObject>();
            var objective = root.AddComponent<BarObjective>();
            root.AddComponent<GigDirector>();
            var so = new SerializedObject(objective);
            so.FindProperty("guestPrefab").objectReferenceValue = guestPrefab.GetComponent<NetworkObject>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return Save(root, BarDirectorPath);
        }
    }
}
