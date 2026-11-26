using UnityEngine;

namespace EmotionalEntity
{
    /// <summary>Desktop stand-in: WASD walk, Q/E turn, R/F hands reach forward.</summary>
    public class SimulatedTracking : ITrackingSource
    {
        public string Name { get { return "Simulated (keyboard)"; } }
        public float Height = 1.65f;
        public float WalkSpeed = 1.2f;
        Vector3 pos = new Vector3(0f, 0f, -0.6f);
        float yaw;
        float leftReach, rightReach;

        public void Tick(float dt)
        {
            Vector2 m = InputShim.Move();
            yaw += InputShim.Turn() * 80f * dt;
            Quaternion r = Quaternion.Euler(0f, yaw, 0f);
            pos += r * new Vector3(m.x, 0f, m.y) * WalkSpeed * dt;
            leftReach = Mathf.MoveTowards(leftReach, InputShim.LeftHandHeld() ? 1f : 0f, dt * 3f);
            rightReach = Mathf.MoveTowards(rightReach, InputShim.RightHandHeld() ? 1f : 0f, dt * 3f);
        }

        public bool TryGetPose(TrackedPoint point, out Pose pose)
        {
            Quaternion r = Quaternion.Euler(0f, yaw, 0f);
            Vector3 head = pos + Vector3.up * Height;
            switch (point)
            {
                case TrackedPoint.Head: pose = new Pose(head, r); return true;
                case TrackedPoint.LeftHand: pose = new Pose(head + r * new Vector3(-0.22f, -0.45f + 0.2f * leftReach, 0.15f + 0.5f * leftReach), r); return true;
                case TrackedPoint.RightHand: pose = new Pose(head + r * new Vector3(0.22f, -0.45f + 0.2f * rightReach, 0.15f + 0.5f * rightReach), r); return true;
            }
            pose = default(Pose);
            return false;
        }

        public void Dispose() { }
    }

    /// <summary>Hides which Unity input backend the project uses.</summary>
    public static class InputShim
    {
#if ENABLE_INPUT_SYSTEM
        static UnityEngine.InputSystem.Keyboard K { get { return UnityEngine.InputSystem.Keyboard.current; } }
        public static Vector2 Move()
        {
            if (K == null) return Vector2.zero;
            return new Vector2((K.dKey.isPressed ? 1 : 0) - (K.aKey.isPressed ? 1 : 0), (K.wKey.isPressed ? 1 : 0) - (K.sKey.isPressed ? 1 : 0));
        }
        public static float Turn() { return K == null ? 0f : (K.eKey.isPressed ? 1 : 0) - (K.qKey.isPressed ? 1 : 0); }
        public static bool LeftHandHeld() { return K != null && K.rKey.isPressed; }
        public static bool RightHandHeld() { return K != null && K.fKey.isPressed; }
        public static bool EmergencyStopPressed() { return K != null && K.spaceKey.wasPressedThisFrame; }
        public static bool TogglePanelPressed() { return K != null && K.f1Key.wasPressedThisFrame; }
        public static int ConditionKeyPressed()
        {
            if (K == null) return -1;
            if (K.digit1Key.wasPressedThisFrame) return 0;
            if (K.digit2Key.wasPressedThisFrame) return 1;
            if (K.digit3Key.wasPressedThisFrame) return 2;
            return -1;
        }
        public static bool TouchPressed() { return K != null && K.tKey.wasPressedThisFrame; }
        public static bool HugPressed() { return K != null && K.hKey.wasPressedThisFrame; }
        public static bool ClearStopPressed() { return K != null && K.cKey.wasPressedThisFrame; }
        public static bool HideEntityPressed() { return K != null && K.vKey.wasPressedThisFrame; }
        public static bool ObserverPressed() { return K != null && K.oKey.wasPressedThisFrame; }
#else
        public static Vector2 Move()
        {
            return new Vector2((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0), (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
        }
        public static float Turn() { return (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0); }
        public static bool LeftHandHeld() { return Input.GetKey(KeyCode.R); }
        public static bool RightHandHeld() { return Input.GetKey(KeyCode.F); }
        public static bool EmergencyStopPressed() { return Input.GetKeyDown(KeyCode.Space); }
        public static bool TogglePanelPressed() { return Input.GetKeyDown(KeyCode.F1); }
        public static int ConditionKeyPressed()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) return 0;
            if (Input.GetKeyDown(KeyCode.Alpha2)) return 1;
            if (Input.GetKeyDown(KeyCode.Alpha3)) return 2;
            return -1;
        }
        public static bool TouchPressed() { return Input.GetKeyDown(KeyCode.T); }
        public static bool HugPressed() { return Input.GetKeyDown(KeyCode.H); }
        public static bool ClearStopPressed() { return Input.GetKeyDown(KeyCode.C); }
        public static bool HideEntityPressed() { return Input.GetKeyDown(KeyCode.V); }
        public static bool ObserverPressed() { return Input.GetKeyDown(KeyCode.O); }
#endif
    }
}
