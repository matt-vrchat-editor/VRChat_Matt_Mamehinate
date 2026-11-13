using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SquirtlePartsFix
{
    const string Flag = "Assets/Editor/squirtle_parts_fix.flag";
    const string PrefabPath = "Assets/Kawaii Panda Jersey/mamehinata/black_white.prefab";
    const string MeshPath = "Assets/Kawaii Panda Jersey/mamehinata/backpack_squirtle.asset";
    const string BinPath = "C:/Users/mattg/Tools/squirtle_parts.bin";
    const string BonesPath = "C:/Users/mattg/Tools/squirtle_bones.json";
    const string MatFolder = "Assets/Squirtle_v01/PartsMats";
    const string BaseTex = "Assets/Squirtle_v01/Texture/Squirtle_BaseColor_1001.png";
    const string NrmTex = "Assets/Squirtle_v01/Texture/Squirtle_Normal_1001.png";
    const string RoughTex = "Assets/Squirtle_v01/Texture/Squirtle_Roughness_1001.png";
    const string TemplateMat = "Assets/Squirtle_v01/Squirtle_backpack.mat";
    const string StrapBlueMat = "Assets/Kawaii Panda Jersey/material/_hairgreen/strap_squirtle_blue.mat";
    const string LogPath = "C:/Users/mattg/Tools/squirtle_parts_log.txt";

    // eye covers = solid white; tail clips into avatar back
    static readonly string[] DropParts = { "eye_L_cover", "eye_R_cover", "tail" };

    static SquirtlePartsFix()
    {
        EditorApplication.delayCall += TryRun;
    }

    internal static void TryRun()
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
            // keep flag so a later refresh can retry once the prefab is valid again
        }
    }

    static void Run()
    {
        if (!Directory.Exists(MatFolder))
        {
            Directory.CreateDirectory(MatFolder);
            AssetDatabase.Refresh();
        }

        var baseTex = AssetDatabase.LoadAssetAtPath<Texture>(BaseTex);
        var nrmTex = AssetDatabase.LoadAssetAtPath<Texture>(NrmTex);
        var roughTex = AssetDatabase.LoadAssetAtPath<Texture>(RoughTex);
        var template = AssetDatabase.LoadAssetAtPath<Material>(TemplateMat);

        int vertCount;
        Vector3[] verts, norms;
        Vector2[] uvs;
        BoneWeight[] weights;
        List<string> parts;
        List<int[]> partTris;
        ReadBin(out vertCount, out verts, out norms, out uvs, out weights, out parts, out partTris);

        // One submesh + material per part (body/hands/feet/face/eyes…).
        var keepIdx = new List<int>();
        for (int i = 0; i < parts.Count; i++)
        {
            if (DropParts.Contains(parts[i])) continue;
            keepIdx.Add(i);
        }
        var useParts = keepIdx.Select(i => parts[i]).ToList();
        var useTris = keepIdx.Select(i => partTris[i]).ToList();

        var mats = new Material[useParts.Count];
        for (int i = 0; i < useParts.Count; i++)
            mats[i] = EnsurePartMaterial(useParts[i], template, baseTex, nrmTex, roughTex);

        var mesh = new Mesh();
        mesh.name = "backpack_squirtle";
        mesh.indexFormat = vertCount > 65535
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = verts;
        mesh.normals = norms;
        mesh.uv = uvs;
        mesh.boneWeights = weights;
        mesh.subMeshCount = useParts.Count;
        for (int i = 0; i < useParts.Count; i++)
            mesh.SetTriangles(useTris[i], i);

        // Temporary bindposes; recalculated after bone move
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        var smr = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "backpack");
        var bones = smr.bones;
        ApplyBoneLocals(root, bones);
        // bindposes from current rest pose
        var bind = new Matrix4x4[bones.Length];
        var smrL2W = smr.transform.localToWorldMatrix;
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] != null)
                bind[i] = bones[i].worldToLocalMatrix * smrL2W;
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

        smr.sharedMesh = saved;
        smr.sharedMaterials = mats;

        var pouch = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "panda_pouch");
        if (pouch != null) pouch.gameObject.SetActive(false);

        // Moving panda_* bones broke the jersey strap bindposes (shared skeleton).
        // Rebuild strap bindposes at the new rest pose and paint straps light-blue.
        var strapBlue = AssetDatabase.LoadAssetAtPath<Material>(StrapBlueMat);
        var strapSmr = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == "strap");
        Mesh strapMesh = strapSmr != null ? BakeStrapBindposes(strapSmr) : null;
        int strapFixed = FixStrapRenderers(root, strapBlue, strapMesh);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        // Scene instances
        foreach (var r in Object.FindObjectsOfType<SkinnedMeshRenderer>(true))
        {
            if (r.name == "backpack")
            {
                ApplyBoneLocals(r.transform.root.gameObject, r.bones);
                r.sharedMesh = saved;
                r.sharedMaterials = mats;
            }
        }
        foreach (var sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects())
            strapFixed += FixStrapRenderers(sceneRoot, strapBlue, strapMesh);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        var log = "parts " + string.Join(",", useParts)
            + " verts " + vertCount
            + " tris " + useTris.Sum(t => t.Length / 3)
            + " mats " + mats.Length
            + " straps " + strapFixed
            + " bounds " + saved.bounds;
        File.WriteAllText(LogPath, log);
        Debug.Log("Squirtle parts fix: " + log);
    }

    const string StrapMeshPath = "Assets/Kawaii Panda Jersey/mamehinata/strap_squirtle.asset";

    static Mesh BakeStrapBindposes(SkinnedMeshRenderer r)
    {
        if (r.sharedMesh == null || r.bones == null || r.bones.Length == 0) return null;
        var mesh = Object.Instantiate(r.sharedMesh);
        mesh.name = "strap_squirtle";
        var bind = new Matrix4x4[r.bones.Length];
        var l2w = r.transform.localToWorldMatrix;
        for (int i = 0; i < r.bones.Length; i++)
        {
            if (r.bones[i] != null)
                bind[i] = r.bones[i].worldToLocalMatrix * l2w;
            else
                bind[i] = Matrix4x4.identity;
        }
        mesh.bindposes = bind;
        if (AssetDatabase.LoadAssetAtPath<Mesh>(StrapMeshPath) != null)
            AssetDatabase.DeleteAsset(StrapMeshPath);
        AssetDatabase.CreateAsset(mesh, StrapMeshPath);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<Mesh>(StrapMeshPath);
    }

    static int FixStrapRenderers(GameObject root, Material strapBlue, Mesh bakedStrapMesh = null)
    {
        int n = 0;
        foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (r == null || r.name != "strap") continue;
            r.gameObject.SetActive(true);
            r.enabled = true;
            var mesh = bakedStrapMesh;
            if (mesh == null)
                mesh = BakeStrapBindposes(r);
            if (mesh != null)
                r.sharedMesh = mesh;
            if (strapBlue != null)
            {
                var mats = r.sharedMaterials;
                // Slot 1 is the fabric (was panda.mat); keep clear_black + silver
                if (mats != null && mats.Length >= 2)
                {
                    mats[1] = strapBlue;
                    r.sharedMaterials = mats;
                }
                else
                    r.sharedMaterial = strapBlue;
            }
            n++;
        }
        return n;
    }

    static Material EnsurePartMaterial(string part, Material template, Texture baseTex, Texture nrmTex, Texture roughTex)
    {
        string path = MatFolder + "/sq_" + part + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = template != null ? new Material(template) : new Material(Shader.Find("Standard"));
            mat.name = "sq_" + part;
            AssetDatabase.CreateAsset(mat, path);
            mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        bool cover = part.Contains("cover");
        mat.SetTexture("_MainTex", baseTex);
        mat.SetTexture("_BaseMap", baseTex);
        mat.SetTexture("_BaseColorMap", baseTex);
        mat.SetTexture("_BumpMap", nrmTex);
        if (mat.HasProperty("_SmoothnessTex")) mat.SetTexture("_SmoothnessTex", roughTex);
        // Keep Squirtle blue even: weak normals + soft toon shadow (avoids blotchy face).
        if (mat.HasProperty("_UseBumpMap")) mat.SetFloat("_UseBumpMap", 1);
        if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", cover ? 0.05f : 0.08f);
        if (mat.HasProperty("_UseReflection")) mat.SetFloat("_UseReflection", 0);
        if (mat.HasProperty("_ShadowStrength")) mat.SetFloat("_ShadowStrength", 0.22f);
        if (mat.HasProperty("_ShadowBlur")) mat.SetFloat("_ShadowBlur", 0.5f);
        if (mat.HasProperty("_ShadowBorder")) mat.SetFloat("_ShadowBorder", 0.65f);
        if (mat.HasProperty("_AsUnlit")) mat.SetFloat("_AsUnlit", 0.18f);
        if (mat.HasProperty("_LightMinLimit")) mat.SetFloat("_LightMinLimit", 0.25f);
        if (mat.HasProperty("_RimNormalStrength")) mat.SetFloat("_RimNormalStrength", 0.1f);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_ShadowColor")) mat.SetColor("_ShadowColor", new Color(0.92f, 0.94f, 0.98f, 1f));

        if (cover)
        {
            // Cutout: keep iris/pupil pixels from the cover UV island, discard empty atlas.
            if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.5f);
            if (mat.HasProperty("_TransparentMode")) mat.SetFloat("_TransparentMode", 1); // cutout
            if (mat.HasProperty("_UseClipping")) mat.SetFloat("_UseClipping", 1);
            if (mat.HasProperty("_ClippingMode")) mat.SetFloat("_ClippingMode", 0);
            if (mat.HasProperty("_CutoffMode")) mat.SetFloat("_CutoffMode", 1);
            mat.renderQueue = 2450;
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.EnableKeyword("_ALPHATEST_ON");
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void ApplyBoneLocals(GameObject anyRoot, Transform[] bones)
    {
        if (!File.Exists(BonesPath)) return;
        // Minimal JSON parse for our known structure
        var text = File.ReadAllText(BonesPath);
        foreach (var boneName in new[]
                 {
                     "panda_root", "panda_body", "panda_arm_L", "panda_arm_R",
                     "panda_leg_L", "panda_leg_R", "panda_head"
                 })
        {
            var t = bones.FirstOrDefault(b => b != null && b.name == boneName);
            if (t == null)
            {
                // search hierarchy
                t = anyRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == boneName);
            }
            if (t == null) continue;

            var pos = ExtractVec3(text, boneName, "localPosition");
            var rot = ExtractVec4(text, boneName, "localRotation");
            if (pos.HasValue) t.localPosition = pos.Value;
            if (rot.HasValue) t.localRotation = new Quaternion(rot.Value.x, rot.Value.y, rot.Value.z, rot.Value.w);
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

    static void ReadBin(out int count, out Vector3[] verts, out Vector3[] normals, out Vector2[] uvs,
        out BoneWeight[] weights, out List<string> parts, out List<int[]> partTris)
    {
        using (var br = new BinaryReader(File.OpenRead(BinPath)))
        {
            count = br.ReadInt32();
            verts = new Vector3[count];
            normals = new Vector3[count];
            uvs = new Vector2[count];
            weights = new BoneWeight[count];
            for (int i = 0; i < count; i++) verts[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            for (int i = 0; i < count; i++) normals[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            for (int i = 0; i < count; i++) uvs[i] = new Vector2(br.ReadSingle(), br.ReadSingle());
            for (int i = 0; i < count; i++)
            {
                int n = br.ReadByte();
                var bw = new BoneWeight();
                for (int k = 0; k < n; k++)
                {
                    int bone = br.ReadInt32();
                    float w = br.ReadSingle();
                    if (k == 0) { bw.boneIndex0 = bone; bw.weight0 = w; }
                    else if (k == 1) { bw.boneIndex1 = bone; bw.weight1 = w; }
                    else if (k == 2) { bw.boneIndex2 = bone; bw.weight2 = w; }
                    else if (k == 3) { bw.boneIndex3 = bone; bw.weight3 = w; }
                }
                weights[i] = bw;
            }
            int nParts = br.ReadInt32();
            parts = new List<string>(nParts);
            partTris = new List<int[]>(nParts);
            for (int p = 0; p < nParts; p++)
            {
                int nameLen = br.ReadInt32();
                string name = Encoding.UTF8.GetString(br.ReadBytes(nameLen));
                int triCount = br.ReadInt32();
                var tris = new int[triCount];
                for (int i = 0; i < triCount; i++) tris[i] = br.ReadInt32();
                parts.Add(name);
                partTris.Add(tris);
            }
        }
    }
}

// bump

// bump2


// trigger 2026-11-13T13:51:03.2674116-03:00

// rebuild marker 20261113-backpack-straps

