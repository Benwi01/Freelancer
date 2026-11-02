using System;
using UnityEngine;
using UnityEngine.UI;

namespace Freelancer
{
    /// <summary>
    /// Main menu character customizer: a turning 3D preview plus "&lt; option &gt;" rows for colour,
    /// hat, eyes, mouth and outfit. Choices are saved on this PC and used when you join a gig.
    /// </summary>
    public class CustomizePanel : MonoBehaviour
    {
        static Color PreviewBackground => MainMenuUI.CardColor; // character stands right on the card

        PlayerLook m_Look;
        CharacterPreview m_Preview;
        readonly System.Collections.Generic.List<Action> m_Refreshers = new();

        public void Build(Transform canvas, GameObject playerPrefab, Vector2 anchor, Vector2 offset)
        {
            m_Look = PlayerLook.LoadLocal();
            m_Look.SaveLocal(); // keep the random first-time colour stable

            var panel = UiFactory.CreatePanel("CustomizePanel", canvas, MainMenuUI.CardColor);
            UiFactory.Round(panel, 24f);
            UiFactory.Place(panel.rectTransform, anchor, new Vector2(560, 760), offset);

            var column = UiFactory.CreateColumn("Rows", panel.transform, 8);
            UiFactory.Stretch(column);
            var pad = column.GetComponent<VerticalLayoutGroup>();
            pad.padding = new RectOffset(28, 28, 22, 26);

            var header = UiFactory.CreateText("Header", column, "YOUR FREELANCER", 24, TextAnchor.MiddleLeft);
            header.fontStyle = FontStyle.Bold;
            header.color = MainMenuUI.Orange;
            UiFactory.SetPreferredHeight(header, 30);

            var previewImage = UiFactory.CreateRect("Preview", column).gameObject.AddComponent<RawImage>();
            UiFactory.SetPreferredHeight(previewImage, 340);
            if (playerPrefab != null)
            {
                m_Preview = new GameObject("CharacterPreview").AddComponent<CharacterPreview>();
                m_Preview.transform.SetParent(transform, false);
                m_Preview.Init(playerPrefab, PreviewBackground);
                previewImage.texture = m_Preview.Texture;
            }
            else previewImage.color = PreviewBackground;

            AddRow(column, "Colour", PlayerLook.ColorNames, () => m_Look.Color, v => m_Look.Color = v);
            AddRow(column, "Hat", PlayerLook.HatNames, () => m_Look.Hat, v => m_Look.Hat = v);
            AddRow(column, "Eyes", PlayerLook.EyeNames, () => m_Look.Eyes, v => m_Look.Eyes = v);
            AddRow(column, "Mouth", PlayerLook.MouthNames, () => m_Look.Mouth, v => m_Look.Mouth = v);
            AddRow(column, "Outfit", PlayerLook.OutfitNames, () => m_Look.Outfit, v => m_Look.Outfit = v);

            var random = UiFactory.Solid(UiFactory.CreateButton("Randomize", column, "Surprise me", Randomize), MainMenuUI.Navy, Color.white);
            random.GetComponentInChildren<Text>().fontSize = 26;
            UiFactory.SetPreferredHeight(random, 56);

            Changed();
        }

        void AddRow(Transform parent, string label, string[] names, Func<byte> get, Action<byte> set)
        {
            var row = UiFactory.CreateRect(label + "Row", parent);
            UiFactory.SetPreferredHeight(row, 52);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;

            void Step(int dir)
            {
                set((byte)((get() + dir + names.Length) % names.Length));
                Changed();
            }

            var caption = UiFactory.CreateText("Label", row, label.ToUpperInvariant(), 20, TextAnchor.MiddleLeft);
            caption.color = new Color(MainMenuUI.Navy.r, MainMenuUI.Navy.g, MainMenuUI.Navy.b, 0.55f);
            Width(caption, 110);
            var prev = UiFactory.Solid(UiFactory.CreateButton("Prev", row, "<", () => Step(-1)), MainMenuUI.Stone, MainMenuUI.Navy);
            Width(prev, 52);
            var text = UiFactory.CreateText("Value", row, "", 28);
            text.fontStyle = FontStyle.Bold;
            text.color = MainMenuUI.Navy;
            Width(text, 0, flexible: true);
            var next = UiFactory.Solid(UiFactory.CreateButton("Next", row, ">", () => Step(1)), MainMenuUI.Stone, MainMenuUI.Navy);
            Width(next, 52);

            m_Refreshers.Add(() => text.text = names[get() % names.Length]);
        }

        static void Width(Component c, float width, bool flexible = false)
        {
            var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.flexibleWidth = flexible ? 1f : 0f;
        }

        void Randomize()
        {
            m_Look.Color = (byte)UnityEngine.Random.Range(0, PlayerLook.Colors.Length);
            m_Look.Hat = (byte)UnityEngine.Random.Range(0, PlayerLook.HatNames.Length);
            m_Look.Eyes = (byte)UnityEngine.Random.Range(0, PlayerLook.EyeNames.Length);
            m_Look.Mouth = (byte)UnityEngine.Random.Range(0, PlayerLook.MouthNames.Length);
            m_Look.Outfit = (byte)UnityEngine.Random.Range(0, PlayerLook.OutfitNames.Length);
            Changed();
        }

        void Changed()
        {
            m_Look.SaveLocal();
            foreach (var refresh in m_Refreshers) refresh();
            if (m_Preview != null) m_Preview.Show(m_Look);
        }
    }
}
