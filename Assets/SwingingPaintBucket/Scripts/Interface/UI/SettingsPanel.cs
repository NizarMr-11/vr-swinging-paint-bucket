using UnityEngine;
using UnityEngine.UI;
using SwingingPaintBucket.Pendulum;
using SwingingPaintBucket.Bucket;
using System.Collections.Generic;
using SwingingPaintBucket.Materials;
using SwingingPaintBucket.Simulation;
using SwingingPaintBucket.Canvas;
using SwingingPaintBucket.Particles;

namespace SwingingPaintBucket.Interface.UI
{
    public class SettingsPanel : MonoBehaviour
    {
        private RetroUIConfig _config;
        private PendulumSimulator _pendulum;
        private BucketController _bucket;
        private EnvironmentController _environment;
        private Transform _parentTransform;

        // Pendulum Parameters
        private Slider _massSlider, _ropeLengthSlider, _gravitySlider, _dampingSlider;
        private Slider _initialAngleSlider, _angularVelocitySlider;
        private Slider _initialPhiSlider, _phiAngularVelocitySlider;

        // Paint Parameters
        private Slider _paintVolumeSlider, _viscositySlider, _densitySlider, _nozzleRadiusSlider;
        private Slider _dischargeSlider, _paintLossSlider, _absorptionSlider;

        // Paint Parameters - NEW
        private Slider _volumePerParticleSlider;
        private Slider _maxParticlesSlider;

        // Environment Parameters
        private Slider _windXSlider, _windYSlider, _windZSlider;
        private Slider _temperatureSlider, _humiditySlider;

        // Canvas Parameters - NEW
        private Slider _textureWidthSlider, _textureHeightSlider;
        private Slider _particleImpactSizeSlider;
        private Slider _materialSpreadMultiplierSlider;

        // Material selection using tabs instead of dropdown
        private int _selectedMaterialIndex = 0;
        private List<GameObject> _materialTabButtons = new List<GameObject>();
        private List<Text> _materialTabTexts = new List<Text>();
        private Color _selectedTabColor = new Color(0.3f, 0.6f, 1f);
        private Color _unselectedTabColor = new Color(0.15f, 0.15f, 0.2f);

        // Canvas Surface Type - NEW
        private int _selectedSurfaceIndex = 0;
        private List<GameObject> _surfaceTabButtons = new List<GameObject>();
        private List<Text> _surfaceTabTexts = new List<Text>();

        private Image _paintColorPreview;
        private Color _selectedPaintColor = Color.red;

        // Canvas reference for applying settings
        private CanvasController _canvasController;

        [System.Obsolete]
        public void Initialize(Transform parent, RetroUIConfig config)
        {
            Debug.Log("SettingsPanel.Initialize called!");
            
            _config = config;
            _parentTransform = parent;
            
            var simManager = FindObjectOfType<SimulationManager>();
            if (simManager != null && simManager.BucketObject != null)
            {
                _pendulum = simManager.BucketObject.GetComponent<PendulumSimulator>();
                _bucket = simManager.BucketObject.GetComponent<BucketController>();
                _environment = _pendulum?.Environment;
                
                // Find CanvasController
                _canvasController = FindObjectOfType<CanvasController>();
                
                Debug.Log("Found Pendulum, Bucket and Canvas references");
            }
            else
            {
                Debug.LogWarning("Could not find SimulationManager or BucketObject");
            }

            BuildSettingsUI(parent);
            LoadSavedValues();
            
            Debug.Log("SettingsPanel initialization complete!");
        }

        private void BuildSettingsUI(Transform parent)
        {
            Debug.Log("Building Settings UI with parent: " + parent.name);
            
            var vLayout = parent.GetComponent<VerticalLayoutGroup>();
            if (vLayout == null)
            {
                vLayout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
                Debug.Log("Added VerticalLayoutGroup to parent");
            }
            
            vLayout.spacing = 15;
            vLayout.padding = new RectOffset(20, 20, 10, 10);
            vLayout.childForceExpandWidth = true;
            vLayout.childControlHeight = true;
            vLayout.childForceExpandHeight = false;

            BuildPendulumSection(parent);
            BuildPaintSection(parent);
            BuildMaterialSection(parent);
            BuildColorSection(parent);
            BuildEnvironmentSection(parent);
            BuildCanvasSection(parent);
            
            Debug.Log("All sections built successfully!");
        }

