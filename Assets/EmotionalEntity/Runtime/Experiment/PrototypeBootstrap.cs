using System;
using UnityEngine;

namespace EmotionalEntity
{
    public enum TrackingKind { Auto, Simulated, SceneNodes }
    public enum HapticKind { Simulated, Bhaptics }

    /// <summary>The one component in the scene. It builds the whole prototype when Play starts.
    /// Every setting below is also shown on the "Prototype" object in the Inspector.
    /// Runs before MiddleVR so that on a Mac it can switch MiddleVR off before MiddleVR tries to load its Windows libraries.</summary>
    [DefaultExecutionOrder(-10000)]
    public class PrototypeBootstrap : MonoBehaviour
    {
        [Header("Display and tracking")]
        [Tooltip("Desktop: development on any computer. MiddleVR: the CAVE PC, cameras come from the MVRManager.")]
        public DisplayMode display = DisplayMode.Desktop;
        [Tooltip("Auto: keyboard on Desktop, MiddleVR tracker nodes in MiddleVR.")]
        public TrackingKind tracking = TrackingKind.Auto;

        [Header("MiddleVR tracker nodes (names from your .vrx; empty = not tracked)")]
        public string headNode = "HeadNode";
        public string torsoNode = "";
        public string leftHandNode = "";
        public string rightHandNode = "HandNode";

        [Header("Safety area, metres from the CAVE floor centre (MiddleVR only)")]
        [Tooltip("The physical walking area. Contacts pause when the head is within Safety Margin of its edge.")]
        public Vector3 arenaMin = new Vector3(-1.5f, 0f, -1.5f);
        public Vector3 arenaMax = new Vector3(1.5f, 2.6f, 1.5f);
        [Range(0f, 1f)] public float safetyMargin = 0.3f;
        [Tooltip("Untick to test without the safety area (for example before its values are known). Contacts then never pause near a wall.")]
        public bool enableSafetyArea = true;
        [Tooltip("A tracker node that has not moved at all for this long counts as lost. 0 = never.")]
        [Range(0f, 10f)] public float nodeLostAfterSeconds = 1.5f;

        [Header("CAVE operator status (3D text on the walls; F1 shows/hides)")]
        public bool showStatusAtStart = true;
        public Vector3 statusBoardPosition = new Vector3(0f, 2.1f, 1.4f);
        [Range(-180f, 180f)] public float statusBoardYaw = 0f;

        [Header("Haptics")]
        public HapticKind haptics = HapticKind.Simulated;
        [Tooltip("RealtimeMotors (version 1): every motor follows the contact. PresetEvent (version 2): the condition's bHaptics Portal pattern plays on a trigger.")]
        public HapticMode hapticMode = HapticMode.RealtimeMotors;
        [Tooltip("TactSuit Air has 16 motors on the first and last row of each side.")]
        public bool tactSuitAirLayout;
        public HapticCalibration calibration = new HapticCalibration();

        [Header("Session")]
        public string participant = "P00";
        public TrialCondition[] conditions;

        EntityController controller;
        FramePresenter presenter;
        DesktopRig rig;
        IEntityVisual visual;
        IEventSink log;
        ITrackingSource trackingSource;
        IHapticDevice device;
        OperatorPanel panel;
        StatusBoard board;
        float statusTimer, logTimer;
        string lastLoggedKey = "none";

        public EntityController Controller { get { return controller; } }

        static readonly string[] MiddleVrObjects = { "MVRManager", "MVRInputSystem", "MVR_URP" };

        void Awake()
        {
            if (display != DisplayMode.MiddleVR) return;
            bool windows = Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer;
            if (windows) return;
            // MiddleVR only ships Windows libraries. Turn off every MiddleVR object before its Awake runs, then use Desktop mode.
            int off = 0;
            foreach (string n in MiddleVrObjects)
            {
                GameObject go = GameObject.Find(n);
                if (go != null) { go.SetActive(false); off++; }
            }
            // Also catch MiddleVR components on objects with other names.
            foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (mb == null || mb.gameObject == gameObject) continue;
                Type t = mb.GetType();
                bool mvr = (t.Namespace != null && t.Namespace.StartsWith("MiddleVR")) || t.Name.StartsWith("MVR");
                if (mvr && mb.gameObject.activeSelf) { mb.gameObject.SetActive(false); off++; }
            }
            Debug.LogWarning("[Prototype] This is the CAVE scene, and MiddleVR runs on Windows only. MiddleVR is switched off for this run; " +
                "using Desktop mode with keyboard tracking. On a Mac, use the EmotionalEntity_Desktop scene. (" + off + " MiddleVR objects switched off)");
        }

