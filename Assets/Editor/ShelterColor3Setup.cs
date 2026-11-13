#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ShelterColor3Setup {
    const string HostName = "Mamehinata_PC_kisekae (Windows)";
    const string ColorPath = "Roupas/Shelter/Color3";
    const string ClipPath = "Assets/AVATAR PRINCIPAL/Animations/Shelter_Color3.anim";
    const string TeeMatPath = "Assets/SHELTER X BECKENZI/Layered Tee/Materials/BlusaDoFlopy.mat";
    const string PantsMatPath = "Assets/SHELTER X BECKENZI/Rave Pants/Materials/RaveMat.mat";
    const string BootsMatPath = "Assets/SHELTER X BECKENZI/Stompers/Materials/BootsWhite.mat";
    const string DoneMarker = "Assets/Editor/ShelterColor3Setup.done";

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
                Debug.LogError("[Shelter Color3] " + e);
            }
        };
    }

    [MenuItem("Tools/Setup Shelter Color3 Swap")]
    public static void Setup() {
        var host = FindNamed(HostName) ?? FindNamed("a");
        if (host == null) throw new Exception("Nao achei Mamehinata/a");

        var animRoot = host.transform.root;
        var teeRoot = FindChildRecursive(animRoot, "Aqui meu Amor");
        if (teeRoot == null) throw new Exception("Nao achei Aqui meu Amor");

        var tee = FindChildRecursive(teeRoot, "Tee");
        var sleeves = FindChildRecursive(teeRoot, "TeeSleeves");
        var pantsRoot = FindChildRecursive(animRoot, "Mamehinataravepants Variant");
        var bootsRoot = FindChildRecursive(animRoot, "MameShelterStompers Variant");
        var pants = pantsRoot != null
            ? (FindChildRecursive(pantsRoot, "Rave Pants") ?? pantsRoot.GetComponentsInChildren<Renderer>(true)
                .FirstOrDefault(r => r.name.IndexOf("Rave", StringComparison.OrdinalIgnoreCase) >= 0
                                  || r.name.IndexOf("Pants", StringComparison.OrdinalIgnoreCase) >= 0)?.transform)
            : null;
        if (tee == null) throw new Exception("Nao achei Tee");
        if (sleeves == null) throw new Exception("Nao achei TeeSleeves");
        if (pants == null) throw new Exception("Nao achei Rave Pants");
        if (bootsRoot == null) throw new Exception("Nao achei Stompers");

        var teeMat = AssetDatabase.LoadAssetAtPath<Material>(TeeMatPath);
        var pantsMat = AssetDatabase.LoadAssetAtPath<Material>(PantsMatPath);
        var bootsMat = AssetDatabase.LoadAssetAtPath<Material>(BootsMatPath);
        if (teeMat == null) throw new Exception("BlusaDoFlopy missing");
        if (pantsMat == null) throw new Exception("RaveMat missing");
        if (bootsMat == null) throw new Exception("BootsWhite missing");

        var clip = EnsureClip(animRoot, tee, sleeves, pants, bootsRoot, teeMat, pantsMat, bootsMat);
        CreateColorToggle(host, clip);

        EditorUtility.SetDirty(host);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(host.scene);
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(host.scene);
        Debug.Log($"[Shelter Color3] OK -> {ColorPath} (BlusaDoFlopy + bermuda preta + tenis branco)");
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

    static AnimationClip EnsureClip(
        Transform animRoot, Transform tee, Transform sleeves, Transform pants, Transform bootsRoot,
        Material teeMat, Material pantsMat, Material bootsMat
    ) {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null) {
            clip = new AnimationClip { name = "Shelter_Color3", frameRate = 60 };
            AssetDatabase.CreateAsset(clip, ClipPath);
        }

        foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            AnimationUtility.SetObjectReferenceCurve(clip, b, null);
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
            AnimationUtility.SetEditorCurve(clip, b, null);

        SetMaterialCurve(clip, animRoot, tee, teeMat, 0);
        SetMaterialCurve(clip, animRoot, pants, pantsMat, 0);

        var bootRenderers = bootsRoot.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.name.IndexOf("Stomper", StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();
        if (bootRenderers.Count == 0)
            bootRenderers = bootsRoot.GetComponentsInChildren<Renderer>(true).ToList();
        foreach (var r in bootRenderers) {
            var count = r.sharedMaterials != null ? r.sharedMaterials.Length : 1;
            for (var i = 0; i < Math.Max(1, count); i++)
                SetMaterialCurve(clip, animRoot, r.transform, bootsMat, i);
        }

        var sleevesPath = AnimationUtility.CalculateTransformPath(sleeves, animRoot);
        AnimationUtility.SetEditorCurve(clip, new EditorCurveBinding {
            path = sleevesPath,
            type = typeof(GameObject),
            propertyName = "m_IsActive"
        }, AnimationCurve.Constant(0f, 0f, 0f));

        EditorUtility.SetDirty(clip);
        return clip;
    }

    static void SetMaterialCurve(AnimationClip clip, Transform animRoot, Transform mesh, Material mat, int slot) {
        var renderer = mesh.GetComponent<Renderer>();
        if (renderer == null) return;
        var path = AnimationUtility.CalculateTransformPath(mesh, animRoot);
        AnimationUtility.SetObjectReferenceCurve(clip, new EditorCurveBinding {
            path = path,
            type = renderer is SkinnedMeshRenderer ? typeof(SkinnedMeshRenderer) : typeof(MeshRenderer),
            propertyName = $"m_Materials.Array.data[{slot}]"
        }, new[] {
            new ObjectReferenceKeyframe { time = 0f, value = mat }
        });
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
        SetField(toggle, "enableExclusiveTag", true);
        SetField(toggle, "exclusiveTag", "ShelterColor");
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
