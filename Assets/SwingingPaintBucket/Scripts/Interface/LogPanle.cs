using UnityEngine;
using UnityEngine.UI;
using SwingingPaintBucket.Pendulum;
using SwingingPaintBucket.Bucket;

namespace SwingingPaintBucket.Interface.UI
{
    public class LogPanel : MonoBehaviour
    {
        [Header("References")]
        public PendulumSimulator Pendulum;
        public BucketController Bucket;

        // UI Elements
        private GameObject _contentContainer;
        private Text _pendulumTitle, _paintTitle, _materialTitle;
        
        // Pendulum values
        private Text _massValue, _lengthValue, _angleValue, _angVelValue, _momentumValue;
        // Paint values
        private Text _volumeValue, _remainingValue, _statusValue, _viscosityValue, _densityValue, _nozzleValue, _flowValue;
        // Material values
        private Text _matTypeValue, _dischargeValue, _lossValue, _absorptionValue;

        private float _updateTimer;
        private const float UPDATE_INTERVAL = 0.2f;

        // Colors
        private readonly Color _labelColor = new Color(0.6f, 0.65f, 0.7f);
        private readonly Color _valueColor = new Color(0.9f, 0.92f, 0.95f);
        private readonly Color _titleColor = new Color(1f, 0.8f, 0f);
        private readonly Color _separatorColor = new Color(1f, 0.8f, 0f, 0.2f);
        private readonly Color _cardBgColor = new Color(0.08f, 0.08f, 0.15f, 0.5f);
        private readonly Color _activeColor = new Color(0.2f, 0.9f, 0.3f);
        private readonly Color _emptyColor = new Color(0.6f, 0.3f, 0.3f);

        public void BuildUI(Transform parent)
        {
            // ===== PENDULUM CARD =====
            CreateSectionTitle(parent, "⚡ PENDULUM", out _pendulumTitle);
            
            var pendulumCard = CreateCard(parent);
            CreateInfoRow(pendulumCard.transform, "Mass", out _, out _massValue, "kg");
            CreateInfoRow(pendulumCard.transform, "Length", out _, out _lengthValue, "m");
            CreateInfoRow(pendulumCard.transform, "Angle", out _, out _angleValue, "°");
            CreateInfoRow(pendulumCard.transform, "Ang.Vel", out _, out _angVelValue, "rad/s");
            CreateInfoRow(pendulumCard.transform, "Momentum", out _, out _momentumValue, "kg·m/s");
            
            CreateSeparator(parent);

            // ===== PAINT CARD =====
            CreateSectionTitle(parent, "🎨 PAINT", out _paintTitle);
            
            var paintCard = CreateCard(parent);
            CreateInfoRow(paintCard.transform, "Volume", out _, out _volumeValue, "L");
            CreateInfoRow(paintCard.transform, "Remaining", out _, out _remainingValue, "%");
            CreateInfoRow(paintCard.transform, "Status", out _, out _statusValue, "");
            CreateInfoRow(paintCard.transform, "Viscosity", out _, out _viscosityValue, "");
            CreateInfoRow(paintCard.transform, "Density", out _, out _densityValue, "g/cm³");
            CreateInfoRow(paintCard.transform, "Nozzle", out _, out _nozzleValue, "mm");
            CreateInfoRow(paintCard.transform, "Flow Rate", out _, out _flowValue, "L/s");
            
            CreateSeparator(parent);

            // ===== MATERIAL CARD =====
            CreateSectionTitle(parent, "🔧 MATERIAL", out _materialTitle);
            
            var materialCard = CreateCard(parent);
            CreateInfoRow(materialCard.transform, "Type", out _, out _matTypeValue, "");
            CreateInfoRow(materialCard.transform, "Discharge", out _, out _dischargeValue, "");
            CreateInfoRow(materialCard.transform, "Loss Rate", out _, out _lossValue, "");
            CreateInfoRow(materialCard.transform, "Absorption", out _, out _absorptionValue, "");

            UpdateInfo();
        }

        private void CreateSectionTitle(Transform parent, string title, out Text titleText)
        {
            var titleGo = new GameObject("SectionTitle", typeof(RectTransform));
            titleGo.transform.SetParent(parent, false);
            
            var layout = titleGo.AddComponent<LayoutElement>();
            layout.preferredHeight = 18;
            layout.flexibleWidth = 1f;
            
            // Add small top padding
            var rt = titleGo.GetComponent<RectTransform>();
            
            titleText = titleGo.AddComponent<Text>();
            titleText.text = title;
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 11;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = _titleColor;
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.raycastTarget = false;
            
            // Add subtle shadow
            var shadow = titleGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.4f);
            shadow.effectDistance = new Vector2(1, -1);
        }

        private GameObject CreateCard(Transform parent)
        {
            var card = new GameObject("Card", typeof(RectTransform));
            card.transform.SetParent(parent, false);
            
            var layout = card.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;
            
            var vLayout = card.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 1;
            vLayout.padding = new RectOffset(6, 6, 3, 3);
            vLayout.childForceExpandWidth = true;
            vLayout.childControlHeight = true;
            
            // Card background
            var bg = card.AddComponent<Image>();
            bg.color = _cardBgColor;
            bg.raycastTarget = false;
            
            return card;
        }

