#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ShadowLynxColorSwapSetup {
    const string HostName = "Mamehinata_PC_kisekae (Windows)";
    const string OutfitName = "ShadowLynxHoodie_mf_color1";
    const string WearPath = "Roupas/ShadowLynx/On";
    const string ColorPath = "Roupas/ShadowLynx/Color2";
    const string ClipPath = "Assets/AVATAR PRINCIPAL/Animations/ShadowLynx_Color2.anim";
    const string DoneMarker = "Assets/Editor/ShadowLynxColorSwapSetup.done";

    static readonly string[] MeshNames = { "Inner", "Parka", "Rucksack", "Shoes" };
    static readonly string[] MaterialPaths = {
        "Assets/N/ShadowLynxHoodie/Material/Inner_color2.mat",
        "Assets/N/ShadowLynxHoodie/Material/Parka_color2.mat",
        "Assets/N/ShadowLynxHoodie/Material/Rucksack_color2.mat",
        "Assets/N/ShadowLynxHoodie/Material/Shoes_color2.mat",
    };

    [InitializeOnLoadMethod]
    static void AutoRun() {
        EditorApplication.delayCall += () => {
            if (System.IO.File.Exists(DoneMarker)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) {
                EditorApplication.delayCall += () => AutoRun();
                return;
            }
            try {
                Setup();
                System.IO.File.WriteAllText(DoneMarker, DateTime.Now.ToString("o"));
            } catch (Exception e) {
                Debug.LogError("[ShadowLynx] " + e);
            }
        };
    }

    [MenuItem("Tools/Setup ShadowLynx Color2 Swap")]
    public static void Setup() {
        var host = FindNamed(HostName) ?? FindNamed("a");
        if (host == null) throw new Exception("Nao achei Mamehinata/a");

        var outfit = FindChildRecursive(host.transform.root, OutfitName)
                     ?? FindChildRecursive(host.transform, OutfitName);
        if (outfit == null) throw new Exception("Nao achei " + OutfitName);

        // anim root = avatar root (a)
        var animRoot = host.transform.root;
        RenameWearToggle(host);
        var clip = BuildClip(animRoot, outfit);
        CreateColorToggle(host, clip);

        EditorUtility.SetDirty(host);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(host.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ShadowLynx] OK -> {WearPath} + {ColorPath} on {OutfitName}");
    }

    static GameObject FindNamed(string name) {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(go => go.name == name && go.scene.IsValid() && go.scene.isLoaded && go.hideFlags == HideFlags.None);
    }

    static Transform FindChildRecursive(Transform root, string name) {
        if (root == null) return null;
        if (root.name == name) return root;
        foreach (Transform c in root) {
            var f = FindChildRecursive(c, name);
            if (f != null) return f;
        }
        return null;
    }

    static void RenameWearToggle(GameObject host) {
        var vrcfuryType = FindType("VF.Model.VRCFury");
        var toggleType = FindType("VF.Model.Feature.Toggle");
        if (vrcfuryType == null || toggleType == null) return;

        foreach (var comp in host.GetComponents<Component>()) {
            if (comp == null || comp.GetType() != vrcfuryType) continue;
            var content = vrcfuryType.GetField("content")?.GetValue(comp);
            if (content == null || content.GetType() != toggleType) continue;
            var nameField = toggleType.GetField("name");
            var current = nameField?.GetValue(content) as string;
            if (current == "Roupas/ShadowLynx" || current == "Roupas/ShadowLynx/") {
                nameField.SetValue(content, WearPath);
                EditorUtility.SetDirty(comp);
            }
        }
    }

    static AnimationClip BuildClip(Transform animRoot, Transform outfit) {
        var dir = System.IO.Path.GetDirectoryName(ClipPath);
        if (!AssetDatabase.IsValidFolder(dir)) {
            System.IO.Directory.CreateDirectory(dir.Replace('\\', '/'));
            AssetDatabase.Refresh();
        }

        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null) {
            clip = new AnimationClip { name = "ShadowLynx_Color2", frameRate = 60 };
            AssetDatabase.CreateAsset(clip, ClipPath);
        }

        foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            AnimationUtility.SetObjectReferenceCurve(clip, b, null);

        for (var i = 0; i < MeshNames.Length; i++) {
            var mesh = FindChildRecursive(outfit, MeshNames[i]);
            if (mesh == null) continue;
            var renderer = mesh.GetComponent<Renderer>();
            if (renderer == null) continue;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPaths[i]);
            if (mat == null) continue;

            var path = AnimationUtility.CalculateTransformPath(mesh, animRoot);
            var binding = new EditorCurveBinding {
                path = path,
                type = renderer is SkinnedMeshRenderer ? typeof(SkinnedMeshRenderer) : typeof(MeshRenderer),
                propertyName = "m_Materials.Array.data[0]"
            };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, new[] {
                new ObjectReferenceKeyframe { time = 0f, value = mat }
            });
        }

        EditorUtility.SetDirty(clip);
        return clip;
    }

    static void CreateColorToggle(GameObject host, AnimationClip clip) {
        var vrcfuryType = FindType("VF.Model.VRCFury");
        var toggleType = FindType("VF.Model.Feature.Toggle");
        var stateType = FindType("VF.Model.State");
        var animActionType = FindType("VF.Model.StateAction.AnimationClipAction");
        var actionType = FindType("VF.Model.StateAction.Action");
        if (vrcfuryType == null || toggleType == null || stateType == null || animActionType == null)
            throw new Exception("VRCFury types missing");

        foreach (var comp in host.GetComponents<Component>().ToArray()) {
            if (comp == null || comp.GetType() != vrcfuryType) continue;
            var content = vrcfuryType.GetField("content")?.GetValue(comp);
            if (content == null || content.GetType() != toggleType) continue;
            var name = toggleType.GetField("name")?.GetValue(content) as string;
            if (name == ColorPath) Undo.DestroyObjectImmediate(comp);
        }

        var vf = Undo.AddComponent(host, vrcfuryType);
        var toggle = Activator.CreateInstance(toggleType);
        SetField(toggle, "name", ColorPath);
        SetField(toggle, "saved", true);
        SetField(toggle, "defaultOn", false);
        SetField(toggle, "enableExclusiveTag", false);
        SetField(toggle, "exclusiveOffState", false);
        SetField(toggle, "version", 3);

        var state = Activator.CreateInstance(stateType);
        var actionsField = stateType.GetField("actions");
        var actions = (IList)actionsField.GetValue(state);
        if (actions == null) {
            actions = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(actionType));
            actionsField.SetValue(state, actions);
        }

        var animAction = Activator.CreateInstance(animActionType);
        var clipField = animActionType.GetField("clip");
        var guidClip = Activator.CreateInstance(clipField.FieldType);
        var clipPath = AssetDatabase.GetAssetPath(clip);
        var guid = AssetDatabase.AssetPathToGUID(clipPath);
        SetField(guidClip, "objRef", clip);
        SetField(guidClip, "id", $"{guid}|{clipPath}");
        SetField(guidClip, "guid", guid);
        SetField(guidClip, "fileID", 7400000L);
        SetField(guidClip, "version", 1);
        clipField.SetValue(animAction, guidClip);
        actions.Add(animAction);

        SetField(toggle, "state", state);
        vrcfuryType.GetField("content").SetValue(vf, toggle);
        EditorUtility.SetDirty(vf);
    }

    static void SetField(object target, string name, object value) {
        var type = target.GetType();
        while (type != null) {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) { field.SetValue(target, value); return; }
            type = type.BaseType;
        }
    }

    static Type FindType(string fullName) {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
            var t = asm.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }
}
#endif
