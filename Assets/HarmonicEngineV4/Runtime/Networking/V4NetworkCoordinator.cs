using System;
using HarmonicEngineV4.Networking.State;
using HarmonicEngineV4.Networking.Transport;
using HarmonicEngineV4.Simulation;
using UnityEngine;

namespace HarmonicEngineV4.Networking
{
    /// <summary>
    /// Milestone B/D glue: host captures topology and publishes patches; client applies incoming state.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class V4NetworkCoordinator : MonoBehaviour
    {
        [SerializeField] private V4PipelineRoot pipeline;
        [SerializeField] private ulong sessionId = 0xC0FFEE01UL;
        [SerializeField] private V4NetworkRole role = V4NetworkRole.Offline;
        [SerializeField] private bool publishOnFrameCompleted = true;

        private readonly V4NetworkSession _session = new V4NetworkSession();
        private IV4NetworkTransport _transport;
        private Topology _hostPrevious;
        private bool _frameHooked;

        public bool autoCreateLoopbackTransport = true;

        public V4NetworkSession Session => _session;
        public IV4NetworkTransport Transport => _transport;

        public bool PublishOnFrameCompleted
        {
            get => publishOnFrameCompleted;
            set
            {
                if (publishOnFrameCompleted == value)
                {
                    return;
                }

                publishOnFrameCompleted = value;
                SyncFrameHook();
            }
        }

        public void Configure(V4PipelineRoot targetPipeline, V4NetworkRole networkRole, IV4NetworkTransport transport, ulong session)
        {
            pipeline = targetPipeline;
            role = networkRole;
            sessionId = session;
            _transport = transport;
            BindRole();
            SyncFrameHook();
        }

        private void OnEnable()
        {
            if (pipeline == null)
            {
                pipeline = FindFirstObjectByType<V4PipelineRoot>();
            }

            if (role != V4NetworkRole.Offline && _transport == null && autoCreateLoopbackTransport)
            {
                _transport = new V4LoopbackTransport();
            }

            BindRole();
            SyncFrameHook();
        }

        private void OnDisable()
        {
            SyncFrameHook(forceOff: true);
        }

        private void Update()
        {
            if (role == V4NetworkRole.Host)
            {
                _transport?.Poll();
            }
        }

        private void LateUpdate()
        {
            if (role == V4NetworkRole.Client)
            {
                ApplyClientStateIfNeeded();
            }
        }

        public void ApplyClientStateIfNeeded()
        {
            if (_session.PollAndApplyIncoming() && _session.Authoritative != null && pipeline != null && pipeline.Initialized)
            {
                V4TopologyBridge.Apply(pipeline, _session.Authoritative);
            }
        }

        private void BindRole()
        {
            if (_transport == null || role == V4NetworkRole.Offline)
            {
                return;
            }

            if (role == V4NetworkRole.Host)
            {
                Topology seed = pipeline != null && pipeline.Initialized
                    ? V4TopologyBridge.Capture(pipeline, sessionId)
                    : CreateEmptyTopology();
                _session.StartHost(sessionId, seed, _transport);
                _hostPrevious = seed.CloneTopology();
            }
            else if (role == V4NetworkRole.Client)
            {
                _session.StartClient(sessionId, _transport);
                if (_transport is V4UdpTransport udp)
                {
                    udp.EnsureHelloSent(sessionId);
                }
            }
        }

        public void HostSendSnapshot()
        {
            if (pipeline == null || !pipeline.Initialized)
            {
                return;
            }

            Topology current = V4TopologyBridge.Capture(pipeline, sessionId);
            _session.HostPublishSnapshot(current);
            _hostPrevious = current.CloneTopology();
        }

        public void HostSendPatch()
        {
            if (pipeline == null || !pipeline.Initialized)
            {
                return;
            }

            Topology current = V4TopologyBridge.Capture(pipeline, sessionId);
            _session.HostPublishPatch(current);
            _hostPrevious = current.CloneTopology();
        }

        private void OnFrameCompleted(long frameIndex, int liveCount, int escaped, int settled)
        {
            if (role != V4NetworkRole.Host || !publishOnFrameCompleted || pipeline == null || !pipeline.Initialized)
            {
                return;
            }

            Topology current = V4TopologyBridge.Capture(pipeline, sessionId);
            if (_hostPrevious == null)
            {
                _session.HostPublishSnapshot(current);
            }
            else
            {
                _session.HostPublishPatch(current);
            }

            _hostPrevious = current.CloneTopology();
        }

        private void SyncFrameHook(bool forceOff = false)
        {
            if (pipeline == null)
            {
                return;
            }

            bool shouldHook = !forceOff && isActiveAndEnabled && role == V4NetworkRole.Host && publishOnFrameCompleted;
            if (shouldHook && !_frameHooked)
            {
                pipeline.FrameCompleted += OnFrameCompleted;
                _frameHooked = true;
            }
            else if (!shouldHook && _frameHooked)
            {
                pipeline.FrameCompleted -= OnFrameCompleted;
                _frameHooked = false;
            }
        }

        private Topology CreateEmptyTopology()
        {
            return new Topology
            {
                schemaVersion = V4NetworkConstants.SchemaVersion,
                sessionId = sessionId,
                frameIndex = 0,
                particles = Array.Empty<ParticleState>()
            };
        }
    }
}
