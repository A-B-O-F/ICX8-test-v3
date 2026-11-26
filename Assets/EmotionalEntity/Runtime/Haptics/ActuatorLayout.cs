using UnityEngine;

namespace EmotionalEntity
{
    /// <summary>Where each actuator sits on the torso, and how it merges at lower granularity.
    /// Default is a 4x4 grid per side, indices front 0-15, back 16-31 (bHaptics TactSuit Pro order).
    /// Physical left/right must be confirmed with the operator panel's motor walk, then set the mirror flags.</summary>
    public class ActuatorLayout
    {
        public const int Cols = 4, Rows = 4;
        public int Count { get { return Cols * Rows * 2; } }
        public bool MirrorFront, MirrorBack;
        /// <summary>TactSuit Air only has the first and last row of each side.</summary>
        public bool OuterRowsOnly;

        public bool IsActive(int index)
        {
            if (!OuterRowsOnly) return true;
            int row = (index % (Cols * Rows)) / Cols;
            return row == 0 || row == Rows - 1;
        }

        public Vector3 LocalPosition(int index, float shoulderWidth, float torsoDepth)
        {
            bool back = index >= Cols * Rows;
            int i = index % (Cols * Rows);
            int col = i % Cols, row = i / Cols;
            float x = (col - (Cols - 1) * 0.5f) / (Cols - 1) * shoulderWidth * 0.7f;
            if (back ? MirrorBack : MirrorFront) x = -x;
            float y = 0.2f - row * (0.4f / (Rows - 1));
            return new Vector3(x, y, (back ? -0.5f : 0.5f) * torsoDepth);
        }

        /// <summary>Merges into groups: every member of a group outputs the group's peak, so a contact
        /// never disappears at coarse granularity. Output is written in place.</summary>
        public void ApplyGranularity(float[] v, Granularity g)
        {
            int gc, gr;
            switch (g)
            {
                case Granularity.Groups16: gc = 2; gr = 4; break;
                case Granularity.Groups8: gc = 2; gr = 2; break;
                case Granularity.Groups4: gc = 2; gr = 1; break;
                default: return;
            }
            int per = Cols * Rows;
            for (int side = 0; side < 2; side++)
            for (int a = 0; a < gr; a++)
            for (int b = 0; b < gc; b++)
            {
                float peak = 0f;
                for (int r = 0; r < Rows; r++) for (int c = 0; c < Cols; c++)
                    if (r * gr / Rows == a && c * gc / Cols == b && IsActive(side * per + r * Cols + c))
                        peak = Mathf.Max(peak, v[side * per + r * Cols + c]);
                for (int r = 0; r < Rows; r++) for (int c = 0; c < Cols; c++)
                    if (r * gr / Rows == a && c * gc / Cols == b)
                        v[side * per + r * Cols + c] = peak;
            }
        }
    }

    /// <summary>Per-participant mapping from relative stimulus to device output, with a hard ceiling.</summary>
    [System.Serializable]
    public class HapticCalibration
    {
        [Range(0f, 1f)] public float detectionThreshold = 0.1f;
        [Range(0f, 1f)] public float comfortCeiling = 0.8f;
        [Range(0f, 1f)] public float hardLimit = 0.9f;

        public float Map(float relative)
        {
            if (relative <= 0.001f) return 0f;
            float top = Mathf.Min(comfortCeiling, hardLimit);
            return Mathf.Min(hardLimit, Mathf.Lerp(detectionThreshold, top, Mathf.Clamp01(relative)));
        }
    }
}
