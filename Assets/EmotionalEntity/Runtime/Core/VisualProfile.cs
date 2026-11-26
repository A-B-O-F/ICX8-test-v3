using UnityEngine;

namespace EmotionalEntity
{
    [CreateAssetMenu(menuName = "Emotional Entity/Visual Profile")]
    public class VisualProfile : ScriptableObject
    {
        [Header("Form")]
        [Range(0.2f, 1.5f)] public float radius = 0.55f;
        [Range(0f, 1f)] public float cohesion = 0.6f;
        [Range(0.4f, 1.6f)] public float verticalStretch = 1f;
        [Range(-1f, 1f)] public float droop = 0f;

        [Header("Motion")]
        [Range(0f, 1f)] public float turbulence = 0.25f;
        [Range(0.2f, 6f)] public float flowFrequency = 1.5f;
        [Range(0f, 3f)] public float flowSpeed = 0.25f;
        [Range(0f, 6f)] public float pulseRate = 0.25f;
        [Range(0f, 0.5f)] public float pulseDepth = 0.05f;
        [Range(0f, 6.3f)] public float pulseScatter = 0.5f;

        [Header("Particles (count changes rebuild the mesh)")]
        [Range(1000, 400000)] public int detailCount = 80000;
        [Range(0, 3000)] public int volumeCount = 500;
        [Range(0.002f, 0.04f)] public float detailSize = 0.009f;
        [Tooltip("Per-particle opacity. Lower it when raising the count, or the cloud burns out to white.")]
        [Range(0.01f, 1f)] public float detailOpacity = 0.1f;
        [Range(0.05f, 0.8f)] public float volumeSize = 0.38f;
        [Range(0f, 1f)] public float volumeOpacity = 0.035f;
        [Range(0f, 1f)] public float additive = 0.75f;
        [Range(0f, 8f)] public float brightness = 1.2f;

        [Header("Colour")]
        [ColorUsage(false, true)] public Color colorA = new Color(0.35f, 0.75f, 1f);
        [ColorUsage(false, true)] public Color colorB = new Color(0.9f, 0.95f, 1f);

        [Header("Gesture shapes")]
        [Range(0f, 1f)] public float streamFraction = 0.45f;
        [Range(0.05f, 4f)] public float streamSpeed = 0.5f;
        [Range(0.01f, 0.3f)] public float streamTipRadius = 0.05f;
        [Range(0.2f, 0.9f)] public float hugRadius = 0.42f;
        [Range(0.3f, 1.6f)] public float hugHeight = 0.9f;
        [Tooltip("Half angle kept clear in front of the chest, degrees. In a CAVE the body blocks projection, so particles in front of it cannot be shown correctly.")]
        [Range(0f, 90f)] public float hugFrontClear = 35f;
        [Range(0.1f, 0.5f)] public float headClearance = 0.28f;
    }
}
