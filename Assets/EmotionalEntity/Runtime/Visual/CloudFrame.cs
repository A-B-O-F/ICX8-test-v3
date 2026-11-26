using UnityEngine;

namespace EmotionalEntity
{
    /// <summary>Every VisualProfile value as plain data, so it can be sent to other cluster nodes.</summary>
    public struct VisualParams
    {
        public float Radius, Cohesion, VerticalStretch, Droop;
        public float Turbulence, FlowFrequency, FlowSpeed, PulseRate, PulseDepth, PulseScatter;
        public int DetailCount, VolumeCount;
        public float DetailSize, DetailOpacity, VolumeSize, VolumeOpacity, Additive, Brightness;
        public Color ColorA, ColorB;
        public float StreamFraction, StreamSpeed, StreamTipRadius, HugRadius, HugHeight, HugFrontClear, HeadClearance;

        public static VisualParams From(VisualProfile v)
        {
            return new VisualParams
            {
                Radius = v.radius, Cohesion = v.cohesion, VerticalStretch = v.verticalStretch, Droop = v.droop,
                Turbulence = v.turbulence, FlowFrequency = v.flowFrequency, FlowSpeed = v.flowSpeed,
                PulseRate = v.pulseRate, PulseDepth = v.pulseDepth, PulseScatter = v.pulseScatter,
                DetailCount = v.detailCount, VolumeCount = v.volumeCount,
                DetailSize = v.detailSize, DetailOpacity = v.detailOpacity, VolumeSize = v.volumeSize,
                VolumeOpacity = v.volumeOpacity, Additive = v.additive, Brightness = v.brightness,
                ColorA = v.colorA, ColorB = v.colorB,
                StreamFraction = v.streamFraction, StreamSpeed = v.streamSpeed, StreamTipRadius = v.streamTipRadius,
                HugRadius = v.hugRadius, HugHeight = v.hugHeight, HugFrontClear = v.hugFrontClear, HeadClearance = v.headClearance
            };
        }

        public void WriteTo(VisualProfile v)
        {
            v.radius = Radius; v.cohesion = Cohesion; v.verticalStretch = VerticalStretch; v.droop = Droop;
            v.turbulence = Turbulence; v.flowFrequency = FlowFrequency; v.flowSpeed = FlowSpeed;
            v.pulseRate = PulseRate; v.pulseDepth = PulseDepth; v.pulseScatter = PulseScatter;
            v.detailCount = DetailCount; v.volumeCount = VolumeCount;
            v.detailSize = DetailSize; v.detailOpacity = DetailOpacity; v.volumeSize = VolumeSize;
            v.volumeOpacity = VolumeOpacity; v.additive = Additive; v.brightness = Brightness;
            v.colorA = ColorA; v.colorB = ColorB;
            v.streamFraction = StreamFraction; v.streamSpeed = StreamSpeed; v.streamTipRadius = StreamTipRadius;
            v.hugRadius = HugRadius; v.hugHeight = HugHeight; v.hugFrontClear = HugFrontClear; v.headClearance = HeadClearance;
        }
    }

    /// <summary>One rendered frame of the entity: what it looks like and where everything is.
    /// Kept as plain values so behaviour and rendering stay separate (and could be sent over a network later).</summary>
    public struct CloudFrame
    {
        public EntityVisualState State;
        public VisualParams Visual;
        public int Visible;
    }

    /// <summary>Draws CloudFrames.</summary>
    public class FramePresenter
    {
        readonly IEntityVisual visual;
        readonly VisualProfile scratch = ScriptableObject.CreateInstance<VisualProfile>();
        int lastVisible = -1;
        public int FramesPresented { get; private set; }
        public CloudFrame Last { get; private set; }

        public FramePresenter(IEntityVisual entityVisual) { visual = entityVisual; }

        public void Present(CloudFrame f)
        {
            if (f.Visible != lastVisible) { visual.SetVisible(f.Visible != 0); lastVisible = f.Visible; }
            f.Visual.WriteTo(scratch);
            visual.Apply(scratch, f.State);
            Last = f;
            FramesPresented++;
        }
    }
}
