using UnityEngine;

namespace EmotionalEntity
{
    /// <summary>Follows tracked objects in the scene by name. MiddleVR creates one GameObject per node of the
    /// .vrx configuration (for example "HeadNode"), already positioned by the CAVE tracker.
    /// Leave a name empty when that body part is not tracked.</summary>
    public class NodeTracking : ITrackingSource
    {
        public string HeadNode = "HeadNode";
        public string TorsoNode = "";
        public string LeftHandNode = "";
        public string RightHandNode = "HandNode";
        [Tooltip("A node whose pose has not changed at all for this long is treated as lost. Real trackers always jitter. 0 disables.")]
        public float StaleSeconds = 1.5f;

        readonly Transform[] nodes = new Transform[4];
        readonly Pose[] last = new Pose[4];
        readonly float[] still = new float[4];
        float searchTimer;

        public string Name { get { return "Scene nodes (" + HeadNode + ")"; } }

        string NameOf(int i)
        {
            switch ((TrackedPoint)i)
            {
                case TrackedPoint.Head: return HeadNode;
                case TrackedPoint.Torso: return TorsoNode;
                case TrackedPoint.LeftHand: return LeftHandNode;
                default: return RightHandNode;
            }
        }

        public bool Found(TrackedPoint p) { return nodes[(int)p] != null; }

        public void Tick(float dt)
        {
            searchTimer -= dt;
            bool search = searchTimer <= 0f;
            if (search) searchTimer = 1f;
            for (int i = 0; i < 4; i++)
            {
                string n = NameOf(i);
                if (string.IsNullOrEmpty(n)) { nodes[i] = null; continue; }
                if (nodes[i] == null && search)
                {
                    GameObject go = GameObject.Find(n);
                    nodes[i] = go != null ? go.transform : null;
                }
                if (nodes[i] == null) continue;
                Pose p = new Pose(nodes[i].position, nodes[i].rotation);
                bool same = p.position == last[i].position && Quaternion.Dot(p.rotation, last[i].rotation) > 0.9999999f;
                still[i] = same ? still[i] + dt : 0f;
                last[i] = p;
            }
        }

        public bool TryGetPose(TrackedPoint point, out Pose pose)
        {
            int i = (int)point;
            pose = last[i];
            if (nodes[i] == null || !nodes[i].gameObject.activeInHierarchy) return false;
            return StaleSeconds <= 0f || still[i] < StaleSeconds;
        }

        public void Dispose() { }
    }
}
