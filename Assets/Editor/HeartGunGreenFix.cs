#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class HeartGunGreenFix {
    const string PrefabPath = "Assets/[CooGee Works]/HeartParticle_MA_Gimmick/Heart_Gun_Gimmick.prefab";
    const string DoneMarker = "Assets/Editor/HeartGunGreenFix.done";

    static readonly Color Lime = new Color(0.55f, 1.00f, 0.22f, 1f);
    static readonly Color Mint = new Color(0.35f, 0.98f, 0.55f, 1f);
    static readonly Color Forest = new Color(0.08f, 0.48f, 0.16f, 1f);
    static readonly Color Chartreuse = new Color(0.72f, 1.00f, 0.12f, 1f);
    static readonly Color TealGreen = new Color(0.12f, 0.78f, 0.42f, 1f);
    static readonly Color DarkLeaf = new Color(0.18f, 0.38f, 0.10f, 1f);

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
                Debug.LogError("[HeartGunGreen] " + e);
            }
        };
    }

    [MenuItem("Tools/Fix Heart Gun Greens")]
    public static void Run() {
        var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try {
            RecolorRoot(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        } finally {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        var sceneRoots = Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(go => go.name == "Heart_Gun_Gimmick" && go.scene.IsValid() && go.scene.isLoaded && go.hideFlags == HideFlags.None);
        foreach (var go in sceneRoots) {
            RecolorRoot(go);
            EditorUtility.SetDirty(go);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(go.scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[HeartGunGreen] coracoes rosa -> verde; impacto com tons de verde");
    }

    static void RecolorRoot(GameObject root) {
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true)) {
            StripPink(ps);
            var n = ps.gameObject.name;
            if (n.Contains("Hit_Particle") || n.Contains("Cute heart") || n.Contains("eff_heart") || n == "Core")
                ApplyGreenVariety(ps);
        }
    }

    static void StripPink(ParticleSystem ps) {
        var main = ps.main;
        main.startColor = FixMmg(main.startColor, false);

        var col = ps.colorOverLifetime;
        if (col.enabled) col.color = FixMmg(col.color, false);

        var cbs = ps.colorBySpeed;
        if (cbs.enabled) cbs.color = FixMmg(cbs.color, false);

        var trails = ps.trails;
        if (trails.enabled) {
            trails.colorOverLifetime = FixMmg(trails.colorOverLifetime, false);
            trails.colorOverTrail = FixMmg(trails.colorOverTrail, false);
        }
        EditorUtility.SetDirty(ps);
    }

    static void ApplyGreenVariety(ParticleSystem ps) {
        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(Forest, Lime);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var gA = new Gradient();
        gA.SetKeys(
            new[] {
                new GradientColorKey(Mint, 0f),
                new GradientColorKey(Lime, 0.45f),
                new GradientColorKey(Forest, 1f)
            },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        var gB = new Gradient();
        gB.SetKeys(
            new[] {
                new GradientColorKey(Chartreuse, 0f),
                new GradientColorKey(TealGreen, 0.4f),
                new GradientColorKey(DarkLeaf, 1f)
            },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.9f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(gA, gB);
        EditorUtility.SetDirty(ps);
    }

    static ParticleSystem.MinMaxGradient FixMmg(ParticleSystem.MinMaxGradient mmg, bool variety) {
        switch (mmg.mode) {
            case ParticleSystemGradientMode.Color:
                return new ParticleSystem.MinMaxGradient(ToGreen(mmg.color));
            case ParticleSystemGradientMode.TwoColors:
                return new ParticleSystem.MinMaxGradient(ToGreen(mmg.colorMin), ToGreen(mmg.colorMax));
            case ParticleSystemGradientMode.Gradient:
                return new ParticleSystem.MinMaxGradient(FixGrad(mmg.gradient));
            case ParticleSystemGradientMode.TwoGradients:
                return new ParticleSystem.MinMaxGradient(FixGrad(mmg.gradientMin), FixGrad(mmg.gradientMax));
            case ParticleSystemGradientMode.RandomColor:
                var g = FixGrad(mmg.gradient);
                var n = new ParticleSystem.MinMaxGradient(g);
                n.mode = ParticleSystemGradientMode.RandomColor;
                return n;
            default:
                return mmg;
        }
    }

    static Gradient FixGrad(Gradient g) {
        if (g == null) return g;
        var ck = g.colorKeys;
        for (int i = 0; i < ck.Length; i++)
            ck[i].color = ToGreen(ck[i].color);
        var n = new Gradient();
        n.SetKeys(ck, g.alphaKeys);
        n.mode = g.mode;
        return n;
    }

    static Color ToGreen(Color c) {
        float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
        if (c.a < 0.02f || max < 0.08f) return c;
        float sat = max > 1e-6f ? (max - min) / max : 0f;
        if (sat < 0.08f) return c;
        bool alreadyGreen = c.g >= c.r * 1.02f && c.g >= c.b * 0.95f && c.g >= max * 0.9f;
        if (alreadyGreen) return c;
        return new Color(max * 0.18f, max * 0.98f, max * 0.22f, c.a);
    }
}
#endif
