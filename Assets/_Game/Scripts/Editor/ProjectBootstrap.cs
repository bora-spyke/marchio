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
        const string PresetsFolder = Root + "/Config/Presets";

        [MenuItem("Marchio/Bootstrap Project (creates only missing assets)")]
        public static void Build()
        {
            if (Application.isPlaying) { Debug.LogError("[Marchio] Exit Play mode before bootstrapping."); return; }
            EnsureFolders();
            var cfg = LoadOrCreate<GameConfig>(ConfigPath);
            var solid = Material("Unlit_Solid", "Universal Render Pipeline/Lit", Color.white);
            var line = Material("Line", "Sprites/Default", Color.white);
            var particle = Material("Particle_Additive", "Legacy Shaders/Particles/Additive", Color.white);

            BuildPlayer(cfg, solid, line);
            BuildEnemy("Enemy_Chaser", PrimitiveType.Sphere, solid);
            BuildEnemy("Enemy_Fast", PrimitiveType.Cube, solid);
            BuildEnemy("Enemy_Ranged", PrimitiveType.Capsule, solid);
            BuildProjectile("Projectile_Enemy", solid);
            BuildProjectile("Projectile_Player", solid);
            BuildPanelSettings();
            BuildEnemyTypes(cfg);
            BuildPresets();
            AssetDatabase.SaveAssets();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) BuildScene();
            else Debug.Log("[Marchio] Scene exists, not regenerated. Use Marchio/Regenerate Scene (DESTRUCTIVE) if you really want that.");
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
            if (!AssetDatabase.IsValidFolder(Root + "/Config/Enemies")) AssetDatabase.CreateFolder(Root + "/Config", "Enemies");
            if (!AssetDatabase.IsValidFolder(PresetsFolder)) AssetDatabase.CreateFolder(Root + "/Config", "Presets");
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
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
                AssetDatabase.CreateAsset(mat, path);
            }
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

        static bool PrefabExists(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + name + ".prefab") != null;

        static void SavePrefab(GameObject go)
        {
            PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/" + go.name + ".prefab");
            Object.DestroyImmediate(go);
        }

        [MenuItem("Marchio/Regenerate Scene (DESTRUCTIVE)")]
        public static void RegenerateScene()
        {
            if (Application.isPlaying) { Debug.LogError("[Marchio] Exit Play mode first."); return; }
            if (!EditorUtility.DisplayDialog("Regenerate Game scene?", "This overwrites Assets/_Game/Scenes/Game.unity and drops every hand-made scene change. Continue?", "Overwrite", "Cancel")) return;
            BuildScene();
        }

        [MenuItem("Marchio/Apply Player Settings")]
        public static void ApplyPlayerSettingsMenu() => ApplyPlayerSettings();

        static T Prefab<T>(string name) where T : Component
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + name + ".prefab").GetComponent<T>();
        }

        static void BuildPlayer(GameConfig cfg, Material solid, Material line)
        {
            if (PrefabExists("Player")) return;
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
            var emitter = new GameObject("TrailEmitter");
            emitter.transform.SetParent(visual.transform, false);
            emitter.transform.localPosition = new Vector3(-cfg.playerRadius, 1f, 0f);
            var trailLine = Line("Trail", root.transform, line, cfg.trail, 4f, false, true);
            Set(pc, "visualRoot", visual.transform);
            Set(pc, "visualRenderer", body.GetComponent<MeshRenderer>());
            Set(trail, "line", trailLine);
            Set(trail, "emitter", emitter.transform);
            SavePrefab(root);
        }

        static void BuildEnemy(string name, PrimitiveType shape, Material solid)
        {
            if (PrefabExists(name)) return;
            var root = new GameObject(name);
            var en = root.AddComponent<Enemy>();
            var visual = Primitive("Visual", shape, solid, root.transform);
            Set(en, "visualRoot", visual.transform);
            Set(en, "visualRenderer", visual.GetComponent<MeshRenderer>());
            SavePrefab(root);
        }

        static void BuildProjectile(string name, Material solid)
        {
            if (PrefabExists(name)) return;
            var root = new GameObject(name);
            var p = root.AddComponent<Projectile>();
            var visual = Primitive("Visual", PrimitiveType.Sphere, solid, root.transform);
            Set(p, "visualRoot", visual.transform);
            Set(p, "visualRenderer", visual.GetComponent<MeshRenderer>());
            SavePrefab(root);
        }

        static void BuildLinePrefab<T>(string name, Material line, Color color, float width, bool loop) where T : Component
        {
            if (PrefabExists(name)) return;
            var root = new GameObject(name);
            var c = root.AddComponent<T>();
            var lr = Line("Line", root.transform, line, color, width, loop, true);
            Set(c, "line", lr);
            SavePrefab(root);
        }

        static EnemyTypeSO EnemyType(string name, string prefab, System.Action<EnemyTypeSO> defaults, float score)
        {
            var path = Root + "/Config/Enemies/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyTypeSO>(path);
            var so = existing != null ? existing : ScriptableObject.CreateInstance<EnemyTypeSO>();
            if (existing == null)
            {
                so.displayName = name;
                defaults(so);
                AssetDatabase.CreateAsset(so, path);
            }
            so.score = score;
            so.prefab = Prefab<Enemy>(prefab);
            EditorUtility.SetDirty(so);
            return so;
        }

        static void BuildEnemyTypes(GameConfig cfg)
        {
            var chaser = EnemyType("Chaser", "Enemy_Chaser", t =>
            {
                t.behavior = EnemyBehavior.Chase;
                t.hp = 50f; t.speed = 90f; t.radius = 14f; t.contactDamage = 10f; t.xp = 1;
                t.fireIntervalMs = 1900f; t.projectileSpeed = 130f; t.projectileDamage = 5f; t.fireMinDist = 70f;
                t.initialFireDelayMs = new Vector2(300f, 1100f);
            }, 8f);
            EnemyType("Fast", "Enemy_Fast", t =>
            {
                t.behavior = EnemyBehavior.Chase;
                t.hp = 30f; t.speed = 150f; t.radius = 12f; t.contactDamage = 10f; t.xp = 1;
                t.fireIntervalMs = 1700f; t.projectileSpeed = 150f; t.projectileDamage = 5f; t.fireMinDist = 70f;
                t.initialFireDelayMs = new Vector2(300f, 1100f);
            }, 8f);
            EnemyType("Ranged", "Enemy_Ranged", t =>
            {
                t.behavior = EnemyBehavior.KeepDistance;
                t.hp = 60f; t.speed = 60f; t.radius = 15f; t.contactDamage = 10f; t.xp = 2;
                t.fireIntervalMs = 1400f; t.projectileSpeed = 150f; t.projectileDamage = 8f;
                t.preferredDist = 190f; t.preferredDistJitter = 40f; t.retreatFraction = 0.7f;
                t.initialFireDelayMs = new Vector2(400f, 1000f);
            }, 12f);
        }

        static EnemyTypeSO Type(string name) => AssetDatabase.LoadAssetAtPath<EnemyTypeSO>(Root + "/Config/Enemies/" + name + ".asset");

        static SpawnPhase[] DemoSpawnPhases()
        {
            var chaser = Type("Chaser");
            var fast = Type("Fast");
            var ranged = Type("Ranged");
            return new[]
            {
                new SpawnPhase { startS = 0f, rateMult = 1f, weights = new[] { W(chaser, 1f) } },
                new SpawnPhase { startS = 15f, rateMult = 1.5f, weights = new[] { W(chaser, 0.6f), W(fast, 0.2f), W(ranged, 0.2f) } },
                new SpawnPhase { startS = 30f, rateMult = 2f, weights = new[] { W(chaser, 0.5f), W(fast, 0.25f), W(ranged, 0.25f) } }
            };
        }

        static SpawnWeight W(EnemyTypeSO type, float weight) => new SpawnWeight { type = type, weight = weight };

        static SpawnEntry E(EnemyTypeSO type, int count) => new SpawnEntry { type = type, count = count };

        static WaveConfig Wave(params SpawnEntry[] spawns) => new WaveConfig { spawns = spawns };

        static LevelConfig[] DefaultLevels()
        {
            var c = Type("Chaser");
            var f = Type("Fast");
            var r = Type("Ranged");
            return new[]
            {
                new LevelConfig { waves = new[] { Wave(E(c, 6)), Wave(E(c, 6), E(f, 3)), Wave(E(c, 5), E(f, 3), E(r, 2)), Wave(E(c, 7), E(f, 4), E(r, 2)), Wave(E(c, 8), E(f, 4), E(r, 3)), Wave(E(c, 8), E(f, 5), E(r, 3)) } },
                new LevelConfig { waves = new[] { Wave(E(c, 6)), Wave(E(c, 6), E(f, 2)), Wave(E(c, 5), E(f, 3), E(r, 2)) } },
                new LevelConfig { waves = new[] { Wave(E(c, 7), E(f, 2)), Wave(E(c, 6), E(f, 3), E(r, 2)), Wave(E(c, 8), E(f, 3), E(r, 3)) } },
                new LevelConfig { waves = new[] { Wave(E(c, 8), E(f, 3)), Wave(E(c, 8), E(f, 4), E(r, 3)), Wave(E(c, 10), E(f, 5), E(r, 4)) } }
            };
        }

        static RunPreset Preset(string name, System.Action<RunPreset> defaults)
        {
            var path = PresetsFolder + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<RunPreset>(path);
            var so = existing != null ? existing : ScriptableObject.CreateInstance<RunPreset>();
            if (existing == null)
            {
                defaults(so);
                AssetDatabase.CreateAsset(so, path);
            }
            if (so.spawnPhases == null || so.spawnPhases.Length == 0) so.spawnPhases = DemoSpawnPhases();
            if (so.levels == null || so.levels.Length == 0) so.levels = DefaultLevels();
            EditorUtility.SetDirty(so);
            return so;
        }

        static void BuildPresets()
        {
            Preset("DEMO", p => { p.levelCount = 4; p.powerUpUnlockLevel = 2; });
            Preset("DEMO_SHORT", p => { p.levelCount = 4; p.powerUpUnlockLevel = 2; });
            Preset("LIVE", p => { p.levelCount = 0; p.powerUpUnlockLevel = 4; p.fillUpgradeLastLevel = 999; });
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
            var preset = AssetDatabase.LoadAssetAtPath<RunPreset>(PresetsFolder + "/DEMO.asset");
            var player = Prefab<PlayerController>("Player");
            var enemyProj = Prefab<Projectile>("Projectile_Enemy");
            var playerProj = Prefab<Projectile>("Projectile_Player");

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 10f;
            cam.farClipPlane = 4000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = cfg.bg;
            camGo.transform.position = new Vector3(0f, 880f, -410f);
            camGo.transform.rotation = Quaternion.Euler(65f, 0f, 0f);
            camGo.AddComponent<AudioListener>();
            var urpCam = cam.GetUniversalAdditionalCameraData();
            urpCam.renderShadows = false;
            urpCam.renderPostProcessing = false;
            var rig = camGo.AddComponent<CameraRig>();
            BuildGround(cfg);
            BuildShade(camGo.transform);

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
            Set(gm, "preset", preset);
            Set(gm, "enemyProjectilePrefab", enemyProj);
            Set(gm, "playerProjectilePrefab", playerProj);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.25f, 0.35f);
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (File.Exists("Assets/Scenes/SampleScene.unity")) AssetDatabase.DeleteAsset("Assets/Scenes");
        }

        public static GroundTiler BuildGround(GameConfig cfg)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/ground_square.png");
            var mat = Material("Ground", "Universal Render Pipeline/Unlit", cfg.groundTint);
            mat.mainTexture = texture;
            mat.enableInstancing = true;
            MakeTransparent(mat);
            EditorUtility.SetDirty(mat);
            var go = new GameObject("Ground");
            var tiler = go.AddComponent<GroundTiler>();
            Set(tiler, "material", mat);
            return tiler;
        }

        public static void BuildShade(Transform camera)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Textures/shdw.png");
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            var root = new GameObject("CameraShade");
            root.transform.SetParent(camera, false);
            var shade = root.AddComponent<CameraShade>();
            foreach (var edge in new[] { "bottom", "top", "left", "right" })
            {
                var go = new GameObject("Shade_" + edge);
                go.transform.SetParent(root.transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                if (mat != null) sr.sharedMaterial = mat;
                sr.drawMode = SpriteDrawMode.Sliced;
                Set(shade, edge, sr);
            }
        }

        public static void MakeTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Spyke Games";
            PlayerSettings.productName = "Hollow Busters";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.spykegames.hollowbusters");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan, UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.use32BitDisplayBuffer = true;

            PlayerSettings.WebGL.template = "PROJECT:Marchio";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.WebGL.initialMemorySize = 256;
            PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
            PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;
            PlayerSettings.WebGL.showDiagnostics = false;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.High);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.High);
            PlayerSettings.runInBackground = false;
            AssetDatabase.SaveAssets();
        }
    }
}
