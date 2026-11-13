using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Disables leftover panda/bear backpack meshes so only Squirtle remains.
/// backpack_straps accidentally included the blue bear body from the jersey backpack.
/// </summary>
[InitializeOnLoad]
public static class SquirtleRemoveBear
{
    const string Flag = "Assets/Editor/squirtle_remove_bear.flag";
    const string PrefabPath = "Assets/Kawaii Panda Jersey/mamehinata/black_white.prefab";
    const string LogPath = "C:/Users/mattg/Tools/squirtle_remove_bear_log.txt";

    static readonly string[] HideNames =
    {
        // backpack_straps is strap-only now — do NOT hide it
        "strap_squirtle_link",  // baked static connector from earlier attempt
        "panda_pouch",
    };

    static SquirtleRemoveBear()
    {
        EditorApplication.delayCall += TryRun;
    }

    static void TryRun()
    {
        if (!File.Exists(Flag)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryRun;
            return;
        }
        try
        {
            Run();
            if (File.Exists(Flag)) File.Delete(Flag);
            var meta = Flag + ".meta";
            if (File.Exists(meta)) File.Delete(meta);
        }
        catch (System.Exception ex)
        {
            File.WriteAllText(LogPath, ex.ToString());
            Debug.LogError(ex);
        }
    }

    static void Run()
    {
        int n = 0;
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        n += HideBearPieces(root);
        // Also disable skinned "strap" if it still shows bear hardware
        var strap = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .FirstOrDefault(r => r.name == "strap");
        if (strap != null)
        {
            strap.enabled = false;
            strap.gameObject.SetActive(false);
            n++;
        }
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        foreach (var sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects())
            n += HideBearPieces(sceneRoot);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        var msg = "hidden bear pieces count=" + n;
        File.WriteAllText(LogPath, msg);
        Debug.Log("SquirtleRemoveBear: " + msg);
    }

    static int HideBearPieces(GameObject root)
    {
        int n = 0;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!HideNames.Contains(t.name)) continue;
            t.gameObject.SetActive(false);
            foreach (var r in t.GetComponents<Renderer>())
                r.enabled = false;
            n++;
        }
        return n;
    }
}