        private void BuildPendulumSection(Transform parent)
        {
            Debug.Log("Building Pendulum Section...");
            
            CreateSectionHeader(parent, "⚡ PENDULUM PHYSICS");
            
            _massSlider = CreateHorizontalSlider(parent, "Mass", 0.1f, 50f, 1f, "kg");
            _ropeLengthSlider = CreateHorizontalSlider(parent, "Rope Length", 0.5f, 20f, 5f, "m");
            _gravitySlider = CreateHorizontalSlider(parent, "Gravity", 0f, 20f, 9.81f, "m/s²");
            _dampingSlider = CreateHorizontalSlider(parent, "Damping", 0f, 1f, 0.05f, "");
            _initialAngleSlider = CreateHorizontalSlider(parent, "Initial Angle", -180f, 180f, 45f, "°");
            _angularVelocitySlider = CreateHorizontalSlider(parent, "Angular Velocity", -10f, 10f, 0f, "rad/s");
            _initialPhiSlider = CreateHorizontalSlider(parent, "Initial Phi Angle", -180f, 180f, 0f, "°");
            _phiAngularVelocitySlider = CreateHorizontalSlider(parent, "Phi Angular Velocity", -10f, 10f, 0f, "rad/s");
            
            CreateSeparator(parent);
            
            Debug.Log("Pendulum Section built with 8 sliders");
        }

        private void BuildPaintSection(Transform parent)
        {
            Debug.Log("Building Paint Section...");
            
            CreateSectionHeader(parent, "🎨 PAINT PROPERTIES");
            
            _paintVolumeSlider = CreateHorizontalSlider(parent, "Paint Volume", 0.1f, 10f, 2f, "L");
            _viscositySlider = CreateHorizontalSlider(parent, "Viscosity", 0.1f, 10f, 1f, "Pa·s");
            _densitySlider = CreateHorizontalSlider(parent, "Density", 0.1f, 5f, 1f, "g/cm³");
            _nozzleRadiusSlider = CreateHorizontalSlider(parent, "Nozzle Radius", 0.001f, 0.05f, 0.005f, "m");
            _dischargeSlider = CreateHorizontalSlider(parent, "Discharge Coeff.", 0.1f, 1f, 0.7f, "");
            _paintLossSlider = CreateHorizontalSlider(parent, "Paint Loss Rate", 0f, 0.5f, 0f, "L/s");
            _absorptionSlider = CreateHorizontalSlider(parent, "Absorption Rate", 0f, 0.1f, 0f, "L/s");
            
            CreateSeparator(parent);
            
            // Particle emission parameters
            CreateSectionHeader(parent, "💧 PARTICLE EMISSION");
            
            _volumePerParticleSlider = CreateHorizontalSlider(parent, "Volume Per Particle", 0.00001f, 0.001f, 0.0001f, "L");
            _maxParticlesSlider = CreateHorizontalSlider(parent, "Max Particles", 1000f, 500000f, 100000f, "");
            
            CreateSeparator(parent);
            
            Debug.Log("Paint Section built with 9 sliders");
        }

        private void BuildMaterialSection(Transform parent)
        {
            Debug.Log("Building Material Section...");
            
            CreateSectionHeader(parent, "🔧 BUCKET MATERIAL");
            
            // Material Tabs
            CreateMaterialTabs(parent);
            
            CreateSeparator(parent);
            
            Debug.Log("Material Section built with tabs");
        }