        void Start()
        {
            if (conditions == null || conditions.Length == 0)
            {
                conditions = new TrialCondition[PresetLibrary.Names.Length];
                for (int i = 0; i < conditions.Length; i++) conditions[i] = PresetLibrary.Create(PresetLibrary.Names[i]);
            }

            if (display == DisplayMode.MiddleVR && !MiddleVrInScene())
            {
                bool windows = Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer;
                if (windows) Debug.LogError("[Prototype] Display is MiddleVR but no active MVRManager is in the scene. If this is a built program, " +
                    "start it with --config <your .vrx>, otherwise MiddleVR turns itself off. Falling back to Desktop with keyboard tracking.");
                if (Camera.main != null) Camera.main.gameObject.SetActive(false); // the MiddleVR template camera
                display = DisplayMode.Desktop;
            }

            visual = new ProceduralCloudVisual(transform, conditions[0].seed);
            presenter = new FramePresenter(visual);
            trackingSource = MakeTracking();
            device = haptics == HapticKind.Bhaptics ? (IHapticDevice)new BhapticsHapticDevice() : new SimulatedHapticDevice();

            var body = new BodyZoneManager();
            var layout = new ActuatorLayout { OuterRowsOnly = tactSuitAirLayout };
            var manager = new HapticManager(device, layout, body) { Calibration = calibration, Mode = hapticMode };
            log = new CsvEventSink(participant);

            string displayName;
            Vector3 min = Vector3.zero, max = Vector3.zero;
            float margin = 0f;
            if (display == DisplayMode.MiddleVR)
            {
                displayName = "MiddleVR CAVE";
                if (enableSafetyArea) { min = arenaMin; max = arenaMax; margin = safetyMargin; }
                OperatorKeys.GlobalKeysEnabled = true;
                WarnIfSystemCenterMoved();
            }
            else
            {
                rig = new DesktopRig(transform);
                displayName = "Desktop";
            }

            controller = new EntityController(trackingSource, body, manager, presenter.Present, log, min, max, margin);
            controller.SetCondition(conditions[0], Time.timeAsDouble);

            panel = gameObject.AddComponent<OperatorPanel>();
            panel.Init(controller, conditions, displayName);
            // In the CAVE the PC's window is a wall the participant sees. Keep the panel off until F1.
            if (display == DisplayMode.MiddleVR)
            {
                panel.Visible = showStatusAtStart;
                board = new StatusBoard(transform, statusBoardPosition, statusBoardYaw) { Visible = showStatusAtStart };
                if (!OperatorKeys.GlobalKeysSupported) Debug.LogWarning("[Prototype] Keys read from Windows are not available on this platform.");
            }

            log.Mark(Time.timeAsDouble, "session", "display=" + displayName + " tracking=" + trackingSource.Name +
                " haptics=" + device.Name + " mode=" + hapticMode);
            Debug.Log("[Prototype] Display: " + displayName + " | Tracking: " + trackingSource.Name +
                " | Haptics: " + device.Status + " | Mode: " + hapticMode + " | Log: " + ((CsvEventSink)log).Path);
        }

        /// <summary>Looks for MiddleVR's manager by type name, so this project compiles with or without MiddleVR.</summary>
        static bool MiddleVrInScene()
        {
            foreach (System.Reflection.Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = a.GetType("MVRManagerScript");
                if (t != null) return UnityEngine.Object.FindAnyObjectByType(t) != null;
            }
            return false;
        }

        static void WarnIfSystemCenterMoved()
        {
            GameObject mgr = GameObject.Find("MVRManager");
            if (mgr != null && mgr.transform.position.sqrMagnitude > 1e-4f)
                Debug.LogWarning("[Prototype] MVRManager is at " + mgr.transform.position + ", not the world origin. With 'Attach To Camera' MiddleVR " +
                    "puts the whole CAVE under the first camera, so set Main Camera Position and Rotation to 0 (or untick Attach To Camera).");
            GameObject sc = GameObject.Find("SystemCenter");
            if (sc != null && (sc.transform.position.sqrMagnitude > 1e-6f || Quaternion.Angle(sc.transform.rotation, Quaternion.identity) > 0.01f))
                Debug.LogWarning("[Prototype] SystemCenter is not at the world origin. Entity home and the safety area are in world " +
                    "coordinates, so keep SystemCenter at the origin and do not add MiddleVR navigation.");
        }

        ITrackingSource MakeTracking()
        {
            TrackingKind k = tracking;
            if (k == TrackingKind.Auto) k = display == DisplayMode.MiddleVR ? TrackingKind.SceneNodes : TrackingKind.Simulated;
            if (display == DisplayMode.Desktop && k == TrackingKind.SceneNodes)
            {
                Debug.LogWarning("[Prototype] Tracker nodes are only moved by MiddleVR. Using keyboard tracking on Desktop.");
                k = TrackingKind.Simulated;
            }
            if (k == TrackingKind.SceneNodes)
                return new NodeTracking { HeadNode = headNode, TorsoNode = torsoNode, LeftHandNode = leftHandNode, RightHandNode = rightHandNode, StaleSeconds = nodeLostAfterSeconds };
            return new SimulatedTracking();
        }

