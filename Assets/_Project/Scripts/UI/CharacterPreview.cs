using UnityEngine;

namespace Freelancer
{
    /// <summary>
    /// Renders a (non-networked) copy of the player prefab into a texture for the menu, slowly
    /// turning so you can see the outfit from different sides.
    /// </summary>
    public class CharacterPreview : MonoBehaviour
    {
        static readonly Vector3 k_Stage = new(0f, -500f, 0f); // far away from the menu scene

        GameObject m_Instance;
        PlayerAppearance m_Appearance;
        Camera m_Camera;
        float m_Time;

        public RenderTexture Texture { get; private set; }

        public void Init(GameObject playerPrefab, Color background)
        {
            Texture = new RenderTexture(1040, 600, 24) { antiAliasing = 4, name = "CharacterPreview" };

            // A plain copy: it is never spawned on the network, so its network scripts stay idle.
            m_Instance = Instantiate(playerPrefab, k_Stage, Quaternion.Euler(0f, 180f, 0f));
            m_Instance.name = "PreviewCharacter";
            foreach (var cam in m_Instance.GetComponentsInChildren<Camera>(true)) cam.gameObject.SetActive(false);
            m_Appearance = m_Instance.GetComponent<PlayerAppearance>();

            var camGo = new GameObject("PreviewCamera", typeof(Camera));
            camGo.transform.SetParent(transform, false);
            camGo.transform.position = k_Stage + new Vector3(0f, 1.15f, -3.3f);
            camGo.transform.LookAt(k_Stage + new Vector3(0f, 0.92f, 0f));
            m_Camera = camGo.GetComponent<Camera>();
            m_Camera.clearFlags = CameraClearFlags.SolidColor;
            m_Camera.backgroundColor = background;
            m_Camera.fieldOfView = 34f;
            m_Camera.nearClipPlane = 0.1f;
            m_Camera.farClipPlane = 20f;
            m_Camera.targetTexture = Texture;

            var lightGo = new GameObject("PreviewLight", typeof(Light));
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.rotation = Quaternion.Euler(35f, 150f, 0f);
            var light = lightGo.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.9f;
            light.shadows = LightShadows.None;
        }

        public void Show(PlayerLook look)
        {
            if (m_Appearance != null) m_Appearance.Apply(look);
        }

        void Update()
        {
            if (m_Instance == null) return;
            // Turn gently back and forth so the front stays mostly in view.
            m_Time += Time.deltaTime;
            float yaw = 180f + Mathf.Sin(m_Time * 0.6f) * 50f;
            m_Instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        void OnDestroy()
        {
            if (m_Instance != null) Destroy(m_Instance);
            if (m_Camera != null) m_Camera.targetTexture = null;
            if (Texture != null) Texture.Release();
        }
    }
}
