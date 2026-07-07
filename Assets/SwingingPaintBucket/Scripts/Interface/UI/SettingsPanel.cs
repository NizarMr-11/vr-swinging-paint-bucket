using UnityEngine;
using UnityEngine.UI;
using SwingingPaintBucket.Pendulum;
using SwingingPaintBucket.Bucket;
using System.Collections.Generic;
using SwingingPaintBucket.Materials;
using SwingingPaintBucket.Simulation;

namespace SwingingPaintBucket.Interface.UI
{
    public class SettingsPanel : MonoBehaviour
    {
        private RetroUIConfig _config;
        private PendulumSimulator _pendulum;
        private BucketController _bucket;
        private Transform _parentTransform;

        private Slider _massSlider, _ropeLengthSlider, _gravitySlider, _dampingSlider;
        private Slider _initialAngleSlider, _angularVelocitySlider;
        private Slider _paintVolumeSlider, _viscositySlider, _densitySlider, _nozzleRadiusSlider;
        private Slider _dischargeSlider, _paintLossSlider, _absorptionSlider;
        
        // Material selection using tabs instead of dropdown
        private int _selectedMaterialIndex = 0;
        private List<GameObject> _materialTabButtons = new List<GameObject>();
        private List<Text> _materialTabTexts = new List<Text>();
        private Color _selectedTabColor = new Color(0.3f, 0.6f, 1f);
        private Color _unselectedTabColor = new Color(0.15f, 0.15f, 0.2f);
        
        private Image _paintColorPreview;
        private Color _selectedPaintColor = Color.red;

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
                Debug.Log("Found Pendulum and Bucket references");
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
            
            CreateSeparator(parent);
            
            Debug.Log("Pendulum Section built with 6 sliders");
        }

        private void BuildPaintSection(Transform parent)
        {
            Debug.Log("Building Paint Section...");
            
            CreateSectionHeader(parent, "🎨 PAINT PROPERTIES");
            
            _paintVolumeSlider = CreateHorizontalSlider(parent, "Paint Volume", 0.1f, 10f, 2f, "L");
            _viscositySlider = CreateHorizontalSlider(parent, "Viscosity", 0.1f, 10f, 1f, "");
            _densitySlider = CreateHorizontalSlider(parent, "Density", 0.1f, 5f, 1f, "g/cm³");
            _nozzleRadiusSlider = CreateHorizontalSlider(parent, "Nozzle Radius", 0.001f, 0.05f, 0.005f, "m");
            
            CreateSeparator(parent);
            
            Debug.Log("Paint Section built with 4 sliders");
        }

