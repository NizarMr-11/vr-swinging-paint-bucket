// Why struct and not class?
//   - Particle count can reach millions
//   - struct is stored in Stack → does not cause Garbage Collection
//   - class is stored in Heap → causes GC on repeated deletion
//

using UnityEngine;

namespace SwingingPaintBucket.Particles
{
    public struct PaintParticle
    {

        public Vector3 Position;

        public Vector3 Velocity;

        public Vector3 Acceleration;

  
        public float Mass;

  
        public Color Color;


        public float Viscosity;


        public float Density;


        public bool IsActive;

        // ---- Constructor ----

        /// <summary>
        /// Creates a new paint particle with all its initial properties
        /// </summary>
        public PaintParticle(
            Vector3 position,
            Vector3 velocity,
            float mass,
            Color color,
            float viscosity,
            float density)
        {
            Position     = position;
            Velocity     = velocity;
            Acceleration = Vector3.zero;
            Mass         = mass;
            Color        = color;
            Viscosity    = viscosity;
            Density      = density;
            IsActive     = true;
        }

        // ---- Methods ----

        /// <summary>
        /// Updates particle position and velocity based on the time step dt
        /// Equations:
        ///   velocity = velocity + acceleration * dt
        ///   position = position + velocity * dt
        /// </summary>
        public void Step(float dt)
        {
            Velocity += Acceleration * dt;
            Position += Velocity * dt;
        }
    }
}
