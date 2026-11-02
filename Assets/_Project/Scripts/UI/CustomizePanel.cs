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
        static readonly Color k_PreviewBackground = new(0.2f, 0.17f, 0.3f);

        PlayerLook m_Look;
        CharacterPreview m_Preview;
        readonly System.Collections.Generic.List<Action> m_Refreshers = new();

        public void Build(Transform canvas, GameObject playerPrefab, Vector2 offset)
        {
            m_Look = PlayerLook.LoadLocal();
            m_Look.SaveLocal(); // keep the random first-time colour stable

            var panel = UiFactory.CreatePanel("CustomizePanel", canvas, UiFactory.PanelColor);
            UiFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(560, 680), offset);

            var column = UiFactory.CreateColumn("Rows", panel.transform, 8);
            UiFactory.Stretch(column);
            var pad = column.GetComponent<VerticalLayoutGroup>();
            pad.padding = new RectOffset(20, 20, 16, 20);

            var header = UiFactory.CreateText("Header", column, "YOUR FREELANCER", 34);
            header.fontStyle = FontStyle.Bold;
            UiFactory.SetPreferredHeight(header, 44);

            var previewImage = UiFactory.CreateRect("Preview", column).gameObject.AddComponent<RawImage>();
            UiFactory.SetPreferredHeight(previewImage, 300);
            if (playerPrefab != null)
            {
                m_Preview = new GameObject("CharacterPreview").AddComponent<CharacterPreview>();
                m_Preview.transform.SetParent(transform, false);
                m_Preview.Init(playerPrefab, k_PreviewBackground);
                previewImage.texture = m_Preview.Texture;
            }
            else previewImage.color = k_PreviewBackground;

            AddRow(column, "Colour", PlayerLook.ColorNames, () => m_Look.Color, v => m_Look.Color = v);
            AddRow(column, "Hat", PlayerLook.HatNames, () => m_Look.Hat, v => m_Look.Hat = v);
            AddRow(column, "Eyes", PlayerLook.EyeNames, () => m_Look.Eyes, v => m_Look.Eyes = v);
            AddRow(column, "Mouth", PlayerLook.MouthNames, () => m_Look.Mouth, v => m_Look.Mouth = v);
            AddRow(column, "Outfit", PlayerLook.OutfitNames, () => m_Look.Outfit, v => m_Look.Outfit = v);

            var random = UiFactory.CreateButton("Randomize", column, "Surprise me", Randomize);
            random.GetComponentInChildren<Text>().fontSize = 28;
            UiFactory.SetPreferredHeight(random, 52);

            Changed();
        }

        void AddRow(Transform parent, string label, string[] names, Func<byte> get, Action<byte> set)
        {
            var row = UiFactory.CreateRect(label + "Row", parent);
            UiFactory.SetPreferredHeight(row, 50);
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

            var prev = UiFactory.CreateButton("Prev", row, "<", () => Step(-1));
            Width(prev, 60);
            var text = UiFactory.CreateText("Value", row, "", 28);
            Width(text, 0, flexible: true);
            var next = UiFactory.CreateButton("Next", row, ">", () => Step(1));
            Width(next, 60);

            m_Refreshers.Add(() => text.text = $"{label}:  <b>{names[get() % names.Length]}</b>");
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
