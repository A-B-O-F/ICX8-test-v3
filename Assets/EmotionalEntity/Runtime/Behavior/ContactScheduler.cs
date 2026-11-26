using System;
using UnityEngine;

namespace EmotionalEntity
{
    /// <summary>Deterministic contact timetable. Same seed and schedule give the same plan.</summary>
    public class ContactScheduler
    {
        readonly ContactSchedule s;
        readonly System.Random rng;
        float escalated;
        int burstStep;

        public bool Running { get; private set; }
        public int Issued { get; private set; }
        public double NextTime { get; private set; }

        public ContactScheduler(ContactSchedule schedule, int seed)
        {
            s = schedule;
            rng = new System.Random(seed);
            escalated = schedule.interval;
        }

        public void Start(double now)
        {
            if (Running) return;
            Running = true;
            NextTime = now + s.firstDelay;
        }

        public void Stop() { Running = false; }

        public bool Exhausted { get { return s.maxContacts > 0 && Issued >= s.maxContacts; } }
        public bool Due(double now) { return Running && !Exhausted && now >= NextTime; }

        /// <summary>Marks the current contact as issued and returns its planned time.</summary>
        public double Consume(double now)
        {
            double planned = NextTime;
            Issued++;
            NextTime = Math.Max(now, planned) + NextInterval();
            return planned;
        }

        /// <summary>Pull the next contact forward (used by the Flare reaction).</summary>
        public void Hasten(double now, float within)
        {
            if (Running && NextTime > now + within) NextTime = now + within;
        }

        float NextInterval()
        {
            float d;
            switch (s.pattern)
            {
                case SchedulePattern.Burst:
                    burstStep++;
                    if (burstStep < s.burstCount) d = s.burstInterval;
                    else { burstStep = 0; d = s.interval; }
                    break;
                case SchedulePattern.Escalating:
                    d = escalated;
                    escalated = Mathf.Max(s.minInterval, escalated * (1f - s.escalationRate));
                    break;
                default:
                    d = s.interval;
                    break;
            }
            float j = s.jitter * ((float)rng.NextDouble() * 2f - 1f);
            return Mathf.Max(0.05f, d * (1f + j));
        }

        public int PickIndex(int count) { return count <= 1 ? 0 : rng.Next(count); }
    }
}
