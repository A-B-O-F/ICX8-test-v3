using System;
using UnityEngine;

namespace EmotionalEntity
{
    /// <summary>Decides what the vest does each frame, in one of two modes.
    /// RealtimeMotors (version 1): contact point on the body > per-motor field > granularity > calibration > device.
    /// PresetEvent (version 2): start the condition's Portal pattern when the trigger becomes true, stop it when false.</summary>
    public class HapticManager
    {
        readonly IBodyModel body;
        readonly BodyZoneManager dims;
        readonly float[] field, output;
        readonly IHapticDevice device;
        double nextSend;
        bool wasActive, stopped;
        string playingEvent;

        public HapticMode Mode = HapticMode.RealtimeMotors;
        public ActuatorLayout Layout { get; private set; }
        public HapticCalibration Calibration = new HapticCalibration();
        [Range(0f, 1f)] public float MasterGain = 1f;
        public float UpdateHz = 25f;
        public float[] LastOutput { get { return output; } }
        public IHapticDevice Device { get { return device; } }
        /// <summary>Realtime: peak output x seconds. Preset: pattern intensity x seconds playing (the pattern's
        /// own shape is unknown here). Lets conditions be compared on total stimulus.</summary>
        public float Dose { get; private set; }
        public float Peak { get; private set; }
        /// <summary>The preset event playing now, or null.</summary>
        public string PlayingEvent { get { return playingEvent; } }
        /// <summary>Log lines from the preset mode: (kind, detail).</summary>
        public event Action<string, string> OnNote;

        public HapticManager(IHapticDevice dev, ActuatorLayout layout, BodyZoneManager bodyZones)
        {
            device = dev; Layout = layout; dims = bodyZones; body = bodyZones;
            field = new float[layout.Count];
            output = new float[layout.Count];
        }

        /// <param name="allowed">False while tracking is lost or the participant is near a wall.</param>
        public void Tick(double time, float dt, EntityBrain brain, StimulusProfile st, bool allowed = true)
        {
            if (stopped) return;
            if (Mode == HapticMode.PresetEvent) TickPreset(dt, brain, st, allowed);
            else TickRealtime(time, dt, brain, st);
        }

        // ---------- Version 1 ----------
        void TickRealtime(double time, float dt, EntityBrain brain, StimulusProfile st)
        {
            if (playingEvent != null) StopPreset("mode_changed");
            float relative = st.intensity * brain.Envelope;
            bool active = relative > 0.001f && (brain.State == EntityState.Contact || brain.State == EntityState.Hold || brain.State == EntityState.Release);

            if (active) ComputeField(brain, st, relative);
            else Array.Clear(field, 0, field.Length);

            Peak = 0f;
            for (int i = 0; i < output.Length; i++)
            {
                output[i] = Layout.IsActive(i) ? Calibration.Map(field[i]) * MasterGain : 0f;
                if (output[i] > Peak) Peak = output[i];
            }
            Dose += Peak * dt;

            if (!active)
            {
                if (wasActive) device.StopAll();
                wasActive = false;
                return;
            }
            wasActive = true;
            if (time < nextSend) return;
            float period = 1f / Mathf.Max(1f, UpdateHz);
            nextSend = time + period;
            device.Send(output, Mathf.CeilToInt(period * 2000f)); // overlap frames so the output never gaps
        }

        void ComputeField(EntityBrain brain, StimulusProfile st, float relative)
        {
            Array.Clear(field, 0, field.Length);
            if (brain.ActiveGesture == GestureType.Hug)
            {
                if (st.hugZones != null)
                    foreach (BodyZone z in st.hugZones) AddContact(body.ZonePosition(z), relative, st.contactSigma * 1.6f);
            }
            else AddContact(body.ZonePosition(brain.TargetZone), relative, st.contactSigma);
            for (int i = 0; i < field.Length; i++) field[i] = Mathf.Min(field[i], relative);
            Layout.ApplyGranularity(field, st.granularity);
        }

        void AddContact(Vector3 worldPoint, float strength, float sigma)
        {
            Pose t = body.TorsoPose;
            float k = 1f / (2f * sigma * sigma);
            for (int i = 0; i < field.Length; i++)
            {
                Vector3 p = t.position + t.rotation * Layout.LocalPosition(i, dims.ShoulderWidth, dims.TorsoDepth);
                float d2 = (p - worldPoint).sqrMagnitude;
                float v = strength * Mathf.Exp(-d2 * k);
                if (v < 0.01f) v = 0f;
                field[i] = Mathf.Max(field[i], v);
            }
        }

        // ---------- Version 2 ----------
        void TickPreset(float dt, EntityBrain brain, StimulusProfile st, bool allowed)
        {
            if (wasActive) { device.StopAll(); wasActive = false; }
            Array.Clear(output, 0, output.Length);
            Peak = 0f;

            bool want;
            float d = (brain.Position - body.TorsoPose.position).magnitude;
            if (st.presetTrigger == PresetTrigger.DuringContact)
                want = brain.State == EntityState.Contact || brain.State == EntityState.Hold;
            else
                want = playingEvent != null ? d <= Mathf.Max(st.presetStopDistance, st.presetStartDistance) : d < st.presetStartDistance;
            want &= allowed;

            if (want && playingEvent == null && !string.IsNullOrEmpty(st.presetEventId))
            {
                float intensity = st.presetIntensity * MasterGain;
                if (device.PlayEvent(st.presetEventId, intensity, st.presetLoop, st.presetLoopIntervalMs))
                {
                    playingEvent = st.presetEventId;
                    Note("preset_haptic_start", playingEvent + " d=" + d.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                }
                else Note("preset_haptic_failed", st.presetEventId);
            }
            else if (!want && playingEvent != null)
            {
                StopPreset("d=" + d.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            }
            if (playingEvent != null) Dose += st.presetIntensity * MasterGain * dt;
        }

        void StopPreset(string why)
        {
            device.StopEvent(playingEvent);
            Note("preset_haptic_stop", playingEvent + " " + why);
            playingEvent = null;
        }

        void Note(string kind, string detail) { if (OnNote != null) OnNote(kind, detail); }

        // ---------- Shared ----------
        /// <summary>Called on condition change: silence everything and forget what was playing.</summary>
        public void ResetOutput()
        {
            if (playingEvent != null) StopPreset("condition_changed");
            device.StopAll();
            wasActive = false;
            Peak = 0f;
            Array.Clear(output, 0, output.Length);
        }

        /// <summary>Drives one motor directly, for the motor walk and calibration. Bypasses the entity.</summary>
        public void TestActuator(int index, float level, int ms)
        {
            Array.Clear(output, 0, output.Length);
            output[Mathf.Clamp(index, 0, output.Length - 1)] = Mathf.Min(level, Calibration.hardLimit);
            device.Send(output, ms);
        }

        /// <summary>Plays a Portal event once from the panel, to check the Event ID.</summary>
        public bool TestEvent(string eventId, float intensity) { return device.PlayEvent(eventId, intensity, false, 0); }

        public void EmergencyStop()
        {
            stopped = true;
            Peak = 0f;
            if (playingEvent != null) Note("preset_haptic_stop", playingEvent + " emergency");
            playingEvent = null;
            Array.Clear(field, 0, field.Length);
            Array.Clear(output, 0, output.Length);
            device.StopAll();
        }

        public void Resume() { stopped = false; }
        public bool Stopped { get { return stopped; } }
    }
}
