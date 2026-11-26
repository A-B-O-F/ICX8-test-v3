using System;
using UnityEngine;

namespace EmotionalEntity
{
    public struct BrainInput
    {
        public bool TrackingValid;
        public bool ContactAllowed;  // false when the user is too close to a physical wall, or E-stop
    }

    /// <summary>Idle > Approach > Contact > Hold > Release > Retreat. No Unity lifecycle, so it can be
    /// unit-tested and could run on a single master node.</summary>
    public class EntityBrain
    {
        readonly TrialCondition cond;
        readonly BehaviorProfile b;
        readonly ContactSchedule s;
        readonly StimulusProfile st;
        readonly IBodyModel body;
        readonly ContactScheduler scheduler;

        double stateStart, plannedTime;
        GestureType activeGesture;
        bool manualPending, wasHandInside, engaged;
        GestureType manualGesture;
        BodyZone manualZone;

        public EntityState State { get; private set; }
        public Vector3 Position { get; private set; }
        public BodyZone TargetZone { get; private set; }
        public int ContactIndex { get; private set; }
        public float TouchWeight { get; private set; }
        public float HugWeight { get; private set; }
        /// <summary>0-1 stimulus envelope. Haptic intensity = StimulusProfile.intensity x this.</summary>
        public float Envelope { get; private set; }
        public float Agitation { get; private set; }
        public GestureType ActiveGesture { get { return activeGesture; } }
        public ContactScheduler Scheduler { get { return scheduler; } }

        public event Action<TouchEvent> OnTouch;
        public event Action<EntityState, EntityState> OnState;
        public event Action<string> OnInteraction;

        public EntityBrain(TrialCondition condition, IBodyModel bodyModel)
        {
            cond = condition; b = condition.behavior; s = condition.schedule; st = condition.stimulus; body = bodyModel;
            scheduler = new ContactScheduler(s, condition.seed);
            Position = condition.home;
            State = EntityState.Idle;
        }

        public void RequestGesture(GestureType gesture, BodyZone zone)
        {
            manualPending = true; manualGesture = gesture; manualZone = zone;
        }

        public void Abort(double time)
        {
            manualPending = false;
            scheduler.Stop();
            if (State == EntityState.Contact || State == EntityState.Hold)
                Emit(ContactPhase.Broken, time, "aborted");
            if (State != EntityState.Idle && State != EntityState.Retreat) Enter(EntityState.Retreat, time);
            Envelope = 0f;
        }

        public void Tick(double time, float dt, BrainInput input)
        {
            if (dt <= 0f) return;
            UpdateEngagement(time, input);
            UpdateHand(time, dt);

            float elapsed = (float)(time - stateStart);
            switch (State)
            {
                case EntityState.Idle: TickIdle(time, dt, input); break;
                case EntityState.Approach: TickApproach(time, dt, input, elapsed); break;
                case EntityState.Contact: TickContact(time, dt, input, elapsed); break;
                case EntityState.Hold: TickHold(time, dt, input, elapsed); break;
                case EntityState.Release: TickRelease(time, dt, elapsed); break;
                case EntityState.Retreat: TickRetreat(time, dt, elapsed); break;
            }

            bool touchShape = activeGesture != GestureType.Hug &&
                ((State == EntityState.Approach && Dist() < 1.2f) || State == EntityState.Contact);
            TouchWeight = Mathf.MoveTowards(TouchWeight, touchShape ? 1f : 0f, dt * (touchShape ? 2.5f : 1.5f));
            Agitation = Mathf.MoveTowards(Agitation, 0f, dt * 0.5f);
        }

        void UpdateEngagement(double time, BrainInput input)
        {
            bool want;
            float userFromHome = Flat(body.Head - cond.home).magnitude;
            float userFromEntity = Flat(body.Head - Position).magnitude;
            switch (b.trigger)
            {
                case TriggerMode.TerritoryEntry:
                    want = userFromHome < (engaged ? b.territoryRadius * 1.25f : b.territoryRadius);
                    break;
                case TriggerMode.Proximity:
                    want = userFromEntity < (engaged ? b.proximityRadius * 1.5f : b.proximityRadius);
                    break;
                default:
                    want = true;
                    break;
            }
            want &= input.TrackingValid && input.ContactAllowed;
            if (want == engaged) return;
            engaged = want;
            if (engaged) { scheduler.Start(time); Note("engaged"); }
            else { scheduler.Stop(); Note("disengaged"); }
        }