        void Update()
        {
            if (controller == null) return;
            double now = Time.timeAsDouble;
            if (OperatorKeys.EmergencyStopPressed()) controller.EmergencyStop(now);
            if (OperatorKeys.ClearStopPressed() && controller.EmergencyStopped) controller.ClearEmergencyStop(now);
            int c = OperatorKeys.ConditionKeyPressed();
            if (c >= 0 && c < conditions.Length) controller.SetCondition(conditions[c], now);
            if (OperatorKeys.TouchPressed()) controller.TriggerGesture(GestureType.Touch, panel.SelectedZone);
            if (OperatorKeys.HugPressed()) controller.TriggerGesture(GestureType.Hug, BodyZone.Chest);
            if (OperatorKeys.HideEntityPressed())
            {
                controller.EntityVisible = !controller.EntityVisible;
                controller.Log.Mark(now, "entity_visible", controller.EntityVisible.ToString());
            }
            if (rig != null && OperatorKeys.ObserverPressed()) { rig.Observer = !rig.Observer; panel.SetDisplayName(rig.Name); }

            controller.Tick(now, Time.deltaTime);
            if (rig != null) rig.UpdateViewer(controller.Body.HeadPose, controller.Body.TorsoPose);

            if (OperatorKeys.LastKey != lastLoggedKey)
            {
                lastLoggedKey = OperatorKeys.LastKey;
                controller.Log.Mark(now, "operator_key", lastLoggedKey);
            }
            if (board != null)
            {
                board.Visible = panel.Visible;
                statusTimer -= Time.unscaledDeltaTime;
                if (board.Visible && statusTimer <= 0f) { statusTimer = 0.25f; board.SetText(StatusText()); }
            }
            if (display == DisplayMode.MiddleVR)
            {
                logTimer -= Time.unscaledDeltaTime;
                if (logTimer <= 0f) { logTimer = 5f; Debug.Log("[Prototype] Status\n" + StatusText()); }
            }
        }

        /// <summary>One block of plain text: what the operator needs to see in the CAVE.</summary>
        string StatusText()
        {
            EntityController c = controller;
            BodyZoneManager b = c.Body;
            var sb = new System.Text.StringBuilder(400);
            sb.Append("OPERATOR  (F1 hide)   ").Append(c.Condition.conditionId).Append("   ").Append(c.Brain.State);
            sb.Append("   contact #").Append(c.Brain.ContactIndex).AppendLine();
            var nt = trackingSource as NodeTracking;
            string head = nt != null && !nt.Found(TrackedPoint.Head) ? "NOT FOUND: \"" + headNode + "\"" : b.HeadValid ? "OK" : "LOST (not moving)";
            sb.Append("Head ").Append(head).Append("  at ").Append(b.Head.ToString("F2")).AppendLine();
            if (nt != null && !string.IsNullOrEmpty(rightHandNode))
                sb.Append("Hand \"").Append(rightHandNode).Append("\" ").Append(nt.Found(TrackedPoint.RightHand) ? "found" : "NOT FOUND").AppendLine();
            sb.Append(!enableSafetyArea ? "Safety area OFF" : c.UserInSafeArea ? "Inside safety area" : "NEAR WALL / OUTSIDE AREA: contacts paused").AppendLine();
            sb.Append("Next contact ").Append(c.Brain.Scheduler.Running ? (c.Brain.Scheduler.NextTime - Time.timeAsDouble).ToString("0.0") + " s" : "waiting for trigger (" + c.Condition.behavior.trigger + ")").AppendLine();
            sb.Append("Haptics ").Append(c.Haptics.Mode).Append(": ").Append(c.Haptics.Device.Status).AppendLine();
            if (c.EmergencyStopped) sb.Append("EMERGENCY STOP  (C to clear)").AppendLine();
            if (!c.EntityVisible) sb.Append("Entity hidden (V)").AppendLine();
            sb.Append("Last key: ").Append(OperatorKeys.LastKey).AppendLine();
            sb.Append("1/2/3 entity  T touch  H hug  V show  Space stop");
            return sb.ToString();
        }

        void OnDestroy()
        {
            if (device != null) device.Dispose();
            if (trackingSource != null) trackingSource.Dispose();
            if (rig != null) rig.Dispose();
            if (board != null) board.Dispose();
            OperatorKeys.GlobalKeysEnabled = false;
            if (visual != null) visual.Dispose();
            if (log != null) log.Dispose();
        }
    }
}
