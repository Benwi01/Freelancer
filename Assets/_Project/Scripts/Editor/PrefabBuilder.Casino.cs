using System.Collections.Generic;
using Unity.Netcode;
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

            // Wheel: wooden bowl, a gold track, and the spinning part with 37 pockets.
            var wc = new Vector3(-0.95f, top, 0f);
            LowPolyMeshes.Visual(cyl, "Bowl", t, wc + new Vector3(0f, 0.03f, 0f), new Vector3(1.1f, 0.03f, 1.1f), wood);
            LowPolyMeshes.Visual(cyl, "Track", t, wc + new Vector3(0f, 0.062f, 0f), new Vector3(0.96f, 0.004f, 0.96f), SetupUtil.Mat("Track", new Color(0.55f, 0.4f, 0.25f), 0.7f));
            var wheel = SetupUtil.Empty("Wheel", t, wc + new Vector3(0f, 0.07f, 0f)).transform;
            LowPolyMeshes.Visual(cyl, "Disc", wheel, Vector3.zero, new Vector3(0.72f, 0.01f, 0.72f), black);
            int n = RouletteRules.WheelOrder.Length;
            for (int i = 0; i < n; i++)
            {
                int number = RouletteRules.WheelOrder[i];
                float a = i * 360f / n;
                var dir = Quaternion.Euler(0f, a, 0f);
                var mat = number == 0 ? green : RouletteRules.IsRed(number) ? red : black;
                SceneKit.Box("Pocket" + number, wheel, dir * new Vector3(0f, 0.012f, 0.3f), new Vector3(0.046f, 0.006f, 0.11f), mat, dir);
                SceneKit.Box("Fret", wheel, Quaternion.Euler(0f, a + 360f / n / 2f, 0f) * new Vector3(0f, 0.016f, 0.3f), new Vector3(0.003f, 0.008f, 0.1f), gold,
                    Quaternion.Euler(0f, a + 360f / n / 2f, 0f));
                SceneKit.Label(number.ToString(), wheel, wheel.TransformPoint(dir * new Vector3(0f, 0.017f, 0.388f)), dir * Quaternion.Euler(90f, 180f, 0f), 0.022f, Palette.PartyWhite);
            }
            LowPolyMeshes.Visual(cyl, "Hub", wheel, new Vector3(0f, 0.03f, 0f), new Vector3(0.22f, 0.03f, 0.22f), SetupUtil.Mat("Mirror", new Color(0.85f, 0.88f, 0.95f), 0.95f));
            LowPolyMeshes.Visual(cyl, "Spindle", wheel, new Vector3(0f, 0.08f, 0f), new Vector3(0.04f, 0.05f, 0.04f), gold);
            for (int i = 0; i < 4; i++)
                SceneKit.Box("Spoke", wheel, new Vector3(0f, 0.09f, 0f), new Vector3(0.2f, 0.008f, 0.008f), gold, Quaternion.Euler(0f, i * 45f, 0f));
            var ball = LowPolyMeshes.Visual(LowPolyMeshes.Sphere, "Ball", t, wc + new Vector3(0f, 0.1f, 0.3f), Vector3.one * 0.035f,
                SetupUtil.Mat("Ivory", new Color(0.98f, 0.97f, 0.93f), 0.8f)).transform;

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
            so.FindProperty("ball").objectReferenceValue = ball;
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
