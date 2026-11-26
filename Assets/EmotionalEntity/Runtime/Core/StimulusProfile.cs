using UnityEngine;

namespace EmotionalEntity
{
    [CreateAssetMenu(menuName = "Emotional Entity/Stimulus Profile")]
    public class StimulusProfile : ScriptableObject
    {
        [Tooltip("Relative to the participant's calibrated range: 0 = detection threshold, 1 = comfort ceiling. Not force or pressure.")]
        [Range(0f, 1f)] public float intensity = 0.3f;
        public BodyZone[] touchZones = { BodyZone.LeftShoulder, BodyZone.RightShoulder };
        public BodyZone[] hugZones = { BodyZone.Back, BodyZone.LeftSide, BodyZone.RightSide };
        [Tooltip("Spatial spread of a contact on the body, metres. Smaller feels more pointed.")]
        [Range(0.03f, 0.4f)] public float contactSigma = 0.1f;
        public Granularity granularity = Granularity.Full32;
        [Range(0f, 1f)] public float attack = 0.15f;
        [Range(0f, 1f)] public float release = 0.25f;
        [Tooltip("Slow swell while a hug holds, Hz.")]
        [Range(0f, 2f)] public float holdSwellRate = 0.25f;
        [Range(0f, 0.8f)] public float holdSwellDepth = 0.2f;

        [Header("Version 2: preset bHaptics event (used when Prototype > Haptic Mode = Preset Event)")]
        [Tooltip("Event ID exactly as in the bHaptics Developer Portal. Empty = this condition plays nothing.")]
        public string presetEventId = "";
        public PresetTrigger presetTrigger = PresetTrigger.Proximity;
        [Tooltip("Proximity: start when the cloud centre is closer than this to the chest, metres.")]
        [Range(0.2f, 3f)] public float presetStartDistance = 0.7f;
        [Tooltip("Proximity: stop when it is further than this. Keep it above the start distance so the vest does not flicker.")]
        [Range(0.2f, 3.5f)] public float presetStopDistance = 0.9f;
        [Tooltip("Multiplier on the pattern as designed in the Portal. 1 = as designed.")]
        [Range(0.1f, 2f)] public float presetIntensity = 1f;
        [Tooltip("Repeat the pattern while the trigger holds. Off = play once per trigger.")]
        public bool presetLoop = true;
        [Range(0, 2000)] public int presetLoopIntervalMs = 200;
    }
}
