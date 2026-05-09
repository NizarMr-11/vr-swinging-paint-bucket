// ============================================================
// ملف : PaintParticle.cs
// المجلد : Scripts/Particles/
// الغرض : تمثيل جسيم طلاء واحد بجميع خصائصه الفيزيائية
//
// لماذا struct وليس class؟
//   - عدد الجسيمات قد يصل لملايين
//   - struct يُخزَّن في Stack → لا يسبب Garbage Collection
//   - class يُخزَّن في Heap → يسبب GC عند الحذف المتكرر
//
// التبعيات : SwingingPaintBucket.Core
// ============================================================

using UnityEngine;

namespace SwingingPaintBucket.Particles
{
    public struct PaintParticle
    {
        // ---- الخصائص الحركية ----

        /// <summary>
        /// موقع الجسيم الحالي في الفضاء ثلاثي الأبعاد (X, Y, Z)
        /// يُحدَّث في كل خطوة زمنية بناءً على السرعة
        /// </summary>
        public Vector3 Position;

        /// <summary>
        /// سرعة الجسيم واتجاهه (متجه ثلاثي الأبعاد)
        /// يرث جزءاً من سرعة الدلو + سرعة تدفق Torricelli
        /// </summary>
        public Vector3 Velocity;

        /// <summary>
        /// التسارع الناتج عن مجموع القوى المؤثرة (الجاذبية + مقاومة الهواء)
        /// a = F_total / mass
        /// </summary>
        public Vector3 Acceleration;

        // ---- الخصائص المادية ----

        /// <summary>
        /// كتلة الجسيم بالكيلوغرام
        /// تؤثر على مقدار تأثير القوى عليه: F = ma
        /// </summary>
        public float Mass;

        /// <summary>
        /// لون الجسيم بصيغة Unity Color (R, G, B, A من 0 إلى 1)
        /// </summary>
        public Color Color;

        /// <summary>
        /// لزوجة الطلاء — كلما زادت كلما تباطأ الانتشار على اللوحة
        /// وتأثر شكل البقعة عند الاصطدام
        /// </summary>
        public float Viscosity;

        /// <summary>
        /// كثافة الطلاء — تؤثر على سمك الأثر المتروك على اللوحة
        /// </summary>
        public float Density;

        // ---- حالة الجسيم ----

        /// <summary>
        /// هل الجسيم لا يزال نشطاً في الهواء؟
        /// يصبح false عند اصطدامه باللوحة
        /// </summary>
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