        void UpdateHand(double time, float dt)
        {
            float reach = cond.visual.radius * 0.8f;
            bool inside = false;
            Vector3 hand = Vector3.zero;
            if (body.HandValid(BodyZone.LeftHand) && (body.ZonePosition(BodyZone.LeftHand) - Position).magnitude < reach)
            { inside = true; hand = body.ZonePosition(BodyZone.LeftHand); }
            if (body.HandValid(BodyZone.RightHand) && (body.ZonePosition(BodyZone.RightHand) - Position).magnitude < reach)
            { inside = true; hand = body.ZonePosition(BodyZone.RightHand); }

            if (inside && !wasHandInside)
            {
                Note("user_hand_entered_entity:" + b.handReaction);
                if (b.handReaction == HandReaction.Flare) { Agitation = 1f; scheduler.Hasten(time, 0.3f); }
            }
            if (inside && State == EntityState.Idle)
            {
                Vector3 away = Position - hand;
                if (b.handReaction == HandReaction.Yield) Position += away.normalized * 0.6f * dt;
                else if (b.handReaction == HandReaction.Lean) Position -= away.normalized * 0.12f * dt;
            }
            wasHandInside = inside;
        }

        void TickIdle(double time, float dt, BrainInput input)
        {
            Vector3 hover = new Vector3(0f, Mathf.Sin((float)time * 0.5f) * 0.03f, 0f) * dt;
            Position += hover;
            if (b.retreatToHome) Position = MovementModel.Step(Position, cond.home, b.retreatSpeed * 0.3f, Trajectory.Direct, time, dt);

            if (manualPending && input.ContactAllowed)
            {
                manualPending = false;
                activeGesture = manualGesture; TargetZone = manualZone; plannedTime = time;
                ContactIndex++;
                Enter(EntityState.Approach, time);
                return;
            }
            bool single = b.gesture != GestureType.RepeatedTouch && b.gesture != GestureType.Hug && scheduler.Issued >= 1;
            if (!single && scheduler.Due(time) && input.ContactAllowed)
            {
                plannedTime = scheduler.Consume(time);
                activeGesture = b.gesture;
                BodyZone[] zones = st.touchZones;
                TargetZone = activeGesture == GestureType.Hug || zones == null || zones.Length == 0
                    ? BodyZone.Chest : zones[scheduler.PickIndex(zones.Length)];
                ContactIndex++;
                Enter(EntityState.Approach, time);
            }
        }

        void TickApproach(double time, float dt, BrainInput input, float elapsed)
        {
            if (!input.ContactAllowed) { Enter(EntityState.Retreat, time); return; }
            Vector3 target = ApproachTarget();
            Position = MovementModel.Step(Position, target, b.approachSpeed, b.trajectory, time, dt);

            float reach = activeGesture == GestureType.Hug ? 0.55f : b.contactDistance + body.ZoneRadius(TargetZone);
            if (Dist() <= reach)
            {
                Emit(ContactPhase.Start, time, null);
                Enter(EntityState.Contact, time);
                return;
            }
            bool tooFar = b.maxChaseDistance > 0f && (body.Head - cond.home).magnitude > b.maxChaseDistance;
            if (elapsed > b.approachTimeout || tooFar)
            {
                Emit(ContactPhase.Missed, time, tooFar ? "user_out_of_range" : "approach_timeout");
                Enter(EntityState.Retreat, time);
            }
        }

        void TickContact(double time, float dt, BrainInput input, float elapsed)
        {
            if (!input.ContactAllowed) { Emit(ContactPhase.Broken, time, "contact_not_allowed"); Enter(EntityState.Retreat, time); return; }
            if (activeGesture == GestureType.Hug)
            {
                if (BreakHug(time)) return;
                Position = MovementModel.Step(Position, body.TorsoPose.position, 1.5f, Trajectory.Direct, time, dt);
                float k = Mathf.Clamp01(elapsed / s.envelopSeconds);
                HugWeight = k * k * (3f - 2f * k);
                Envelope = HugWeight;
                if (k >= 1f) { Emit(ContactPhase.Hold, time, null); Enter(EntityState.Hold, time); }
                return;
            }

            Position = MovementModel.Step(Position, ApproachTarget(), Mathf.Max(b.approachSpeed, 2f), Trajectory.Direct, time, dt);
            if (Dist() > b.breakDistance + body.ZoneRadius(TargetZone))
            {
                Emit(ContactPhase.Broken, time, "user_moved_away");
                Envelope = 0f;
                Enter(EntityState.Retreat, time);
                return;
            }
            float d = Mathf.Max(0.01f, s.contactDuration);
            float u = elapsed / d;
            Envelope = Mathf.Min(st.attack > 0f ? u / st.attack : 1f, st.release > 0f ? (1f - u) / st.release : 1f);
            Envelope = Mathf.Clamp01(Envelope);
            if (u >= 1f) { Envelope = 0f; Emit(ContactPhase.End, time, null); Enter(EntityState.Retreat, time); }
        }