        private void CreateMaterialTabs(Transform parent)
        {
            // Container للـ Tabs
            var tabsContainer = new GameObject("TabsContainer", typeof(RectTransform));
            tabsContainer.transform.SetParent(parent, false);
            
            var layoutElement = tabsContainer.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 55;
            layoutElement.flexibleWidth = 1f;
            
            var hLayout = tabsContainer.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 5;
            hLayout.padding = new RectOffset(0, 0, 5, 5);
            hLayout.childForceExpandWidth = true;
            hLayout.childControlHeight = true;

            // الحصول على أنواع المواد
            var materialTypes = System.Enum.GetValues(typeof(BucketMaterialType));
            
            foreach (BucketMaterialType materialType in materialTypes)
            {
                string materialName = materialType.ToString();
                bool isSelected = (_selectedMaterialIndex == (int)materialType);
                
                // إنشاء زر التبويب
                var tabGo = new GameObject("Tab_" + materialName, typeof(RectTransform));
                tabGo.transform.SetParent(tabsContainer.transform, false);
                
                var tabLayout = tabGo.AddComponent<LayoutElement>();
                tabLayout.flexibleWidth = 1f;
                tabLayout.preferredHeight = 45;
                
                var tabImg = tabGo.AddComponent<Image>();
                tabImg.color = isSelected ? _selectedTabColor : _unselectedTabColor;
                
                // إضافة Outline لتمييز التبويب المحدد
                if (isSelected)
                {
                    var outline = tabGo.AddComponent<Outline>();
                    outline.effectColor = _config.AccentColor;
                    outline.effectDistance = new Vector2(2, 2);
                }

                // نص التبويب
                var tabTextGo = new GameObject("Text", typeof(RectTransform));
                tabTextGo.transform.SetParent(tabGo.transform, false);
                var tabTextRt = tabTextGo.GetComponent<RectTransform>();
                tabTextRt.anchorMin = Vector2.zero;
                tabTextRt.anchorMax = Vector2.one;
                tabTextRt.sizeDelta = Vector2.zero;
                
                var tabText = tabTextGo.AddComponent<Text>();
                tabText.text = materialName;
                tabText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                tabText.fontSize = 16;
                tabText.fontStyle = FontStyle.Bold;
                tabText.color = isSelected ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                tabText.alignment = TextAnchor.MiddleCenter;

                // إضافة Button
                var tabBtn = tabGo.AddComponent<Button>();
                int index = (int)materialType;
                tabBtn.onClick.AddListener(() => SelectMaterialTab(index));
                
                // تخزين المراجع
                _materialTabButtons.Add(tabGo);
                _materialTabTexts.Add(tabText);
            }
        }

        private void SelectMaterialTab(int index)
        {
            Debug.Log("Selecting material tab: " + index);
            
            _selectedMaterialIndex = index;
            
            // تحديث مظهر جميع التبويبات
            for (int i = 0; i < _materialTabButtons.Count; i++)
            {
                bool isSelected = (i == index);
                var tabImg = _materialTabButtons[i].GetComponent<Image>();
                var tabText = _materialTabTexts[i];
                var outline = _materialTabButtons[i].GetComponent<Outline>();
                
                // تحديث اللون
                tabImg.color = isSelected ? _selectedTabColor : _unselectedTabColor;
                tabText.color = isSelected ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                
                // إضافة/إزالة الـ Outline
                if (isSelected)
                {
                    if (outline == null)
                    {
                        outline = _materialTabButtons[i].AddComponent<Outline>();
                        outline.effectColor = _config.AccentColor;
                        outline.effectDistance = new Vector2(2, 2);
                    }
                }
                else
                {
                    if (outline != null)
                        DestroyImmediate(outline);
                }
            }
            
            // تحديث الـ Bucket
            if (_bucket != null)
            {
                _bucket.MaterialType = (BucketMaterialType)index;
                Debug.Log("Material type updated to: " + (BucketMaterialType)index);
            }
        }

