#if UNITY_EDITOR
using HarmonicEngineV4.Profiles;
using HarmonicEngineV4.Rendering;
using HarmonicEngineV4.Simulation;
using HarmonicEngineV4.UI.Lab2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace HarmonicEngineV4.Editor
{
    public static class V4Lab2RuntimeUIBuilder
    {
        private const string ScenePath = "Assets/Scenes/HarmonicEngineLab2.unity";
        private const string Lab2LiquidPath = "Assets/HarmonicEngineV4/Profiles/V4Liquid_Lab2.asset";
        private const string DefaultLiquidPath = "Assets/HarmonicEngineV4/Profiles/V4Liquid_Default.asset";

        [MenuItem("HarmonicEngineV4/Lab2/Fix Event System Input Module")]
        public static void FixEventSystemInputModule()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            EnsureEventSystem();
            EditorSceneManager.MarkSceneDirty(scene);
            if (scene.path == ScenePath)
            {
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log("[V4Lab2RuntimeUIBuilder] EventSystem now uses InputSystemUIInputModule.");
        }

        [MenuItem("HarmonicEngineV4/Lab2/Create Runtime UI Canvas")]
        public static void CreateRuntimeUiCanvas()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject pipelineGo = GameObject.Find("V4 Pipeline");
            GameObject bucketGo = GameObject.Find("Bucket");
            if (pipelineGo == null || bucketGo == null)
            {
                Debug.LogError("[V4Lab2RuntimeUIBuilder] Bucket or V4 Pipeline not found.");
                return;
            }

            V4PipelineRoot pipeline = pipelineGo.GetComponent<V4PipelineRoot>();
            V4Bucket bucket = bucketGo.GetComponent<V4Bucket>();
            V4SphericalPendulumController pendulum = bucketGo.GetComponent<V4SphericalPendulumController>();
            V4TorricelliEjectionSettings torricelli = bucketGo.GetComponent<V4TorricelliEjectionSettings>();
            V4RenderingSettings rendering = pipelineGo.GetComponent<V4RenderingSettings>();
            if (rendering == null)
            {
                rendering = pipelineGo.AddComponent<V4RenderingSettings>();
            }

            Transform pivot = GameObject.Find("Pendulum Pivot")?.transform;
            V4LiquidProfile lab2Profile = AssetDatabase.LoadAssetAtPath<V4LiquidProfile>(Lab2LiquidPath);
            V4LiquidProfile defaultProfile = AssetDatabase.LoadAssetAtPath<V4LiquidProfile>(DefaultLiquidPath);

            if (pipeline != null)
            {
                pipeline.autoRun = false;
                pipeline.deferAutoInitialize = true;
                if (lab2Profile != null)
                {
                    pipeline.globalProfile = lab2Profile;
                }
            }

            DestroyExistingUi();

            EnsureEventSystem();

            GameObject root = new GameObject("Lab2 UI");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            var setup = root.AddComponent<V4Lab2RuntimeSetupController>();
            var tuning = root.AddComponent<V4Lab2RuntimeTuningController>();
            var stats = root.AddComponent<V4Lab2StatsHudController>();
            var polish = root.AddComponent<V4Lab2UiPolish>();

            GameObject startupPanel = BuildStartupPanel(root.transform);
            GameObject wizardPanel = BuildWizardPanel(root.transform, out WizardWidgets wizard);
            GameObject playHud = BuildPlayHud(root.transform, out PlayHudWidgets hud);
            GameObject tuningPanel = BuildTuningPanel(root.transform, out TuningWidgets tuningWidgets);

            SerializedObject setupSo = new SerializedObject(setup);
            setupSo.FindProperty("pipeline").objectReferenceValue = pipeline;
            setupSo.FindProperty("bucket").objectReferenceValue = bucket;
            setupSo.FindProperty("pendulum").objectReferenceValue = pendulum;
            setupSo.FindProperty("pivotTransform").objectReferenceValue = pivot;
            setupSo.FindProperty("torricelli").objectReferenceValue = torricelli;
            setupSo.FindProperty("rendering").objectReferenceValue = rendering;
            setupSo.FindProperty("lab2LiquidProfile").objectReferenceValue = lab2Profile;
            setupSo.FindProperty("defaultLiquidProfile").objectReferenceValue = defaultProfile;
            setupSo.FindProperty("startupPanel").objectReferenceValue = startupPanel;
            setupSo.FindProperty("wizardPanel").objectReferenceValue = wizardPanel;
            setupSo.FindProperty("playHud").objectReferenceValue = playHud;
            setupSo.FindProperty("runSceneButton").objectReferenceValue = startupPanel.transform.Find("Card/RunSceneButton").GetComponent<Button>();
            setupSo.FindProperty("setupSceneButton").objectReferenceValue = startupPanel.transform.Find("Card/SetupSceneButton").GetComponent<Button>();
            AssignWizardRefs(setupSo, wizard);
            setupSo.FindProperty("pauseButton").objectReferenceValue = hud.pauseButton;
            setupSo.FindProperty("pauseButtonLabel").objectReferenceValue = hud.pauseLabel;
            setupSo.FindProperty("tuningToggleButton").objectReferenceValue = hud.tuningToggle;
            setupSo.FindProperty("statsHud").objectReferenceValue = stats;
            setupSo.FindProperty("tuning").objectReferenceValue = tuning;
            setupSo.FindProperty("uiPolish").objectReferenceValue = polish;
            setupSo.ApplyModifiedPropertiesWithoutUndo();

            tuning.Bind(
                setup,
                pipeline,
                pendulum,
                torricelli,
                rendering,
                tuningPanel,
                tuningWidgets.carry,
                tuningWidgets.damping,
                tuningWidgets.torricelli,
                tuningWidgets.pbf,
                tuningWidgets.ghost,
                tuningWidgets.ssFluid,
                tuningWidgets.debugPoints,
                tuningWidgets.resetPendulum,
                tuningWidgets.openSetup,
                tuningWidgets.close);

            stats.Bind(
                pipeline,
                pendulum,
                bucketGo.GetComponent<V4BucketMotionSettings>(),
                hud.statsText,
                hud.pausedBadge,
                hud.statsGroup,
                hud.collapseButton);

            setup.MarkUiBuilt();
            tuningPanel.SetActive(false);
            wizardPanel.SetActive(false);
            playHud.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            Debug.Log("[V4Lab2RuntimeUIBuilder] Lab2 runtime UI created and scene saved.");
        }

        private static void DestroyExistingUi()
        {
            GameObject existing = GameObject.Find("Lab2 UI");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }
        }

        private static void EnsureEventSystem()
        {
#if ENABLE_INPUT_SYSTEM
            EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<EventSystem>();
                go.AddComponent<InputSystemUIInputModule>();
                return;
            }

            StandaloneInputModule legacy = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacy != null)
            {
                Object.DestroyImmediate(legacy);
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
#else
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<EventSystem>();
                go.AddComponent<StandaloneInputModule>();
            }