        void TickHold(double time, float dt, BrainInput input, float elapsed)
        {
            if (!input.ContactAllowed) { Emit(ContactPhase.Broken, time, "contact_not_allowed"); Enter(EntityState.Release, time); return; }
            if (BreakHug(time)) return;
            Position = body.TorsoPose.position;
            float w = Mathf.Sin(2f * Mathf.PI * st.holdSwellRate * elapsed);
            Envelope = Mathf.Clamp01(1f + st.holdSwellDepth * (w - 1f) * 0.5f);
            if (elapsed >= s.holdSeconds) Enter(EntityState.Release, time);
        }

        void TickRelease(double time, float dt, float elapsed)
        {
            Position = MovementModel.Step(Position, body.TorsoPose.position, 1.5f, Trajectory.Direct, time, dt);
            float k = Mathf.Clamp01(elapsed / s.releaseSeconds);
            HugWeight = Mathf.Min(HugWeight, 1f - k * k * (3f - 2f * k));
            Envelope = HugWeight;
            if (k >= 1f)
            {
                HugWeight = 0f; Envelope = 0f;
                Emit(ContactPhase.End, time, null);
                Enter(EntityState.Retreat, time);
            }
        }

        void TickRetreat(double time, float dt, float elapsed)
        {
            Envelope = 0f;
            HugWeight = Mathf.MoveTowards(HugWeight, 0f, dt * 2f);
            Vector3 target = b.retreatToHome ? cond.home : RetreatPoint();
            Position = MovementModel.Step(Position, target, b.retreatSpeed, Trajectory.Direct, time, dt);
            if ((target - Position).magnitude < 0.05f || elapsed > 8f) Enter(EntityState.Idle, time);
        }

        bool BreakHug(double time)
        {
            if (body.Speed <= b.hugBreakSpeed) return false;
            Emit(ContactPhase.Broken, time, "user_walked_out_of_hug");
            Enter(EntityState.Release, time);
            return true;
        }

        Vector3 ApproachTarget()
        {
            if (activeGesture == GestureType.Hug) return body.TorsoPose.position;
            Vector3 zone = body.ZonePosition(TargetZone);
            Vector3 outward = Position - zone;
            float m = outward.magnitude;
            return m < 1e-3f ? zone : zone + outward / m * (b.contactDistance * 0.5f);
        }

        Vector3 RetreatPoint()
        {
            Vector3 away = Flat(Position - body.Head);
            if (away.sqrMagnitude < 1e-3f) away = -(body.TorsoPose.rotation * Vector3.forward);
            Vector3 p = body.Head + away.normalized * b.retreatDistance;
            p.y = cond.home.y;
            return p;
        }

        float Dist()
        {
            Vector3 tgt = activeGesture == GestureType.Hug ? body.TorsoPose.position : body.ZonePosition(TargetZone);
            return (Position - tgt).magnitude;
        }

        void Enter(EntityState next, double time)
        {
            EntityState prev = State;
            State = next;
            stateStart = time;
            if (OnState != null) OnState(prev, next);
        }

        void Emit(ContactPhase phase, double time, string note)
        {
            if (OnTouch == null) return;
            OnTouch(new TouchEvent
            {
                Condition = cond.conditionId, ContactIndex = ContactIndex, Gesture = activeGesture, Phase = phase,
                Zone = TargetZone, WorldPosition = body.ZonePosition(TargetZone), CommandedIntensity = st.intensity,
                PlannedTime = plannedTime, ActualTime = time, Note = note
            });
        }

        void Note(string text) { if (OnInteraction != null) OnInteraction(text); }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
