using System;
using HarmonicEngineV4.Networking.Serialization;

namespace HarmonicEngineV4.Networking.State
{
    public sealed class Topology : IEquatable<Topology>
    {
        public int schemaVersion;
        public ulong sessionId;
        public uint frameIndex;
        [PrimitiveSubType(FieldType.Object)]
        public WorldTopology world;
        [PrimitiveSubType(FieldType.Object)]
        public BucketTopology bucket;
        [PrimitiveSubType(FieldType.Object)]
        public SimulationTopology simulation;
        [PrimitiveSubType(FieldType.Object)]
        public CanvasGrid canvas;
        [PrimitiveSubType(FieldType.Object)]
        public ParticleState[] particles;

        public Topology CloneTopology() => new Topology
        {
            schemaVersion = schemaVersion,
            sessionId = sessionId,
            frameIndex = frameIndex,
            world = world == null ? null : new WorldTopology
            {
                gravity = world.gravity,
                fixedDeltaTime = world.fixedDeltaTime,
                randomSeed = world.randomSeed
            },
            bucket = bucket == null ? null : new BucketTopology
            {
                center = bucket.center,
                innerRadius = bucket.innerRadius,
                outerRadius = bucket.outerRadius,
                height = bucket.height,
                wallThickness = bucket.wallThickness,
                bottomThickness = bucket.bottomThickness,
                topBandHeight = bucket.topBandHeight,
                topBandOffset = bucket.topBandOffset,
                holes = bucket.holes == null ? null : (HoleDefinition[])bucket.holes.Clone()
            },
            simulation = simulation == null ? null : new SimulationTopology
            {
                activeParticleCount = simulation.activeParticleCount,
                maxParticles = simulation.maxParticles,
                substeps = simulation.substeps,
                globalDensity = simulation.globalDensity,
                profiles = simulation.profiles == null ? null : (LiquidProfileTopology[])simulation.profiles.Clone()
            },
            canvas = canvas,
            particles = CloneParticles(particles)
        };

        public bool Equals(Topology other)
        {
            if (other == null)
            {
                return false;
            }

            if (schemaVersion != other.schemaVersion
                || sessionId != other.sessionId
                || frameIndex != other.frameIndex)
            {
                return false;
            }

            if (!Equals(world, other.world)
                || !Equals(bucket, other.bucket)
                || !Equals(simulation, other.simulation)
                || !Equals(canvas, other.canvas))
            {
                return false;
            }

            if (particles == null && other.particles == null)
            {
                return true;
            }

            if (particles == null || other.particles == null || particles.Length != other.particles.Length)
            {
                return false;
            }

            for (int i = 0; i < particles.Length; i++)
            {
                if (!particles[i].Equals(other.particles[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj) => Equals(obj as Topology);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = schemaVersion;
                hash = (hash * 397) ^ sessionId.GetHashCode();
                hash = (hash * 397) ^ (int)frameIndex;
                hash = (hash * 397) ^ (world?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (bucket?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (simulation?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (canvas?.GetHashCode() ?? 0);
                if (particles != null)
                {
                    foreach (ParticleState particle in particles)
                    {
                        hash = (hash * 397) ^ (particle?.GetHashCode() ?? 0);
                    }
                }

                return hash;
            }
        }

        private static ParticleState[] CloneParticles(ParticleState[] source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new ParticleState[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                clone[i] = (ParticleState)source[i].Clone();
            }

            return clone;
        }
    }
}