        private void BuildColorSection(Transform parent)
        {
            Debug.Log("Building Color Section...");
            
            CreateSectionHeader(parent, "🌈 APPEARANCE");
            
            var row = new GameObject("ColorRow", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 15;
            rowLayout.childForceExpandWidth = true;
            rowLayout.padding = new RectOffset(0, 0, 5, 5);
            
            var layoutElement = row.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 60;
            layoutElement.flexibleWidth = 1f;

            var labelGo = CreateSmallText(row.transform, "Paint Color", _config.TextColor);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.sizeDelta = new Vector2(200, 35);
            var labelText = labelGo.GetComponent<Text>();
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.fontSize = 18;

            var swatch = new GameObject("Swatch", typeof(RectTransform));
            swatch.transform.SetParent(row.transform, false);
            swatch.AddComponent<LayoutElement>().preferredWidth = 80;
            swatch.AddComponent<LayoutElement>().preferredHeight = 40;
            
            _paintColorPreview = swatch.AddComponent<Image>();
            _paintColorPreview.color = _selectedPaintColor;

            var btn = swatch.AddComponent<Button>();
            btn.onClick.AddListener(CycleColor);
            
            Debug.Log("Color Section built");
        }

        private void BuildEnvironmentSection(Transform parent)
        {
            Debug.Log("Building Environment Section...");
            
            CreateSectionHeader(parent, "🌤 ENVIRONMENT");
            
            _windXSlider = CreateHorizontalSlider(parent, "Wind X", -20f, 20f, 0f, "N");
            _windYSlider = CreateHorizontalSlider(parent, "Wind Y", -20f, 20f, 0f, "N");
            _windZSlider = CreateHorizontalSlider(parent, "Wind Z", -20f, 20f, 0f, "N");
            _temperatureSlider = CreateHorizontalSlider(parent, "Temperature", 0f, 50f, 20f, "°C");
            _humiditySlider = CreateHorizontalSlider(parent, "Humidity", 0f, 100f, 50f, "%");
            
            CreateSeparator(parent);
            
            Debug.Log("Environment Section built with 5 sliders");
        }

        private void BuildCanvasSection(Transform parent)
        {
            Debug.Log("Building Canvas Section...");
            
            CreateSectionHeader(parent, "🖼 CANVAS SETTINGS");
            
            // Canvas Surface Tabs
            CreateSurfaceTabs(parent);
            
            _textureWidthSlider = CreateHorizontalSlider(parent, "Texture Width", 256f, 4096f, 1024f, "px");
            _textureHeightSlider = CreateHorizontalSlider(parent, "Texture Height", 256f, 4096f, 1024f, "px");
            _particleImpactSizeSlider = CreateHorizontalSlider(parent, "Particle Impact Size", 0.1f, 10f, 1f, "");
            _materialSpreadMultiplierSlider = CreateHorizontalSlider(parent, "Spread Multiplier", 0.1f, 5f, 1f, "");
            
            CreateSeparator(parent);
            
            Debug.Log("Canvas Section built with 4 sliders and surface tabs");
        }

        private void CreateSurfaceTabs(Transform parent)
        {
            // Container للـ Tabs
            var tabsContainer = new GameObject("SurfaceTabsContainer", typeof(RectTransform));
            tabsContainer.transform.SetParent(parent, false);
            
            var layoutElement = tabsContainer.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 55;
            layoutElement.flexibleWidth = 1f;
            
            var hLayout = tabsContainer.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 5;
            hLayout.padding = new RectOffset(0, 0, 5, 5);
            hLayout.childForceExpandWidth = true;
            hLayout.childControlHeight = true;

            // الحصول على أنواع الأسطح
            var surfaceTypes = System.Enum.GetValues(typeof(CanvasSurfaceType));
            
            foreach (CanvasSurfaceType surfaceType in surfaceTypes)
            {
                string surfaceName = surfaceType.ToString();
                bool isSelected = (_selectedSurfaceIndex == (int)surfaceType);
                
                // إنشاء زر التبويب
                var tabGo = new GameObject("SurfaceTab_" + surfaceName, typeof(RectTransform));
                tabGo.transform.SetParent(tabsContainer.transform, false);
                
                var tabLayout = tabGo.AddComponent<LayoutElement>();
                tabLayout.flexibleWidth = 1f;
                tabLayout.preferredHeight = 45;
                
                var tabImg = tabGo.AddComponent<Image>();
                tabImg.color = isSelected ? _selectedTabColor : _unselectedTabColor;
                
                if (isSelected)
                {
                    var outline = tabGo.AddComponent<Outline>();
                    outline.effectColor = _config.AccentColor;
                    outline.effectDistance = new Vector2(2, 2);
                }

                // نص التبويب
                var tabTextGo = new GameObject("Text", typeof(RectTransform));
                tabTextGo.transform.SetParent(tabGo.transform, false);
                var tabTextRt = tabTextGo.GetComponent<RectTransform>();
                tabTextRt.anchorMin = Vector2.zero;
                tabTextRt.anchorMax = Vector2.one;
                tabTextRt.sizeDelta = Vector2.zero;
                
                var tabText = tabTextGo.AddComponent<Text>();
                tabText.text = surfaceName;
                tabText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                tabText.fontSize = 14;
                tabText.fontStyle = FontStyle.Bold;
                tabText.color = isSelected ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                tabText.alignment = TextAnchor.MiddleCenter;

                // إضافة Button
                var tabBtn = tabGo.AddComponent<Button>();
                int index = (int)surfaceType;
                tabBtn.onClick.AddListener(() => SelectSurfaceTab(index));
                
                // تخزين المراجع
                _surfaceTabButtons.Add(tabGo);
                _surfaceTabTexts.Add(tabText);
            }
        }

        private void SelectSurfaceTab(int index)
        {
            Debug.Log("Selecting surface tab: " + index);
            
            _selectedSurfaceIndex = index;
            
            // تحديث مظهر جميع التبويبات
            for (int i = 0; i < _surfaceTabButtons.Count; i++)
            {
                bool isSelected = (i == index);
                var tabImg = _surfaceTabButtons[i].GetComponent<Image>();
                var tabText = _surfaceTabTexts[i];
                var outline = _surfaceTabButtons[i].GetComponent<Outline>();
                
                tabImg.color = isSelected ? _selectedTabColor : _unselectedTabColor;
                tabText.color = isSelected ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                
                if (isSelected)
                {
                    if (outline == null)
                    {
                        outline = _surfaceTabButtons[i].AddComponent<Outline>();
                        outline.effectColor = _config.AccentColor;
                        outline.effectDistance = new Vector2(2, 2);
                    }
                }
                else
                {
                    if (outline != null)
                        DestroyImmediate(outline);
                }
            }
            
            // تحديث الـ Canvas
            if (_canvasController != null)
            {
                _canvasController.SurfaceType = (CanvasSurfaceType)index;
                Debug.Log("Surface type updated to: " + (CanvasSurfaceType)index);
            }
        }

        private Slider CreateHorizontalSlider(Transform parent, string label, float min, float max, float current, string unit)
        {
            var container = new GameObject("SliderContainer_" + label, typeof(RectTransform));
            container.transform.SetParent(parent, false);
            
            var layoutElement = container.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 50;
            layoutElement.flexibleWidth = 1f;

            var hLayout = container.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 15;
            hLayout.padding = new RectOffset(0, 0, 5, 5);
            hLayout.childForceExpandWidth = true;
            hLayout.childControlHeight = true;

            var labelGo = CreateSmallText(container.transform, label, _config.TextColor);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.sizeDelta = new Vector2(200, 35);
            var labelText = labelGo.GetComponent<Text>();
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.fontSize = 18;

            var sliderContainer = new GameObject("SliderArea", typeof(RectTransform));
            sliderContainer.transform.SetParent(container.transform, false);
            sliderContainer.AddComponent<LayoutElement>().flexibleWidth = 1f;
            sliderContainer.AddComponent<LayoutElement>().preferredHeight = 40;

            var sliderHLayout = sliderContainer.AddComponent<HorizontalLayoutGroup>();
            sliderHLayout.spacing = 10;
            sliderHLayout.childForceExpandWidth = true;
            sliderHLayout.childControlHeight = true;

            var sliderGo = new GameObject("Slider", typeof(RectTransform));
            sliderGo.transform.SetParent(sliderContainer.transform, false);
            sliderGo.AddComponent<LayoutElement>().flexibleWidth = 1f;
            sliderGo.AddComponent<LayoutElement>().preferredHeight = 25;

            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = current;

            var bg = new GameObject("Background", typeof(RectTransform));
            bg.transform.SetParent(sliderGo.transform, false);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0, 0.3f);
            bgRt.anchorMax = new Vector2(1, 0.7f);
            bgRt.sizeDelta = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.3f);

            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            var fillAreaRt = fillArea.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0, 0.3f);
            fillAreaRt.anchorMax = new Vector2(1, 0.7f);
            fillAreaRt.sizeDelta = Vector2.zero;

            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(fillArea.transform, false);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.sizeDelta = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = _config.AccentColor;