        private void BuildMaterialSection(Transform parent)
        {
            Debug.Log("Building Material Section...");
            
            CreateSectionHeader(parent, "🔧 MATERIAL & FLOW");
            
            // Material Tabs - نظام جانبي
            CreateMaterialTabs(parent);

            // Flow sliders
            _dischargeSlider = CreateHorizontalSlider(parent, "Discharge Coeff.", 0.1f, 1f, 0.7f, "");
            _paintLossSlider = CreateHorizontalSlider(parent, "Paint Loss Rate", 0f, 0.5f, 0f, "");
            _absorptionSlider = CreateHorizontalSlider(parent, "Absorption Rate", 0f, 0.1f, 0f, "");
            
            CreateSeparator(parent);
            
            Debug.Log("Material Section built with tabs and 3 sliders");
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
            
            // تحديث الـ Bucket إذا كان موجوداً
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

            var valueGo = CreateSmallText(sliderContainer.transform, current.ToString("0.0") + " " + unit, _config.AccentColor);
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
                    localValueText.text = val.ToString("0.0") + " " + unit;
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
                new Color(1f, 0.2f, 0.8f)
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
            
            if (_massSlider != null) _massSlider.value = PlayerPrefs.GetFloat("Sim_Mass", _pendulum?.Mass ?? 1f);
            if (_ropeLengthSlider != null) _ropeLengthSlider.value = PlayerPrefs.GetFloat("Sim_RopeLength", _pendulum?.RopeLength ?? 5f);
            if (_gravitySlider != null) _gravitySlider.value = PlayerPrefs.GetFloat("Sim_Gravity", _pendulum?.Gravity ?? 9.81f);
            if (_dampingSlider != null) _dampingSlider.value = PlayerPrefs.GetFloat("Sim_Damping", _pendulum?.DampingCoefficient ?? 0.05f);
            if (_initialAngleSlider != null) _initialAngleSlider.value = PlayerPrefs.GetFloat("Sim_InitialAngle", _pendulum?.InitialAngleDegrees ?? 45f);
            if (_angularVelocitySlider != null) _angularVelocitySlider.value = PlayerPrefs.GetFloat("Sim_AngularVelocity", _pendulum?.InitialAngularVelocity ?? 0f);
            
            if (_paintVolumeSlider != null) _paintVolumeSlider.value = PlayerPrefs.GetFloat("Sim_PaintVolume", _bucket?.InitialPaintVolume ?? 2f);
            if (_viscositySlider != null) _viscositySlider.value = PlayerPrefs.GetFloat("Sim_Viscosity", _bucket?.Viscosity ?? 1f);
            if (_densitySlider != null) _densitySlider.value = PlayerPrefs.GetFloat("Sim_Density", _bucket?.Density ?? 1f);
            if (_nozzleRadiusSlider != null) _nozzleRadiusSlider.value = PlayerPrefs.GetFloat("Sim_NozzleRadius", _bucket?.NozzleRadius ?? 0.005f);
            if (_dischargeSlider != null) _dischargeSlider.value = PlayerPrefs.GetFloat("Sim_Discharge", _bucket?.DischargeCoefficent ?? 0.7f);
            if (_paintLossSlider != null) _paintLossSlider.value = PlayerPrefs.GetFloat("Sim_PaintLoss", _bucket?.PaintLossRate ?? 0f);
            if (_absorptionSlider != null) _absorptionSlider.value = PlayerPrefs.GetFloat("Sim_Absorption", _bucket?.AbsorptionRate ?? 0f);
            
            // تحميل المادة المختارة
            _selectedMaterialIndex = PlayerPrefs.GetInt("Sim_MaterialIndex", 0);
            
            // تحديث التبويبات
            if (_materialTabButtons.Count > 0)
            {
                SelectMaterialTab(_selectedMaterialIndex);
            }
            
            Debug.Log("Settings loaded successfully! Material index: " + _selectedMaterialIndex);
        }

        public void ApplySettings()
        {
            Debug.Log("Applying settings...");
            
            if (_massSlider != null) PlayerPrefs.SetFloat("Sim_Mass", _massSlider.value);
            if (_ropeLengthSlider != null) PlayerPrefs.SetFloat("Sim_RopeLength", _ropeLengthSlider.value);
            if (_gravitySlider != null) PlayerPrefs.SetFloat("Sim_Gravity", _gravitySlider.value);
            if (_dampingSlider != null) PlayerPrefs.SetFloat("Sim_Damping", _dampingSlider.value);
            if (_initialAngleSlider != null) PlayerPrefs.SetFloat("Sim_InitialAngle", _initialAngleSlider.value);
            if (_angularVelocitySlider != null) PlayerPrefs.SetFloat("Sim_AngularVelocity", _angularVelocitySlider.value);
            
            if (_paintVolumeSlider != null) PlayerPrefs.SetFloat("Sim_PaintVolume", _paintVolumeSlider.value);
            if (_viscositySlider != null) PlayerPrefs.SetFloat("Sim_Viscosity", _viscositySlider.value);
            if (_densitySlider != null) PlayerPrefs.SetFloat("Sim_Density", _densitySlider.value);
            if (_nozzleRadiusSlider != null) PlayerPrefs.SetFloat("Sim_NozzleRadius", _nozzleRadiusSlider.value);
            if (_dischargeSlider != null) PlayerPrefs.SetFloat("Sim_Discharge", _dischargeSlider.value);
            if (_paintLossSlider != null) PlayerPrefs.SetFloat("Sim_PaintLoss", _paintLossSlider.value);
            if (_absorptionSlider != null) PlayerPrefs.SetFloat("Sim_Absorption", _absorptionSlider.value);
            
            // حفظ المادة المختارة
            PlayerPrefs.SetInt("Sim_MaterialIndex", _selectedMaterialIndex);
            
            if (_pendulum != null)
            {
                _pendulum.Mass = _massSlider.value;
                _pendulum.RopeLength = _ropeLengthSlider.value;
                _pendulum.Gravity = _gravitySlider.value;
                _pendulum.DampingCoefficient = _dampingSlider.value;
                _pendulum.InitialAngleDegrees = _initialAngleSlider.value;
                _pendulum.InitialAngularVelocity = _angularVelocitySlider.value;
                Debug.Log("Pendulum properties updated");
            }
            
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