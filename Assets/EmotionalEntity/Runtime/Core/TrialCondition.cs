using UnityEngine;

namespace EmotionalEntity
{
    /// <summary>One experimental condition. Profiles can be crossed freely, e.g. OMA motion with APA stimulus.</summary>
    [CreateAssetMenu(menuName = "Emotional Entity/Trial Condition")]
    public class TrialCondition : ScriptableObject
    {
        public string conditionId = "OMA";
        public int seed = 12345;
        public Vector3 home = new Vector3(0f, 1.3f, 1.6f);
        public VisualProfile visual;
        public BehaviorProfile behavior;
        public ContactSchedule schedule;
        public StimulusProfile stimulus;

        public TrialCondition RuntimeCopy()
        {
            var c = Instantiate(this);
            c.conditionId = conditionId;
            c.visual = Instantiate(visual);
            c.behavior = Instantiate(behavior);
            c.schedule = Instantiate(schedule);
            c.stimulus = Instantiate(stimulus);
            return c;
        }
    }
}
