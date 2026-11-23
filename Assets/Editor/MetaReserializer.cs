using System.Collections.Generic;
using UnityEditor;

public static class MetaReserializer
{
    [MenuItem("Tools/Reserialize Selected Meta")]
    private static void ReserializeSelected()
    {
        var paths = new HashSet<string>();

        foreach (var guid in Selection.assetGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(path))
            {
                foreach (var childGuid in AssetDatabase.FindAssets(string.Empty, new[] { path }))
                {
                    paths.Add(AssetDatabase.GUIDToAssetPath(childGuid));
                }
            }
            else if (!string.IsNullOrEmpty(path))
            {
                paths.Add(path);
            }
        }

        if (paths.Count == 0)
        {
            return;
        }

        AssetDatabase.ForceReserializeAssets(paths, ForceReserializeAssetsOptions.ReserializeMetadata);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}