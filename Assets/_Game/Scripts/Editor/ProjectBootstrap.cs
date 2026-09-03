using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Marchio.Editor
{
    public static class ProjectBootstrap
    {
        const string Root = "Assets/_Game";
        const string ConfigPath = Root + "/Config/GameConfig.asset";
        const string ScenePath = Root + "/Scenes/Game.unity";
        const string PanelPath = Root + "/UI/PanelSettings.asset";

        [MenuItem("Marchio/Bootstrap Project")]
        public static void Build()
        {
            EnsureFolders();
            var cfg = LoadOrCreate<GameConfig>(ConfigPath);
            var solid = Material("Unlit_Solid", "Universal Render Pipeline/Unlit", Color.white);
            var line = Material("Line", "Sprites/Default", Color.white);
            var particle = Material("Particle_Additive", "Legacy Shaders/Particles/Additive", Color.white);

            BuildPlayer(cfg, solid, line);
            BuildEnemy("Enemy_Chaser", PrimitiveType.Sphere, solid);
            BuildEnemy("Enemy_Fast", PrimitiveType.Cube, solid);
            BuildEnemy("Enemy_Ranged", PrimitiveType.Capsule, solid);
            BuildBoss(cfg, solid, line);
            BuildProjectile("Projectile_Enemy", solid);
            BuildProjectile("Projectile_Player", solid);
            BuildLinePrefab<Barrier>("Barrier", line, cfg.loopEdge, 3f, true);
            BuildLinePrefab<DeadTrail>("DeadTrail", line, cfg.trail, 3f, false);
            BuildPanelSettings();
            AssetDatabase.SaveAssets();

            BuildScene();
            ApplyPlayerSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Marchio] Bootstrap complete");
        }

        static void EnsureFolders()
        {
            foreach (var f in new[] { "Config", "Scenes", "Prefabs", "Materials", "UI" })
            {
                var path = Root + "/" + f;
                if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(Root, f);
            }
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, path);
            return so;
        }

        static Material Material(string name, string shader, Color color)
        {
            var path = Root + "/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = Shader.Find(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static GameObject Primitive(string name, PrimitiveType type, Material mat, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.GetComponent<MeshRenderer>().receiveShadows = false;
            go.transform.SetParent(parent, false);
            return go;
        }

        static LineRenderer Line(string name, Transform parent, Material mat, Color color, float width, bool loop, bool worldSpace)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.loop = loop;
            lr.useWorldSpace = worldSpace;
            lr.numCornerVertices = 4;
            lr.numCapVertices = 4;
            lr.alignment = LineAlignment.View;
            lr.positionCount = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogError($"Missing field {field} on {target.name}"); return; }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SavePrefab(GameObject go)
        {
            PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/" + go.name + ".prefab");
            Object.DestroyImmediate(go);
        }

        static T Prefab<T>(string name) where T : Component
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + name + ".prefab").GetComponent<T>();
        }

        static void BuildPlayer(GameConfig cfg, Material solid, Material line)
        {
            var root = new GameObject("Player");
            var pc = root.AddComponent<PlayerController>();
            root.AddComponent<AutoAttack>();
            var trail = root.AddComponent<LoopTrail>();
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var body = Primitive("Body", PrimitiveType.Cylinder, solid, visual.transform);
            body.transform.localScale = new Vector3(cfg.playerRadius * 2f, 4f, cfg.playerRadius * 2f);
            var nose = Primitive("Nose", PrimitiveType.Cube, solid, visual.transform);
            nose.transform.localScale = new Vector3(cfg.playerRadius * 0.8f, 6f, 6f);
            nose.transform.localPosition = new Vector3(cfg.playerRadius * 0.9f, 0f, 0f);
            var trailLine = Line("Trail", root.transform, line, cfg.trail, 4f, false, true);
            var flashLine = Line("Flash", root.transform, line, cfg.loopEdge, 3f, true, true);
            Set(pc, "visualRoot", visual.transform);
            Set(pc, "visualRenderer", body.GetComponent<MeshRenderer>());
            Set(trail, "line", trailLine);
            Set(trail, "flashLine", flashLine);
            SavePrefab(root);
        }

        static void BuildEnemy(string name, PrimitiveType shape, Material solid)
        {
            var root = new GameObject(name);
            var en = root.AddComponent<Enemy>();
            var visual = Primitive("Visual", shape, solid, root.transform);
            Set(en, "visualRoot", visual.transform);
            Set(en, "visualRenderer", visual.GetComponent<MeshRenderer>());
            SavePrefab(root);
        }

        static void BuildBoss(GameConfig cfg, Material solid, Material line)
        {
            var root = new GameObject("Boss");
            var boss = root.AddComponent<BossController>();
            var visual = Primitive("Visual", PrimitiveType.Sphere, solid, root.transform);
            var ring = Line("TelegraphRing", root.transform, line, cfg.telegraph, 3f, true, false);
            Set(boss, "visualRoot", visual.transform);
            Set(boss, "visualRenderer", visual.GetComponent<MeshRenderer>());
            Set(boss, "telegraphRing", ring);
            SavePrefab(root);
        }

        static void BuildProjectile(string name, Material solid)
        {
            var root = new GameObject(name);
            var p = root.AddComponent<Projectile>();
            var visual = Primitive("Visual", PrimitiveType.Sphere, solid, root.transform);
            Set(p, "visualRoot", visual.transform);
            Set(p, "visualRenderer", visual.GetComponent<MeshRenderer>());
            SavePrefab(root);
        }

        static void BuildLinePrefab<T>(string name, Material line, Color color, float width, bool loop) where T : Component
        {
            var root = new GameObject(name);
            var c = root.AddComponent<T>();
            var lr = Line("Line", root.transform, line, color, width, loop, true);
            Set(c, "line", lr);
            SavePrefab(root);
        }

        static void BuildPanelSettings()
        {
            var panel = LoadOrCreate<PanelSettings>(PanelPath);
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(Root + "/UI/Neon.tss");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(390, 844);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 1f;
            panel.clearColor = false;
            EditorUtility.SetDirty(panel);
        }

        static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            var particleMat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Particle_Additive.mat");
            var player = Prefab<PlayerController>("Player");
            var chaser = Prefab<Enemy>("Enemy_Chaser");
            var fast = Prefab<Enemy>("Enemy_Fast");
            var ranged = Prefab<Enemy>("Enemy_Ranged");
            var boss = Prefab<BossController>("Boss");
            var enemyProj = Prefab<Projectile>("Projectile_Enemy");
            var playerProj = Prefab<Projectile>("Projectile_Player");
            var barrier = Prefab<Barrier>("Barrier");
            var deadTrail = Prefab<DeadTrail>("DeadTrail");

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = cfg.referenceHeightPx * 0.5f;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 1000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = cfg.bg;
            camGo.transform.position = new Vector3(0f, 500f, 0f);
            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            camGo.AddComponent<AudioListener>();
            var urpCam = cam.GetUniversalAdditionalCameraData();
            urpCam.renderShadows = false;
            urpCam.renderPostProcessing = false;
            var rig = camGo.AddComponent<CameraRig>();

            var game = new GameObject("Game");
            var gm = game.AddComponent<GameManager>();
            var input = game.AddComponent<InputReader>();
            var waves = game.AddComponent<WaveManager>();
            var upgrades = game.AddComponent<UpgradeManager>();
            var pools = new GameObject("Pools");
            pools.transform.SetParent(game.transform, false);

            var playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(player.gameObject);

            var fxGo = new GameObject("ParticleFx");
            var ps = fxGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 3000;
            main.startLifetime = 0.45f;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.15f;
            limit.limit = 0f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var psr = fxGo.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = particleMat;
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;
            var fx = fxGo.AddComponent<ParticleFx>();

            var hud = new GameObject("HUD");
            var hudDoc = hud.AddComponent<UIDocument>();
            hudDoc.panelSettings = panel;
            hudDoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root + "/UI/Hud.uxml");
            hudDoc.sortingOrder = 0;
            hud.AddComponent<HudController>();

            var screens = new GameObject("Screens");
            var screensDoc = screens.AddComponent<UIDocument>();
            screensDoc.panelSettings = panel;
            screensDoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root + "/UI/Screens.uxml");
            screensDoc.sortingOrder = 1;
            screens.AddComponent<ScreensController>();

            Set(gm, "config", cfg);
            Set(gm, "cameraRig", rig);
            Set(gm, "input", input);
            Set(gm, "player", playerInstance.GetComponent<PlayerController>());
            Set(gm, "trail", playerInstance.GetComponent<LoopTrail>());
            Set(gm, "autoAttack", playerInstance.GetComponent<AutoAttack>());
            Set(gm, "waves", waves);
            Set(gm, "upgrades", upgrades);
            Set(gm, "fx", fx);
            Set(gm, "poolRoot", pools.transform);
            Set(gm, "chaserPrefab", chaser);
            Set(gm, "fastPrefab", fast);
            Set(gm, "rangedPrefab", ranged);
            Set(gm, "bossPrefab", boss);
            Set(gm, "enemyProjectilePrefab", enemyProj);
            Set(gm, "playerProjectilePrefab", playerProj);
            Set(gm, "barrierPrefab", barrier);
            Set(gm, "deadTrailPrefab", deadTrail);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (File.Exists("Assets/Scenes/SampleScene.unity")) AssetDatabase.DeleteAsset("Assets/Scenes");
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Spyke Games";
            PlayerSettings.productName = "NeonLoop";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.spykegames.neonloop");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan, UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.use32BitDisplayBuffer = true;
            AssetDatabase.SaveAssets();
        }
    }
}
