using System.IO;
using UnityEngine;

namespace EmotionalEntity
{
    /// <summary>Operator UI on Display 0: pick a condition, fire gestures, tune parameters live,
    /// test motors, see the body map. F1 hides it. Space is the emergency stop.</summary>
    public class OperatorPanel : MonoBehaviour
    {
        EntityController ctl;
        TrialCondition[] conditions;
        string displayName;
        bool visible = true;
        int tab;
        Vector2 scroll;
        BodyZone zone = BodyZone.LeftShoulder;
        public BodyZone SelectedZone { get { return zone; } }
        public bool Visible { get { return visible; } set { visible = value; } }
        int motor;
        float testLevel = 0.5f;
        bool walking;
        float walkTimer;
        string message = "";
        readonly string[] tabs = { "Run", "Visual", "Behaviour", "Stimulus", "Motors" };

        public void Init(EntityController controller, TrialCondition[] cs, string display)
        {
            ctl = controller; conditions = cs; displayName = display;
        }

        public void SetDisplayName(string display) { displayName = display; }

        void Update()
        {
            if (OperatorKeys.TogglePanelPressed()) visible = !visible;
            if (!walking || ctl == null) return;
            walkTimer -= Time.deltaTime;
            if (walkTimer > 0f) return;
            walkTimer = 0.6f;
            ctl.Haptics.TestActuator(motor, testLevel, 400);
            message = "Motor " + motor + " (" + (motor < 16 ? "front" : "back") + ")";
            motor++;
            if (motor >= ctl.Haptics.Layout.Count) { motor = 0; walking = false; message = "Walk finished"; }
        }

        void OnGUI()
        {
            if (ctl == null) return;
            if (ctl.EmergencyStopped)
            {
                GUI.color = Color.red;
                if (GUI.Button(new Rect(Screen.width - 240, 10, 230, 44), "STOPPED. Click to clear")) ctl.ClearEmergencyStop(Time.timeAsDouble);
                GUI.color = Color.white;
            }
            if (!visible) return;

            GUILayout.BeginArea(new Rect(10, 10, 370, Screen.height - 20), GUI.skin.box);
            GUILayout.Label(displayName + "  |  " + ctl.Tracking.Name);
            GUILayout.Label("Haptics: " + ctl.Haptics.Device.Name + " | " + ctl.Haptics.Mode + " | " + ctl.Haptics.Device.Status);
            GUILayout.Label(ctl.Condition.conditionId + "  " + ctl.Brain.State + "  contact #" + ctl.Brain.ContactIndex
                + "  next " + (ctl.Brain.Scheduler.Running ? (ctl.Brain.Scheduler.NextTime - Time.timeAsDouble).ToString("0.0") + " s" : "off"));
            GUILayout.Label("User speed " + ctl.Body.Speed.ToString("0.00") + " m/s  dose " + ctl.Haptics.Dose.ToString("0.00")
                + (ctl.UserInSafeArea ? "" : "  NEAR WALL: contact paused") + (ctl.Body.HeadValid ? "" : "  TRACKING LOST"));
            if (GUILayout.Button("EMERGENCY STOP (Space)")) ctl.EmergencyStop(Time.timeAsDouble);

            tab = GUILayout.Toolbar(tab, tabs);
            scroll = GUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case 0: RunTab(); break;
                case 1: VisualTab(ctl.Condition.visual); break;
                case 2: BehaviourTab(ctl.Condition.behavior, ctl.Condition.schedule); break;
                case 3: StimulusTab(ctl.Condition.stimulus); break;
                case 4: MotorTab(); break;
            }
            GUILayout.EndScrollView();
            if (message.Length > 0) GUILayout.Label(message);
            GUILayout.EndArea();
            DrawBodyMap();
        }

