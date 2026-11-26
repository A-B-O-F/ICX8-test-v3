using UnityEngine;

namespace EmotionalEntity
{
    /// <summary>Desktop: one window on the development computer, keyboard-simulated participant.
    /// MiddleVR: the MVRManager in the scene builds every CAVE wall camera from its .vrx; the prototype creates none.</summary>
    public enum DisplayMode { Desktop, MiddleVR }

    public interface IDisplayRig
    {
        string Name { get; }
        void UpdateViewer(Pose head, Pose torso);
        void Dispose();
    }

    /// <summary>Desktop preview. First person from the simulated head, or press O for an observer view
    /// from behind, which shows a grey stand-in body so touches and hugs can be checked by eye.</summary>
    public class DesktopRig : IDisplayRig
    {
        readonly Camera cam;
        readonly GameObject standIn;
        public bool Observer;
        public string Name { get { return Observer ? "Desktop (observer view, O)" : "Desktop (first person, O)"; } }

        public DesktopRig(Transform parent)
        {
            var go = new GameObject("DesktopCamera");
            go.transform.SetParent(parent, false);
            cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.012f, 0.02f);
            cam.fieldOfView = 70f;
            go.AddComponent<AudioListener>();

            // Seen from inside in first person, so its back faces are culled and it stays invisible there.
            standIn = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            standIn.name = "ParticipantStandIn";
            standIn.transform.SetParent(parent, false);
            standIn.transform.localScale = new Vector3(0.42f, 0.85f, 0.26f);
            Collider c = standIn.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
        }

        public void UpdateViewer(Pose head, Pose torso)
        {
            standIn.transform.SetPositionAndRotation(new Vector3(torso.position.x, (head.position.y + 0.1f) * 0.5f, torso.position.z), torso.rotation);
            if (!Observer) { cam.transform.SetPositionAndRotation(head.position, head.rotation); return; }
            Vector3 back = torso.rotation * new Vector3(0.9f, 0.9f, -2.6f);
            Vector3 eye = head.position + back;
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(torso.position + Vector3.up * 0.1f - eye, Vector3.up));
        }

        public void Dispose()
        {
            if (cam != null) Object.Destroy(cam.gameObject);
            if (standIn != null) Object.Destroy(standIn);
        }
    }
}