            var handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(sliderGo.transform, false);
            var handleRt = handle.GetComponent<RectTransform>();
            handleRt.anchorMin = new Vector2(0, 0);
            handleRt.anchorMax = new Vector2(0, 1);
            handleRt.sizeDelta = new Vector2(25, 0);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = _config.SecondaryColor;

            slider.targetGraphic = handleImg;
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;

            var valueGo = CreateSmallText(sliderContainer.transform, current.ToString("0.00") + " " + unit, _config.AccentColor);
            var valueRt = valueGo.GetComponent<RectTransform>();
            valueRt.sizeDelta = new Vector2(120, 35);
            var valueText = valueGo.GetComponent<Text>();
            valueText.alignment = TextAnchor.MiddleRight;
            valueText.fontStyle = FontStyle.Bold;
            valueText.fontSize = 18;

            Text localValueText = valueText;
            slider.onValueChanged.AddListener((val) => 
            {
                if (localValueText != null)
                    localValueText.text = val.ToString("0.00") + " " + unit;
            });

            return slider;
        }

        private GameObject CreateSmallText(Transform parent, string text, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(200, 35);

            var txt = go.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 18;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;

            return go;
        }

        private void CycleColor()
        {
            Color[] palette = { 
                new Color(1f, 0.2f, 0.2f),
                new Color(0.2f, 0.6f, 1f),
                new Color(0.2f, 1f, 0.4f),
                new Color(1f, 0.8f, 0f),
                new Color(0f, 1f, 0.8f),
                new Color(1f, 0.2f, 0.8f),
                new Color(0.8f, 0.2f, 1f),
                new Color(1f, 0.5f, 0f)
            };
            
            int index = 0;
            for (int i = 0; i < palette.Length; i++)
            {
                if (palette[i] == _selectedPaintColor)
                {
                    index = i;
                    break;
                }
            }
            
            index = (index + 1) % palette.Length;
            _selectedPaintColor = palette[index];
            if (_paintColorPreview != null)
                _paintColorPreview.color = _selectedPaintColor;
        }

        private void CreateSectionHeader(Transform parent, string title)
        {
            var go = new GameObject("Header_" + title, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 45;
            layout.flexibleWidth = 1f;
            
            var txt = go.AddComponent<Text>();
            txt.text = title;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 24;
            txt.fontStyle = FontStyle.Bold;
            txt.color = _config.AccentColor;
            txt.alignment = TextAnchor.MiddleLeft;
        }

        private void CreateSeparator(Transform parent)
        {
            var go = new GameObject("Separator", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 2;
            layout.flexibleWidth = 1f;
            
            var img = go.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.08f);
        }

        private void LoadSavedValues()
        {
            Debug.Log("Loading saved values...");
            
            // Pendulum values
            if (_massSlider != null) _massSlider.value = PlayerPrefs.GetFloat("Sim_Mass", _pendulum?.Mass ?? 1f);
            if (_ropeLengthSlider != null) _ropeLengthSlider.value = PlayerPrefs.GetFloat("Sim_RopeLength", _pendulum?.RopeLength ?? 5f);
            if (_gravitySlider != null) _gravitySlider.value = PlayerPrefs.GetFloat("Sim_Gravity", _pendulum?.Gravity ?? 9.81f);
            if (_dampingSlider != null) _dampingSlider.value = PlayerPrefs.GetFloat("Sim_Damping", _pendulum?.DampingCoefficient ?? 0.05f);
            if (_initialAngleSlider != null) _initialAngleSlider.value = PlayerPrefs.GetFloat("Sim_InitialAngle", _pendulum?.InitialAngleDegrees ?? 45f);
            if (_angularVelocitySlider != null) _angularVelocitySlider.value = PlayerPrefs.GetFloat("Sim_AngularVelocity", _pendulum?.InitialAngularVelocity ?? 0f);
            if (_initialPhiSlider != null) _initialPhiSlider.value = PlayerPrefs.GetFloat("Sim_InitialPhi", _pendulum?.InitialPhiDegrees ?? 0f);
            if (_phiAngularVelocitySlider != null) _phiAngularVelocitySlider.value = PlayerPrefs.GetFloat("Sim_PhiAngularVelocity", 0f);
            
            // Paint values
            if (_paintVolumeSlider != null) _paintVolumeSlider.value = PlayerPrefs.GetFloat("Sim_PaintVolume", _bucket?.InitialPaintVolume ?? 2f);
            if (_viscositySlider != null) _viscositySlider.value = PlayerPrefs.GetFloat("Sim_Viscosity", _bucket?.Viscosity ?? 1f);
            if (_densitySlider != null) _densitySlider.value = PlayerPrefs.GetFloat("Sim_Density", _bucket?.Density ?? 1f);
            if (_nozzleRadiusSlider != null) _nozzleRadiusSlider.value = PlayerPrefs.GetFloat("Sim_NozzleRadius", _bucket?.NozzleRadius ?? 0.005f);
            if (_dischargeSlider != null) _dischargeSlider.value = PlayerPrefs.GetFloat("Sim_Discharge", _bucket?.DischargeCoefficent ?? 0.7f);
            if (_paintLossSlider != null) _paintLossSlider.value = PlayerPrefs.GetFloat("Sim_PaintLoss", _bucket?.PaintLossRate ?? 0f);
            if (_absorptionSlider != null) _absorptionSlider.value = PlayerPrefs.GetFloat("Sim_Absorption", _bucket?.AbsorptionRate ?? 0f);
            
            // Particle emission values
            if (_volumePerParticleSlider != null) _volumePerParticleSlider.value = PlayerPrefs.GetFloat("Sim_VolumePerParticle", 0.0001f);
            if (_maxParticlesSlider != null) _maxParticlesSlider.value = PlayerPrefs.GetFloat("Sim_MaxParticles", 100000f);
            
            // Environment values
            if (_windXSlider != null) _windXSlider.value = PlayerPrefs.GetFloat("Sim_WindX", _environment != null ? _environment.WindForce.x : 0f);
            if (_windYSlider != null) _windYSlider.value = PlayerPrefs.GetFloat("Sim_WindY", _environment != null ? _environment.WindForce.y : 0f);
            if (_windZSlider != null) _windZSlider.value = PlayerPrefs.GetFloat("Sim_WindZ", _environment != null ? _environment.WindForce.z : 0f);
            if (_temperatureSlider != null) _temperatureSlider.value = PlayerPrefs.GetFloat("Sim_Temperature", _environment?.Temperature ?? 20f);
            if (_humiditySlider != null) _humiditySlider.value = PlayerPrefs.GetFloat("Sim_Humidity", _environment?.Humidity ?? 50f);
            
            // Canvas values
            if (_textureWidthSlider != null) _textureWidthSlider.value = PlayerPrefs.GetFloat("Sim_TextureWidth", _canvasController?.TextureWidth ?? 1024f);
            if (_textureHeightSlider != null) _textureHeightSlider.value = PlayerPrefs.GetFloat("Sim_TextureHeight", _canvasController?.TextureHeight ?? 1024f);
            if (_particleImpactSizeSlider != null) _particleImpactSizeSlider.value = PlayerPrefs.GetFloat("Sim_ParticleImpactSize", _canvasController?.ParticleImpactSize ?? 1f);
            if (_materialSpreadMultiplierSlider != null) _materialSpreadMultiplierSlider.value = PlayerPrefs.GetFloat("Sim_MaterialSpreadMultiplier", _canvasController?.MaterialSpreadMultiplier ?? 1f);
            
            // Material and Surface indices
            _selectedMaterialIndex = PlayerPrefs.GetInt("Sim_MaterialIndex", 0);
            _selectedSurfaceIndex = PlayerPrefs.GetInt("Sim_SurfaceIndex", 0);
            
            // Update tabs
            if (_materialTabButtons.Count > 0)
            {
                SelectMaterialTab(_selectedMaterialIndex);
            }
            if (_surfaceTabButtons.Count > 0)
            {
                SelectSurfaceTab(_selectedSurfaceIndex);
            }
            
            Debug.Log("Settings loaded successfully!");
        }

        [System.Obsolete]
        public void ApplySettings()
        {
            Debug.Log("Applying settings...");
            
            // Save Pendulum values
            if (_massSlider != null) PlayerPrefs.SetFloat("Sim_Mass", _massSlider.value);
            if (_ropeLengthSlider != null) PlayerPrefs.SetFloat("Sim_RopeLength", _ropeLengthSlider.value);
            if (_gravitySlider != null) PlayerPrefs.SetFloat("Sim_Gravity", _gravitySlider.value);
            if (_dampingSlider != null) PlayerPrefs.SetFloat("Sim_Damping", _dampingSlider.value);
            if (_initialAngleSlider != null) PlayerPrefs.SetFloat("Sim_InitialAngle", _initialAngleSlider.value);
            if (_angularVelocitySlider != null) PlayerPrefs.SetFloat("Sim_AngularVelocity", _angularVelocitySlider.value);
            if (_initialPhiSlider != null) PlayerPrefs.SetFloat("Sim_InitialPhi", _initialPhiSlider.value);
            if (_phiAngularVelocitySlider != null) PlayerPrefs.SetFloat("Sim_PhiAngularVelocity", _phiAngularVelocitySlider.value);
            
            // Save Paint values
            if (_paintVolumeSlider != null) PlayerPrefs.SetFloat("Sim_PaintVolume", _paintVolumeSlider.value);
            if (_viscositySlider != null) PlayerPrefs.SetFloat("Sim_Viscosity", _viscositySlider.value);
            if (_densitySlider != null) PlayerPrefs.SetFloat("Sim_Density", _densitySlider.value);
            if (_nozzleRadiusSlider != null) PlayerPrefs.SetFloat("Sim_NozzleRadius", _nozzleRadiusSlider.value);
            if (_dischargeSlider != null) PlayerPrefs.SetFloat("Sim_Discharge", _dischargeSlider.value);
            if (_paintLossSlider != null) PlayerPrefs.SetFloat("Sim_PaintLoss", _paintLossSlider.value);
            if (_absorptionSlider != null) PlayerPrefs.SetFloat("Sim_Absorption", _absorptionSlider.value);
            
            // Save Particle Emission values
            if (_volumePerParticleSlider != null) PlayerPrefs.SetFloat("Sim_VolumePerParticle", _volumePerParticleSlider.value);
            if (_maxParticlesSlider != null) PlayerPrefs.SetFloat("Sim_MaxParticles", _maxParticlesSlider.value);
            
            // Save Environment values
            if (_windXSlider != null) PlayerPrefs.SetFloat("Sim_WindX", _windXSlider.value);
            if (_windYSlider != null) PlayerPrefs.SetFloat("Sim_WindY", _windYSlider.value);
            if (_windZSlider != null) PlayerPrefs.SetFloat("Sim_WindZ", _windZSlider.value);
            if (_temperatureSlider != null) PlayerPrefs.SetFloat("Sim_Temperature", _temperatureSlider.value);
            if (_humiditySlider != null) PlayerPrefs.SetFloat("Sim_Humidity", _humiditySlider.value);
            
            // Save Canvas values
            if (_textureWidthSlider != null) PlayerPrefs.SetFloat("Sim_TextureWidth", _textureWidthSlider.value);
            if (_textureHeightSlider != null) PlayerPrefs.SetFloat("Sim_TextureHeight", _textureHeightSlider.value);
            if (_particleImpactSizeSlider != null) PlayerPrefs.SetFloat("Sim_ParticleImpactSize", _particleImpactSizeSlider.value);
            if (_materialSpreadMultiplierSlider != null) PlayerPrefs.SetFloat("Sim_MaterialSpreadMultiplier", _materialSpreadMultiplierSlider.value);
            
            // Save Material and Surface indices
            PlayerPrefs.SetInt("Sim_MaterialIndex", _selectedMaterialIndex);
            PlayerPrefs.SetInt("Sim_SurfaceIndex", _selectedSurfaceIndex);
            
            // Apply to Pendulum
            if (_pendulum != null)
            {
                _pendulum.Mass = _massSlider.value;
                _pendulum.RopeLength = _ropeLengthSlider.value;
                _pendulum.Gravity = _gravitySlider.value;
                _pendulum.DampingCoefficient = _dampingSlider.value;
                _pendulum.InitialAngleDegrees = _initialAngleSlider.value;
                _pendulum.InitialAngularVelocity = _angularVelocitySlider.value;
                _pendulum.InitialPhiDegrees = _initialPhiSlider.value;
                // Phi angular velocity is not directly in PendulumSimulator, but we can store it
                Debug.Log("Pendulum properties updated");
            }
            
            // Apply to Bucket
            if (_bucket != null)
            {
                _bucket.InitialPaintVolume = _paintVolumeSlider.value;
                _bucket.Viscosity = _viscositySlider.value;
                _bucket.Density = _densitySlider.value;
                _bucket.NozzleRadius = _nozzleRadiusSlider.value;
                _bucket.MaterialType = (BucketMaterialType)_selectedMaterialIndex;
                _bucket.DischargeCoefficent = _dischargeSlider.value;
                _bucket.PaintLossRate = _paintLossSlider.value;
                _bucket.AbsorptionRate = _absorptionSlider.value;
                _bucket.PaintColors = CreateSolidGradient(_selectedPaintColor);
                Debug.Log("Bucket properties updated. Material: " + (BucketMaterialType)_selectedMaterialIndex);
            }
            
            // Apply to Environment
            if (_environment != null)
            {
                _environment.WindForce = new Vector3(
                    _windXSlider.value,
                    _windYSlider.value,
                    _windZSlider.value
                );
                _environment.Temperature = _temperatureSlider.value;
                _environment.Humidity = _humiditySlider.value;
                Debug.Log("Environment properties updated");
            }
            
            // Apply to Canvas
            if (_canvasController != null)
            {
                _canvasController.SurfaceType = (CanvasSurfaceType)_selectedSurfaceIndex;
                _canvasController.TextureWidth = (int)_textureWidthSlider.value;
                _canvasController.TextureHeight = (int)_textureHeightSlider.value;
                _canvasController.ParticleImpactSize = _particleImpactSizeSlider.value;
                _canvasController.MaterialSpreadMultiplier = _materialSpreadMultiplierSlider.value;
                Debug.Log("Canvas properties updated. Surface: " + (CanvasSurfaceType)_selectedSurfaceIndex);
            }
            
            // Apply Particle Emitter settings
            var emitter = FindObjectOfType<ParticleEmitter>();
            if (emitter != null)
            {
                emitter.MaxParticles = (int)_maxParticlesSlider.value;
                emitter.VolumePerParticle = _volumePerParticleSlider.value;
                Debug.Log("Particle Emitter properties updated");
            }
            
            PlayerPrefs.Save();
            Debug.Log("Settings applied successfully!");
        }

        private Gradient CreateSolidGradient(Color color)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(color, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                }
            );
            return gradient;
        }
    }
}