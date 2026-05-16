// لماذا struct وليس class؟
//   - عدد الجسيمات قد يصل لملايين
//   - struct يُخزَّن في Stack → لا يسبب Garbage Collection
//   - class يُخزَّن في Heap → يسبب GC عند الحذف المتكرر
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
        /// إنشاء جسيم طلاء جديد بجميع خصائصه الابتدائية
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
        /// تحديث موقع وسرعة الجسيم بناءً على الخطوة الزمنية dt
        /// المعادلات:
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
