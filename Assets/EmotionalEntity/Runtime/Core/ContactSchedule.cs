using UnityEngine;

namespace EmotionalEntity
{
    /// <summary>The only place timing lives: frequency, interval, duration and escalation.</summary>
    [CreateAssetMenu(menuName = "Emotional Entity/Contact Schedule")]
    public class ContactSchedule : ScriptableObject
    {
        public SchedulePattern pattern = SchedulePattern.Regular;
        [Range(0f, 20f)] public float firstDelay = 2f;
        [Range(0.5f, 30f)] public float interval = 8f;
        [Range(0f, 0.5f)] public float jitter = 0f;
        [Tooltip("Single touch length, seconds.")]
        [Range(0.05f, 5f)] public float contactDuration = 1.5f;
        [Tooltip("0 = unlimited.")]
        [Min(0)] public int maxContacts = 0;

        [Header("Burst")]
        [Range(2, 8)] public int burstCount = 3;
        [Range(0.2f, 5f)] public float burstInterval = 1f;

        [Header("Escalating (changes frequency, so it lives here)")]
        [Range(0f, 0.5f)] public float escalationRate = 0.1f;
        [Range(0.5f, 10f)] public float minInterval = 2f;

        [Header("Hug")]
        [Range(0.2f, 5f)] public float envelopSeconds = 1.8f;
        [Range(0.5f, 15f)] public float holdSeconds = 4f;
        [Range(0.2f, 5f)] public float releaseSeconds = 1.2f;

        /// <summary>Contacts per minute implied by the base interval (the plan's ContactFrequency).</summary>
        public float ContactFrequencyPerMinute { get { return 60f / Mathf.Max(0.001f, interval); } }
    }
}
