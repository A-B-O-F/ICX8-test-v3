using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace EmotionalEntity
{
    public interface IEventSink
    {
        void Touch(TouchEvent e, float peakOutput);
        void Mark(double time, string kind, string detail);
        void Dispose();
    }

    /// <summary>One CSV per session. An LSL marker sink can be added with the same interface.</summary>
    public class CsvEventSink : IEventSink
    {
        readonly StreamWriter w;
        public string Path { get; private set; }

        public CsvEventSink(string participant)
        {
            string dir = System.IO.Path.Combine(Application.persistentDataPath, "Sessions");
            Directory.CreateDirectory(dir);
            Path = System.IO.Path.Combine(dir, participant + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv");
            w = new StreamWriter(Path);
            w.WriteLine("time,kind,condition,contact,gesture,phase,zone,x,y,z,intensity,peak_output,planned_time,timing_error,detail");
        }

        static string N(double v) { return v.ToString("0.####", CultureInfo.InvariantCulture); }

        public void Touch(TouchEvent e, float peak)
        {
            w.WriteLine(string.Join(",", new[] {
                N(e.ActualTime), "touch", e.Condition, e.ContactIndex.ToString(CultureInfo.InvariantCulture),
                e.Gesture.ToString(), e.Phase.ToString(), e.Zone.ToString(),
                N(e.WorldPosition.x), N(e.WorldPosition.y), N(e.WorldPosition.z),
                N(e.CommandedIntensity), N(peak), N(e.PlannedTime), N(e.ActualTime - e.PlannedTime), e.Note ?? "" }));
            w.Flush();
        }

        public void Mark(double time, string kind, string detail)
        {
            w.WriteLine(N(time) + "," + kind + ",,,,,,,,,,,,," + (detail ?? "").Replace(',', ';'));
            w.Flush();
        }

        public void Dispose() { w.Dispose(); }
    }
}
