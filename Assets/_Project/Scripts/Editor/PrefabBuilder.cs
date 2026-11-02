using UnityEditor;
using UnityEngine;

namespace Freelancer.EditorTools
{
    /// <summary>Builds the networked prefabs (player, cake, props) from primitives.</summary>
    static class PrefabBuilder
    {
        [MenuItem("Freelancer/Setup/Build Prefabs", priority = 21)]
        public static void BuildAllPrefabs()
        {
            SetupUtil.EnsureFolders();
            SetupMenu.GetOrCreatePrefabList();
            AssetDatabase.SaveAssets();
            Debug.Log("[Freelancer] Prefabs built.");
        }
    }
}
