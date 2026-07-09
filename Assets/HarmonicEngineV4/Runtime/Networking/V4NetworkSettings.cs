using System;
using System.Collections;
using HarmonicEngineV4.Networking.Transport;
using HarmonicEngineV4.Simulation;
using UnityEngine;

namespace HarmonicEngineV4.Networking
{
    /// <summary>
    /// Gameplay networking settings on the pipeline object. Configures <see cref="V4NetworkCoordinator"/>,
    /// host/client role, transport selection, and whether the client runs local simulation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(V4PipelineRoot))]
    [DefaultExecutionOrder(-90)]
    public sealed class V4NetworkSettings : MonoBehaviour
    {
        [Header("Session")]
        [Tooltip("When disabled, the pipeline runs offline with no coordinator.")]
        public bool enableNetworking;

        public V4NetworkRole role = V4NetworkRole.Offline;

        public ulong sessionId = 0xC0FFEE01UL;

        [Header("Transport")]
        public V4TransportKind transportKind = V4TransportKind.Loopback;

        [Tooltip("UDP port the host binds to. Clients connect to this port on hostAddress.")]
        public int udpPort = V4NetworkConstants.DefaultUdpPort;

        [Tooltip("Host address used by clients when transportKind is Udp.")]
        public string hostAddress = "127.0.0.1";

        [Tooltip("Optional second pipeline settings object for loopback pairing validation.")]
        public V4NetworkSettings loopbackPeer;

        [Header("Behavior")]
        [Tooltip("Host: publish snapshot/patch after each completed simulation frame.")]
        public bool publishOnFrameCompleted = true;

        [Tooltip("Client: step simulation locally between network applies. Off = receive-only mirror.")]
        public bool clientSimulatesLocally;

        [Header("Bucket authority")]
        [Tooltip("Host keeps keyboard bucket control enabled.")]
        public bool hostAllowsBucketInput = true;

        [Tooltip("Receive-only clients ignore local bucket keyboard input.")]
        public bool disableClientBucketInput = true;

        private V4PipelineRoot _pipeline;
        private V4NetworkCoordinator _coordinator;
        private Coroutine _waitForPipeline;
        private IV4NetworkTransport _ownedTransport;

        private void OnEnable()
        {
            Apply();
            if (isActiveAndEnabled)
            {
                _waitForPipeline = StartCoroutine(ApplyWhenPipelineReady());
            }
        }

        private void OnDisable()
        {
            if (_waitForPipeline != null)
            {
                StopCoroutine(_waitForPipeline);
                _waitForPipeline = null;
            }

            Teardown();
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled)
            {
                Apply();
            }
        }

        public void Apply()
        {
            if (!_pipeline)
            {
                _pipeline = GetComponent<V4PipelineRoot>();
            }

            if (_pipeline == null)
            {
                return;
            }

            if (!enableNetworking || role == V4NetworkRole.Offline)
            {
                Teardown();
                _pipeline.autoRun = true;
                ApplyBucketInputAuthority(offline: true);
                return;
            }

            IV4NetworkTransport transport = ResolveTransport();
            if (transport == null)
            {
                return;
            }

            _coordinator = GetOrCreateCoordinator();
            _coordinator.autoCreateLoopbackTransport = false;
            _coordinator.PublishOnFrameCompleted = publishOnFrameCompleted;
            _coordinator.Configure(_pipeline, role, transport, sessionId);

            _pipeline.autoRun = role == V4NetworkRole.Host || (role == V4NetworkRole.Client && clientSimulatesLocally);
            ApplyBucketInputAuthority(offline: false);
        }

        private void ApplyBucketInputAuthority(bool offline)
        {
            if (_pipeline?.bucket == null)
            {
                return;
            }

            V4BucketMotionController motion = _pipeline.bucket.GetComponent<V4BucketMotionController>();
            V4BucketMotionSettings motionSettings = _pipeline.bucket.GetComponent<V4BucketMotionSettings>();
            if (motionSettings != null && motionSettings.IsPendulumMode)
            {
                if (motion != null)
                {
                    motion.inputEnabled = false;
                }

                return;
            }

            if (motion == null)
            {
                return;
            }

            if (offline)
            {
                motion.inputEnabled = true;
                return;
            }

            motion.inputEnabled = role switch
            {
                V4NetworkRole.Host => hostAllowsBucketInput,
                V4NetworkRole.Client => !disableClientBucketInput || clientSimulatesLocally,
                _ => true
            };
        }

        private IEnumerator ApplyWhenPipelineReady()
        {
            while (_pipeline != null && !_pipeline.Initialized)
            {
                yield return null;
            }

            if (!isActiveAndEnabled || !enableNetworking || role == V4NetworkRole.Offline)
            {
                yield break;
            }

            Apply();

            if (role == V4NetworkRole.Host && _coordinator != null)
            {
                _coordinator.HostSendSnapshot();
            }
        }

        private void Teardown()
        {
            if (_coordinator != null)
            {
                _coordinator.enabled = false;
            }

            ReleaseOwnedTransport();
        }

        private IV4NetworkTransport ResolveTransport()
        {
            ReleaseOwnedTransport();

            switch (transportKind)
            {
                case V4TransportKind.Loopback:
                    if (loopbackPeer != null && loopbackPeer.role == role)
                    {
                        Debug.LogWarning("[V4NetworkSettings] loopbackPeer should be the opposite role (Host vs Client).");
                    }

                    return V4NetworkLoopbackPair.Resolve(role, sessionId);

                case V4TransportKind.Udp:
                    _ownedTransport = role == V4NetworkRole.Host
                        ? V4UdpTransport.CreateHost(udpPort)
                        : V4UdpTransport.CreateClient(hostAddress, udpPort);
                    if (_ownedTransport is V4UdpTransport udp && role == V4NetworkRole.Client)
                    {
                        udp.EnsureHelloSent(sessionId);
                    }

                    return _ownedTransport;

                default:
                    Debug.LogWarning($"[V4NetworkSettings] Unsupported transport kind {transportKind}.");
                    return null;
            }
        }

        private void ReleaseOwnedTransport()
        {
            if (_ownedTransport is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _ownedTransport = null;
        }

        private V4NetworkCoordinator GetOrCreateCoordinator()
        {
            if (_coordinator == null)
            {
                _coordinator = GetComponent<V4NetworkCoordinator>();
            }

            if (_coordinator == null)
            {
                _coordinator = gameObject.AddComponent<V4NetworkCoordinator>();
            }

            _coordinator.enabled = true;
            return _coordinator;
        }
    }
}
