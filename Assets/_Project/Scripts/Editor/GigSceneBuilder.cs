using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Freelancer.EditorTools
{
    /// <summary>Builds the Cake Delivery gig scene: greybox level, spawn points and gig objects.</summary>
    static class GigSceneBuilder
    {
        [MenuItem("Freelancer/Setup/Build Cake Delivery Scene", priority = 22)]
        public static void BuildCakeDeliveryScene()
        {
            SetupUtil.EnsureFolders();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0, 8, -14);
                cam.transform.rotation = Quaternion.Euler(25, 0, 0);
            }

            var level = new GameObject("Level").transform;
            SetupUtil.Block("Ground", level, new Vector3(0, -0.5f, 0), new Vector3(60, 1, 60),
                SetupUtil.Mat("Asphalt", new Color(0.32f, 0.33f, 0.36f)));

            AddPlayerSpawner(new Vector3(0, 0, -6), 0f);
            for (int i = 0; i < 4; i++)
            {
                AddPropSpawner(PrefabBuilder.BoxPath, new Vector3(-3 + i * 2f, 0.5f, 0));
                AddPropSpawner(PrefabBuilder.BucketPath, new Vector3(-3 + i * 2f, 0.5f, 2));
            }

            new GameObject("SessionHud").AddComponent<SessionHud>();

            EditorSceneManager.SaveScene(scene, SetupUtil.GigScenePath);
            Debug.Log("[Freelancer] Built " + SetupUtil.GigScenePath);
        }

        /// <summary>Four spawn points in a row around <paramref name="center"/>, facing yaw.</summary>
        static void AddPlayerSpawner(Vector3 center, float yaw)
        {
            var go = new GameObject("PlayerSpawner");
            go.transform.position = center;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            for (int i = 0; i < SessionManager.MaxPlayers; i++)
                SetupUtil.Empty("Spawn" + (i + 1), go.transform, new Vector3(-1.8f + i * 1.2f, 0.05f, 0));

            var spawner = go.AddComponent<PlayerSpawner>();
            var so = new SerializedObject(spawner);
            so.FindProperty("playerPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<NetworkObject>(PrefabBuilder.PlayerPath);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddPropSpawner(string prefabPath, Vector3 position, float yaw = 0f, string name = null)
        {
            var go = new GameObject(name ?? "Spawn_" + System.IO.Path.GetFileNameWithoutExtension(prefabPath));
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var spawner = go.AddComponent<ServerSpawner>();
            var so = new SerializedObject(spawner);
            so.FindProperty("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<NetworkObject>(prefabPath);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
