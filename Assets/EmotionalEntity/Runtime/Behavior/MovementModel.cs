using UnityEngine;

namespace EmotionalEntity
{
    /// <summary>Moves the entity's centre toward a moving target using a named trajectory.</summary>
    public static class MovementModel
    {
        public static Vector3 Step(Vector3 pos, Vector3 target, float speed, Trajectory shape, double time, float dt)
        {
            Vector3 to = target - pos;
            float dist = to.magnitude;
            if (dist < 1e-4f) return target;
            Vector3 dir = to / dist;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            side.Normalize();
            float t = (float)time;
            float near = Mathf.Clamp01((dist - 0.25f) / 1.5f); // shape fades out so contact is precise

            Vector3 v = dir * speed;
            switch (shape)
            {
                case Trajectory.Arc:
                    v += (side * 0.45f + Vector3.up * 0.1f) * speed * near;
                    break;
                case Trajectory.Orbit:
                    v = (dir * 0.45f + side * 0.9f * near) * speed;
                    break;
                case Trajectory.Hesitant:
                    v *= 0.55f + 0.95f * Mathf.Sin(t * 3.1f);   // repeatedly advances and backs off
                    v += side * Mathf.Sin(t * 1.7f) * 0.3f * speed * near;
                    break;
            }
            Vector3 stepVec = v * dt;
            return stepVec.magnitude >= dist && Vector3.Dot(stepVec, to) > 0f ? target : pos + stepVec;
        }
    }
}
