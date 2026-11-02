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

            new GameObject("SessionHud").AddComponent<SessionHud>();

            EditorSceneManager.SaveScene(scene, SetupUtil.GigScenePath);
            Debug.Log("[Freelancer] Built " + SetupUtil.GigScenePath);
        }
    }
}
