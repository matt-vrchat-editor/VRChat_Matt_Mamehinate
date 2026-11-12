#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ParentLiquidToCanBone {
    const string DoneMarker = "Assets/Editor/ParentLiquidToCanBone.done";

    [InitializeOnLoadMethod]
    static void AutoRun() {
        EditorApplication.delayCall += () => {
            if (System.IO.File.Exists(DoneMarker)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) {
                EditorApplication.delayCall += () => AutoRun();
                return;
            }
            try {
                Run();
                System.IO.File.WriteAllText(DoneMarker, DateTime.Now.ToString("o"));
            } catch (Exception e) {
                Debug.LogError("[LiquidBone] " + e);
            }
        };
    }

    [MenuItem("Tools/Parent Liquid To Can Bone")]
    public static void Run() {
        var neru = FindNamed("NERU_can");
        if (neru == null) throw new Exception("Nao achei NERU_can");

        var bone = neru.transform.Find("Armature.001/Bone/Bone.001");
        if (bone == null) throw new Exception("Nao achei Armature.001/Bone/Bone.001");

        var liquid = neru.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name == "Liquid");
        if (liquid == null) throw new Exception("Nao achei Liquid dentro de NERU_can");

        Undo.SetTransformParent(liquid, bone, "Parent Liquid to can PhysBone");
        liquid.SetParent(bone, true);

        // Liquid.cs keeps a world-space fill plane; in VRChat the script is stripped,
        // and in editor it makes the liquid look glued to the world instead of the can.
        var liquidBehaviour = liquid.GetComponent("Liquid") as MonoBehaviour;
        if (liquidBehaviour != null) {
            Undo.RecordObject(liquidBehaviour, "Disable world-space liquid plane");
            liquidBehaviour.enabled = false;
            EditorUtility.SetDirty(liquidBehaviour);
        }

        EditorUtility.SetDirty(liquid);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(liquid.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(liquid.gameObject.scene);
        Debug.Log("[LiquidBone] Liquid agora segue Bone.001 (PhysBone da lata)");
    }

    static GameObject FindNamed(string name) {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(go => go.name == name && go.scene.IsValid() && go.scene.isLoaded && go.hideFlags == HideFlags.None);
    }
}
#endif
