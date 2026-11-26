using System;
using UnityEngine;

namespace EmotionalEntity
{
    public enum TrackingKind { Auto, Simulated, SceneNodes }
    public enum HapticKind { Simulated, Bhaptics }

    /// <summary>The one component in the scene. It builds the whole prototype when Play starts.
    /// Every setting below is also shown on the "Prototype" object in the Inspector.</summary>
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

        public EntityController Controller { get { return controller; } }

        void Start()
        {
            if (conditions == null || conditions.Length == 0)
            {
                conditions = new TrialCondition[PresetLibrary.Names.Length];
                for (int i = 0; i < conditions.Length; i++) conditions[i] = PresetLibrary.Create(PresetLibrary.Names[i]);
            }

            if (display == DisplayMode.MiddleVR && !MiddleVrInScene())
            {
                Debug.LogError("[Prototype] Display is MiddleVR but no MVRManager is in the scene (or MiddleVR is not installed). " +
                    "MiddleVR runs on Windows only. Falling back to Desktop with keyboard tracking.");
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
                min = arenaMin; max = arenaMax; margin = safetyMargin;
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
            if (display == DisplayMode.MiddleVR) panel.Visible = false;

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
                return new NodeTracking { HeadNode = headNode, TorsoNode = torsoNode, LeftHandNode = leftHandNode, RightHandNode = rightHandNode };
            return new SimulatedTracking();
        }

        void Update()
        {
            if (controller == null) return;
            double now = Time.timeAsDouble;
            if (InputShim.EmergencyStopPressed()) controller.EmergencyStop(now);
            if (InputShim.ClearStopPressed() && controller.EmergencyStopped) controller.ClearEmergencyStop(now);
            int c = InputShim.ConditionKeyPressed();
            if (c >= 0 && c < conditions.Length) controller.SetCondition(conditions[c], now);
            if (InputShim.TouchPressed()) controller.TriggerGesture(GestureType.Touch, panel.SelectedZone);
            if (InputShim.HugPressed()) controller.TriggerGesture(GestureType.Hug, BodyZone.Chest);
            if (InputShim.HideEntityPressed())
            {
                controller.EntityVisible = !controller.EntityVisible;
                controller.Log.Mark(now, "entity_visible", controller.EntityVisible.ToString());
            }
            if (rig != null && InputShim.ObserverPressed()) { rig.Observer = !rig.Observer; panel.SetDisplayName(rig.Name); }

            controller.Tick(now, Time.deltaTime);
            if (rig != null) rig.UpdateViewer(controller.Body.HeadPose, controller.Body.TorsoPose);
        }

        void OnDestroy()
        {
            if (device != null) device.Dispose();
            if (trackingSource != null) trackingSource.Dispose();
            if (rig != null) rig.Dispose();
            if (visual != null) visual.Dispose();
            if (log != null) log.Dispose();
        }
    }
}
