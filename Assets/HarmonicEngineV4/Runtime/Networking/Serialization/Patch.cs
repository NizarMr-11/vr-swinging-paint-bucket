using System;
using System.IO;
using HarmonicEngineV4.Networking.State;

namespace HarmonicEngineV4.Networking.Serialization
{
    public sealed class Patch : IPatchable, IEquatable<Patch>, ISelfSerializable
    {
        public Topology Topology { get; set; }
        [MultiplexedArray]
        public ParticleState[] Particles { get; set; }

        public void Write(BinaryWriter writer) => new PatchEmitter(typeof(Patch)).Emit(writer, this);

        public void Write(BinaryWriter writer, IPatchable basis) =>
            new PatchEmitter(typeof(Patch)).Emit(writer, this, basis);

        public void Read(BinaryReader reader) => Read(reader, null);

        public void Read(BinaryReader reader, IPatchable basis)
        {
            Patch read = (Patch)new PatchEmitter(typeof(Patch)).Read(reader, basis);
            Topology = read.Topology;
            Particles = read.Particles;
        }

        public IPatchable Clone() => new Patch
        {
            Topology = Topology?.CloneTopology(),
            Particles = CloneParticles(Particles)
        };

        public static Patch CreateDelta(Topology oldState, Topology newState)
        {
            if (newState == null)
            {
                throw new ArgumentNullException(nameof(newState));
            }

            return new Patch
            {
                Topology = newState.CloneTopology(),
                Particles = CloneParticles(newState.particles)
            };
        }

        public Topology Apply(Topology basis)
        {
            if (basis == null)
            {
                throw new ArgumentNullException(nameof(basis));
            }

            Topology result = Topology != null ? Topology.CloneTopology() : basis.CloneTopology();
            result.particles = ApplyParticles(basis.particles, Particles);
            return result;
        }

        public static byte[] Serialize(Patch patch, Topology basis)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            new PatchEmitter(typeof(Patch)).Emit(writer, patch, new PatchBasis(basis));
            return stream.ToArray();
        }

        public static Topology DeserializeAndApply(byte[] data, Topology basis)
        {
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);
            var patch = new Patch();
            patch.Read(reader, new PatchBasis(basis));
            return patch.Apply(basis);
        }

        private sealed class PatchBasis : IPatchable
        {
            public Topology Topology { get; }
            public ParticleState[] Particles { get; }

            public PatchBasis(Topology topology)
            {
                Topology = topology;
                Particles = topology.particles;
            }

            public void Read(BinaryReader reader, IPatchable basis) => throw new NotSupportedException();
            public void Write(BinaryWriter writer, IPatchable basis) => throw new NotSupportedException();
            public IPatchable Clone() => throw new NotSupportedException();
        }

        private static ParticleState[] ApplyParticles(ParticleState[] basisParticles, ParticleState[] patchParticles)
        {
            if (patchParticles == null)
            {
                return CloneParticles(basisParticles);
            }

            var result = new ParticleState[patchParticles.Length];
            for (int i = 0; i < patchParticles.Length; i++)
            {
                ParticleState basis = basisParticles != null && i < basisParticles.Length
                    ? basisParticles[i]
                    : new ParticleState();
                result[i] = patchParticles[i].Apply(basis);
            }

            return result;
        }

        private static ParticleState[] CloneParticles(ParticleState[] particles)
        {
            if (particles == null)
            {
                return null;
            }

            var clone = new ParticleState[particles.Length];
            for (int i = 0; i < particles.Length; i++)
            {
                clone[i] = (ParticleState)particles[i].Clone();
            }

            return clone;
        }

        public bool Equals(Patch other)
        {
            if (other == null)
            {
                return false;
            }

            if (!Equals(Topology, other.Topology))
            {
                return false;
            }

            if (Particles == null && other.Particles == null)
            {
                return true;
            }

            if (Particles == null || other.Particles == null || Particles.Length != other.Particles.Length)
            {
                return false;
            }

            for (int i = 0; i < Particles.Length; i++)
            {
                if (!Particles[i].Equals(other.Particles[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj) => Equals(obj as Patch);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Topology?.GetHashCode() ?? 0;
                if (Particles != null)
                {
                    foreach (ParticleState particle in Particles)
                    {
                        hash = (hash * 397) ^ (particle?.GetHashCode() ?? 0);
                    }
                }

                return hash;
            }
        }
    }
}
