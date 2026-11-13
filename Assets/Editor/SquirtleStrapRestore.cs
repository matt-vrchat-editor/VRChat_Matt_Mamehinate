using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SquirtleStrapRestore
{
    const string Flag = "Assets/Editor/squirtle_strap_restore.flag";
    const string PrefabPath = "Assets/Kawaii Panda Jersey/mamehinata/black_white.prefab";
    const string BonesPath = "C:/Users/mattg/Tools/squirtle_bones.json";
    const string OrigBonesPath = "C:/Users/mattg/Tools/panda_bones_original.json";
    const string StrapBlueMat = "Assets/Kawaii Panda Jersey/material/_hairgreen/strap_squirtle_blue.mat";
    const string BakedMeshPath = "Assets/Kawaii Panda Jersey/mamehinata/strap_squirtle_baked.asset";
    const string LogPath = "C:/Users/mattg/Tools/squirtle_strap_log.txt";
    // Original jersey strap submesh
    const string JerseyFbx = "Assets/Kawaii Panda Jersey/mamehinata/fbx/mamehinata_Jersey_fix.fbx";

    static readonly string[] BoneNames =
    {
        "panda_root", "panda_body", "panda_arm_L", "panda_arm_R",
        "panda_leg_L", "panda_leg_R", "panda_head"
    };

    static SquirtleStrapRestore()
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
        var strapBlue = AssetDatabase.LoadAssetAtPath<Material>(StrapBlueMat);
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        var strap = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .FirstOrDefault(r => r.name == "strap");
        if (strap == null)
            throw new System.Exception("strap SMR not found");

        // Restore original FBX strap mesh (undo broken bake)
        var fbx = AssetDatabase.LoadAllAssetsAtPath(JerseyFbx);
        Mesh origStrap = null;
        foreach (var o in fbx)
        {
            if (o is Mesh m && m.name == "strap")
            {
                origStrap = m;
                break;
            }
        }
        if (origStrap == null)
        {
            // fallback: any mesh named containing strap
            foreach (var o in fbx)
            {
                if (o is Mesh m && m.name.ToLower().Contains("strap"))
                {
                    origStrap = m;
                    break;
                }
            }
        }
        if (origStrap != null)
            strap.sharedMesh = origStrap;

        // Put bones back to ORIGINAL rest so strap skins correctly, bake a static copy
        ApplyBoneJson(root, OrigBonesPath);
        var baked = new Mesh();
        strap.BakeMesh(baked, true);
        baked.name = "strap_squirtle_baked";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(BakedMeshPath) != null)
            AssetDatabase.DeleteAsset(BakedMeshPath);
        AssetDatabase.CreateAsset(baked, BakedMeshPath);
        AssetDatabase.SaveAssets();
        baked = AssetDatabase.LoadAssetAtPath<Mesh>(BakedMeshPath);

        // Convert baked mesh from strap local into a static child that keeps the look
        // BakeMesh returns mesh in local space of the renderer.
        EnsureStaticStrap(strap, baked, strapBlue);

        // Hide skinned strap (static one is the visible connector)
        strap.enabled = false;

        // Restore Squirtle-aligned bones for the plush
        ApplyBoneJson(root, BonesPath);

        // Refresh Squirtle backpack bindposes for the Squirtle bone pose
        var backpack = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .FirstOrDefault(r => r.name == "backpack");
        if (backpack != null && backpack.sharedMesh != null)
            RecalcBindposesInPlace(backpack);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        // Scene
        foreach (var sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (var s in sceneRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (s.name != "strap") continue;
                // Prefer updating from prefab instance; also ensure static copy exists
                ApplyBoneJson(s.transform.root.gameObject, OrigBonesPath);
                var b = new Mesh();
                s.BakeMesh(b, true);
                EnsureStaticStrap(s, b, strapBlue);
                s.enabled = false;
                ApplyBoneJson(s.transform.root.gameObject, BonesPath);
                var bp = s.transform.root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .FirstOrDefault(r => r.name == "backpack");
                if (bp != null) RecalcBindposesInPlace(bp);
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        var msg = "strap baked+static blue, skinned strap hidden, squirtle bones restored";
        File.WriteAllText(LogPath, msg);
        Debug.Log("SquirtleStrapRestore: " + msg);
    }

    static void EnsureStaticStrap(SkinnedMeshRenderer strap, Mesh baked, Material blue)
    {
        const string childName = "strap_squirtle_link";
        var parent = strap.transform.parent != null ? strap.transform.parent : strap.transform;
        var existing = parent.Find(childName);
        GameObject go;
        if (existing != null) go = existing.gameObject;
        else
        {
            go = new GameObject(childName);
            go.transform.SetParent(parent, false);
        }
        go.transform.localPosition = strap.transform.localPosition;
        go.transform.localRotation = strap.transform.localRotation;
        go.transform.localScale = strap.transform.localScale;
        go.layer = strap.gameObject.layer;
        go.SetActive(true);

        var mf = go.GetComponent<MeshFilter>();
        if (mf == null) mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = baked;

        var mr = go.GetComponent<MeshRenderer>();
        if (mr == null) mr = go.AddComponent<MeshRenderer>();
        mr.enabled = true;
        if (blue != null)
        {
            // All slots light-blue so the connector reads clearly on the shell
            var mats = strap.sharedMaterials;
            if (mats != null && mats.Length > 0)
            {
                var copy = (Material[])mats.Clone();
                for (int i = 0; i < copy.Length; i++)
                {
                    // keep clear_black on slot 0 if present; paint fabric+metal blue-ish
                    if (i == 0 && mats[0] != null && mats[0].name.ToLower().Contains("clear"))
                        copy[i] = mats[0];
                    else
                        copy[i] = blue;
                }
                mr.sharedMaterials = copy;
            }
            else mr.sharedMaterial = blue;
        }
    }

    static void RecalcBindposesInPlace(SkinnedMeshRenderer smr)
    {
        var mesh = smr.sharedMesh;
        if (mesh == null || smr.bones == null) return;
        // Don't mutate shared FBX; only custom assets
        var path = AssetDatabase.GetAssetPath(mesh);
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".asset")) return;
        var bind = new Matrix4x4[smr.bones.Length];
        var l2w = smr.transform.localToWorldMatrix;
        for (int i = 0; i < smr.bones.Length; i++)
        {
            if (smr.bones[i] != null)
                bind[i] = smr.bones[i].worldToLocalMatrix * l2w;
            else
                bind[i] = Matrix4x4.identity;
        }
        mesh.bindposes = bind;
        EditorUtility.SetDirty(mesh);
    }

    static void ApplyBoneJson(GameObject anyRoot, string path)
    {
        if (!File.Exists(path)) return;
        var text = File.ReadAllText(path);
        foreach (var boneName in BoneNames)
        {
            var t = anyRoot.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(x => x.name == boneName);
            if (t == null) continue;
            var pos = ExtractVec3(text, boneName, "localPosition");
            var rot = ExtractVec4(text, boneName, "localRotation");
            if (pos.HasValue) t.localPosition = pos.Value;
            if (rot.HasValue)
                t.localRotation = new Quaternion(rot.Value.x, rot.Value.y, rot.Value.z, rot.Value.w);
        }
    }

    static Vector3? ExtractVec3(string json, string bone, string field)
    {
        int bi = json.IndexOf("\"" + bone + "\"");
        if (bi < 0) return null;
        int fi = json.IndexOf("\"" + field + "\"", bi);
        if (fi < 0) return null;
        int a = json.IndexOf('[', fi);
        int b = json.IndexOf(']', a);
        var nums = json.Substring(a + 1, b - a - 1).Split(',')
            .Select(s => float.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        return new Vector3(nums[0], nums[1], nums[2]);
    }

    static Vector4? ExtractVec4(string json, string bone, string field)
    {
        int bi = json.IndexOf("\"" + bone + "\"");
        if (bi < 0) return null;
        int fi = json.IndexOf("\"" + field + "\"", bi);
        if (fi < 0) return null;
        int a = json.IndexOf('[', fi);
        int b = json.IndexOf(']', a);
        var nums = json.Substring(a + 1, b - a - 1).Split(',')
            .Select(s => float.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        return new Vector4(nums[0], nums[1], nums[2], nums[3]);
    }
}

// touch 2026-11-13T14:21:08.3861559-03:00
