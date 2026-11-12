#if UNITY_EDITOR
// FORCE_RELOAD 2026-11-12T11:13:12.4564896-03:00
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ShelterColorSwapSetup {
    const string HostName = "Mamehinata_PC_kisekae (Windows)";
    const string WearPath = "Roupas/Shelter/On";
    const string ColorPath = "Roupas/Shelter/Color2";
    const string ClipPath = "Assets/AVATAR PRINCIPAL/Animations/Shelter_Color2.anim";
    const string DoneMarker = "Assets/Editor/ShelterColorSwapSetup.done";

    const string TeeMatPath = "Assets/SHELTER X BECKENZI/Layered Tee/Materials/TeeMat.mat";
    const string RaveMatPath = "Assets/SHELTER X BECKENZI/Rave Pants/Materials/RaveMat.mat";
    const string BootsBlackPath = "Assets/SHELTER X BECKENZI/Stompers/Materials/BottsBlack.mat";

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
                Debug.LogError("[Shelter] " + e);
            }
        };
    }

    [MenuItem("Tools/Setup Shelter Color2 Swap")]
    public static void Setup() {
        var host = FindNamed(HostName) ?? FindNamed("a");
        if (host == null) throw new Exception("Nao achei Mamehinata/a");

        var animRoot = host.transform.root;
        var teeRoot = FindShelterTeeRoot(host, animRoot);
        if (teeRoot == null) throw new Exception("Nao achei Aqui meu Amor (Shelter)");

        var pantsRoot = FindChildRecursive(animRoot, "Mamehinataravepants Variant")
                        ?? FindChildRecursive(host.transform, "Mamehinataravepants Variant");
        var bootsRoot = FindChildRecursive(animRoot, "MameShelterStompers Variant")
                        ?? FindChildRecursive(host.transform, "MameShelterStompers Variant");
        if (pantsRoot == null) throw new Exception("Nao achei Mamehinataravepants Variant");
        if (bootsRoot == null) throw new Exception("Nao achei MameShelterStompers Variant");

        var tee = FindChildRecursive(teeRoot, "Tee");
        var sleeves = FindChildRecursive(teeRoot, "TeeSleeves");
        var pants = FindChildRecursive(pantsRoot, "Rave Pants")
                    ?? FindRendererChild(pantsRoot, "Rave")
                    ?? FindRendererChild(pantsRoot, "Pants");
        if (tee == null) throw new Exception("Nao achei Tee");
        if (sleeves == null) throw new Exception("Nao achei TeeSleeves");
        if (pants == null) throw new Exception("Nao achei mesh da bermuda");

        // Color1 default: sleeves off
        if (sleeves.gameObject.activeSelf) {
            sleeves.gameObject.SetActive(false);
            EditorUtility.SetDirty(sleeves.gameObject);
        }

        RenameWearToggle(host);
        var clip = BuildClip(animRoot, tee, sleeves, pants, bootsRoot);
        CreateColorToggle(host, clip);

        EditorUtility.SetDirty(host);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(host.scene);
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(host.scene);
        Debug.Log($"[Shelter] OK -> {WearPath} + {ColorPath} (Tee/Pants/Boots + TeeSleeves)");
    }

    static Transform FindShelterTeeRoot(GameObject host, Transform animRoot) {
        // Prefer the Aqui meu Amor already wired into Roupas/Shelter toggle
        var fromToggle = FindObjectFromWearToggle(host);
        if (fromToggle != null) {
            if (fromToggle.name == "Aqui meu Amor") return fromToggle.transform;
            var parent = FindParentNamed(fromToggle.transform, "Aqui meu Amor");
            if (parent != null) return parent;
        }

        // Prefer the instance that has Tee + TeeSleeves
        foreach (var t in animRoot.GetComponentsInChildren<Transform>(true)) {
            if (t.name != "Aqui meu Amor") continue;
            if (FindChildRecursive(t, "Tee") != null && FindChildRecursive(t, "TeeSleeves") != null)
                return t;
        }
        return FindChildRecursive(animRoot, "Aqui meu Amor");
    }

    static GameObject FindObjectFromWearToggle(GameObject host) {
        var vrcfuryType = FindType("VF.Model.VRCFury");
        var toggleType = FindType("VF.Model.Feature.Toggle");
        if (vrcfuryType == null || toggleType == null) return null;

        foreach (var comp in host.GetComponents<Component>()) {
            if (comp == null || comp.GetType() != vrcfuryType) continue;
            var content = vrcfuryType.GetField("content")?.GetValue(comp);
            if (content == null || content.GetType() != toggleType) continue;
            var name = toggleType.GetField("name")?.GetValue(content) as string;
            if (name != "Roupas/Shelter" && name != WearPath && name != "Roupas/Shelter/") continue;

            var state = toggleType.GetField("state")?.GetValue(content);
            var actions = state?.GetType().GetField("actions")?.GetValue(state) as IList;
            if (actions == null) continue;
            foreach (var action in actions) {
                if (action == null) continue;
                var objField = action.GetType().GetField("obj");
                if (objField == null) continue;
                var obj = objField.GetValue(action) as GameObject;
                if (obj != null) return obj;
            }
        }
        return null;
    }

    static Transform FindParentNamed(Transform t, string name) {
        while (t != null) {
            if (t.name == name) return t;
            t = t.parent;
        }
        return null;
    }

    static Transform FindRendererChild(Transform root, string nameContains) {
        foreach (var r in root.GetComponentsInChildren<Renderer>(true)) {
            if (r.name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0)
                return r.transform;
        }
        return null;
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
            if (current == "Roupas/Shelter" || current == "Roupas/Shelter/") {
                nameField.SetValue(content, WearPath);
                EditorUtility.SetDirty(comp);
            }
        }
    }

    static AnimationClip BuildClip(Transform animRoot, Transform tee, Transform sleeves, Transform pants, Transform bootsRoot) {
        var dir = System.IO.Path.GetDirectoryName(ClipPath);
        if (!AssetDatabase.IsValidFolder(dir)) {
            System.IO.Directory.CreateDirectory(dir.Replace('\\', '/'));
            AssetDatabase.Refresh();
        }

        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null) {
            clip = new AnimationClip { name = "Shelter_Color2", frameRate = 60 };
            AssetDatabase.CreateAsset(clip, ClipPath);
        }

        foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            AnimationUtility.SetObjectReferenceCurve(clip, b, null);
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
            AnimationUtility.SetEditorCurve(clip, b, null);

        var teeMat = AssetDatabase.LoadAssetAtPath<Material>(TeeMatPath);
        var raveMat = AssetDatabase.LoadAssetAtPath<Material>(RaveMatPath);
        var bootsMat = AssetDatabase.LoadAssetAtPath<Material>(BootsBlackPath);
        if (teeMat == null) throw new Exception("TeeMat missing");
        if (raveMat == null) throw new Exception("RaveMat missing");
        if (bootsMat == null) throw new Exception("BottsBlack missing");

        SetMaterialCurve(clip, animRoot, tee, teeMat, 0);
        SetMaterialCurve(clip, animRoot, pants, raveMat, 0);

        // StompersL / StompersR (and any other boot renderers)
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

        // TeeSleeves ON for Color2 / black shirt
        var sleevesPath = AnimationUtility.CalculateTransformPath(sleeves, animRoot);
        AnimationUtility.SetEditorCurve(clip, new EditorCurveBinding {
            path = sleevesPath,
            type = typeof(GameObject),
            propertyName = "m_IsActive"
        }, AnimationCurve.Constant(0f, 0f, 1f));

        EditorUtility.SetDirty(clip);
        return clip;
    }

    static void SetMaterialCurve(AnimationClip clip, Transform animRoot, Transform mesh, Material mat, int slot) {
        var renderer = mesh.GetComponent<Renderer>();
        if (renderer == null) return;
        var path = AnimationUtility.CalculateTransformPath(mesh, animRoot);
        var binding = new EditorCurveBinding {
            path = path,
            type = renderer is SkinnedMeshRenderer ? typeof(SkinnedMeshRenderer) : typeof(MeshRenderer),
            propertyName = $"m_Materials.Array.data[{slot}]"
        };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, new[] {
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
