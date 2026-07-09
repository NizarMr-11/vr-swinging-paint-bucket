using HarmonicEngineV4.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HarmonicEngineV4.UI.Lab2
{
    /// <summary>Interactive top-down bucket floor diagram for hole setup.</summary>
    public sealed class V4Lab2HoleDiagramController : MonoBehaviour
    {
        [SerializeField] private V4Bucket bucket;
        [SerializeField] private RectTransform diagramRoot;
        [SerializeField] private Image bucketFloorImage;
        [SerializeField] private RectTransform hole0Marker;
        [SerializeField] private RectTransform hole1Marker;
        [SerializeField] private Image hole0Fill;
        [SerializeField] private Image hole1Fill;
        [SerializeField] private Image hole0Selection;
        [SerializeField] private Image hole1Selection;
        [SerializeField] private Slider hole0RadiusSlider;
        [SerializeField] private Slider hole1RadiusSlider;
        [SerializeField] private Toggle hole1EnabledToggle;
        [SerializeField] private Text infoText;

        private int _selectedHole;
        private float _diagramRadius = 120f;
        private float _bucketInnerRadius = 0.3f;
        private V4Lab2SessionConfig _config;
        private System.Action _onConfigDirty;

        public void Bind(
            V4Bucket bucketRef,
            V4Lab2SessionConfig config,
            Slider hole0Slider,
            Slider hole1Slider,
            Toggle hole1Toggle,
            Text info,
            System.Action onConfigDirty)
        {
            bucket = bucketRef;
            _config = config;
            hole0RadiusSlider = hole0Slider;
            hole1RadiusSlider = hole1Slider;
            hole1EnabledToggle = hole1Toggle;
            infoText = info;
            _onConfigDirty = onConfigDirty;
            EnsureVisuals();
            WireControls();
            RefreshFromConfig();
        }

        private void Awake()
        {
            EnsureVisuals();
            WireMarkerDrag(hole0Marker, 0);
            WireMarkerDrag(hole1Marker, 1);
        }

        public void RefreshFromConfig()
        {
            if (_config == null || bucket == null)
            {
                return;
            }

            _bucketInnerRadius = Mathf.Max(0.05f, bucket.innerRadius);
            UpdateMarker(0, _config.hole0LocalXZ, _config.hole0Radius);
            UpdateMarker(1, _config.hole1LocalXZ, _config.hole1Radius);

            if (hole1EnabledToggle != null)
            {
                hole1EnabledToggle.SetIsOnWithoutNotify(_config.hole1Radius > 1e-4f);
            }

            if (hole1Marker != null)
            {
                hole1Marker.gameObject.SetActive(_config.hole1Radius > 1e-4f || (hole1EnabledToggle != null && hole1EnabledToggle.isOn));
            }

            UpdateInfoText();
            UpdateSelectionVisuals();
        }

        private void WireControls()
        {
            if (hole0RadiusSlider != null)
            {
                hole0RadiusSlider.onValueChanged.RemoveListener(OnHole0RadiusChanged);
                hole0RadiusSlider.onValueChanged.AddListener(OnHole0RadiusChanged);
            }

            if (hole1RadiusSlider != null)
            {
                hole1RadiusSlider.onValueChanged.RemoveListener(OnHole1RadiusChanged);
                hole1RadiusSlider.onValueChanged.AddListener(OnHole1RadiusChanged);
            }

            if (hole1EnabledToggle != null)
            {
                hole1EnabledToggle.onValueChanged.RemoveListener(OnHole1EnabledChanged);
                hole1EnabledToggle.onValueChanged.AddListener(OnHole1EnabledChanged);
            }
        }

        private void OnHole0RadiusChanged(float value)
        {
            if (_config == null)
            {
                return;
            }

            _config.hole0Radius = value;
            UpdateMarker(0, _config.hole0LocalXZ, value);
            MarkDirty();
        }

        private void OnHole1RadiusChanged(float value)
        {
            if (_config == null)
            {
                return;
            }

            _config.hole1Radius = value;
            UpdateMarker(1, _config.hole1LocalXZ, value);
            MarkDirty();
        }

        private void OnHole1EnabledChanged(bool enabled)
        {
            if (_config == null)
            {
                return;
            }

            if (!enabled)
            {
                _config.hole1Radius = 0f;
                if (hole1RadiusSlider != null)
                {
                    hole1RadiusSlider.SetValueWithoutNotify(0f);
                }
            }
            else if (_config.hole1Radius <= 1e-4f)
            {
                _config.hole1Radius = 0.03f;
                if (hole1RadiusSlider != null)
                {
                    hole1RadiusSlider.SetValueWithoutNotify(0.03f);
                }
            }

            if (hole1Marker != null)
            {
                hole1Marker.gameObject.SetActive(enabled);
            }

            UpdateMarker(1, _config.hole1LocalXZ, _config.hole1Radius);
            MarkDirty();
        }

        public void SelectHole(int index)
        {
            _selectedHole = Mathf.Clamp(index, 0, 1);
            UpdateSelectionVisuals();
            UpdateInfoText();
        }

        public void SetHoleLocalXZ(int holeIndex, Vector2 localXZ)
        {
            if (_config == null)
            {
                return;
            }

            localXZ = ClampToBucket(localXZ);
            if (holeIndex == 0)
            {
                _config.hole0LocalXZ = localXZ;
                UpdateMarker(0, localXZ, _config.hole0Radius);
            }
            else
            {
                _config.hole1LocalXZ = localXZ;
                UpdateMarker(1, localXZ, _config.hole1Radius);
            }

            MarkDirty();
        }

        private Vector2 ClampToBucket(Vector2 localXZ)
        {
            float maxR = _bucketInnerRadius * 0.92f;
            float r = localXZ.magnitude;
            if (r > maxR && r > 1e-6f)
            {
                return localXZ / r * maxR;
            }

            return localXZ;
        }

        private void UpdateMarker(int holeIndex, Vector2 localXZ, float radius)
        {
            RectTransform marker = holeIndex == 0 ? hole0Marker : hole1Marker;
            Image fill = holeIndex == 0 ? hole0Fill : hole1Fill;
            if (marker == null)
            {
                return;
            }

            float uiScale = _diagramRadius / _bucketInnerRadius;
            marker.anchoredPosition = new Vector2(localXZ.x * uiScale, localXZ.y * uiScale);
            float diameter = Mathf.Max(8f, radius * 2f * uiScale);
            marker.sizeDelta = new Vector2(diameter, diameter);
            if (fill != null)
            {
                fill.color = holeIndex == 0
                    ? new Color(1f, 0.55f, 0.2f, 0.75f)
                    : new Color(0.3f, 0.85f, 1f, 0.75f);
            }
        }

        private void UpdateSelectionVisuals()
        {
            if (hole0Selection != null)
            {
                hole0Selection.enabled = _selectedHole == 0;
            }

            if (hole1Selection != null)
            {
                hole1Selection.enabled = _selectedHole == 1;
            }
        }

        private void UpdateInfoText()
        {
            if (infoText == null || _config == null)
            {
                return;
            }

            Vector2 pos = _selectedHole == 0 ? _config.hole0LocalXZ : _config.hole1LocalXZ;
            float radius = _selectedHole == 0 ? _config.hole0Radius : _config.hole1Radius;
            infoText.text =
                $"Hole {_selectedHole}: pos ({pos.x:F3}, {pos.y:F3}) m  radius {radius:F3} m\n" +
                "Click a hole to select. Drag to reposition on the bucket floor.";
        }

        private void MarkDirty()
        {
            UpdateInfoText();
            _onConfigDirty?.Invoke();
        }

        private void WireMarkerDrag(RectTransform marker, int holeIndex)
        {
            if (marker == null)
            {
                return;
            }

            var trigger = marker.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = marker.gameObject.AddComponent<EventTrigger>();
            }

            trigger.triggers.Clear();

            var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener(_ => SelectHole(holeIndex));
            trigger.triggers.Add(click);

            var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            drag.callback.AddListener(data =>
            {
                if (data is PointerEventData pointer && diagramRoot != null)
                {
                    SelectHole(holeIndex);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        diagramRoot,
                        pointer.position,
                        pointer.pressEventCamera,
                        out Vector2 local);
                    Vector2 bucketXZ = local / (_diagramRadius / _bucketInnerRadius);
                    SetHoleLocalXZ(holeIndex, bucketXZ);
                }
            });
            trigger.triggers.Add(drag);
        }

        private void EnsureVisuals()
        {
            if (diagramRoot == null)
            {
                diagramRoot = transform.Find("DiagramRoot") as RectTransform;
            }

            if (bucket != null)
            {
                _bucketInnerRadius = Mathf.Max(0.05f, bucket.innerRadius);
            }
        }
    }
}
