using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace Freelancer.EditorTools
{
    /// <summary>Prefabs for Lucky Lenny's casino: the roulette table.</summary>
    static partial class PrefabBuilder
    {
        public const string RoulettePath = SetupUtil.PrefabsDir + "/Casino_Roulette.prefab";

        public static void BuildCasinoPrefabs() => Register(BuildRoulette());

        /// <summary>
        /// Roulette table, long along x: wheel at the -x end, the betting layout on the felt at +x,
        /// chip stacks on the players' edge. Players stand on the +z side; the croupier on the -z side.
        /// Table top at y 0.9. Every betting spot, chip stack and the wheel has its own trigger, so the
        /// E prompt picks out exactly what you're looking at.
        /// </summary>
        static GameObject BuildRoulette()
        {
            var root = new GameObject("Casino_Roulette");
            root.AddComponent<NetworkObject>();
            var table = root.AddComponent<RouletteTable>();
            var t = root.transform;
            Mesh cyl = LowPolyMeshes.Cylinder;
            var wood = SetupUtil.Mat("RouletteWood", new Color(0.36f, 0.2f, 0.12f), 0.5f);
            var felt = SetupUtil.Mat("Felt", new Color(0.1f, 0.42f, 0.24f));
            var gold = SetupUtil.Mat("PartyGold", Palette.PartyGold);
            var red = SetupUtil.Mat("RouletteRed", new Color(0.78f, 0.12f, 0.14f));
            var black = SetupUtil.Mat("RouletteBlack", new Color(0.1f, 0.1f, 0.12f));
            var green = SetupUtil.Mat("RouletteGreen", new Color(0.12f, 0.6f, 0.3f));
            var white = SetupUtil.Mat("PartyWhite", Palette.PartyWhite);
            const float top = 0.9f;

            // Table body (solid) and rim.
            var body = SetupUtil.Block("Body", t, new Vector3(0.1f, 0.42f, 0f), new Vector3(3f, 0.84f, 1.4f), wood);
            body.isStatic = false;
            SceneKit.Box("Rim", t, new Vector3(0.1f, top - 0.03f, 0f), new Vector3(3.2f, 0.06f, 1.6f), wood);
            SceneKit.Box("Felt", t, new Vector3(0.45f, top + 0.003f, 0f), new Vector3(2f, 0.006f, 1.3f), felt);
            SceneKit.Box("Pedestal", t, new Vector3(0.1f, 0.06f, 0f), new Vector3(3.1f, 0.12f, 1.5f), SetupUtil.Mat("RouletteWoodDark", new Color(0.25f, 0.14f, 0.09f)));

            // ---- Wheel (all sizes in metres from the wheel centre, which sits on the table top) ----
            // Static bowl: wooden base, a wall round the outside, a sloped ball track with gold
            // deflectors. The spinning head: a numbered ring sloping into 37 pockets split by gold
            // frets, and a stepped cone with a cross-shaped turret. Every surface the ball can touch
            // has a collider; the ball itself is a small physics sphere the server sends round.
            var wc = new Vector3(-0.95f, top, 0f);
            // The ball glides round a slick wall and track, slowed mostly by drag, then gets caught by
            // grippy pockets and frets; deflectors knock it about on the way down.
            var wheelPm = PhysicsMat("PM_RouletteWheel", 0.35f, 0.6f);
            var wallPm = PhysicsMat("PM_RouletteWall", 0f, 0f);
            var trackPm = PhysicsMat("PM_RouletteTrack", 0.1f, 0.04f);
            var deflectPm = PhysicsMat("PM_RouletteDeflector", 0.5f, 0.2f);
            var darkWood = SetupUtil.Mat("RouletteWoodDark", new Color(0.25f, 0.14f, 0.09f));
            var polished = SetupUtil.Mat("RouletteWheelWood", new Color(0.45f, 0.24f, 0.12f), 0.75f);
            var trackMat = SetupUtil.Mat("Track", new Color(0.55f, 0.4f, 0.25f), 0.7f);
            var chromeMat = SetupUtil.Mat("Chrome", new Color(0.78f, 0.8f, 0.84f), 0.75f);
            var bowl = SetupUtil.Empty("Bowl", t, wc).transform;
            LowPolyMeshes.Visual(cyl, "Base", bowl, new Vector3(0f, 0.02f, 0f), new Vector3(1.22f, 0.02f, 1.22f), darkWood);
            // Smooth revolved surfaces (boxes leave seams that kick a fast ball off the track):
            // the sloped track (r 0.39 -> 0.5075), then the wall and its rounded top.
            Revolved(bowl, "Track", "RouletteTrack", new[] { new Vector2(0.39f, 0.098f), new Vector2(0.5075f, 0.134f) }, trackMat, trackPm);
            Revolved(bowl, "Wall", "RouletteWall", new[]
            {
                new Vector2(0.5075f, 0.134f), new Vector2(0.5075f, 0.218f), new Vector2(0.58f, 0.218f), new Vector2(0.6f, 0.04f),
            }, darkWood, wallPm);
            Revolved(bowl, "Trim", "RouletteTrim", new[] { new Vector2(0.535f, 0.2185f), new Vector2(0.575f, 0.2185f) }, gold, null);
            const float slope = 17.03f; // atan(0.036 / 0.1175)
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f + 22.5f;
                var rot = Quaternion.Euler(0f, a, 0f);
                var d = Diamond(bowl, "Deflector", rot * new Vector3(0f, 0.115f, 0.42f), rot * Quaternion.Euler(-slope, 45f, 0f),
                    new Vector3(0.024f, 0.016f, 0.024f), gold, deflectPm);
                d.name = "Deflector" + i;
            }

            var wheel = SetupUtil.Empty("Wheel", t, wc).transform;
            var wheelBody = wheel.gameObject.AddComponent<Rigidbody>();
            wheelBody.isKinematic = true;
            wheelBody.interpolation = RigidbodyInterpolation.Interpolate;
            int n = RouletteRules.WheelOrder.Length;
            float step = 360f / n;
            for (int i = 0; i < n; i++)
            {
                int number = RouletteRules.WheelOrder[i];
                var dir = Quaternion.Euler(0f, i * step, 0f);
                var between = Quaternion.Euler(0f, i * step + step / 2f, 0f);
                var mat = number == 0 ? green : RouletteRules.IsRed(number) ? red : black;
                // Pocket floor, the numbered ring sloping down into it, and the fret to its right.
                Solid(wheel, "Pocket" + number, dir * new Vector3(0f, 0.053f, 0.3f), dir, new Vector3(0.054f, 0.01f, 0.112f), mat, wheelPm);
                Solid(wheel, "Ring" + number, dir * new Vector3(0f, 0.088f, 0.3725f), dir * Quaternion.Euler(-10f, 0f, 0f),
                    new Vector3(0.066f, 0.01f, 0.04f), mat, wheelPm);
                Solid(wheel, "Fret" + i, between * new Vector3(0f, 0.072f, 0.302f), between, new Vector3(0.004f, 0.028f, 0.116f), gold, wheelPm);
                SceneKit.Label(number.ToString(), wheel, wheel.TransformPoint(dir * new Vector3(0f, 0.0945f, 0.3725f)),
                    dir * Quaternion.Euler(80f, 180f, 0f), 0.018f, Palette.PartyWhite);
            }
            // Stepped cone and the turret on top.
            var cone = LowPolyMeshes.Visual(cyl, "Cone", wheel, new Vector3(0f, 0.078f, 0f), new Vector3(0.49f, 0.02f, 0.49f), polished);
            Collide(cone, wheelPm, convexMesh: cyl);
            var cone2 = LowPolyMeshes.Visual(cyl, "Cone2", wheel, new Vector3(0f, 0.11f, 0f), new Vector3(0.34f, 0.014f, 0.34f), polished);
            Collide(cone2, wheelPm, convexMesh: cyl);
            LowPolyMeshes.Visual(cyl, "Dome", wheel, new Vector3(0f, 0.132f, 0f), new Vector3(0.15f, 0.012f, 0.15f), chromeMat);
            LowPolyMeshes.Visual(cyl, "Spindle", wheel, new Vector3(0f, 0.17f, 0f), new Vector3(0.03f, 0.04f, 0.03f), gold);
            for (int i = 0; i < 4; i++)
            {
                var arm = Quaternion.Euler(0f, i * 90f, 0f);
                SceneKit.Box("TurretArm", wheel, arm * new Vector3(0f, 0.2f, 0.05f), new Vector3(0.012f, 0.012f, 0.1f), gold, arm);
                LowPolyMeshes.Visual(LowPolyMeshes.Sphere, "TurretKnob", wheel, arm * new Vector3(0f, 0.2f, 0.1f), Vector3.one * 0.026f, gold);
            }
            LowPolyMeshes.Visual(LowPolyMeshes.Sphere, "TurretTop", wheel, new Vector3(0f, 0.22f, 0f), Vector3.one * 0.03f, gold);

            // The ball: a child of the table, not the wheel (a moving parent would drag it around).
            var ball = LowPolyMeshes.Visual(LowPolyMeshes.Sphere, "Ball", t, wc + new Vector3(0f, 0.0755f, 0.3f), Vector3.one * 0.035f,
                SetupUtil.Mat("Ivory", new Color(0.98f, 0.97f, 0.93f), 0.8f)).transform;
            var ballCol = ball.gameObject.AddComponent<SphereCollider>();
            ballCol.radius = 0.5f;
            ballCol.contactOffset = 0.002f;
            ballCol.sharedMaterial = PhysicsMat("PM_RouletteBall", 0.4f, 0f);
            var ballBody = ball.gameObject.AddComponent<Rigidbody>();
            ballBody.mass = 0.02f;
            ballBody.isKinematic = true;
            ballBody.interpolation = RigidbodyInterpolation.Interpolate;
            ballBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            ballBody.solverIterations = 30;
            ballBody.solverVelocityIterations = 10;
            ballBody.linearDamping = 0.07f;
            ballBody.angularDamping = 0.5f;
            ballBody.maxAngularVelocity = 400f;
            var ballNt = ball.gameObject.AddComponent<NetworkTransform>();
            ballNt.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            ballNt.InLocalSpace = true;
            ballNt.SyncRotAngleX = ballNt.SyncRotAngleY = ballNt.SyncRotAngleZ = false;
            ballNt.SyncScaleX = ballNt.SyncScaleY = ballNt.SyncScaleZ = false;
            ballNt.Interpolate = true;
            ball.gameObject.AddComponent<NetworkRigidbody>();

            // Betting layout: zero, 12 x 3 numbers, then the six even-money boxes.
            var cells = new Transform[37];
            float y = top + 0.008f;
            const float cw = 0.11f, ch = 0.155f, x0 = -0.2f;
            SceneKit.Box("Cell0", t, new Vector3(x0 - 0.065f, y, 0.1f), new Vector3(0.11f, 0.004f, 0.48f), green);
            cells[0] = Spot(t, table, "Spot0", new Vector3(x0 - 0.065f, y, 0.1f), new Vector3(0.11f, 0.03f, 0.48f), RouletteBetKind.Straight, 0);
            SceneKit.Label("0", t, new Vector3(x0 - 0.065f, y + 0.004f, 0.1f), Quaternion.Euler(90f, 180f, 0f), 0.05f, Palette.PartyWhite);
            for (int c = 0; c < 12; c++)
            for (int r = 0; r < 3; r++)
            {
                int number = c * 3 + (3 - r);
                var pos = new Vector3(x0 + c * cw + cw / 2f, y, 0.26f - r * ch);
                SceneKit.Box("Cell" + number, t, pos, new Vector3(cw - 0.008f, 0.004f, ch - 0.008f), RouletteRules.IsRed(number) ? red : black);
                SceneKit.Label(number.ToString(), t, pos + new Vector3(0f, 0.004f, 0f), Quaternion.Euler(90f, 180f, 0f), 0.04f, Palette.PartyWhite);
                cells[number] = Spot(t, table, "Spot" + number, pos, new Vector3(cw - 0.008f, 0.03f, ch - 0.008f), RouletteBetKind.Straight, number);
            }
            // Even-money row, in the order of RouletteBetKind (Red, Black, Odd, Even, Low, High), laid out like a real table.
            var outside = new Transform[6];
            var boxes = new (RouletteBetKind kind, string label, Material mat)[]
            {
                (RouletteBetKind.Low, "1-18", felt), (RouletteBetKind.Even, "EVEN", felt), (RouletteBetKind.Red, "", red),
                (RouletteBetKind.Black, "", black), (RouletteBetKind.Odd, "ODD", felt), (RouletteBetKind.High, "19-36", felt),
            };
            for (int i = 0; i < boxes.Length; i++)
            {
                var pos = new Vector3(x0 + i * 0.22f + 0.11f, y, -0.3f);
                SceneKit.Box("Outside_" + boxes[i].kind, t, pos, new Vector3(0.21f, 0.004f, 0.16f), boxes[i].mat);
                SceneKit.Box("Outline_" + boxes[i].kind, t, pos - new Vector3(0f, 0.001f, 0f), new Vector3(0.22f, 0.004f, 0.17f), white);
                if (boxes[i].label.Length > 0)
                    SceneKit.Label(boxes[i].label, t, pos + new Vector3(0f, 0.004f, 0f), Quaternion.Euler(90f, 180f, 0f), 0.04f, Palette.PartyWhite);
                outside[(int)boxes[i].kind] = Spot(t, table, "Spot_" + boxes[i].kind, pos, new Vector3(0.21f, 0.03f, 0.16f), boxes[i].kind, 0);
            }
            SceneKit.Label("PAYS 35:1", t, new Vector3(0.46f, y + 0.002f, 0.43f), Quaternion.Euler(90f, 180f, 0f), 0.03f, Palette.PartyGold);
            SceneKit.Label("PAYS 1:1", t, new Vector3(0.46f, y + 0.002f, -0.43f), Quaternion.Euler(90f, 180f, 0f), 0.03f, Palette.PartyGold);
            var chipRoot = SetupUtil.Empty("Chips", t, Vector3.zero).transform;

            // Board hanging over the table: phase, then the last few numbers.
            var boardPos = new Vector3(0.1f, 2.45f, -0.4f);
            SceneKit.Box("BoardBack", t, boardPos + new Vector3(0f, 0f, -0.04f), new Vector3(2.4f, 0.75f, 0.06f), SetupUtil.Mat("CasinoSign", new Color(0.55f, 0.1f, 0.2f)));
            SceneKit.Box("BoardFrame", t, boardPos + new Vector3(0f, 0f, -0.06f), new Vector3(2.5f, 0.85f, 0.04f), gold);
            foreach (float x in new[] { -1f, 1f })
                SceneKit.Box("BoardChain", t, boardPos + new Vector3(x, 0.9f, -0.04f), new Vector3(0.015f, 1f, 0.015f), gold);
            var boardGo = new GameObject("Board");
            boardGo.transform.SetParent(t, false);
            boardGo.transform.SetPositionAndRotation(boardPos + new Vector3(0f, 0f, 0.0f), Quaternion.Euler(0f, 180f, 0f));
            var board = boardGo.AddComponent<TMPro.TextMeshPro>();
            board.text = "PLACE YOUR BETS";
            board.fontSize = 2.3f;
            board.alignment = TMPro.TextAlignmentOptions.Center;
            board.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            board.color = Color.white;
            board.rectTransform.sizeDelta = new Vector2(2.3f, 0.7f);

            // Chip stacks on the players' edge of the felt: E picks your chip size.
            var stacks = new List<RouletteChipStack>();
            var chipColors = new[]
            {
                (10, new Color(0.95f, 0.95f, 0.92f)), (25, new Color(0.8f, 0.15f, 0.18f)),
                (50, new Color(0.2f, 0.4f, 0.85f)), (100, new Color(0.12f, 0.12f, 0.14f)),
            };
            for (int i = 0; i < chipColors.Length; i++)
            {
                var (amount, color) = chipColors[i];
                var stack = SetupUtil.Empty("Chips$" + amount, t, new Vector3(0.85f - i * 0.3f, top + 0.006f, 0.55f));
                var chipMat = SetupUtil.Mat("Chip" + amount, color, 0.4f);
                for (int c = 0; c < 6; c++)
                    LowPolyMeshes.Visual(cyl, "Chip", stack.transform, new Vector3(c % 2 * 0.004f, 0.006f + c * 0.012f, 0f), new Vector3(0.075f, 0.006f, 0.075f), chipMat);
                LowPolyMeshes.Visual(cyl, "Edge", stack.transform, new Vector3(0f, 0.075f, 0f), new Vector3(0.05f, 0.002f, 0.05f), white);
                SceneKit.Label("$" + amount, stack.transform, stack.transform.position + new Vector3(0f, 0.002f, 0.075f), Quaternion.Euler(90f, 180f, 0f), 0.028f, Palette.PartyGold);
                var box = stack.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = new Vector3(0f, 0.05f, 0f);
                box.size = new Vector3(0.14f, 0.12f, 0.14f);
                var cs = stack.AddComponent<RouletteChipStack>();
                var cso = new SerializedObject(cs);
                cso.FindProperty("amount").intValue = amount;
                cso.ApplyModifiedPropertiesWithoutUndo();
                stacks.Add(cs);
            }

            // The wheel: E spins it.
            var spin = SetupUtil.Empty("WheelSpin", t, wc);
            var spinBox = spin.AddComponent<BoxCollider>();
            spinBox.isTrigger = true;
            spinBox.center = new Vector3(0f, 0.12f, 0f);
            spinBox.size = new Vector3(1.15f, 0.28f, 1.15f);
            var spinSo = new SerializedObject(spin.AddComponent<RouletteWheelSpin>());
            spinSo.FindProperty("table").objectReferenceValue = table;
            spinSo.ApplyModifiedPropertiesWithoutUndo();

            // Glows: where you're aiming, and the winning number.
            var hover = SceneKit.Box("HoverGlow", t, Vector3.zero, Vector3.one, SetupUtil.Transparent("RouletteHover", new Color(1f, 1f, 0.85f, 0.45f)));
            hover.SetActive(false);
            var win = SceneKit.Box("WinGlow", t, Vector3.zero, Vector3.one, SetupUtil.Emissive("RouletteWin", Palette.PartyGold, 3f));
            win.SetActive(false);
            foreach (var r in new[] { hover, win }) r.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var so = new SerializedObject(table);
            so.FindProperty("wheel").objectReferenceValue = wheel;
            so.FindProperty("wheelBody").objectReferenceValue = wheelBody;
            so.FindProperty("ball").objectReferenceValue = ball;
            so.FindProperty("ballBody").objectReferenceValue = ballBody;
            so.FindProperty("pocketRadius").floatValue = 0.3f;
            so.FindProperty("restHeight").floatValue = 0.0755f;
            so.FindProperty("launchRadius").floatValue = 0.49f;   // ball centre against the wall
            so.FindProperty("launchHeight").floatValue = 0.1465f; // on the track (0.129 at r 0.49) + ball radius
            so.FindProperty("settleRadius").floatValue = 0.36f;
            so.FindProperty("board").objectReferenceValue = board;
            so.FindProperty("chipRoot").objectReferenceValue = chipRoot;
            SetObjects(so.FindProperty("outsideSpots"), outside);
            SetObjects(so.FindProperty("numberCells"), cells);
            so.FindProperty("hoverMarker").objectReferenceValue = hover.transform;
            so.FindProperty("winMarker").objectReferenceValue = win.transform;
            SetObjects(so.FindProperty("chipStacks"), stacks.ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();

            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.isStatic = false;
            return Save(root, RoulettePath);
        }

        /// <summary>A ring of boxes round the wheel centre (y from <paramref name="y0"/> to <paramref name="y1"/>), tilted by <paramref name="tilt"/> degrees.</summary>
        static void Ring(Transform parent, string name, int count, float radius, float radialSize, float y0, float y1, float tilt, Material mat, PhysicsMaterial pm)
        {
            float width = 2f * Mathf.PI * radius / count * 1.12f;
            for (int i = 0; i < count; i++)
            {
                var yaw = Quaternion.Euler(0f, i * 360f / count, 0f);
                var size = tilt == 0f ? new Vector3(width, y1 - y0, radialSize) : new Vector3(width, 0.01f, radialSize / Mathf.Cos(tilt * Mathf.Deg2Rad));
                var go = SceneKit.Box(name + i, parent, yaw * new Vector3(0f, (y0 + y1) / 2f, radius), size, mat, yaw * Quaternion.Euler(tilt, 0f, 0f));
                if (pm != null) Collide(go, pm);
            }
        }

        /// <summary>
        /// A smooth surface of revolution round the wheel centre from a (radius, height) profile, as both
        /// the visual and (with a physics material) a collider. Faces point to the ball's side.
        /// </summary>
        static GameObject Revolved(Transform parent, string name, string meshName, Vector2[] profile, Material mat, PhysicsMaterial pm)
        {
            const int segments = 256;
            string path = LowPolyMeshes.Dir + "/" + meshName + ".asset";
            SetupUtil.EnsureFolder(LowPolyMeshes.Dir);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = meshName };
                AssetDatabase.CreateAsset(mesh, path);
            }
            mesh.Clear();
            // One ring of vertices per profile segment end, duplicated per segment so corners stay crisp.
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int k = 0; k < profile.Length - 1; k++)
            {
                int start = verts.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    foreach (var pt in new[] { profile[k], profile[k + 1] })
                        verts.Add(new Vector3(Mathf.Sin(a) * pt.x, pt.y, Mathf.Cos(a) * pt.x));
                }
                for (int i = 0; i < segments; i++)
                {
                    int a0 = start + i * 2, b0 = a0 + 1, a1 = a0 + 2, b1 = a0 + 3;
                    tris.AddRange(new[] { a0, b0, a1, b0, b1, a1 });
                }
            }
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (pm != null)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.sharedMaterial = pm;
                mc.contactOffset = 0.002f;
            }
            return go;
        }

        /// <summary>A visual box that the ball bounces off.</summary>
        static GameObject Solid(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 size, Material mat, PhysicsMaterial pm)
        {
            var go = SceneKit.Box(name, parent, pos, size, mat, rot);
            Collide(go, pm);
            return go;
        }

        static GameObject Diamond(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 size, Material mat, PhysicsMaterial pm) =>
            Solid(parent, name, pos, rot, size, mat, pm);

        /// <summary>Give a visual piece a tight collider (box, or a convex mesh for round parts).</summary>
        static void Collide(GameObject go, PhysicsMaterial pm, Mesh convexMesh = null)
        {
            Collider c;
            if (convexMesh != null)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = convexMesh;
                mc.convex = true;
                c = mc;
            }
            else c = go.AddComponent<BoxCollider>();
            c.sharedMaterial = pm;
            c.contactOffset = 0.002f;
        }

        static PhysicsMaterial PhysicsMat(string name, float bounce, float friction)
        {
            SetupUtil.EnsureFolder(PhysicsDir);
            string path = PhysicsDir + "/" + name + ".asset";
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (pm == null)
            {
                pm = new PhysicsMaterial(name);
                AssetDatabase.CreateAsset(pm, path);
            }
            pm.bounciness = bounce;
            pm.dynamicFriction = friction;
            pm.staticFriction = friction;
            pm.bounceCombine = PhysicsMaterialCombine.Average;
            pm.frictionCombine = PhysicsMaterialCombine.Average;
            EditorUtility.SetDirty(pm);
            return pm;
        }

        /// <summary>A betting spot: a thin trigger over the felt that you look at and press E on.</summary>
        static Transform Spot(Transform parent, RouletteTable table, string name, Vector3 pos, Vector3 size, RouletteBetKind kind, int number)
        {
            var go = SetupUtil.Empty(name, parent, pos);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, size.y / 2f, 0f);
            box.size = size;
            var so = new SerializedObject(go.AddComponent<RouletteSpot>());
            so.FindProperty("table").objectReferenceValue = table;
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("number").intValue = number;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go.transform;
        }
    }
}
