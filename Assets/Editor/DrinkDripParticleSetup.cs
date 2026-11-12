#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class DrinkDripParticleSetup {
    const string RootName = "Dripping liquid root";
    const string ChildName = "DripParticles";
    const string MatPath = "Assets/NERUZine/RoomWear/DrinkDrip/DrinkDrip.mat";
    const string TrailMatPath = "Assets/NERUZine/RoomWear/DrinkDrip/DrinkDripTrail.mat";
    const string DoneMarker = "Assets/Editor/DrinkDripParticleSetup.done";
    const string RelParticlePath = "NERU_can/can/Dripping liquid root/DripParticles";
    const string OnClipPath = "Assets/NERUZine/RoomWear/Animation/Drink/DrinkPour_On.anim";
    const string OffClipPath = "Assets/NERUZine/RoomWear/Animation/Drink/DrinkPour_Off.anim";
    const string ControllerPath = "Assets/NERUZine/RoomWear/Animation/Drink/DrinkPour.controller";

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
                Debug.LogError("[DrinkDrip] " + e);
            }
        };
    }

    [MenuItem("Tools/Setup Drink Drip Particles")]
    public static void Setup() {
        var root = FindNamed(RootName);
        if (root == null) throw new Exception("Nao achei '" + RootName + "' na cena");

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        var trailMat = AssetDatabase.LoadAssetAtPath<Material>(TrailMatPath);
        if (mat == null) throw new Exception("Falta " + MatPath);
        if (trailMat == null) throw new Exception("Falta " + TrailMatPath);

        var existing = root.transform.Find(ChildName);
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        var go = new GameObject(ChildName);
        Undo.RegisterCreatedObjectUndo(go, "Create drip particles");
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var ps = go.AddComponent<ParticleSystem>();
        Configure(ps, mat, trailMat);
        go.SetActive(false);

        // Remove always-on pour from hand clips; only DrinkPour + Fist drives particles.
        ClearParticleCurve("Assets/NERUZine/RoomWear/Animation/Drink/Drink Left Hand.anim", RelParticlePath);
        ClearParticleCurve("Assets/NERUZine/RoomWear/Animation/Drink/Drink Right Hand.anim", RelParticlePath);
        SetParticleActive("Assets/NERUZine/RoomWear/Animation/Drink/Drink Head.anim", RelParticlePath, 0f);
        SetParticleActive("Assets/NERUZine/RoomWear/Animation/Drink/Drink OFF.anim", RelParticlePath, 0f);

        SetupPourController();

        EditorUtility.SetDirty(go);
        EditorUtility.SetDirty(root);
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(root.scene);
        Debug.Log("[DrinkDrip] OK -> jato so com Fist + gatilho (Drink Left/Right)");
    }

    static void Configure(ParticleSystem ps, Material mat, Material trailMat) {
        var main = ps.main;
        main.duration = 5f;
        main.loop = true;
        main.prewarm = false;
        main.startDelay = 0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.032f);
        main.startSize3D = false;
        main.startRotation = 0f;
        main.startColor = new Color(0.45f, 1f, 0.25f, 0.95f);
        main.gravityModifier = 1.8f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.playOnAwake = true;
        main.maxParticles = 220;
        main.stopAction = ParticleSystemStopAction.None;
        main.cullingMode = ParticleSystemCullingMode.Automatic;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 80f;
        emission.rateOverDistance = 0f;
        emission.SetBursts(Array.Empty<ParticleSystem.Burst>());

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 4f;
        shape.radius = 0.0025f;
        shape.radiusThickness = 1f;
        shape.length = 0.01f;
        shape.position = Vector3.zero;
        shape.rotation = new Vector3(90f, 0f, 0f);
        shape.scale = Vector3.one;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.55f, 0.85f),
            new Keyframe(1f, 0.25f)
        ));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.55f, 1f, 0.3f), 0f),
                new GradientColorKey(new Color(0.2f, 0.85f, 0.12f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.75f, 0.65f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = grad;

        var trails = ps.trails;
        trails.enabled = true;
        trails.mode = ParticleSystemTrailMode.PerParticle;
        trails.ratio = 1f;
        trails.lifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.28f);
        trails.minVertexDistance = 0.0015f;
        trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
        trails.worldSpace = true;
        trails.dieWithParticles = true;
        trails.sizeAffectsWidth = true;
        trails.sizeAffectsLifetime = false;
        trails.inheritParticleColor = true;
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.5f, 0.65f),
            new Keyframe(1f, 0.08f)
        ));
        trails.colorOverLifetime = new ParticleSystem.MinMaxGradient(grad);

        var noise = ps.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = 0.02f;
        noise.strengthY = 0f;
        noise.strengthZ = 0.02f;
        noise.frequency = 0.85f;
        noise.scrollSpeed = 0.35f;
        noise.octaveCount = 1;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = mat;
        renderer.trailMaterial = trailMat;
        renderer.minParticleSize = 0f;
        renderer.maxParticleSize = 0.2f;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.allowRoll = true;
        renderer.sortingFudge = 0f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.15f, -0.28f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.quality = ParticleSystemCollisionQuality.Medium;
        collision.collidesWith = ~0;
        collision.dampen = new ParticleSystem.MinMaxCurve(0.75f);
        collision.bounce = new ParticleSystem.MinMaxCurve(0.08f);
        collision.lifetimeLoss = new ParticleSystem.MinMaxCurve(0.7f);
        collision.radiusScale = 0.9f;
        collision.enableDynamicColliders = false;
        collision.maxCollisionShapes = 256;
        collision.sendCollisionMessages = false;
    }

    static void SetupPourController() {
        // Same relative path as Drink Left/Right Hand clips (MergeAnimator Relative on C02).
        var onClip = BuildActiveClip(OnClipPath, "DrinkPour_On", RelParticlePath, 1f);
        var offClip = BuildActiveClip(OffClipPath, "DrinkPour_Off", RelParticlePath, 0f);

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        // Preserve DrinkPour.controller.meta guid if Unity kept it; CreateAnimatorControllerAtPath reuses path.
        controller.AddParameter("Drink Left", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Drink Right", AnimatorControllerParameterType.Bool);
        controller.AddParameter("GestureLeft", AnimatorControllerParameterType.Int);
        controller.AddParameter("GestureRight", AnimatorControllerParameterType.Int);
        controller.AddParameter("GestureLeftWeight", AnimatorControllerParameterType.Float);
        controller.AddParameter("GestureRightWeight", AnimatorControllerParameterType.Float);

        var sm = controller.layers[0].stateMachine;
        foreach (var child in sm.states.ToArray())
            sm.RemoveState(child.state);

        var off = sm.AddState("Off");
        off.motion = offClip;
        off.writeDefaultValues = false;
        var pourL = sm.AddState("Pour Left");
        pourL.motion = onClip;
        pourL.writeDefaultValues = false;
        var pourR = sm.AddState("Pour Right");
        pourR.motion = onClip;
        pourR.writeDefaultValues = false;
        sm.defaultState = off;

        AddPourTransition(off, pourL, "Drink Left", "GestureLeft", "GestureLeftWeight", 0.35f);
        AddPourTransition(off, pourR, "Drink Right", "GestureRight", "GestureRightWeight", 0.35f);
        AddStopTransition(pourL, off, "Drink Left", "GestureLeft", "GestureLeftWeight", 0.2f);
        AddStopTransition(pourR, off, "Drink Right", "GestureRight", "GestureRightWeight", 0.2f);

        EditorUtility.SetDirty(controller);
        AttachMergeAnimator(controller);
    }

    static AnimationClip BuildActiveClip(string assetPath, string clipName, string transformPath, float active) {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        if (clip == null) {
            clip = new AnimationClip { name = clipName, frameRate = 60 };
            AssetDatabase.CreateAsset(clip, assetPath);
        }
        foreach (var b in AnimationUtility.GetCurveBindings(clip))
            AnimationUtility.SetEditorCurve(clip, b, null);
        var key = new Keyframe(0f, active, float.PositiveInfinity, float.PositiveInfinity);
        AnimationUtility.SetEditorCurve(clip, new EditorCurveBinding {
            path = transformPath,
            type = typeof(GameObject),
            propertyName = "m_IsActive"
        }, new AnimationCurve(key));
        EditorUtility.SetDirty(clip);
        return clip;
    }

    static void SetParticleActive(string clipPath, string transformPath, float active) {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null) throw new Exception("Falta " + clipPath);
        var key = new Keyframe(0f, active, float.PositiveInfinity, float.PositiveInfinity);
        AnimationUtility.SetEditorCurve(clip, new EditorCurveBinding {
            path = transformPath,
            type = typeof(GameObject),
            propertyName = "m_IsActive"
        }, new AnimationCurve(key));
        EditorUtility.SetDirty(clip);
    }

    static void ClearParticleCurve(string clipPath, string transformPath) {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null) return;
        AnimationUtility.SetEditorCurve(clip, new EditorCurveBinding {
            path = transformPath,
            type = typeof(GameObject),
            propertyName = "m_IsActive"
        }, null);
        EditorUtility.SetDirty(clip);
    }

    static void AddPourTransition(AnimatorState from, AnimatorState to, string handParam, string gestureParam, string weightParam, float weight) {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0f;
        t.AddCondition(AnimatorConditionMode.If, 0f, handParam);
        t.AddCondition(AnimatorConditionMode.Equals, 1f, gestureParam); // Fist
        t.AddCondition(AnimatorConditionMode.Greater, weight, weightParam);
    }

    static void AddStopTransition(AnimatorState from, AnimatorState to, string handParam, string gestureParam, string weightParam, float weight) {
        var notHand = from.AddTransition(to);
        notHand.hasExitTime = false;
        notHand.duration = 0f;
        notHand.AddCondition(AnimatorConditionMode.IfNot, 0f, handParam);

        var notFist = from.AddTransition(to);
        notFist.hasExitTime = false;
        notFist.duration = 0f;
        notFist.AddCondition(AnimatorConditionMode.NotEqual, 1f, gestureParam);

        var lowTrigger = from.AddTransition(to);
        lowTrigger.hasExitTime = false;
        lowTrigger.duration = 0f;
        lowTrigger.AddCondition(AnimatorConditionMode.Less, weight, weightParam);
    }

    static void AttachMergeAnimator(AnimatorController controller) {
        var host = FindNamed("C02_GimmickOnly");
        if (host == null) throw new Exception("Nao achei C02_GimmickOnly");

        var maType = FindType("nadena.dev.modular_avatar.core.ModularAvatarMergeAnimator");
        if (maType == null) throw new Exception("ModularAvatarMergeAnimator missing");

        // Drop old empty / DrinkPour merges, then add a clean one.
        foreach (var c in host.GetComponents(maType).ToArray()) {
            var anim = new SerializedObject(c).FindProperty("animator");
            if (anim == null) continue;
            if (anim.objectReferenceValue == null) {
                Undo.DestroyObjectImmediate(c);
                continue;
            }
            var p = AssetDatabase.GetAssetPath(anim.objectReferenceValue);
            if (p != null && p.Contains("DrinkPour"))
                Undo.DestroyObjectImmediate(c);
        }

        var merge = Undo.AddComponent(host, maType);
        var serialized = new SerializedObject(merge);
        serialized.FindProperty("animator").objectReferenceValue = controller;
        var layer = serialized.FindProperty("layerType");
        if (layer != null) layer.intValue = 5; // FX
        var pathMode = serialized.FindProperty("pathMode");
        if (pathMode != null) pathMode.intValue = 0; // Relative — same as Drink.controller
        var del = serialized.FindProperty("deleteAttachedAnimator");
        if (del != null) del.boolValue = true;
        var match = serialized.FindProperty("matchAvatarWriteDefaults");
        if (match != null) match.boolValue = true;
        var prio = serialized.FindProperty("layerPriority");
        if (prio != null) prio.intValue = 100; // after Drink layer so Fist can override hand clip OFF
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(merge);
    }

    static Type FindType(string fullName) {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
            var t = asm.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }

    static GameObject FindNamed(string name) {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(go => go.name == name && go.scene.IsValid() && go.scene.isLoaded && go.hideFlags == HideFlags.None);
    }
}
#endif

// force-fist-v2 2026-11-12T13:15:34.6314410-03:00

