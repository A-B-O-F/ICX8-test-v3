using UnityEngine;

namespace EmotionalEntity
{
    /// <summary>First-pass OMA / APA / ADEUL values. Design assumptions to tune, not findings.</summary>
    public static class PresetLibrary
    {
        public static readonly string[] Names = { "OMA", "APA", "ADEUL" };

        public static TrialCondition Create(string id)
        {
            var c = ScriptableObject.CreateInstance<TrialCondition>();
            c.name = id;
            c.conditionId = id;
            c.visual = ScriptableObject.CreateInstance<VisualProfile>();
            c.behavior = ScriptableObject.CreateInstance<BehaviorProfile>();
            c.schedule = ScriptableObject.CreateInstance<ContactSchedule>();
            c.stimulus = ScriptableObject.CreateInstance<StimulusProfile>();
            Fill(c);
            return c;
        }

        public static void Fill(TrialCondition c)
        {
            VisualProfile v = c.visual; BehaviorProfile b = c.behavior;
            ContactSchedule s = c.schedule; StimulusProfile t = c.stimulus;
            switch (c.conditionId)
            {
                case "APA":
                    v.cohesion = 0.45f; v.turbulence = 0.75f; v.flowFrequency = 3.2f; v.flowSpeed = 1.5f;
                    v.pulseRate = 2.6f; v.pulseDepth = 0.22f; v.pulseScatter = 2.5f; v.brightness = 1.5f;
                    v.detailSize = 0.008f; v.detailOpacity = 0.14f; v.volumeSize = 0.3f; v.volumeOpacity = 0.03f;
                    v.colorA = new Color(1f, 0.16f, 0.05f); v.colorB = new Color(1f, 0.72f, 0.42f);
                    v.streamFraction = 0.6f; v.streamSpeed = 2.4f; v.streamTipRadius = 0.025f; v.hugRadius = 0.36f;
                    b.trigger = TriggerMode.TerritoryEntry; b.gesture = GestureType.RepeatedTouch; b.territoryRadius = 2f;
                    b.approachSpeed = 1.2f; b.trajectory = Trajectory.Direct; b.retreatSpeed = 1.5f;
                    b.retreatToHome = true; b.handReaction = HandReaction.Flare; b.handForce = 0.16f;
                    s.pattern = SchedulePattern.Burst; s.firstDelay = 0.4f; s.interval = 7f; s.jitter = 0.25f;
                    s.contactDuration = 0.3f; s.burstCount = 3; s.burstInterval = 2f;
                    t.intensity = 0.75f; t.contactSigma = 0.07f; t.attack = 0.05f; t.release = 0.15f;
                    t.touchZones = new[] { BodyZone.Chest, BodyZone.LeftShoulder, BodyZone.RightShoulder };
                    break;
                case "ADEUL":
                    v.cohesion = 0.35f; v.turbulence = 0.4f; v.flowFrequency = 2.4f; v.flowSpeed = 0.7f;
                    v.pulseRate = 1.3f; v.pulseDepth = 0.14f; v.pulseScatter = 3.5f; v.brightness = 1.2f;
                    v.detailSize = 0.008f; v.detailOpacity = 0.08f; v.volumeSize = 0.32f; v.volumeOpacity = 0.03f;
                    v.colorA = new Color(0.62f, 0.95f, 0.2f); v.colorB = new Color(0.95f, 0.5f, 0.95f);
                    v.streamFraction = 0.35f; v.streamSpeed = 1.2f; v.streamTipRadius = 0.035f;
                    b.trigger = TriggerMode.Proximity; b.gesture = GestureType.RepeatedTouch;
                    b.approachSpeed = 0.55f; b.trajectory = Trajectory.Hesitant; b.retreatSpeed = 0.7f;
                    b.retreatDistance = 0.8f; b.handReaction = HandReaction.Yield; b.handForce = 0.12f;
                    s.pattern = SchedulePattern.Escalating; s.firstDelay = 1.5f; s.interval = 5f; s.jitter = 0.1f;
                    s.contactDuration = 0.3f; s.escalationRate = 0.1f; s.minInterval = 2f;
                    t.intensity = 0.35f; t.contactSigma = 0.08f; t.attack = 0.1f; t.release = 0.2f;
                    t.touchZones = new[] { BodyZone.LeftShoulder, BodyZone.RightShoulder, BodyZone.LeftSide, BodyZone.RightSide };
                    break;
                default:
                    v.cohesion = 0.75f; v.turbulence = 0.18f; v.flowFrequency = 1.2f; v.flowSpeed = 0.2f;
                    v.pulseRate = 0.22f; v.pulseDepth = 0.06f; v.pulseScatter = 0.4f; v.brightness = 1f;
                    v.detailSize = 0.009f; v.detailOpacity = 0.055f; v.volumeSize = 0.38f; v.volumeOpacity = 0.035f;
                    v.colorA = new Color(0.28f, 0.7f, 1f); v.colorB = new Color(1f, 0.86f, 0.62f);
                    b.trigger = TriggerMode.Scheduled; b.gesture = GestureType.Hug;
                    b.approachSpeed = 0.28f; b.trajectory = Trajectory.Arc; b.retreatSpeed = 0.25f;
                    b.handReaction = HandReaction.Lean; b.handForce = -0.06f;
                    s.pattern = SchedulePattern.Regular; s.firstDelay = 3f; s.interval = 8f;
                    s.contactDuration = 1.5f; s.holdSeconds = 5f; s.envelopSeconds = 2.2f; s.releaseSeconds = 1.8f;
                    t.intensity = 0.25f; t.contactSigma = 0.14f; t.attack = 0.3f; t.release = 0.4f;
                    break;
            }
        }
    }
}
