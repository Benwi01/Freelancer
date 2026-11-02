using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Freelancer.EditorTools
{
    /// <summary>
    /// Menu items that (re)build the project's scenes and prefabs from code.
    /// Every item is safe to run again: it overwrites what it generated last time.
    /// </summary>
    static class SetupMenu
    {
        public const string PrefabListPath = SetupUtil.PrefabsDir + "/FreelancerNetworkPrefabs.asset";

        [MenuItem("Freelancer/Setup/Build All", priority = 0)]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SetupUtil.EnsureFolders();
            ImportTextResources();
            PrefabBuilder.BuildAllPrefabs();
            BuildMainMenuScene();
            HubSceneBuilder.BuildHubScene();
            GigSceneBuilder.BuildCakeDeliveryScene();
            ConfigureBuildSettings();
            EditorSceneManager.OpenScene(SetupUtil.MenuScenePath);
            Debug.Log("[Freelancer] Build All finished. Open MainMenu and press Play.");
        }

        /// <summary>TextMesh Pro's fonts and shaders (one-time import into Assets/TextMesh Pro).</summary>
        [MenuItem("Freelancer/Setup/Import Text Resources", priority = 10)]
        public static void ImportTextResources()
        {
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            {
                Debug.Log("[Freelancer] TextMesh Pro resources already imported.");
                return;
            }
            TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.Refresh();
        }

        [MenuItem("Freelancer/Setup/Build Main Menu Scene", priority = 20)]
        public static void BuildMainMenuScene()
        {
            SetupUtil.EnsureFolders();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.13f, 0.11f, 0.2f);
            }

            // NetworkRoot: NetworkManager + transport + session logic, kept across scenes.
            var root = new GameObject("NetworkRoot");
            root.AddComponent<NetworkRootGuard>();
            var nm = root.AddComponent<NetworkManager>();
            var transport = root.AddComponent<UnityTransport>();
            if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.EnableSceneManagement = true;
            nm.NetworkConfig.ConnectionApproval = false;
            nm.NetworkConfig.PlayerPrefab = null; // players are spawned by the gig, not on connect
            nm.NetworkConfig.Prefabs.NetworkPrefabsLists = new List<NetworkPrefabsList> { GetOrCreatePrefabList() };
            var session = root.AddComponent<SessionManager>();
            var sessionSo = new SerializedObject(session);
            sessionSo.FindProperty("crewStatePrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<NetworkObject>(PrefabBuilder.CrewStatePath);
            sessionSo.ApplyModifiedPropertiesWithoutUndo();

            var menu = new GameObject("MainMenu").AddComponent<MainMenuUI>();
            var menuSo = new SerializedObject(menu);
            menuSo.FindProperty("playerPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabBuilder.PlayerPath);
            menuSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, SetupUtil.MenuScenePath);
            Debug.Log("[Freelancer] Built " + SetupUtil.MenuScenePath);
        }

        [MenuItem("Freelancer/Setup/Configure Build Settings", priority = 40)]
        public static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SetupUtil.MenuScenePath, true),
                new EditorBuildSettingsScene(SetupUtil.HubScenePath, true),
                new EditorBuildSettingsScene(SetupUtil.GigScenePath, true),
            };
            Debug.Log("[Freelancer] Build settings: MainMenu, CakeDelivery.");
        }

        public static NetworkPrefabsList GetOrCreatePrefabList()
        {
            SetupUtil.EnsureFolder(SetupUtil.PrefabsDir);
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(PrefabListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, PrefabListPath);
            }
            return list;
        }

        public static void RegisterNetworkPrefab(GameObject prefab)
        {
            var list = GetOrCreatePrefabList();
            if (!list.Contains(prefab))
                list.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(list);
        }
    }
}
