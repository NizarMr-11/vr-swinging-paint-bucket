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
                tuningWidgets.slosh,
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
            public Slider torricelliHead;
            public Dropdown liquidProfile;
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
            public Text validation;
            public Button apply;
            public Button reset;
            public Button back;
        }

        private static GameObject BuildWizardPanel(Transform parent, out WizardWidgets w)
        {
            w = new WizardWidgets();
            GameObject panel = CreatePanel("SetupWizardPanel", parent, stretch: true);
            GameObject card = CreatePanel("WizardCard", panel.transform, new Vector2(760f, 900f), center: true);

            GameObject scroll = CreateScrollView("ScrollView", card.transform, new Vector2(0f, 70f), new Vector2(720f, 700f));
            Transform content = scroll.transform.Find("Viewport/Content");

            float y = 0f;
            CreateSectionHeader(content, "Pendulum / bucket motion", ref y);
            w.pivotHeight = CreateLabeledSlider(content, "Pivot height (Y)", 1f, 6f, 3f, ref y);
            w.ropeLength = CreateLabeledSlider(content, "Rope length", 0.5f, 5f, 2.6f, ref y);
            w.swingSpeed = CreateLabeledSlider(content, "Initial swing speed", 0f, 5f, 0f, ref y);
            w.swingDirection = CreateLabeledDropdown(content, "Swing direction", new[] { "Side", "Forward" }, ref y);
            w.damping = CreateLabeledSlider(content, "Damping", 0f, 2f, 0.05f, ref y);
            w.twist = CreateLabeledSlider(content, "Twist (deg)", -180f, 180f, 0f, ref y);
            w.slosh = CreateLabeledSlider(content, "Slosh feedback", 0f, 1f, 1f, ref y);

            CreateSectionHeader(content, "Paint layers", ref y);
            w.balanceLayers = CreateInlineButton(content, "Auto-balance thickness", ref y);
            for (int i = 0; i < V4Lab2SessionConfig.LayerCount; i++)
            {
                w.layerThickness[i] = CreateLabeledSlider(content, $"Layer {i + 1} thickness", 0.05f, 0.5f, 0.33f, ref y);
                w.layerBlack[i] = CreateColorSwatch(content, $"L{i + 1} Black", Color.black, ref y);
                w.layerWhite[i] = CreateColorSwatch(content, $"L{i + 1} White", Color.white, ref y);
                w.layerYellow[i] = CreateColorSwatch(content, $"L{i + 1} Yellow", Color.yellow, ref y);
            }

            CreateSectionHeader(content, "Holes", ref y);
            w.hole0 = CreateLabeledSlider(content, "Hole 0 radius", 0f, 0.15f, 0.05f, ref y);
            w.hole1 = CreateLabeledSlider(content, "Hole 1 radius", 0f, 0.15f, 0.03f, ref y);
            w.torricelliHead = CreateLabeledSlider(content, "Exit head height", 0f, 1f, 0.4f, ref y);

            CreateSectionHeader(content, "Simulation", ref y);
            w.liquidProfile = CreateLabeledDropdown(content, "Liquid profile", new[] { "Lab2 Paint", "Default" }, ref y);
            w.density = CreateLabeledSlider(content, "Particle density", 50000f, 400000f, 200000f, ref y);
            w.carry = CreateLabeledSlider(content, "Carry rate", 0f, 50f, 25f, ref y);
            w.pbf = CreateLabeledSlider(content, "PBF iterations", 1f, 4f, 2f, ref y, wholeNumbers: true);
            w.ghost = CreateLabeledSlider(content, "Ghost weight", 0f, 1f, 0f, ref y);
            w.gravity = CreateLabeledSlider(content, "Gravity Y", -20f, -1f, -9.81f, ref y);

            CreateSectionHeader(content, "Liquid tuning", ref y);
            w.viscosity = CreateLabeledSlider(content, "Viscosity", 0f, 1f, 0.35f, ref y);
            w.cohesion = CreateLabeledSlider(content, "Cohesion", 0f, 1f, 0.65f, ref y);
            w.friction = CreateLabeledSlider(content, "Surface friction", 0f, 1f, 0.35f, ref y);
            w.restitution = CreateLabeledSlider(content, "Surface restitution", 0f, 1f, 0.05f, ref y);
            w.zone0 = CreateLabeledSlider(content, "Zone 0 strength", 0f, 5f, 3f, ref y);
            w.colorDiffusion = CreateLabeledSlider(content, "Color diffusion", 0f, 1f, 0.08f, ref y);
            w.settleEpsilon = CreateLabeledSlider(content, "Settle threshold", 0.01f, 0.2f, 0.04f, ref y);

            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.sizeDelta = new Vector2(680f, -y + 40f);

            w.validation = CreateText("ValidationText", card.transform, string.Empty, 14, TextAnchor.MiddleLeft,
                new Vector2(0f, -330f), new Vector2(680f, 40f));
            w.validation.color = new Color(1f, 0.45f, 0.4f);
            w.validation.gameObject.SetActive(false);

            w.apply = CreateButton("ApplyAndStartButton", card.transform, "Apply & Start", true,
                new Vector2(-120f, -380f), new Vector2(220f, 44f));
            w.back = CreateButton("BackButton", card.transform, "Back", false,
                new Vector2(150f, -380f), new Vector2(160f, 44f));
            w.reset = CreateButton("ResetDefaultsButton", card.transform, "Reset defaults", false,
                new Vector2(0f, -430f), new Vector2(220f, 40f));

            return panel;
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
            public Slider slosh;
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
                new Vector2(360f, 720f),
                anchorMin: new Vector2(1f, 0.5f), anchorMax: new Vector2(1f, 0.5f), pivot: new Vector2(1f, 0.5f),
                anchoredPos: new Vector2(-16f, 0f));

            Transform content = panel.transform;
            float y = 300f;
            CreateText("TuningTitle", content, "Runtime tuning", 20, TextAnchor.MiddleCenter,
                new Vector2(0f, y), new Vector2(320f, 36f), bold: true, color: V4Lab2UITheme.AccentColor);
            y -= 50f;
            tw.carry = CreateLabeledSlider(content, "Carry rate", 0f, 50f, 25f, ref y, panelSpace: true);
            tw.damping = CreateLabeledSlider(content, "Pendulum damping", 0f, 2f, 0.05f, ref y, panelSpace: true);
            tw.slosh = CreateLabeledSlider(content, "Slosh feedback", 0f, 1f, 1f, ref y, panelSpace: true);
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
            setupSo.FindProperty("torricelliHeadSlider").objectReferenceValue = w.torricelliHead;
            setupSo.FindProperty("liquidProfileDropdown").objectReferenceValue = w.liquidProfile;
            setupSo.FindProperty("densitySlider").objectReferenceValue = w.density;
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
            V4Lab2UITheme.ApplyPanel(image);

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
            Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

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

        private static void CreateSectionHeader(Transform parent, string title, ref float y)
        {
            y -= V4Lab2UITheme.SectionSpacing;
            CreateText("Header_" + title, parent, title, V4Lab2UITheme.SectionFontSize, TextAnchor.MiddleLeft,
                new Vector2(0f, y), new Vector2(640f, 28f), bold: true, color: V4Lab2UITheme.AccentColor);
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
            bool panelSpace = false)
        {
            y -= panelSpace ? 56f : 48f;
            CreateText("Label_" + label, parent, label, 14, TextAnchor.MiddleLeft,
                new Vector2(-150f, y + 8f), new Vector2(300f, 22f));
            Slider slider = CreateSlider(parent, label + "Slider", min, max, value, new Vector2(120f, y), new Vector2(300f, 20f), wholeNumbers);
            return slider;
        }

        private static Dropdown CreateLabeledDropdown(Transform parent, string label, string[] options, ref float y)
        {
            y -= 52f;
            CreateText("Label_" + label, parent, label, 14, TextAnchor.MiddleLeft,
                new Vector2(-150f, y + 8f), new Vector2(300f, 22f));
            return CreateDropdown(parent, label + "Dropdown", options, new Vector2(120f, y), new Vector2(300f, 30f));
        }

        private static Toggle CreateLabeledToggle(Transform parent, string label, bool value, ref float y, bool panelSpace = false)
        {
            y -= panelSpace ? 44f : 40f;
            var go = new GameObject(label + "Toggle", typeof(RectTransform), typeof(Toggle));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(300f, 28f);

            Toggle toggle = go.GetComponent<Toggle>();
            toggle.isOn = value;
            CreateText("Label", go.transform, label, 14, TextAnchor.MiddleLeft, new Vector2(-120f, 0f), new Vector2(260f, 24f));
            return toggle;
        }

        private static Button CreateInlineButton(Transform parent, string label, ref float y)
        {
            y -= 44f;
            return CreateButton(label.Replace(" ", "") + "Button", parent, label, false, new Vector2(0f, y), new Vector2(280f, 34f));
        }

        private static Button CreateColorSwatch(Transform parent, string label, Color color, ref float y)
        {
            y -= 36f;
            Button button = CreateButton(label.Replace(" ", "") + "Button", parent, label, false,
                new Vector2(0f, y), new Vector2(200f, 28f));
            Image image = button.GetComponent<Image>();
            image.color = Color.Lerp(color, V4Lab2UITheme.ButtonSecondary, 0.35f);
            return button;
        }

        private static Slider CreateSlider(Transform parent, string name, float min, float max, float value, Vector2 pos, Vector2 size, bool wholeNumbers)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

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

        private static Dropdown CreateDropdown(Transform parent, string name, string[] options, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Dropdown));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
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
