using HarmonicEngineV4.Rendering;
using HarmonicEngineV4.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace HarmonicEngineV4.UI.Lab2
{
    /// <summary>Live play-mode tuning sliders (no pipeline rebake).</summary>
    public sealed class V4Lab2RuntimeTuningController : MonoBehaviour
    {
        [SerializeField] private V4PipelineRoot pipeline;
        [SerializeField] private V4SphericalPendulumController pendulum;
        [SerializeField] private V4TorricelliEjectionSettings torricelli;
        [SerializeField] private V4RenderingSettings rendering;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Slider carryRateSlider;
        [SerializeField] private Slider dampingSlider;
        [SerializeField] private Slider sloshSlider;
        [SerializeField] private Slider torricelliSlider;
        [SerializeField] private Slider pbfSlider;
        [SerializeField] private Slider ghostSlider;
        [SerializeField] private Toggle ssFluidToggle;
        [SerializeField] private Toggle debugPointsToggle;
        [SerializeField] private Button resetPendulumButton;
        [SerializeField] private Button openSetupButton;
        [SerializeField] private Button closeButton;

        private V4Lab2RuntimeSetupController _setup;

        public void Bind(
            V4Lab2RuntimeSetupController setup,
            V4PipelineRoot pipelineRoot,
            V4SphericalPendulumController pendulumController,
            V4TorricelliEjectionSettings torricelliSettings,
            V4RenderingSettings renderingSettings,
            GameObject panel,
            Slider carry,
            Slider damping,
            Slider slosh,
            Slider torricelliHead,
            Slider pbf,
            Slider ghost,
            Toggle ssFluid,
            Toggle debugPoints,
            Button resetPendulum,
            Button openSetup,
            Button close)
        {
            _setup = setup;
            pipeline = pipelineRoot;
            pendulum = pendulumController;
            torricelli = torricelliSettings;
            rendering = renderingSettings;
            panelRoot = panel;
            carryRateSlider = carry;
            dampingSlider = damping;
            sloshSlider = slosh;
            torricelliSlider = torricelliHead;
            pbfSlider = pbf;
            ghostSlider = ghost;
            ssFluidToggle = ssFluid;
            debugPointsToggle = debugPoints;
            resetPendulumButton = resetPendulum;
            openSetupButton = openSetup;
            closeButton = close;
            WireEvents();
            SyncFromScene();
        }

        private void Awake()
        {
            WireEvents();
        }

        public void SetVisible(bool visible)
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(visible);
            }
        }

        public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

        public void ToggleVisible()
        {
            SetVisible(!IsVisible);
        }

        public void SyncFromScene()
        {
            if (pipeline != null && carryRateSlider != null)
            {
                carryRateSlider.SetValueWithoutNotify(Mathf.Clamp(pipeline.carryRate, carryRateSlider.minValue, carryRateSlider.maxValue));
            }

            if (pendulum != null && dampingSlider != null)
            {
                dampingSlider.SetValueWithoutNotify(pendulum.dampingCoefficient);
            }

            if (pendulum != null && sloshSlider != null)
            {
                sloshSlider.SetValueWithoutNotify(pendulum.sloshFeedbackScale);
            }

            if (torricelli != null && torricelliSlider != null)
            {
                torricelliSlider.SetValueWithoutNotify(torricelli.headHeight);
            }

            if (pipeline != null && pbfSlider != null)
            {
                pbfSlider.SetValueWithoutNotify(pipeline.pbfIterations);
            }

            if (pipeline != null && ghostSlider != null)
            {
                ghostSlider.SetValueWithoutNotify(pipeline.boundaryGhostWeight);
            }

            if (rendering != null)
            {
                if (ssFluidToggle != null)
                {
                    ssFluidToggle.SetIsOnWithoutNotify(rendering.showScreenSpaceFluid);
                }

                if (debugPointsToggle != null)
                {
                    debugPointsToggle.SetIsOnWithoutNotify(rendering.showDebugPoints);
                }
            }
        }

        private void WireEvents()
        {
            if (carryRateSlider != null)
            {
                carryRateSlider.onValueChanged.RemoveListener(OnCarryRateChanged);
                carryRateSlider.onValueChanged.AddListener(OnCarryRateChanged);
            }

            if (dampingSlider != null)
            {
                dampingSlider.onValueChanged.RemoveListener(OnDampingChanged);
                dampingSlider.onValueChanged.AddListener(OnDampingChanged);
            }

            if (sloshSlider != null)
            {
                sloshSlider.onValueChanged.RemoveListener(OnSloshChanged);
                sloshSlider.onValueChanged.AddListener(OnSloshChanged);
            }

            if (torricelliSlider != null)
            {
                torricelliSlider.onValueChanged.RemoveListener(OnTorricelliChanged);
                torricelliSlider.onValueChanged.AddListener(OnTorricelliChanged);
            }

            if (pbfSlider != null)
            {
                pbfSlider.onValueChanged.RemoveListener(OnPbfChanged);
                pbfSlider.onValueChanged.AddListener(OnPbfChanged);
            }

            if (ghostSlider != null)
            {
                ghostSlider.onValueChanged.RemoveListener(OnGhostChanged);
                ghostSlider.onValueChanged.AddListener(OnGhostChanged);
            }

            if (ssFluidToggle != null)
            {
                ssFluidToggle.onValueChanged.RemoveListener(OnSsFluidChanged);
                ssFluidToggle.onValueChanged.AddListener(OnSsFluidChanged);
            }

            if (debugPointsToggle != null)
            {
                debugPointsToggle.onValueChanged.RemoveListener(OnDebugPointsChanged);
                debugPointsToggle.onValueChanged.AddListener(OnDebugPointsChanged);
            }

            if (resetPendulumButton != null)
            {
                resetPendulumButton.onClick.RemoveListener(OnResetPendulum);
                resetPendulumButton.onClick.AddListener(OnResetPendulum);
            }

            if (openSetupButton != null)
            {
                openSetupButton.onClick.RemoveListener(OnOpenSetup);
                openSetupButton.onClick.AddListener(OnOpenSetup);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(OnClose);
                closeButton.onClick.AddListener(OnClose);
            }
        }

        private void OnCarryRateChanged(float value)
        {
            if (pipeline != null)
            {
                pipeline.carryRate = value;
            }
        }

        private void OnDampingChanged(float value)
        {
            if (pendulum != null)
            {
                pendulum.dampingCoefficient = value;
            }
        }

        private void OnSloshChanged(float value)
        {
            if (pendulum != null)
            {
                pendulum.sloshFeedbackScale = value;
            }
        }

        private void OnTorricelliChanged(float value)
        {
            if (torricelli != null)
            {
                torricelli.headHeight = value;
            }
        }

        private void OnPbfChanged(float value)
        {
            if (pipeline != null)
            {
                pipeline.pbfIterations = Mathf.RoundToInt(value);
            }
        }

        private void OnGhostChanged(float value)
        {
            if (pipeline != null)
            {
                pipeline.boundaryGhostWeight = value;
            }
        }

        private void OnSsFluidChanged(bool value)
        {
            if (rendering != null)
            {
                rendering.showScreenSpaceFluid = value;
                rendering.Apply();
            }
        }

        private void OnDebugPointsChanged(bool value)
        {
            if (rendering != null)
            {
                rendering.showDebugPoints = value;
                rendering.Apply();
            }
        }

        private void OnResetPendulum()
        {
            pendulum?.ResetSimulation();
        }

        private void OnOpenSetup()
        {
            _setup?.OpenSetupFromPlay();
        }

        private void OnClose()
        {
            SetVisible(false);
        }
    }
}
