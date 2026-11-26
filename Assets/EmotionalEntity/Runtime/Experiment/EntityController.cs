using System;
using UnityEngine;

namespace EmotionalEntity
{
    /// <summary>The prototype's control surface. External scripts, a future experiment runner or a network
    /// layer should only need this class.</summary>
    public class EntityController
    {
        readonly BodyZoneManager body;
        readonly ITrackingSource tracking;
        readonly HapticManager haptics;
        readonly Action<CloudFrame> present;
        readonly IEventSink log;
        readonly Vector3 arenaMin, arenaMax;
        readonly float safetyMargin;
        float flash;
        bool trackingWasValid = true;

        public TrialCondition Condition { get; private set; }
        public EntityBrain Brain { get; private set; }
        public BodyZoneManager Body { get { return body; } }
        public HapticManager Haptics { get { return haptics; } }
        public ITrackingSource Tracking { get { return tracking; } }
        public IEventSink Log { get { return log; } }
        public bool EmergencyStopped { get; private set; }
        public bool UserInSafeArea { get; private set; }

        public event Action<TouchEvent> OnTouch;
        /// <summary>Hide the entity without stopping anything else, e.g. between trials.</summary>
        public bool EntityVisible = true;

        public EntityController(ITrackingSource trackingSource, BodyZoneManager bodyZones, HapticManager hapticManager,
            Action<CloudFrame> presentFrame, IEventSink sink, Vector3 arenaMinimum, Vector3 arenaMaximum, float margin)
        {
            tracking = trackingSource; body = bodyZones; haptics = hapticManager; present = presentFrame; log = sink;
            arenaMin = arenaMinimum; arenaMax = arenaMaximum; safetyMargin = margin;
            haptics.OnNote += (kind, detail) => log.Mark(time0, kind, detail);
        }

        /// <summary>Starts a condition from a private copy, so live edits never change the saved profiles.</summary>
        public void SetCondition(TrialCondition condition, double time)
        {
            if (Brain != null) Brain.Abort(time);
            time0 = time;
            haptics.ResetOutput();
            StartBrain(condition.RuntimeCopy(), time);
        }

        /// <summary>Starts a condition object directly, without the private copy.</summary>
        public void SetConditionNoCopy(TrialCondition condition, double time) { StartBrain(condition, time); }

        void StartBrain(TrialCondition condition, double time)
        {
            Condition = condition;
            Brain = new EntityBrain(Condition, body);
            Brain.OnTouch += HandleTouch;
            Brain.OnState += (a, b) => log.Mark(time0, "state", a + ">" + b);
            Brain.OnInteraction += s => log.Mark(time0, "interaction", s);
            log.Mark(time, "condition", Condition.conditionId + " seed=" + Condition.seed);
        }
        double time0;

        public void TriggerGesture(GestureType gesture, BodyZone zone)
        {
            if (Brain != null && !EmergencyStopped) Brain.RequestGesture(gesture, zone);
        }

        /// <summary>Stops all output at once and sends the entity away. Must be cleared by an operator.</summary>
        public void EmergencyStop(double time)
        {
            if (EmergencyStopped) return;
            EmergencyStopped = true;
            haptics.EmergencyStop();
            if (Brain != null) Brain.Abort(time);
            log.Mark(time, "emergency_stop", "");
        }

        public void ClearEmergencyStop(double time)
        {
            EmergencyStopped = false;
            haptics.Resume();
            log.Mark(time, "emergency_clear", "");
        }

        public void Tick(double time, float dt)
        {
            time0 = time;
            tracking.Tick(dt);
            body.Update(tracking, dt);
            if (Brain == null) return;

            Vector3 h = body.Head;
            UserInSafeArea = arenaMax == arenaMin ||
                (h.x > arenaMin.x + safetyMargin && h.x < arenaMax.x - safetyMargin &&
                 h.z > arenaMin.z + safetyMargin && h.z < arenaMax.z - safetyMargin);
            if (body.HeadValid != trackingWasValid)
            {
                log.Mark(time, "tracking", body.HeadValid ? "restored" : "lost");
                trackingWasValid = body.HeadValid;
            }

            var input = new BrainInput
            {
                TrackingValid = body.HeadValid,
                ContactAllowed = body.HeadValid && UserInSafeArea && !EmergencyStopped
            };
            Brain.Tick(time, dt, input);
            haptics.Tick(time, dt, Brain, Condition.stimulus, input.ContactAllowed);

            flash = Mathf.MoveTowards(flash, Brain.State == EntityState.Contact || Brain.State == EntityState.Hold ? Brain.Envelope : 0f, dt * 6f);
            Pose t = body.TorsoPose;
            present(new CloudFrame
            {
                Visible = EntityVisible ? 1 : 0,
                Visual = VisualParams.From(Condition.visual),
                State = new EntityVisualState
            {
                Time = time,
                Center = Brain.Position,
                TouchWeight = Brain.TouchWeight,
                HugWeight = Brain.HugWeight,
                ContactFlash = flash,
                Agitation = Brain.Agitation,
                ContactPoint = body.ZonePosition(Brain.TargetZone),
                HugCenter = t.position,
                HugRight = t.rotation * Vector3.right,
                HugUp = t.rotation * Vector3.up,
                HugForward = t.rotation * Vector3.forward,
                HeadPosition = body.Head,
                LeftHand = body.ZonePosition(BodyZone.LeftHand),
                RightHand = body.ZonePosition(BodyZone.RightHand),
                HandForce = Condition.behavior.handForce
            }
            });
        }

        void HandleTouch(TouchEvent e)
        {
            log.Touch(e, haptics.Peak);
            if (OnTouch != null) OnTouch(e);
        }
    }
}
