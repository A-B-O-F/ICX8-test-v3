using UnityEngine;

namespace EmotionalEntity
{
    public interface ITrackingSource
    {
        string Name { get; }
        /// <summary>True only if this point has a fresh pose.</summary>
        bool TryGetPose(TrackedPoint point, out Pose pose);
        void Tick(float dt);
        void Dispose();
    }

    public interface IBodyModel
    {
        Vector3 Head { get; }
        Pose TorsoPose { get; }
        float Speed { get; }
        bool HandValid(BodyZone hand);
        Vector3 ZonePosition(BodyZone zone);
        float ZoneRadius(BodyZone zone);
    }

    /// <summary>Turns head, optional torso and hands into logical body zones. Moves with the user.</summary>
    public class BodyZoneManager : IBodyModel
    {
        public float ShoulderWidth = 0.42f;
        public float TorsoDepth = 0.24f;
        public float HeadToTorso = 0.45f;

        Pose head = new Pose(new Vector3(0f, 1.65f, 0f), Quaternion.identity);
        Pose torso = new Pose(new Vector3(0f, 1.2f, 0f), Quaternion.identity);
        Pose left, right;
        bool leftOk, rightOk, primed;
        float yaw, speed;
        Vector3 lastFlat;

        public bool HeadValid { get; private set; }
        public bool TorsoIsTracked { get; private set; }
        public Pose HeadPose { get { return head; } }
        public Vector3 Head { get { return head.position; } }
        public Pose TorsoPose { get { return torso; } }
        public float Speed { get { return speed; } }

        public void Update(ITrackingSource src, float dt)
        {
            Pose p;
            HeadValid = src.TryGetPose(TrackedPoint.Head, out p);
            if (HeadValid) head = p;

            TorsoIsTracked = src.TryGetPose(TrackedPoint.Torso, out p);
            if (TorsoIsTracked) torso = p;
            else
            {
                Vector3 f = head.rotation * Vector3.forward; f.y = 0f;
                if (f.sqrMagnitude > 0.02f)
                {
                    float target = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                    yaw = !primed ? target : Mathf.LerpAngle(yaw, target, 1f - Mathf.Exp(-dt * 2.5f));
                }
                Quaternion r = Quaternion.Euler(0f, yaw, 0f);
                torso = new Pose(head.position + Vector3.down * HeadToTorso - r * Vector3.forward * 0.06f, r);
            }
            leftOk = src.TryGetPose(TrackedPoint.LeftHand, out left);
            rightOk = src.TryGetPose(TrackedPoint.RightHand, out right);

            Vector3 flat = head.position; flat.y = 0f;
            if (primed && dt > 0f) speed = Mathf.Lerp(speed, (flat - lastFlat).magnitude / dt, 1f - Mathf.Exp(-dt * 8f));
            lastFlat = flat;
            primed = true;
        }

        public bool HandValid(BodyZone hand)
        {
            return hand == BodyZone.LeftHand ? leftOk : hand == BodyZone.RightHand && rightOk;
        }

        public Vector3 ZonePosition(BodyZone z)
        {
            float w = ShoulderWidth * 0.5f, d = TorsoDepth * 0.5f;
            switch (z)
            {
                case BodyZone.Head: return head.position;
                case BodyZone.Chest: return Local(0f, 0.05f, d);
                case BodyZone.Back: return Local(0f, 0.05f, -d);
                case BodyZone.LeftShoulder: return Local(-w, 0.22f, 0f);
                case BodyZone.RightShoulder: return Local(w, 0.22f, 0f);
                case BodyZone.LeftSide: return Local(-w * 0.85f, -0.05f, 0f);
                case BodyZone.RightSide: return Local(w * 0.85f, -0.05f, 0f);
                case BodyZone.LeftHand: return leftOk ? left.position : Local(-w - 0.08f, -0.35f, 0.1f);
                case BodyZone.RightHand: return rightOk ? right.position : Local(w + 0.08f, -0.35f, 0.1f);
            }
            return torso.position;
        }

        public float ZoneRadius(BodyZone z)
        {
            switch (z)
            {
                case BodyZone.Chest: case BodyZone.Back: return 0.14f;
                case BodyZone.Head: return 0.12f;
                case BodyZone.LeftHand: case BodyZone.RightHand: return 0.07f;
                default: return 0.09f;
            }
        }

        Vector3 Local(float x, float y, float z) { return torso.position + torso.rotation * new Vector3(x, y, z); }
    }
}
