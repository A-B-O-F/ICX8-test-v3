using System.Runtime.InteropServices;
using UnityEngine;

namespace EmotionalEntity
{
    /// <summary>Operator keys, from two sources combined:
    /// 1. Unity's own keyboard (needs the Unity window to have keyboard focus);
    /// 2. in the CAVE on Windows, the physical keyboard read directly from Windows, whichever window has focus.
    /// In a MiddleVR CAVE the visible window is MiddleVR's, not Unity's, so source 2 is what makes the keys work there.</summary>
    public static class OperatorKeys
    {
        // Windows virtual-key codes
        const int VK_SPACE = 0x20, VK_F1 = 0x70, VK_1 = 0x31, VK_2 = 0x32, VK_3 = 0x33;
        const int VK_C = 0x43, VK_H = 0x48, VK_O = 0x4F, VK_T = 0x54, VK_V = 0x56;

        /// <summary>Turned on by PrototypeBootstrap in MiddleVR mode. Note: it also reacts while you type in other programs.</summary>
        public static bool GlobalKeysEnabled;
        /// <summary>Last key that reached the prototype, for the status board and log.</summary>
        public static string LastKey = "none";

        public static bool EmergencyStopPressed() { return Hit(InputShim.EmergencyStopPressed(), VK_SPACE, "Space (stop)"); }
        public static bool ClearStopPressed() { return Hit(InputShim.ClearStopPressed(), VK_C, "C (clear stop)"); }
        public static bool TogglePanelPressed() { return Hit(InputShim.TogglePanelPressed(), VK_F1, "F1 (panel)"); }
        public static bool TouchPressed() { return Hit(InputShim.TouchPressed(), VK_T, "T (touch)"); }
        public static bool HugPressed() { return Hit(InputShim.HugPressed(), VK_H, "H (hug)"); }
        public static bool HideEntityPressed() { return Hit(InputShim.HideEntityPressed(), VK_V, "V (show/hide)"); }
        public static bool ObserverPressed() { return Hit(InputShim.ObserverPressed(), VK_O, "O (view)"); }

        public static int ConditionKeyPressed()
        {
            int c = InputShim.ConditionKeyPressed();
            if (c < 0 && Global(VK_1)) c = 0;
            if (c < 0 && Global(VK_2)) c = 1;
            if (c < 0 && Global(VK_3)) c = 2;
            if (c >= 0) LastKey = (c + 1) + " (condition)";
            return c;
        }

        static bool Hit(bool unityKey, int vk, string name)
        {
            bool hit = Global(vk) | unityKey; // evaluate Global every frame so its edge tracking stays correct
            if (hit) LastKey = name;
            return hit;
        }

        // ---- Windows keyboard state, rising edge per frame ----
        static readonly bool[] now = new bool[256], before = new bool[256];
        static readonly int[] watched = { VK_SPACE, VK_F1, VK_1, VK_2, VK_3, VK_C, VK_H, VK_O, VK_T, VK_V };
        static int polledFrame = -1;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
        static bool IsDown(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }
        public static bool GlobalKeysSupported { get { return true; } }
#else
        static bool IsDown(int vk) { return false; }
        public static bool GlobalKeysSupported { get { return false; } }
#endif

        static bool Global(int vk)
        {
            if (!GlobalKeysEnabled) return false;
            if (polledFrame != Time.frameCount)
            {
                polledFrame = Time.frameCount;
                foreach (int k in watched) { before[k] = now[k]; now[k] = IsDown(k); }
            }
            return now[vk] && !before[vk];
        }
    }
}
