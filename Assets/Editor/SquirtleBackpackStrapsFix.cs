using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Restores the jersey backpack SHOULDER STRAPS (blue submesh of original backpack)
/// that disappeared when backpack mesh was replaced by Squirtle-only.
/// Adds sibling SkinnedMeshRenderer "backpack_straps" painted light-blue.
/// </summary>
[InitializeOnLoad]
public static class SquirtleBackpackStrapsFix
{
    const string Flag = "Assets/Editor/squirtle_backpack_straps.flag";
    const string PrefabPath = "Assets/Kawaii Panda Jersey/mamehinata/black_white.prefab";
    // Strap-only extract (no panda_* body) from Blender extract_straps_only.py
    const string BinPath = "C:/Users/mattg/Tools/backpack_straps_only.bin";
    const string MeshPath = "Assets/Kawaii Panda Jersey/mamehinata/backpack_straps.asset";
    const string StrapBlueMat = "Assets/Kawaii Panda Jersey/material/_hairgreen/strap_squirtle_blue.mat";
    const string LogPath = "C:/Users/mattg/Tools/backpack_straps_log.txt";

    static SquirtleBackpackStrapsFix()
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
        var blue = AssetDatabase.LoadAssetAtPath<Material>(StrapBlueMat);
        if (blue == null) throw new System.Exception("missing " + StrapBlueMat);

        Vector3[] verts, norms;
        Vector2[] uvs;
        List<List<KeyValuePair<string, float>>> weights;
        int[] tris;
        ReadBin(out verts, out norms, out uvs, out weights, out tris);

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        var backpack = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .First(r => r.name == "backpack");
        var bones = backpack.bones;
        var boneIndex = new Dictionary<string, int>();
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] != null && !boneIndex.ContainsKey(bones[i].name))
                boneIndex[bones[i].name] = i;
        }

        var bw = new BoneWeight[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            var w = new BoneWeight();
            var list = weights[i];
            for (int k = 0; k < list.Count && k < 4; k++)
            {
                int bi;
                if (!boneIndex.TryGetValue(list[k].Key, out bi)) bi = 0;
                float wv = list[k].Value;
                if (k == 0) { w.boneIndex0 = bi; w.weight0 = wv; }
                else if (k == 1) { w.boneIndex1 = bi; w.weight1 = wv; }
                else if (k == 2) { w.boneIndex2 = bi; w.weight2 = wv; }
                else { w.boneIndex3 = bi; w.weight3 = wv; }
            }
            // normalize
            float s = w.weight0 + w.weight1 + w.weight2 + w.weight3;
            if (s > 1e-5f)
            {
                w.weight0 /= s; w.weight1 /= s; w.weight2 /= s; w.weight3 /= s;
            }
            else { w.boneIndex0 = 0; w.weight0 = 1; }
            bw[i] = w;
        }

        var mesh = new Mesh();
        mesh.name = "backpack_straps";
        mesh.indexFormat = verts.Length > 65535
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = verts;
        mesh.normals = norms;
        mesh.uv = uvs;
        mesh.boneWeights = bw;
        mesh.triangles = tris;

        // bindposes for CURRENT bone rest (Squirtle-aligned panda_* already applied)
        var bind = new Matrix4x4[bones.Length];
        // Use backpack transform as mesh space (same as original)
        var l2w = backpack.transform.localToWorldMatrix;
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] != null)
                bind[i] = bones[i].worldToLocalMatrix * l2w;
            else
                bind[i] = Matrix4x4.identity;
        }
        mesh.bindposes = bind;
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();

        if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath) != null)
            AssetDatabase.DeleteAsset(MeshPath);
        AssetDatabase.CreateAsset(mesh, MeshPath);
        AssetDatabase.SaveAssets();
        var saved = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);

        // Remove mistaken static strap_squirtle_link if present
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "strap_squirtle_link" || t.name == "strap_squirtle_link")
                Object.DestroyImmediate(t.gameObject);
        }
        // Also leave user's deleted "strap" alone — if missing, fine; if present keep disabled
        var oldStrap = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .FirstOrDefault(r => r.name == "strap");
        if (oldStrap != null) oldStrap.enabled = false;

        var strapsGo = EnsureStrapsObject(root, backpack);
        var smr = strapsGo.GetComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = saved;
        smr.bones = bones;
        smr.rootBone = backpack.rootBone;
        smr.sharedMaterials = new Material[] { blue };
        smr.enabled = true;
        smr.updateWhenOffscreen = true;
        strapsGo.SetActive(true);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        // Scene instances
        foreach (var sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            var bp = sceneRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .FirstOrDefault(r => r.name == "backpack");
            if (bp == null) continue;
            foreach (var t in bp.transform.root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "strap_squirtle_link")
                    Object.DestroyImmediate(t.gameObject);
            }
            var go = EnsureStrapsObject(bp.transform.root.gameObject, bp);
            var s = go.GetComponent<SkinnedMeshRenderer>();
            s.sharedMesh = saved;
            s.bones = bp.bones;
            s.rootBone = bp.rootBone;
            s.sharedMaterials = new Material[] { blue };
            s.enabled = true;
            s.updateWhenOffscreen = true;
            go.SetActive(true);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        var log = "backpack_straps verts " + verts.Length + " tris " + (tris.Length / 3)
                  + " bounds " + saved.bounds;
        File.WriteAllText(LogPath, log);
        Debug.Log("SquirtleBackpackStrapsFix: " + log);
    }

    static GameObject EnsureStrapsObject(GameObject anyRoot, SkinnedMeshRenderer backpack)
    {
        const string name = "backpack_straps";
        var existing = anyRoot.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name == name);
        GameObject go;
        if (existing != null) go = existing.gameObject;
        else
        {
            go = new GameObject(name);
            go.transform.SetParent(backpack.transform.parent, false);
        }
        go.transform.localPosition = backpack.transform.localPosition;
        go.transform.localRotation = backpack.transform.localRotation;
        go.transform.localScale = backpack.transform.localScale;
        go.layer = backpack.gameObject.layer;

        var smr = go.GetComponent<SkinnedMeshRenderer>();
        if (smr == null) smr = go.AddComponent<SkinnedMeshRenderer>();
        return go;
    }

    static void ReadBin(out Vector3[] verts, out Vector3[] norms, out Vector2[] uvs,
        out List<List<KeyValuePair<string, float>>> weights, out int[] tris)
    {
        using (var br = new BinaryReader(File.OpenRead(BinPath)))
        {
            int n = br.ReadInt32();
            verts = new Vector3[n];
            norms = new Vector3[n];
            uvs = new Vector2[n];
            weights = new List<List<KeyValuePair<string, float>>>(n);
            for (int i = 0; i < n; i++)
                verts[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            for (int i = 0; i < n; i++)
                norms[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            for (int i = 0; i < n; i++)
                uvs[i] = new Vector2(br.ReadSingle(), br.ReadSingle());
            for (int i = 0; i < n; i++)
            {
                int wn = br.ReadByte();
                var list = new List<KeyValuePair<string, float>>(wn);
                for (int k = 0; k < wn; k++)
                {
                    int nameLen = br.ReadInt32();
                    string bn = Encoding.UTF8.GetString(br.ReadBytes(nameLen));
                    float w = br.ReadSingle();
                    list.Add(new KeyValuePair<string, float>(bn, w));
                }
                weights.Add(list);
            }
            int tc = br.ReadInt32();
            tris = new int[tc];
            for (int i = 0; i < tc; i++) tris[i] = br.ReadInt32();
        }
    }
}

// touch 2026-11-13T14:21:08.3660523-03:00
