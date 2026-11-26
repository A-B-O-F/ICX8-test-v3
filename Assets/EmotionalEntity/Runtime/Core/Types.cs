using System;
using UnityEngine;

namespace EmotionalEntity
{
    public enum EntityState { Idle, Approach, Contact, Hold, Release, Retreat }
    public enum GestureType { Touch, RepeatedTouch, Hug }
    public enum BodyZone { Head, Chest, Back, LeftShoulder, RightShoulder, LeftSide, RightSide, LeftHand, RightHand }
    public enum TrackedPoint { Head, Torso, LeftHand, RightHand }
    public enum ContactPhase { Start, Hold, End, Broken, Missed }
    public enum TriggerMode { Scheduled, TerritoryEntry, Proximity }
    public enum Trajectory { Direct, Arc, Orbit, Hesitant }
    public enum HandReaction { None, Yield, Lean, Flare }
    public enum SchedulePattern { Regular, Burst, Escalating }
    public enum Granularity { Full32 = 32, Groups16 = 16, Groups8 = 8, Groups4 = 4 }
    /// <summary>RealtimeMotors = version 1, the code sets every motor each frame from the contact.
    /// PresetEvent = version 2, a pattern designed in the bHaptics Portal starts and stops on a trigger.</summary>
    public enum HapticMode { RealtimeMotors, PresetEvent }
    /// <summary>Proximity: while the cloud is near the chest. DuringContact: while the entity touches or hugs.</summary>
    public enum PresetTrigger { Proximity, DuringContact }

    /// <summary>The single public contract between behaviour, visuals, haptics and logging.</summary>
    [Serializable]
    public struct TouchEvent
    {
        public string Condition;
        public int ContactIndex;
        public GestureType Gesture;
        public ContactPhase Phase;
        public BodyZone Zone;
        public Vector3 WorldPosition;
        public float CommandedIntensity;
        public double PlannedTime;
        public double ActualTime;
        public string Note;
    }

    /// <summary>Everything a renderer needs. It is a pure function of this struct, so any
    /// output (one PC, cluster node, HMD) that receives the same state draws the same cloud.</summary>
    public struct EntityVisualState
    {
        public double Time;
        public Vector3 Center;
        public float TouchWeight, HugWeight, ContactFlash, Agitation;
        public Vector3 ContactPoint;
        public Vector3 HugCenter, HugRight, HugUp, HugForward;
        public Vector3 HeadPosition, LeftHand, RightHand;
        public float HandForce;
    }
}
