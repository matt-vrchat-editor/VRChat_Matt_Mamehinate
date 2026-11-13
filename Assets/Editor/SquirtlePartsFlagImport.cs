using UnityEditor;

public class SquirtlePartsFlagImport : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] movedTo, string[] movedFrom)
    {
        foreach (var p in imported)
        {
            if (p != null && p.EndsWith("squirtle_parts_fix.flag"))
            {
                EditorApplication.delayCall += SquirtlePartsFix.TryRun;
                break;
            }
        }
    }
}

// touch 2026-11-13T14:21:08.3811111-03:00