#endif
        }

        private static GameObject BuildStartupPanel(Transform parent)
        {
            GameObject panel = CreatePanel("StartupPanel", parent, stretch: true);
            GameObject card = CreatePanel("Card", panel.transform, new Vector2(480f, 320f), center: true);
            CreateText("TitleText", card.transform, "Harmonic Engine Lab 2", 28, TextAnchor.MiddleCenter,
                new Vector2(0f, 60f), new Vector2(440f, 48f), bold: true, color: V4Lab2UITheme.AccentColor);
            CreateText("SubtitleText", card.transform, "GPU pendulum paint bucket", 16, TextAnchor.MiddleCenter,
                new Vector2(0f, 20f), new Vector2(440f, 28f), muted: true);
            CreateButton("RunSceneButton", card.transform, "Run this scene", primary: true,
                new Vector2(0f, -50f), new Vector2(360f, 48f));
            CreateButton("SetupSceneButton", card.transform, "Setup first", primary: false,
                new Vector2(0f, -110f), new Vector2(360f, 48f));
            return panel;
        }

        private sealed class WizardWidgets
        {
            public V4Lab2WizardTabController tabs;
            public V4Lab2HoleDiagramController holeDiagram;
            public Button[] tabButtons;
            public GameObject[] tabPages;
            public Slider pivotHeight;
            public Slider ropeLength;
            public Slider swingSpeed;
            public Dropdown swingDirection;
            public Slider damping;
            public Slider twist;
            public Slider slosh;
            public Slider[] layerThickness = new Slider[V4Lab2SessionConfig.LayerCount];
            public Button[] layerBlack = new Button[V4Lab2SessionConfig.LayerCount];
            public Button[] layerWhite = new Button[V4Lab2SessionConfig.LayerCount];
            public Button[] layerYellow = new Button[V4Lab2SessionConfig.LayerCount];
            public Button balanceLayers;
            public Slider hole0;
            public Slider hole1;
            public Toggle hole1Enabled;
            public Text holeInfo;
            public Slider torricelliHead;
            public Dropdown liquidProfile;
            public Text particleCount;
            public Slider density;
            public Slider carry;
            public Slider pbf;
            public Slider ghost;
            public Slider gravity;
            public Slider viscosity;
            public Slider cohesion;
            public Slider friction;
            public Slider restitution;
            public Slider zone0;
            public Slider colorDiffusion;
            public Slider settleEpsilon;
            public Toggle loggingEnabled;
            public InputField logDirectory;
            public Button browseLogFolder;
            public Button useDefaultLogFolder;
            public Text logFolderHint;
            public Toggle[] loggingChannels = new Toggle[V4Lab2LoggingChannels.All.Length];
            public Text validation;
            public Button apply;
            public Button reset;
            public Button back;
        }

        private static GameObject BuildWizardPanel(Transform parent, out WizardWidgets w)
        {
            w = new WizardWidgets();
            GameObject panel = CreatePanel("SetupWizardPanel", parent, stretch: true);
            GameObject card = CreatePanel("WizardCard", panel.transform, new Vector2(820f, 900f), center: true);

            CreateText("WizardTitle", card.transform, "Lab 2 setup", 24, TextAnchor.MiddleCenter,
                new Vector2(0f, 410f), new Vector2(760f, 40f), bold: true, color: V4Lab2UITheme.AccentColor);

            GameObject tabsRoot = new GameObject("WizardTabs", typeof(RectTransform));
            tabsRoot.transform.SetParent(card.transform, false);
            RectTransform tabsRootRect = tabsRoot.GetComponent<RectTransform>();
            tabsRootRect.anchorMin = new Vector2(0.5f, 1f);
            tabsRootRect.anchorMax = new Vector2(0.5f, 1f);
            tabsRootRect.pivot = new Vector2(0.5f, 1f);
            tabsRootRect.anchoredPosition = new Vector2(0f, -52f);
            tabsRootRect.sizeDelta = new Vector2(760f, 44f);
            w.tabs = tabsRoot.AddComponent<V4Lab2WizardTabController>();

            string[] tabLabels = { "Pendulum", "Paint", "Holes", "Simulation", "Liquid", "Logging" };
            GameObject tabBar = new GameObject("TabBar", typeof(RectTransform));
            tabBar.transform.SetParent(tabsRoot.transform, false);
            RectTransform tabBarRect = tabBar.GetComponent<RectTransform>();
            tabBarRect.anchorMin = Vector2.zero;
            tabBarRect.anchorMax = Vector2.one;
            tabBarRect.offsetMin = Vector2.zero;
            tabBarRect.offsetMax = Vector2.zero;

            Button[] tabButtons = new Button[tabLabels.Length];
            float tabSpacing = 124f;
            float startX = -((tabLabels.Length - 1) * 0.5f * tabSpacing);
            for (int i = 0; i < tabLabels.Length; i++)
            {
                tabButtons[i] = CreateButton(
                    "Tab_" + tabLabels[i],
                    tabBar.transform,
                    tabLabels[i],
                    primary: i == 0,
                    new Vector2(startX + i * tabSpacing, 0f),
                    new Vector2(116f, 36f));
            }

            GameObject contentArea = new GameObject("TabContentArea", typeof(RectTransform));
            contentArea.transform.SetParent(card.transform, false);
            RectTransform contentAreaRect = contentArea.GetComponent<RectTransform>();
            contentAreaRect.anchorMin = Vector2.zero;
            contentAreaRect.anchorMax = Vector2.one;
            contentAreaRect.offsetMin = new Vector2(24f, 120f);
            contentAreaRect.offsetMax = new Vector2(-24f, -108f);

            Transform pendulumContent;
            Transform paintContent;
            Transform holesContent;
            Transform simulationContent;
            Transform liquidContent;
            Transform loggingContent;
            GameObject pendulumPage = CreateScrollableTabPage("PendulumPage", contentArea.transform, out pendulumContent);
            GameObject paintPage = CreateScrollableTabPage("PaintPage", contentArea.transform, out paintContent);
            GameObject holesPage = CreateScrollableTabPage("HolesPage", contentArea.transform, out holesContent);
            GameObject simulationPage = CreateScrollableTabPage("SimulationPage", contentArea.transform, out simulationContent);
            GameObject liquidPage = CreateScrollableTabPage("LiquidPage", contentArea.transform, out liquidContent);
            GameObject loggingPage = CreateScrollableTabPage("LoggingPage", contentArea.transform, out loggingContent);
            pendulumPage.SetActive(true);

            GameObject[] tabPages = { pendulumPage, paintPage, holesPage, simulationPage, liquidPage, loggingPage };
            w.tabButtons = tabButtons;
            w.tabPages = tabPages;
            w.tabs.Bind(tabButtons, tabPages, 0);

            float y;

            y = 0f;
            CreateSectionHeader(pendulumContent, "Pendulum / bucket motion", ref y, topAnchored: true);
            w.pivotHeight = CreateLabeledSlider(pendulumContent, "Pivot height (Y)", 1f, 6f, 3f, ref y, topAnchored: true);
            w.ropeLength = CreateLabeledSlider(pendulumContent, "Rope length", 0.5f, 5f, 2.6f, ref y, topAnchored: true);
            w.swingSpeed = CreateLabeledSlider(pendulumContent, "Initial swing speed", 0f, 5f, 0f, ref y, topAnchored: true);
            w.swingDirection = CreateLabeledDropdown(pendulumContent, "Swing direction", new[] { "Side", "Forward" }, ref y, topAnchored: true);
            w.damping = CreateLabeledSlider(pendulumContent, "Damping", 0f, 2f, 0.05f, ref y, topAnchored: true);
            w.twist = CreateLabeledSlider(pendulumContent, "Twist (deg)", -180f, 180f, 0f, ref y, topAnchored: true);
            w.slosh = CreateLabeledSlider(pendulumContent, "Slosh feedback", 0f, 1f, 1f, ref y, topAnchored: true);
            FinalizeScrollContent(pendulumContent, y);

            y = 0f;
            CreateSectionHeader(paintContent, "Paint layers", ref y, topAnchored: true);
            w.balanceLayers = CreateInlineButton(paintContent, "Auto-balance thickness", ref y, topAnchored: true);
            for (int i = 0; i < V4Lab2SessionConfig.LayerCount; i++)
            {
                w.layerThickness[i] = CreateLabeledSlider(paintContent, $"Layer {i + 1} thickness", 0f, 0.1f, 0.1f, ref y, topAnchored: true);
                CreateLayerColorRow(paintContent, i + 1, ref y, out w.layerBlack[i], out w.layerWhite[i], out w.layerYellow[i], topAnchored: true);
            }
            FinalizeScrollContent(paintContent, y);

            y = 0f;
            w.holeDiagram = BuildHoleDiagramUi(holesContent, out w.hole0, out w.hole1, out w.hole1Enabled, out w.holeInfo, ref y, topAnchored: true);
            w.torricelliHead = CreateLabeledSlider(holesContent, "Exit head height", 0f, 1f, 0.4f, ref y, topAnchored: true);
            FinalizeScrollContent(holesContent, y);

            y = 0f;
            CreateSectionHeader(simulationContent, "Simulation", ref y, topAnchored: true);
            w.particleCount = CreateText("ParticleCountText", simulationContent,
                "Particles at start: --", 14, TextAnchor.MiddleLeft,
                new Vector2(0f, y), new Vector2(700f, 24f), muted: true, topAnchored: true);
            y -= 28f;
            w.liquidProfile = CreateLabeledDropdown(simulationContent, "Liquid profile", new[] { "Lab2 Paint", "Default" }, ref y, topAnchored: true);
            w.density = CreateLabeledSlider(simulationContent, "Particle density", 50000f, 400000f, 200000f, ref y, topAnchored: true);
            w.carry = CreateLabeledSlider(simulationContent, "Carry rate", 0f, 50f, 25f, ref y, topAnchored: true);
            w.pbf = CreateLabeledSlider(simulationContent, "PBF iterations", 1f, 4f, 2f, ref y, wholeNumbers: true, topAnchored: true);
            w.ghost = CreateLabeledSlider(simulationContent, "Ghost weight", 0f, 1f, 0f, ref y, topAnchored: true);
            w.gravity = CreateLabeledSlider(simulationContent, "Gravity Y", -20f, -1f, -9.81f, ref y, topAnchored: true);
            FinalizeScrollContent(simulationContent, y);

            y = 0f;
            CreateSectionHeader(liquidContent, "Liquid tuning", ref y, topAnchored: true);
            w.viscosity = CreateLabeledSlider(liquidContent, "Viscosity", 0f, 1f, 0.35f, ref y, topAnchored: true);
            w.cohesion = CreateLabeledSlider(liquidContent, "Cohesion", 0f, 1f, 0.65f, ref y, topAnchored: true);
            w.friction = CreateLabeledSlider(liquidContent, "Surface friction", 0f, 1f, 0.35f, ref y, topAnchored: true);
            w.restitution = CreateLabeledSlider(liquidContent, "Surface restitution", 0f, 1f, 0.05f, ref y, topAnchored: true);
            w.zone0 = CreateLabeledSlider(liquidContent, "Zone 0 strength", 0f, 5f, 3f, ref y, topAnchored: true);
            w.colorDiffusion = CreateLabeledSlider(liquidContent, "Color diffusion", 0f, 1f, 0.08f, ref y, topAnchored: true);
            w.settleEpsilon = CreateLabeledSlider(liquidContent, "Settle threshold", 0.01f, 0.2f, 0.04f, ref y, topAnchored: true);
            FinalizeScrollContent(liquidContent, y);

            var defaultChannelSettings = new HarmonicEngineV4.Logging.V4ChannelLogSettings();
            y = 0f;
            CreateSectionHeader(loggingContent, "Run logging", ref y, topAnchored: true);
            w.loggingEnabled = CreateLabeledToggle(loggingContent, "Enable run logging", true, ref y, topAnchored: true);
            y -= 8f;
            CreateText("LogFolderLabel", loggingContent, "Save folder on PC", 14, TextAnchor.MiddleLeft,
                new Vector2(-220f, y), new Vector2(260f, 22f), topAnchored: true);
            y -= 36f;
            w.logDirectory = CreateInputField(loggingContent, "LogDirectoryInput", "Leave empty for project Logs/Engine2",
                string.Empty, new Vector2(0f, y), new Vector2(520f, 32f), topAnchored: true);
            y -= 40f;
            w.browseLogFolder = CreateInlineButton(loggingContent, "Browse...", ref y, topAnchored: true);
            w.useDefaultLogFolder = CreateInlineButton(loggingContent, "Use project default", ref y, topAnchored: true);
            y -= 12f;
            w.logFolderHint = CreateText("LogFolderHint", loggingContent,
                "Runs save to: (project)/Logs/Engine2/run_*", 13, TextAnchor.UpperLeft,
                new Vector2(0f, y), new Vector2(700f, 48f), muted: true, topAnchored: true);
            y -= 56f;
            CreateSectionHeader(loggingContent, "Channels to record", ref y, topAnchored: true);
            for (int i = 0; i < V4Lab2LoggingChannels.All.Length; i++)
            {
                HarmonicEngineV4.Logging.V4LogCategory category = V4Lab2LoggingChannels.All[i];
                bool enabled = V4Lab2LoggingChannels.IsRecorded(defaultChannelSettings, category);
                w.loggingChannels[i] = CreateLabeledToggle(
                    loggingContent,
                    V4Lab2LoggingChannels.GetLabel(category),
                    enabled,
                    ref y,
                    topAnchored: true);
            }
            FinalizeScrollContent(loggingContent, y);

            w.validation = CreateText("ValidationText", card.transform, string.Empty, 14, TextAnchor.MiddleLeft,
                new Vector2(0f, -330f), new Vector2(760f, 40f));
            w.validation.color = new Color(1f, 0.45f, 0.4f);
            w.validation.gameObject.SetActive(false);

            w.apply = CreateButton("ApplyAndStartButton", card.transform, "Apply & Start", true,
                new Vector2(-130f, -380f), new Vector2(220f, 44f));
            w.back = CreateButton("BackButton", card.transform, "Back", false,
                new Vector2(140f, -380f), new Vector2(160f, 44f));
            w.reset = CreateButton("ResetDefaultsButton", card.transform, "Reset defaults", false,
                new Vector2(0f, -430f), new Vector2(220f, 40f));

            return panel;
        }

        private static GameObject CreateScrollableTabPage(string name, Transform parent, out Transform content)
        {
            GameObject page = CreateTabPage(name, parent);
            GameObject scroll = CreateStretchScrollView("ScrollView", page.transform);
            content = scroll.transform.Find("Viewport/Content");
            return page;
        }

        private static void FinalizeScrollContent(Transform content, float y)
        {
            if (content == null)
            {
                return;
            }

            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.sizeDelta = new Vector2(0f, Mathf.Max(120f, -y + 32f));
        }

        private static GameObject CreateTabPage(string name, Transform parent)
        {
            var page = new GameObject(name, typeof(RectTransform));
            page.transform.SetParent(parent, false);
            RectTransform rect = page.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            page.SetActive(false);
            return page;
        }

        private static V4Lab2HoleDiagramController BuildHoleDiagramUi(
            Transform parent,
            out Slider hole0,
            out Slider hole1,
            out Toggle hole1Enabled,
            out Text info,
            ref float y,
            bool topAnchored = false)
        {
            CreateSectionHeader(parent, "Bucket floor — drag holes to reposition", ref y, topAnchored);

            var diagramGo = new GameObject("HoleDiagram", typeof(RectTransform));
            diagramGo.transform.SetParent(parent, false);
            RectTransform diagramRect = diagramGo.GetComponent<RectTransform>();
            ApplyTopAnchoredRect(diagramRect, new Vector2(0f, y), new Vector2(320f, 320f), topAnchored);
            y -= 330f;

            var diagram = diagramGo.AddComponent<V4Lab2HoleDiagramController>();

            GameObject diagramRoot = CreatePanel("DiagramRoot", diagramGo.transform, new Vector2(280f, 280f), center: true);
            RectTransform diagramRootRect = diagramRoot.GetComponent<RectTransform>();
            Image floorImage = diagramRoot.GetComponent<Image>();
            floorImage.color = new Color(0.16f, 0.18f, 0.24f, 1f);
            V4Lab2UITheme.CreateChipBorder(diagramRoot.transform);

            CreateText("BucketLabel", diagramRoot.transform, "Top view", 12, TextAnchor.UpperCenter,
                new Vector2(0f, 118f), new Vector2(200f, 20f), muted: true);

            RectTransform hole0Marker = CreateHoleMarker(diagramRoot.transform, "Hole0Marker", new Color(1f, 0.55f, 0.2f, 0.8f), out Image hole0Fill, out Image hole0Selection);
            RectTransform hole1Marker = CreateHoleMarker(diagramRoot.transform, "Hole1Marker", new Color(0.3f, 0.85f, 1f, 0.8f), out Image hole1Fill, out Image hole1Selection);

            hole0 = CreateLabeledSlider(parent, "Hole 0 radius", 0f, 0.15f, 0.05f, ref y, topAnchored: topAnchored);
            hole1 = CreateLabeledSlider(parent, "Hole 1 radius", 0f, 0.15f, 0.03f, ref y, topAnchored: topAnchored);
            hole1Enabled = CreateLabeledToggle(parent, "Enable hole 1", true, ref y, topAnchored: topAnchored);
            info = CreateText("HoleDiagramInfo", parent,
                "Hole 0: drag on diagram to reposition.", 13, TextAnchor.UpperLeft,
                new Vector2(0f, y), new Vector2(700f, 52f), muted: true, topAnchored: topAnchored);
            y -= 64f;

            SerializedObject diagramSo = new SerializedObject(diagram);
            diagramSo.FindProperty("diagramRoot").objectReferenceValue = diagramRootRect;
            diagramSo.FindProperty("bucketFloorImage").objectReferenceValue = floorImage;
            diagramSo.FindProperty("hole0Marker").objectReferenceValue = hole0Marker;
            diagramSo.FindProperty("hole1Marker").objectReferenceValue = hole1Marker;
            diagramSo.FindProperty("hole0Fill").objectReferenceValue = hole0Fill;
            diagramSo.FindProperty("hole1Fill").objectReferenceValue = hole1Fill;
            diagramSo.FindProperty("hole0Selection").objectReferenceValue = hole0Selection;
            diagramSo.FindProperty("hole1Selection").objectReferenceValue = hole1Selection;
            diagramSo.ApplyModifiedPropertiesWithoutUndo();

            return diagram;
        }

        private static RectTransform CreateHoleMarker(
            Transform parent,
            string name,
            Color fillColor,
            out Image fill,
            out Image selection)
        {
            var marker = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            marker.transform.SetParent(parent, false);
            RectTransform rect = marker.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(24f, 24f);

            fill = marker.GetComponent<Image>();
            fill.color = fillColor;

            var selectionGo = new GameObject("Selection", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            selectionGo.transform.SetParent(marker.transform, false);
            RectTransform selectionRect = selectionGo.GetComponent<RectTransform>();
            selectionRect.anchorMin = Vector2.zero;
            selectionRect.anchorMax = Vector2.one;
            selectionRect.offsetMin = new Vector2(-4f, -4f);
            selectionRect.offsetMax = new Vector2(4f, 4f);
            selection = selectionGo.GetComponent<Image>();
            selection.color = V4Lab2UITheme.AccentColor;
            selection.enabled = false;
            selection.raycastTarget = false;

            return rect;
        }

        private sealed class PlayHudWidgets
        {
            public Text statsText;
            public Text pausedBadge;
            public CanvasGroup statsGroup;
            public Button collapseButton;
            public Button pauseButton;
            public Text pauseLabel;
            public Button tuningToggle;
        }

        private static GameObject BuildPlayHud(Transform parent, out PlayHudWidgets hud)
        {
            hud = new PlayHudWidgets();
            GameObject panel = CreatePanel("PlayHud", parent, stretch: true);
            panel.GetComponent<Image>().raycastTarget = false;

            GameObject statsPanel = CreatePanel("StatsPanel", panel.transform, new Vector2(360f, 220f),
                anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(0f, 1f), pivot: new Vector2(0f, 1f),
                anchoredPos: new Vector2(20f, -20f));
            hud.statsGroup = statsPanel.AddComponent<CanvasGroup>();
            hud.statsText = CreateText("StatsText", statsPanel.transform, "FPS --", 13, TextAnchor.UpperLeft,
                new Vector2(12f, -12f), new Vector2(330f, 170f));
            hud.statsText.fontStyle = FontStyle.Normal;
            hud.pausedBadge = CreateText("PausedBadge", statsPanel.transform, "PAUSED", 16, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(120f, 32f), bold: true, color: V4Lab2UITheme.PausedBadge);
            hud.pausedBadge.gameObject.SetActive(false);
            hud.collapseButton = CreateButton("CollapseStatsButton", statsPanel.transform, "Stats -", false,
                new Vector2(250f, -180f), new Vector2(90f, 28f));

            hud.pauseButton = CreateButton("PauseButton", panel.transform, "Pause", true,
                new Vector2(-20f, -20f), new Vector2(140f, 48f),
                anchorMin: new Vector2(1f, 1f), anchorMax: new Vector2(1f, 1f), pivot: new Vector2(1f, 1f));
            hud.pauseLabel = hud.pauseButton.GetComponentInChildren<Text>();

            hud.tuningToggle = CreateButton("TuningToggleButton", panel.transform, "Tuning", false,
                new Vector2(-20f, -80f), new Vector2(140f, 40f),
                anchorMin: new Vector2(1f, 1f), anchorMax: new Vector2(1f, 1f), pivot: new Vector2(1f, 1f));

            return panel;
        }

        private sealed class TuningWidgets
        {
            public Slider carry;
            public Slider damping;
            public Slider torricelli;
            public Slider pbf;
            public Slider ghost;
            public Toggle ssFluid;
            public Toggle debugPoints;
            public Button resetPendulum;
            public Button openSetup;
            public Button close;
        }

        private static GameObject BuildTuningPanel(Transform parent, out TuningWidgets tw)
        {
            tw = new TuningWidgets();
            GameObject panel = CreatePanel("RuntimeTuningPanel", parent,
                new Vector2(360f, 660f),
                anchorMin: new Vector2(1f, 0.5f), anchorMax: new Vector2(1f, 0.5f), pivot: new Vector2(1f, 0.5f),
                anchoredPos: new Vector2(-16f, 0f));

            Transform content = panel.transform;
            float y = 280f;
            CreateText("TuningTitle", content, "Runtime tuning", 20, TextAnchor.MiddleCenter,
                new Vector2(0f, y), new Vector2(320f, 36f), bold: true, color: V4Lab2UITheme.AccentColor);
            y -= 50f;
            tw.carry = CreateLabeledSlider(content, "Carry rate", 0f, 50f, 25f, ref y, panelSpace: true);
            tw.damping = CreateLabeledSlider(content, "Pendulum damping", 0f, 2f, 0.05f, ref y, panelSpace: true);
            tw.torricelli = CreateLabeledSlider(content, "Torricelli head", 0f, 1f, 0.4f, ref y, panelSpace: true);
            tw.pbf = CreateLabeledSlider(content, "PBF iterations", 1f, 4f, 2f, ref y, panelSpace: true, wholeNumbers: true);
            tw.ghost = CreateLabeledSlider(content, "Ghost weight", 0f, 1f, 0f, ref y, panelSpace: true);
            tw.ssFluid = CreateLabeledToggle(content, "Screen-space fluid", true, ref y, panelSpace: true);
            tw.debugPoints = CreateLabeledToggle(content, "Debug points", false, ref y, panelSpace: true);
            tw.resetPendulum = CreateButton("ResetPendulumButton", content, "Reset pendulum", false,
                new Vector2(0f, y), new Vector2(280f, 40f));
            y -= 52f;
            tw.openSetup = CreateButton("OpenSetupButton", content, "Open setup", false,
                new Vector2(0f, y), new Vector2(280f, 40f));
            y -= 52f;
            tw.close = CreateButton("CloseTuningButton", content, "Close", true,
                new Vector2(0f, y), new Vector2(280f, 40f));
            return panel;
        }

        private static void AssignWizardRefs(SerializedObject setupSo, WizardWidgets w)
        {
            setupSo.FindProperty("pivotHeightSlider").objectReferenceValue = w.pivotHeight;
            setupSo.FindProperty("ropeLengthSlider").objectReferenceValue = w.ropeLength;
            setupSo.FindProperty("swingSpeedSlider").objectReferenceValue = w.swingSpeed;
            setupSo.FindProperty("swingDirectionDropdown").objectReferenceValue = w.swingDirection;
            setupSo.FindProperty("dampingSlider").objectReferenceValue = w.damping;
            setupSo.FindProperty("twistSlider").objectReferenceValue = w.twist;
            setupSo.FindProperty("sloshSlider").objectReferenceValue = w.slosh;
            setupSo.FindProperty("balanceLayersButton").objectReferenceValue = w.balanceLayers;
            setupSo.FindProperty("hole0RadiusSlider").objectReferenceValue = w.hole0;
            setupSo.FindProperty("hole1RadiusSlider").objectReferenceValue = w.hole1;
            setupSo.FindProperty("hole1EnabledToggle").objectReferenceValue = w.hole1Enabled;
            setupSo.FindProperty("holeDiagramInfoText").objectReferenceValue = w.holeInfo;
            setupSo.FindProperty("wizardTabs").objectReferenceValue = w.tabs;
            setupSo.FindProperty("holeDiagram").objectReferenceValue = w.holeDiagram;
            setupSo.FindProperty("torricelliHeadSlider").objectReferenceValue = w.torricelliHead;
            setupSo.FindProperty("liquidProfileDropdown").objectReferenceValue = w.liquidProfile;
            setupSo.FindProperty("densitySlider").objectReferenceValue = w.density;
            setupSo.FindProperty("particleCountText").objectReferenceValue = w.particleCount;
            setupSo.FindProperty("carryRateSlider").objectReferenceValue = w.carry;
            setupSo.FindProperty("pbfSlider").objectReferenceValue = w.pbf;
            setupSo.FindProperty("ghostSlider").objectReferenceValue = w.ghost;
            setupSo.FindProperty("gravitySlider").objectReferenceValue = w.gravity;
            setupSo.FindProperty("viscositySlider").objectReferenceValue = w.viscosity;
            setupSo.FindProperty("cohesionSlider").objectReferenceValue = w.cohesion;
            setupSo.FindProperty("surfaceFrictionSlider").objectReferenceValue = w.friction;
            setupSo.FindProperty("surfaceRestitutionSlider").objectReferenceValue = w.restitution;
            setupSo.FindProperty("zone0StrengthSlider").objectReferenceValue = w.zone0;
            setupSo.FindProperty("colorDiffusionSlider").objectReferenceValue = w.colorDiffusion;
            setupSo.FindProperty("settleEpsilonSlider").objectReferenceValue = w.settleEpsilon;
            setupSo.FindProperty("loggingEnabledToggle").objectReferenceValue = w.loggingEnabled;
            setupSo.FindProperty("logDirectoryInput").objectReferenceValue = w.logDirectory;
            setupSo.FindProperty("browseLogFolderButton").objectReferenceValue = w.browseLogFolder;
            setupSo.FindProperty("useDefaultLogFolderButton").objectReferenceValue = w.useDefaultLogFolder;
            setupSo.FindProperty("logFolderHintText").objectReferenceValue = w.logFolderHint;
            setupSo.FindProperty("validationText").objectReferenceValue = w.validation;
            setupSo.FindProperty("applyAndStartButton").objectReferenceValue = w.apply;
            setupSo.FindProperty("resetDefaultsButton").objectReferenceValue = w.reset;
            setupSo.FindProperty("wizardBackButton").objectReferenceValue = w.back;

            SerializedProperty layerThickness = setupSo.FindProperty("layerThicknessSliders");
            SerializedProperty layerBlack = setupSo.FindProperty("layerBlackButtons");
            SerializedProperty layerWhite = setupSo.FindProperty("layerWhiteButtons");
            SerializedProperty layerYellow = setupSo.FindProperty("layerYellowButtons");
            layerThickness.arraySize = V4Lab2SessionConfig.LayerCount;
            layerBlack.arraySize = V4Lab2SessionConfig.LayerCount;
            layerWhite.arraySize = V4Lab2SessionConfig.LayerCount;
            layerYellow.arraySize = V4Lab2SessionConfig.LayerCount;
            for (int i = 0; i < V4Lab2SessionConfig.LayerCount; i++)
            {
                layerThickness.GetArrayElementAtIndex(i).objectReferenceValue = w.layerThickness[i];
                layerBlack.GetArrayElementAtIndex(i).objectReferenceValue = w.layerBlack[i];
                layerWhite.GetArrayElementAtIndex(i).objectReferenceValue = w.layerWhite[i];
                layerYellow.GetArrayElementAtIndex(i).objectReferenceValue = w.layerYellow[i];
            }

            SerializedProperty loggingChannels = setupSo.FindProperty("loggingChannelToggles");
            loggingChannels.arraySize = V4Lab2LoggingChannels.All.Length;
            for (int i = 0; i < V4Lab2LoggingChannels.All.Length; i++)
            {
                loggingChannels.GetArrayElementAtIndex(i).objectReferenceValue = w.loggingChannels[i];
            }

            if (w.tabs != null && w.tabButtons != null && w.tabPages != null)
            {
                SerializedObject tabsSo = new SerializedObject(w.tabs);
                SerializedProperty tabButtonProp = tabsSo.FindProperty("tabButtons");
                tabButtonProp.arraySize = w.tabButtons.Length;
                for (int i = 0; i < w.tabButtons.Length; i++)
                {
                    tabButtonProp.GetArrayElementAtIndex(i).objectReferenceValue = w.tabButtons[i];
                }

                SerializedProperty tabPageProp = tabsSo.FindProperty("tabPages");
                tabPageProp.arraySize = w.tabPages.Length;
                for (int i = 0; i < w.tabPages.Length; i++)
                {
                    tabPageProp.GetArrayElementAtIndex(i).objectReferenceValue = w.tabPages[i];
                }

                tabsSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void ApplyTopAnchoredRect(RectTransform rect, Vector2 pos, Vector2 size, bool topAnchored)
        {
            if (topAnchored)
            {
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
            }
            else
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
            }

            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        private static GameObject CreatePanel(
            string name,
            Transform parent,
            Vector2? size = null,
            bool stretch = false,
            bool center = false,
            Vector2? anchorMin = null,
            Vector2? anchorMax = null,
            Vector2? pivot = null,
            Vector2? anchoredPos = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            Image image = go.GetComponent<Image>();
            if (stretch)
            {
                V4Lab2UITheme.ApplyPanel(image);
            }
            else
            {
                V4Lab2UITheme.ApplyCard(image);
            }

            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                return go;
            }

            rect.anchorMin = anchorMin ?? (center ? new Vector2(0.5f, 0.5f) : Vector2.zero);
            rect.anchorMax = anchorMax ?? (center ? new Vector2(0.5f, 0.5f) : Vector2.zero);
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size ?? Vector2.zero;
            rect.anchoredPosition = anchoredPos ?? Vector2.zero;
            return go;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string text,
            int fontSize,
            TextAnchor anchor,
            Vector2 pos,
            Vector2 size,
            bool bold = false,
            bool muted = false,
            Color? color = null,
            bool topAnchored = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            ApplyTopAnchoredRect(rect, pos, size, topAnchored);

            Text label = go.GetComponent<Text>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = anchor;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.font = V4Lab2UITheme.DefaultFont;
            label.color = color ?? (muted ? V4Lab2UITheme.MutedTextColor : V4Lab2UITheme.TextColor);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string label,
            bool primary,
            Vector2 pos,
            Vector2 size,
            Vector2? anchorMin = null,
            Vector2? anchorMax = null,
            Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin ?? new Vector2(0.5f, 0.5f);
            rect.anchorMax = anchorMax ?? new Vector2(0.5f, 0.5f);
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            Button button = go.GetComponent<Button>();
            V4Lab2UITheme.ApplyButton(button, primary);
            CreateText("Text", go.transform, label, 14, TextAnchor.MiddleCenter, Vector2.zero, size);
            return button;
        }

        private static void CreateSectionHeader(Transform parent, string title, ref float y, bool topAnchored = false)
        {
            y -= V4Lab2UITheme.SectionSpacing;
            CreateText("Header_" + title, parent, title, V4Lab2UITheme.SectionFontSize, TextAnchor.MiddleLeft,
                new Vector2(0f, y), new Vector2(640f, 28f), bold: true, color: V4Lab2UITheme.AccentColor, topAnchored: topAnchored);
            y -= 28f;
        }

        private static Slider CreateLabeledSlider(
            Transform parent,
            string label,
            float min,
            float max,
            float value,
            ref float y,
            bool wholeNumbers = false,
            bool panelSpace = false,
            bool topAnchored = false)
        {
            y -= panelSpace ? 56f : 48f;
            CreateText("Label_" + label, parent, label, 14, TextAnchor.MiddleLeft,
                new Vector2(-220f, y + 8f), new Vector2(260f, 22f), topAnchored: topAnchored);
            Slider slider = CreateSlider(parent, label + "Slider", min, max, value, new Vector2(40f, y), new Vector2(260f, 22f), wholeNumbers, topAnchored);
            var binder = slider.gameObject.AddComponent<V4Lab2SliderValueLabel>();
            Text valueLabel = V4Lab2UITheme.CreateValueLabel(slider.transform);
            binder.Bind(slider, valueLabel, max > 1000f ? "0" : "0.###", wholeNumbers);
            return slider;
        }

        private static Dropdown CreateLabeledDropdown(Transform parent, string label, string[] options, ref float y, bool topAnchored = false)
        {
            y -= 52f;
            CreateText("Label_" + label, parent, label, 14, TextAnchor.MiddleLeft,
                new Vector2(-150f, y + 8f), new Vector2(300f, 22f), topAnchored: topAnchored);
            return CreateDropdown(parent, label + "Dropdown", options, new Vector2(120f, y), new Vector2(300f, 30f), topAnchored);
        }

        private static Toggle CreateLabeledToggle(Transform parent, string label, bool value, ref float y, bool panelSpace = false, bool topAnchored = false)
        {
            y -= panelSpace ? 44f : 36f;
            var go = new GameObject(label + "Toggle", typeof(RectTransform), typeof(Toggle));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            ApplyTopAnchoredRect(rect, new Vector2(0f, y), new Vector2(640f, 32f), topAnchored);

            Toggle toggle = go.GetComponent<Toggle>();
            toggle.isOn = value;
            CreateText("Label", go.transform, label, 14, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(580f, 24f));
            V4Lab2UITheme.EnsureToggleGraphics(toggle);
            return toggle;
        }

        private static Button CreateInlineButton(Transform parent, string label, ref float y, bool topAnchored = false)
        {
            y -= 44f;
            if (!topAnchored)
            {
                return CreateButton(label.Replace(" ", "") + "Button", parent, label, false, new Vector2(0f, y), new Vector2(280f, 34f));
            }

            var go = new GameObject(label.Replace(" ", "") + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            ApplyTopAnchoredRect(rect, new Vector2(0f, y), new Vector2(280f, 34f), true);
            Button button = go.GetComponent<Button>();
            V4Lab2UITheme.ApplyButton(button, false);
            CreateText("Text", go.transform, label, 14, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(280f, 34f));
            return button;
        }

        private static InputField CreateInputField(
            Transform parent,
            string name,
            string placeholder,
            string value,
            Vector2 pos,
            Vector2 size,
            bool topAnchored = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            ApplyTopAnchoredRect(rect, pos, size, topAnchored);
            Image background = go.GetComponent<Image>();
            background.color = V4Lab2UITheme.SliderTrack;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 4f);
            textRect.offsetMax = new Vector2(-10f, -4f);
            Text text = textGo.GetComponent<Text>();
            text.text = value ?? string.Empty;
            text.supportRichText = false;
            V4Lab2UITheme.ApplyBodyText(text);

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            placeholderGo.transform.SetParent(go.transform, false);
            RectTransform placeholderRect = placeholderGo.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(10f, 4f);
            placeholderRect.offsetMax = new Vector2(-10f, -4f);
            Text placeholderText = placeholderGo.GetComponent<Text>();
            placeholderText.text = placeholder ?? string.Empty;
            placeholderText.fontStyle = FontStyle.Italic;
            V4Lab2UITheme.ApplyBodyText(placeholderText, muted: true);

            InputField input = go.GetComponent<InputField>();
            input.textComponent = text;
            input.placeholder = placeholderText;
            input.text = value ?? string.Empty;
            input.lineType = InputField.LineType.SingleLine;
            return input;
        }

        private static void CreateLayerColorRow(
            Transform parent,
            int layerNumber,
            ref float y,
            out Button black,
            out Button white,
            out Button yellow,
            bool topAnchored = false)
        {
            y -= 52f;
            var row = new GameObject($"Layer{layerNumber}ColorRow", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            ApplyTopAnchoredRect(rowRect, new Vector2(40f, y), new Vector2(580f, 44f), topAnchored);

            CreateText($"Layer{layerNumber}ColorLabel", row.transform, $"Layer {layerNumber} color", 14,
                TextAnchor.MiddleLeft, new Vector2(-250f, 0f), new Vector2(150f, 24f));

            var previewGo = new GameObject($"Layer{layerNumber}Preview",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            previewGo.transform.SetParent(row.transform, false);
            RectTransform previewRect = previewGo.GetComponent<RectTransform>();
            previewRect.anchorMin = new Vector2(0.5f, 0.5f);
            previewRect.anchorMax = new Vector2(0.5f, 0.5f);
            previewRect.pivot = new Vector2(0.5f, 0.5f);
            previewRect.anchoredPosition = new Vector2(-70f, 0f);
            previewRect.sizeDelta = new Vector2(56f, 36f);
            previewGo.GetComponent<Image>().color = Color.white;
            V4Lab2UITheme.CreateChipBorder(previewGo.transform);

            black = CreateColorChipButton(row.transform, $"L{layerNumber}BlackButton", Color.black, new Vector2(10f, 0f));
            white = CreateColorChipButton(row.transform, $"L{layerNumber}WhiteButton", Color.white, new Vector2(58f, 0f));
            yellow = CreateColorChipButton(row.transform, $"L{layerNumber}YellowButton", Color.yellow, new Vector2(106f, 0f));
        }

        private static Button CreateColorChipButton(Transform parent, string name, Color color, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(V4Lab2UITheme.ChipSize, V4Lab2UITheme.ChipSize);

            Button button = go.GetComponent<Button>();
            V4Lab2UITheme.UpgradeButtonToColorChip(button, color);
            return button;
        }

        private static Button CreateColorSwatch(Transform parent, string label, Color color, ref float y)
        {
            y -= 36f;
            Button button = CreateButton(label.Replace(" ", "") + "Button", parent, label, false,
                new Vector2(0f, y), new Vector2(200f, 28f));
            V4Lab2UITheme.UpgradeButtonToColorChip(button, color);
            return button;
        }

        private static Slider CreateSlider(Transform parent, string name, float min, float max, float value, Vector2 pos, Vector2 size, bool wholeNumbers, bool topAnchored = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            ApplyTopAnchoredRect(rect, pos, size, topAnchored);

            var background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            background.transform.SetParent(go.transform, false);
            RectTransform bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = V4Lab2UITheme.SliderTrack;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(5f, 0f);
            fillAreaRect.offsetMax = new Vector2(-5f, 0f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = V4Lab2UITheme.SliderFill;

            var handleSlideArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleSlideArea.transform.SetParent(go.transform, false);
            RectTransform handleAreaRect = handleSlideArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10f, 0f);
            handleAreaRect.offsetMax = new Vector2(-10f, 0f);

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handle.transform.SetParent(handleSlideArea.transform, false);
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(16f, 16f);
            handle.GetComponent<Image>().color = V4Lab2UITheme.AccentColor;

            Slider slider = go.GetComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.value = value;
            V4Lab2UITheme.ApplySlider(slider);
            return slider;
        }

        private static Dropdown CreateDropdown(Transform parent, string name, string[] options, Vector2 pos, Vector2 size, bool topAnchored = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Dropdown));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            ApplyTopAnchoredRect(rect, pos, size, topAnchored);
            go.GetComponent<Image>().color = V4Lab2UITheme.ButtonSecondary;

            Text label = CreateText("Label", go.transform, options.Length > 0 ? options[0] : string.Empty, 14,
                TextAnchor.MiddleLeft, new Vector2(8f, 0f), new Vector2(size.x - 30f, size.y));
            label.raycastTarget = false;

            var arrow = CreateText("Arrow", go.transform, "v", 14, TextAnchor.MiddleCenter,
                new Vector2(size.x * 0.5f - 16f, 0f), new Vector2(20f, size.y));

            var template = new GameObject("Template", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            template.transform.SetParent(go.transform, false);
            template.SetActive(false);
            RectTransform templateRect = template.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = Vector2.zero;
            templateRect.sizeDelta = new Vector2(0f, 120f);
            template.GetComponent<Image>().color = V4Lab2UITheme.PanelColor;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(template.transform, false);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = V4Lab2UITheme.PanelColor;
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 28f);

            var item = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            item.transform.SetParent(content.transform, false);
            RectTransform itemRect = item.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.sizeDelta = new Vector2(0f, 28f);
            Text itemLabel = CreateText("Item Label", item.transform, "Option", 14, TextAnchor.MiddleLeft,
                new Vector2(10f, 0f), new Vector2(size.x - 20f, 24f));

            ScrollRect scrollRect = template.GetComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            Dropdown dropdown = go.GetComponent<Dropdown>();
            dropdown.targetGraphic = go.GetComponent<Image>();
            dropdown.captionText = label;
            dropdown.itemText = itemLabel;
            dropdown.template = templateRect;
            dropdown.options.Clear();
            foreach (string option in options)
            {
                dropdown.options.Add(new Dropdown.OptionData(option));
            }

            dropdown.value = 0;
            dropdown.RefreshShownValue();
            return dropdown;
        }

        private static GameObject CreateStretchScrollView(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(go.transform, false);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = V4Lab2UITheme.PanelColor;
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 400f);

            ScrollRect scroll = go.GetComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = viewportRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            return go;
        }

        private static GameObject CreateScrollView(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.15f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(go.transform, false);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = V4Lab2UITheme.PanelColor;
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 1200f);

            ScrollRect scroll = go.GetComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = viewportRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            return go;
        }

        private static T FindFirstObjectByType<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }
    }
}
#endif
