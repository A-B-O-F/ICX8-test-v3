using UnityEngine;

namespace EmotionalEntity
{
    [CreateAssetMenu(menuName = "Emotional Entity/Behavior Profile")]
    public class BehaviorProfile : ScriptableObject
    {
        [Header("Engagement")]
        public TriggerMode trigger = TriggerMode.Scheduled;
        public GestureType gesture = GestureType.Touch;
        [Range(0.5f, 6f)] public float territoryRadius = 2f;
        [Range(0.5f, 5f)] public float proximityRadius = 1.5f;

        [Header("Approach")]
        [Range(0.05f, 3f)] public float approachSpeed = 0.3f;
        public Trajectory trajectory = Trajectory.Arc;
        [Range(0.05f, 0.6f)] public float contactDistance = 0.18f;
        [Range(2f, 30f)] public float approachTimeout = 12f;
        [Tooltip("Entity gives up if the user is further than this from home (0 = never).")]
        [Range(0f, 20f)] public float maxChaseDistance = 0f;

        [Header("During contact")]
        [Range(0.1f, 2f)] public float breakDistance = 0.45f;
        [Range(0.2f, 3f)] public float hugBreakSpeed = 1.1f;

        [Header("Retreat")]
        [Range(0.05f, 4f)] public float retreatSpeed = 0.3f;
        [Range(0.2f, 5f)] public float retreatDistance = 1.2f;
        public bool retreatToHome = false;

        [Header("When the user reaches into the cloud")]
        public HandReaction handReaction = HandReaction.Lean;
        [Range(-0.3f, 0.3f)] public float handForce = 0.08f;
    }
}