        void RunTab()
        {
            GUILayout.Label("Condition");
            GUILayout.BeginHorizontal();
            foreach (TrialCondition c in conditions)
                if (GUILayout.Button(c.conditionId)) ctl.SetCondition(c, Time.timeAsDouble);
            GUILayout.EndHorizontal();

            GUILayout.Label("Target zone: " + zone);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<")) zone = (BodyZone)Mathf.Max(0, (int)zone - 1);
            if (GUILayout.Button(">")) zone = (BodyZone)Mathf.Min((int)BodyZone.RightHand, (int)zone + 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Touch")) ctl.TriggerGesture(GestureType.Touch, zone);
            if (GUILayout.Button("Hug")) ctl.TriggerGesture(GestureType.Hug, BodyZone.Chest);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save profile")) Save();
            if (GUILayout.Button("Load profile")) Load();
            GUILayout.EndHorizontal();
            ctl.EntityVisible = GUILayout.Toggle(ctl.EntityVisible, "Entity visible (V)");
            GUILayout.Label("Keys: 1/2/3 condition, T touch, H hug, V show/hide, Space stop, C clear stop, F1 panel");
            GUILayout.Label("Desktop only: WASD walk, Q/E turn, R/F reach hands, O observer view");
        }

        void VisualTab(VisualProfile v)
        {
            v.radius = S("Radius", v.radius, 0.2f, 1.5f);
            v.cohesion = S("Cohesion", v.cohesion, 0f, 1f);
            v.verticalStretch = S("Vertical stretch", v.verticalStretch, 0.4f, 1.6f);
            v.droop = S("Droop", v.droop, -1f, 1f);
            v.turbulence = S("Turbulence", v.turbulence, 0f, 1f);
            v.flowFrequency = S("Flow frequency", v.flowFrequency, 0.2f, 6f);
            v.flowSpeed = S("Flow speed", v.flowSpeed, 0f, 3f);
            v.pulseRate = S("Pulse rate Hz", v.pulseRate, 0f, 6f);
            v.pulseDepth = S("Pulse depth", v.pulseDepth, 0f, 0.5f);
            v.pulseScatter = S("Pulse scatter", v.pulseScatter, 0f, 6.3f);
            v.detailCount = Mathf.RoundToInt(S("Detail particles", v.detailCount, 1000, 400000) / 1000f) * 1000;
            v.volumeCount = Mathf.RoundToInt(S("Volume particles", v.volumeCount, 0, 3000) / 50f) * 50;
            v.detailSize = S("Detail size", v.detailSize, 0.002f, 0.04f);
            v.detailOpacity = S("Detail opacity", v.detailOpacity, 0.01f, 1f);
            v.volumeSize = S("Volume size", v.volumeSize, 0.05f, 0.8f);
            v.volumeOpacity = S("Volume opacity", v.volumeOpacity, 0f, 1f);
            v.additive = S("Glow (additive)", v.additive, 0f, 1f);
            v.brightness = S("Brightness", v.brightness, 0f, 8f);
            v.streamFraction = S("Stream share", v.streamFraction, 0f, 1f);
            v.streamSpeed = S("Stream speed", v.streamSpeed, 0.05f, 4f);
            v.hugRadius = S("Hug radius", v.hugRadius, 0.2f, 0.9f);
            v.hugFrontClear = S("Hug front gap deg", v.hugFrontClear, 0f, 90f);
            v.headClearance = S("Head clearance", v.headClearance, 0.1f, 0.5f);
        }

        void BehaviourTab(BehaviorProfile b, ContactSchedule s)
        {
            b.trigger = (TriggerMode)GUILayout.Toolbar((int)b.trigger, new[] { "Scheduled", "Territory", "Proximity" });
            b.gesture = (GestureType)GUILayout.Toolbar((int)b.gesture, new[] { "Touch", "Repeated", "Hug" });
            b.trajectory = (Trajectory)GUILayout.Toolbar((int)b.trajectory, new[] { "Direct", "Arc", "Orbit", "Hesitant" });
            b.handReaction = (HandReaction)GUILayout.Toolbar((int)b.handReaction, new[] { "None", "Yield", "Lean", "Flare" });
            b.territoryRadius = S("Territory radius", b.territoryRadius, 0.5f, 6f);
            b.proximityRadius = S("Proximity radius", b.proximityRadius, 0.5f, 5f);
            b.approachSpeed = S("Approach speed", b.approachSpeed, 0.05f, 3f);
            b.retreatSpeed = S("Retreat speed", b.retreatSpeed, 0.05f, 4f);
            b.retreatDistance = S("Retreat distance", b.retreatDistance, 0.2f, 5f);
            b.contactDistance = S("Contact distance", b.contactDistance, 0.05f, 0.6f);
            b.breakDistance = S("Break distance", b.breakDistance, 0.1f, 2f);
            b.handForce = S("Hand force (+ push, - pull)", b.handForce, -0.3f, 0.3f);
            GUILayout.Label("Timing (some apply from the next contact)");
            s.pattern = (SchedulePattern)GUILayout.Toolbar((int)s.pattern, new[] { "Regular", "Burst", "Escalating" });
            s.interval = S("Interval s", s.interval, 0.5f, 30f);
            s.contactDuration = S("Contact duration s", s.contactDuration, 0.05f, 5f);
            s.jitter = S("Jitter", s.jitter, 0f, 0.5f);
            s.escalationRate = S("Escalation", s.escalationRate, 0f, 0.5f);
            s.minInterval = S("Min interval s", s.minInterval, 0.5f, 10f);
            s.holdSeconds = S("Hug hold s", s.holdSeconds, 0.5f, 15f);
        }

        void StimulusTab(StimulusProfile t)
        {
            GUILayout.Label("Haptic mode (applies to every condition)");
            ctl.Haptics.Mode = (HapticMode)GUILayout.Toolbar((int)ctl.Haptics.Mode, new[] { "V1 Real-time motors", "V2 Preset event" });
            if (ctl.Haptics.Mode == HapticMode.PresetEvent) { PresetSection(t); return; }

            t.intensity = S("Relative intensity", t.intensity, 0f, 1f);
            t.contactSigma = S("Contact spread m", t.contactSigma, 0.03f, 0.4f);
            t.attack = S("Attack", t.attack, 0f, 1f);
            t.release = S("Release", t.release, 0f, 1f);
            t.holdSwellRate = S("Hold swell Hz", t.holdSwellRate, 0f, 2f);
            t.holdSwellDepth = S("Hold swell depth", t.holdSwellDepth, 0f, 0.8f);
            GUILayout.Label("Granularity (actuator groups)");
            int g = t.granularity == Granularity.Full32 ? 0 : t.granularity == Granularity.Groups16 ? 1 : t.granularity == Granularity.Groups8 ? 2 : 3;
            g = GUILayout.Toolbar(g, new[] { "32", "16", "8", "4" });
            t.granularity = g == 0 ? Granularity.Full32 : g == 1 ? Granularity.Groups16 : g == 2 ? Granularity.Groups8 : Granularity.Groups4;
            HapticCalibration c = ctl.Haptics.Calibration;
            GUILayout.Label("Participant calibration (device output)");
            c.detectionThreshold = S("Detection threshold", c.detectionThreshold, 0f, 1f);
            c.comfortCeiling = S("Comfort ceiling", c.comfortCeiling, 0f, 1f);
            GUILayout.Label("Hard limit " + c.hardLimit.ToString("0.00"));
            ctl.Haptics.MasterGain = S("Master gain", ctl.Haptics.MasterGain, 0f, 1f);
        }

        void PresetSection(StimulusProfile t)
        {
            GUILayout.Label("Portal event ID for " + ctl.Condition.conditionId + " (exactly as in the bHaptics Portal)");
            t.presetEventId = GUILayout.TextField(t.presetEventId ?? "").Trim();
            if (string.IsNullOrEmpty(t.presetEventId)) GUILayout.Label("No event ID: this condition plays nothing.");
            t.presetTrigger = (PresetTrigger)GUILayout.Toolbar((int)t.presetTrigger, new[] { "Cloud is near", "During touch/hug" });
            if (t.presetTrigger == PresetTrigger.Proximity)
            {
                t.presetStartDistance = S("Start below m", t.presetStartDistance, 0.2f, 3f);
                t.presetStopDistance = Mathf.Max(t.presetStartDistance, S("Stop above m", t.presetStopDistance, 0.2f, 3.5f));
            }
            t.presetIntensity = S("Pattern intensity x", t.presetIntensity, 0.1f, 2f);
            t.presetLoop = GUILayout.Toggle(t.presetLoop, "Repeat while the trigger holds");
            if (t.presetLoop) t.presetLoopIntervalMs = Mathf.RoundToInt(S("Pause between repeats ms", t.presetLoopIntervalMs, 0, 2000));
            if (GUILayout.Button("Play this event once (test)"))
                message = ctl.Haptics.TestEvent(t.presetEventId, t.presetIntensity) ? "Sent " + t.presetEventId : "Could not play \"" + t.presetEventId + "\". Check the ID and the Developer Window link.";
            ctl.Haptics.MasterGain = S("Master gain", ctl.Haptics.MasterGain, 0f, 1f);
            GUILayout.Label("Granularity and calibration do not apply to preset events: the pattern is fixed in the Portal.");
        }

        void MotorTab()
        {
            testLevel = S("Test level", testLevel, 0f, 1f);
            motor = Mathf.RoundToInt(S("Motor index", motor, 0, ctl.Haptics.Layout.Count - 1));
            if (GUILayout.Button("Pulse this motor")) ctl.Haptics.TestActuator(motor, testLevel, 400);
            if (GUILayout.Button(walking ? "Stop walk" : "Walk all motors")) { walking = !walking; walkTimer = 0f; }
            GUILayout.Label("Use the walk to confirm left/right on the real vest, then set the mirror flags.");
            ActuatorLayout l = ctl.Haptics.Layout;
            l.MirrorFront = GUILayout.Toggle(l.MirrorFront, "Mirror front");
            l.MirrorBack = GUILayout.Toggle(l.MirrorBack, "Mirror back");
        }

        void DrawBodyMap()
        {
            float[] v = ctl.Haptics.LastOutput;
            const float cell = 22f;
            float x0 = 400f, y0 = 14f;
            if (ctl.Haptics.Mode == HapticMode.PresetEvent)
            {
                string e = ctl.Haptics.PlayingEvent;
                GUI.Label(new Rect(x0, y0, 320, 40), e != null ? "Preset event playing: " + e : "Preset event: idle");
                return;
            }
            GUI.Label(new Rect(x0, y0, 90, 20), "Front");
            GUI.Label(new Rect(x0 + 110, y0, 90, 20), "Back");
            Color keep = GUI.color;
            for (int side = 0; side < 2; side++)
            for (int r = 0; r < ActuatorLayout.Rows; r++)
            for (int c = 0; c < ActuatorLayout.Cols; c++)
            {
                int i = side * 16 + r * ActuatorLayout.Cols + c;
                GUI.color = ctl.Haptics.Layout.IsActive(i) ? Color.Lerp(new Color(0.2f, 0.2f, 0.25f), new Color(1f, 0.45f, 0.1f), v[i]) : Color.black;
                GUI.DrawTexture(new Rect(x0 + side * 110 + c * cell, y0 + 22 + r * cell, cell - 2, cell - 2), Texture2D.whiteTexture);
            }
            GUI.color = keep;
        }

        static float S(string label, float value, float min, float max)
        {
            GUILayout.Label(label + ": " + value.ToString(max > 100f ? "0" : "0.###"));
            return GUILayout.HorizontalSlider(value, min, max);
        }

        [System.Serializable]
        class SavedProfile { public string visual, behavior, schedule, stimulus; }

        string SavePath { get { return Path.Combine(Application.persistentDataPath, ctl.Condition.conditionId + "_profile.json"); } }

        void Save()
        {
            TrialCondition c = ctl.Condition;
            var s = new SavedProfile { visual = JsonUtility.ToJson(c.visual), behavior = JsonUtility.ToJson(c.behavior), schedule = JsonUtility.ToJson(c.schedule), stimulus = JsonUtility.ToJson(c.stimulus) };
            File.WriteAllText(SavePath, JsonUtility.ToJson(s, true));
            message = "Saved " + SavePath;
            ctl.Log.Mark(Time.timeAsDouble, "profile_saved", SavePath);
        }

        void Load()
        {
            if (!File.Exists(SavePath)) { message = "No saved profile for " + ctl.Condition.conditionId; return; }
            var s = JsonUtility.FromJson<SavedProfile>(File.ReadAllText(SavePath));
            TrialCondition c = ctl.Condition;
            JsonUtility.FromJsonOverwrite(s.visual, c.visual);
            JsonUtility.FromJsonOverwrite(s.behavior, c.behavior);
            JsonUtility.FromJsonOverwrite(s.schedule, c.schedule);
            JsonUtility.FromJsonOverwrite(s.stimulus, c.stimulus);
            message = "Loaded " + SavePath;
            ctl.Log.Mark(Time.timeAsDouble, "profile_loaded", SavePath);
        }
    }
}