        private void CreateInfoRow(Transform parent, string label, out Text labelText, out Text valueText, string unit)
        {
            var row = new GameObject("Row_" + label, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            
            var layout = row.AddComponent<LayoutElement>();
            layout.preferredHeight = 14;
            layout.flexibleWidth = 1f;
            
            var hLayout = row.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 4;
            hLayout.childForceExpandWidth = false;
            hLayout.childControlWidth = false;
            hLayout.childControlHeight = true;
            hLayout.childAlignment = TextAnchor.MiddleLeft;
            hLayout.padding = new RectOffset(2, 2, 0, 0);

            // Label (left side)
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(row.transform, false);
            var labelLayout = labelGo.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 70;
            labelLayout.preferredHeight = 14;
            
            labelText = labelGo.AddComponent<Text>();
            labelText.text = label + ":";
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 10;
            labelText.fontStyle = FontStyle.Normal;
            labelText.color = _labelColor;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.raycastTarget = false;

            // Spacer
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(row.transform, false);
            spacer.AddComponent<LayoutElement>().flexibleWidth = 1f;

            // Value + Unit (right side)
            var valueGo = new GameObject("Value", typeof(RectTransform));
            valueGo.transform.SetParent(row.transform, false);
            var valueLayout = valueGo.AddComponent<LayoutElement>();
            valueLayout.preferredHeight = 14;
            
            valueText = valueGo.AddComponent<Text>();
            valueText.text = (string.IsNullOrEmpty(unit) ? "--" : $"-- {unit}");
            valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            valueText.fontSize = 10;
            valueText.fontStyle = FontStyle.Bold;
            valueText.color = _valueColor;
            valueText.alignment = TextAnchor.MiddleRight;
            valueText.raycastTarget = false;
        }

        private void CreateSeparator(Transform parent)
        {
            var sep = new GameObject("Separator", typeof(RectTransform));
            sep.transform.SetParent(parent, false);
            
            var layout = sep.AddComponent<LayoutElement>();
            layout.preferredHeight = 4;
            layout.flexibleWidth = 1f;
            
            // Inner line
            var line = new GameObject("Line", typeof(RectTransform));
            line.transform.SetParent(sep.transform, false);
            
            var lineRt = line.GetComponent<RectTransform>();
            lineRt.anchorMin = new Vector2(0.05f, 0.5f);
            lineRt.anchorMax = new Vector2(0.95f, 0.5f);
            lineRt.sizeDelta = new Vector2(0, 1);
            
            var lineImg = line.AddComponent<Image>();
            lineImg.color = _separatorColor;
            lineImg.raycastTarget = false;
        }

        private void Update()
        {
            _updateTimer += Time.deltaTime;
            if (_updateTimer >= UPDATE_INTERVAL)
            {
                _updateTimer = 0f;
                UpdateInfo();
            }
        }

        public void UpdateInfo()
        {
            // ===== PENDULUM =====
            if (Pendulum != null)
            {
                _massValue.text = $"{Pendulum.Mass:F2} kg";
                _lengthValue.text = $"{Pendulum.RopeLength:F2} m";
                
                float angleDeg = Pendulum.Theta * Mathf.Rad2Deg;
                _angleValue.text = $"{angleDeg:F1}°";
                _angVelValue.text = $"{Pendulum.Omega:F2} rad/s";
                _momentumValue.text = $"{Pendulum.Momentum.magnitude:F2} kg·m/s";
                
                // Color coding for extreme angles
                _angleValue.color = Mathf.Abs(angleDeg) > 90 ? new Color(1f, 0.5f, 0.3f) : _valueColor;
            }
            else
            {
                SetAllPendulumToNA();
            }

            // ===== PAINT =====
            if (Bucket != null)
            {
                float currentVolume = Bucket.PaintVolume;
                _volumeValue.text = $"{currentVolume:F3} L";
                
                float percentage = Bucket.InitialPaintVolume > 0 ?
                    (currentVolume / Bucket.InitialPaintVolume) * 100f : 0f;
                _remainingValue.text = $"{percentage:F1}%";
                
                // Color code remaining percentage
                if (percentage > 50f)
                    _remainingValue.color = _activeColor;
                else if (percentage > 20f)
                    _remainingValue.color = new Color(1f, 0.8f, 0.2f);
                else
                    _remainingValue.color = new Color(1f, 0.3f, 0.2f);
                
                _statusValue.text = Bucket.HasPaint ? "● ACTIVE" : "○ EMPTY";
                _statusValue.color = Bucket.HasPaint ? _activeColor : _emptyColor;
                
                _viscosityValue.text = $"{Bucket.Viscosity:F2}";
                _densityValue.text = $"{Bucket.Density:F2} g/cm³";
                
                float nozzleMm = Bucket.NozzleRadius * 1000f;
                _nozzleValue.text = $"{nozzleMm:F1} mm";
                _flowValue.text = $"{Bucket.VolumeThisFrame:F6} L/s";
                
                // ===== MATERIAL =====
                _matTypeValue.text = Bucket.MaterialType.ToString();
                _dischargeValue.text = $"{Bucket.DischargeCoefficent:F2}";
                _lossValue.text = $"{Bucket.PaintLossRate:F3}";
                _absorptionValue.text = $"{Bucket.AbsorptionRate:F3}";
            }
            else
            {
                SetAllPaintToNA();
                SetAllMaterialToNA();
            }
        }

        private void SetAllPendulumToNA()
        {
            _massValue.text = "--";
            _lengthValue.text = "--";
            _angleValue.text = "--";
            _angVelValue.text = "--";
            _momentumValue.text = "--";
            _angleValue.color = _valueColor;
        }

        private void SetAllPaintToNA()
        {
            _volumeValue.text = "--";
            _remainingValue.text = "--";
            _statusValue.text = "NOT FOUND";
            _viscosityValue.text = "--";
            _densityValue.text = "--";
            _nozzleValue.text = "--";
            _flowValue.text = "--";
            _remainingValue.color = _valueColor;
            _statusValue.color = _emptyColor;
        }

        private void SetAllMaterialToNA()
        {
            _matTypeValue.text = "--";
            _dischargeValue.text = "--";
            _lossValue.text = "--";
            _absorptionValue.text = "--";
        }
    }
}