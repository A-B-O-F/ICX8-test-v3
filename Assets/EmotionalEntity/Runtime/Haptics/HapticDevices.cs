using System;
using System.Reflection;
using UnityEngine;

namespace EmotionalEntity
{
    /// <summary>What the prototype needs from a haptic device. Two ways to drive it:
    /// Send = real-time level per motor (version 1); PlayEvent = a pattern designed in the bHaptics Portal (version 2).</summary>
    public interface IHapticDevice
    {
        string Name { get; }
        /// <summary>The driver is usable. The SDK cannot confirm that the motors actually moved.</summary>
        bool Available { get; }
        /// <summary>One line for the operator panel.</summary>
        string Status { get; }
        /// <summary>Version 1. intensities are device output levels 0-1, one per motor.</summary>
        void Send(float[] intensities, int durationMs);
        /// <summary>Version 2. Returns false if the event could not be started.</summary>
        bool PlayEvent(string eventId, float intensity, bool loop, int loopIntervalMs);
        void StopEvent(string eventId);
        void StopAll();
        void Dispose();
    }

    /// <summary>No hardware. The operator panel draws the last motor output as a body map.</summary>
    public class SimulatedHapticDevice : IHapticDevice
    {
        public string Name { get { return "Simulated"; } }
        public bool Available { get { return true; } }
        public string Status { get { return PlayingEvent != null ? "Simulated, event \"" + PlayingEvent + "\" playing" : "Simulated, no hardware"; } }
        public int Sends { get; private set; }
        public int EventStarts { get; private set; }
        public string PlayingEvent { get; private set; }
        public void Send(float[] intensities, int durationMs) { Sends++; }
        public bool PlayEvent(string eventId, float intensity, bool loop, int loopIntervalMs) { EventStarts++; PlayingEvent = eventId; return true; }
        public void StopEvent(string eventId) { if (PlayingEvent == eventId) PlayingEvent = null; }
        public void StopAll() { PlayingEvent = null; }
        public void Dispose() { }
    }

    /// <summary>bHaptics SDK2 (the "bHaptics Haptic Plugin"), found by reflection so this project compiles
    /// before the SDK is imported. Calls Bhaptics.SDK2.BhapticsLibrary:
    ///   PlayMotors(0 = vest, int[] 0-100 per motor, ms)   version 1
    ///   PlayLoop / PlayParam(eventId, intensity, ...)      version 2
    ///   StopByEventId(eventId), StopAll()
    /// Needs: bHaptics Player running on this computer with the vest paired, the app linked in
    /// bHaptics > Developer Window (App ID + API Key), and the SDK's [bhaptics] prefab in the scene.</summary>
    public class BhapticsHapticDevice : IHapticDevice
    {
        const int VestPosition = 0;
        const int MinDurationMs = 100; // SDK guidance: 100 ms or more
        readonly MethodInfo playMotors, stopAll, playLoop, playParam, stopById, available;
        int[] buffer;
        int failures;
        string lastError = "";
        float nextCheck;
        bool playerOk = true;

        public string Name { get { return "bHaptics vest"; } }
        public bool Available { get { return playMotors != null && playerOk && failures == 0; } }

        public string Status
        {
            get
            {
                if (playMotors == null) return "bHaptics SDK2 not imported";
                if (!playerOk) return "bHaptics Player not reachable. Start it and pair the vest.";
                if (failures > 0) return "Call failed x" + failures + ": " + lastError;
                return "SDK ready. Motors > Walk all motors confirms the vest.";
            }
        }

        public BhapticsHapticDevice()
        {
            Type t = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType("Bhaptics.SDK2.BhapticsLibrary");
                if (t != null) break;
            }
            if (t == null)
            {
                Debug.LogWarning("[Haptics] bHaptics SDK2 not found. Import the bHaptics Haptic Plugin, or set Haptics to Simulated.");
                return;
            }
            playMotors = t.GetMethod("PlayMotors", new[] { typeof(int), typeof(int[]), typeof(int) });
            stopAll = t.GetMethod("StopAll", Type.EmptyTypes);
            playLoop = FindByFirstParam(t, "PlayLoop", typeof(string));
            playParam = FindByFirstParam(t, "PlayParam", typeof(string));
            stopById = FindByFirstParam(t, "StopByEventId", typeof(string));
            // Present in recent SDK2 versions; used only for the status line.
            available = t.GetMethod("IsBhapticsAvailable", new[] { typeof(bool) });
        }

        static MethodInfo FindByFirstParam(Type t, string name, Type first)
        {
            foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (m.Name != name) continue;
                ParameterInfo[] p = m.GetParameters();
                if (p.Length > 0 && p[0].ParameterType == first) return m;
            }
            return null;
        }

        /// <summary>Calls with the leading arguments given and the SDK's own defaults for the rest,
        /// so small signature differences between SDK versions do not break the call.</summary>
        static object Call(MethodInfo m, params object[] leading)
        {
            ParameterInfo[] p = m.GetParameters();
            var args = new object[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                Type pt = p[i].ParameterType;
                if (i < leading.Length) args[i] = Convert.ChangeType(leading[i], pt);
                else if (p[i].HasDefaultValue) args[i] = p[i].DefaultValue;
                else args[i] = pt.IsValueType ? Activator.CreateInstance(pt) : null;
            }
            return m.Invoke(null, args);
        }

        public void Send(float[] v, int durationMs)
        {
            if (playMotors == null) return;
            if (buffer == null || buffer.Length != v.Length) buffer = new int[v.Length];
            for (int i = 0; i < v.Length; i++) buffer[i] = Mathf.RoundToInt(Mathf.Clamp01(v[i]) * 100f);
            CheckPlayer();
            try { playMotors.Invoke(null, new object[] { VestPosition, buffer, Mathf.Max(MinDurationMs, durationMs) }); }
            catch (Exception e) { Fail(e); }
        }

        public bool PlayEvent(string eventId, float intensity, bool loop, int loopIntervalMs)
        {
            MethodInfo m = loop ? playLoop : playParam;
            if (m == null || string.IsNullOrEmpty(eventId)) return false;
            CheckPlayer();
            try
            {
                // PlayLoop(eventId, intensity, duration, angleX, offsetY, interval, maxCount)
                // PlayParam(eventId, intensity, duration, angleX, offsetY, requestId)
                object r = loop ? Call(m, eventId, intensity, 1f, 0f, 0f, loopIntervalMs) : Call(m, eventId, intensity);
                return !(r is int) || (int)r != -1;
            }
            catch (Exception e) { Fail(e); return false; }
        }

        public void StopEvent(string eventId)
        {
            if (stopById == null || string.IsNullOrEmpty(eventId)) return;
            try { Call(stopById, eventId); } catch (Exception e) { Fail(e); }
        }

        void CheckPlayer()
        {
            if (available == null || Time.realtimeSinceStartup < nextCheck) return;
            nextCheck = Time.realtimeSinceStartup + 2f;
            try { playerOk = (bool)available.Invoke(null, new object[] { false }); }
            catch (Exception) { playerOk = true; } // cannot tell; do not block output
        }

        void Fail(Exception e)
        {
            failures++;
            lastError = (e.InnerException ?? e).Message;
            if (failures == 1) Debug.LogError("[Haptics] bHaptics call failed: " + lastError +
                ". Is the [bhaptics] prefab in the scene and the app linked in bHaptics > Developer Window?");
        }

        public void StopAll()
        {
            if (stopAll == null) return;
            try { stopAll.Invoke(null, null); } catch (Exception e) { Fail(e); }
        }

        public void Dispose() { StopAll(); }
    }
}
