using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EmotionalEntity
{
    /// <summary>Default renderer. Each particle is a quad whose position is computed in the vertex shader
    /// from its fixed random seed and the synchronised state, with no per-frame simulation. So every wall,
    /// eye and future cluster node draws the identical cloud, and no compute shader or VFX asset is required.</summary>
    public class ProceduralCloudVisual : IEntityVisual
    {
        readonly GameObject root;
        readonly Layer detail, volume;
        readonly Shader shader;
        readonly int seed;

        class Layer
        {
            public GameObject Go; public MeshRenderer Renderer; public MeshFilter Filter; public Material Mat; public int Count = -1;
        }

        public ProceduralCloudVisual(Transform parent, int randomSeed)
        {
            seed = randomSeed;
            shader = Shader.Find("EmotionalEntity/ProceduralCloud");
            if (shader == null) Debug.LogError("[Cloud] Shader not found. It must be in Resources/EmotionalEntity and the project must use URP.");
            root = new GameObject("ParticleEntity");
            root.transform.SetParent(parent, false);
            detail = MakeLayer("Detail", 0f);
            volume = MakeLayer("Volume", 1f);
        }

        Layer MakeLayer(string name, float flag)
        {
            var l = new Layer { Go = new GameObject(name) };
            l.Go.transform.SetParent(root.transform, false);
            l.Filter = l.Go.AddComponent<MeshFilter>();
            l.Renderer = l.Go.AddComponent<MeshRenderer>();
            l.Renderer.shadowCastingMode = ShadowCastingMode.Off;
            l.Renderer.receiveShadows = false;
            l.Mat = new Material(shader);
            l.Mat.SetFloat("_VolumeLayer", flag);
            l.Renderer.sharedMaterial = l.Mat;
            return l;
        }

        public void SetVisible(bool visible) { root.SetActive(visible); }

        public void Apply(VisualProfile v, EntityVisualState s)
        {
            Rebuild(detail, v.detailCount, seed);
            Rebuild(volume, v.volumeCount, seed + 7919);
            Set(detail.Mat, v, s, v.detailSize, v.detailOpacity);
            Set(volume.Mat, v, s, v.volumeSize, v.volumeOpacity);
        }

        void Rebuild(Layer l, int count, int s)
        {
            if (l.Count == count) return;
            l.Count = count;
            if (l.Filter.sharedMesh != null) Object.Destroy(l.Filter.sharedMesh);
            l.Filter.sharedMesh = count > 0 ? BuildMesh(count, s) : null;
        }

        static void Set(Material m, VisualProfile v, EntityVisualState s, float size, float opacity)
        {
            // Wrap time so float precision stays good for long sessions; 3600 s is well past one trial.
            m.SetFloat("_EntityTime", (float)(s.Time % 3600.0));
            m.SetVector("_Center", s.Center);
            m.SetFloat("_Radius", v.radius);
            m.SetFloat("_Cohesion", v.cohesion);
            m.SetFloat("_Stretch", v.verticalStretch);
            m.SetFloat("_Droop", v.droop);
            m.SetFloat("_Turbulence", Mathf.Clamp01(v.turbulence + s.Agitation * 0.5f));
            m.SetFloat("_FlowFreq", v.flowFrequency);
            m.SetFloat("_FlowSpeed", v.flowSpeed);
            m.SetFloat("_PulseRate", v.pulseRate);
            m.SetFloat("_PulseDepth", v.pulseDepth);
            m.SetFloat("_PulseScatter", v.pulseScatter);
            m.SetFloat("_Size", size);
            m.SetFloat("_Opacity", opacity);
            m.SetFloat("_Additive", v.additive);
            m.SetFloat("_Brightness", v.brightness * (1f + s.Agitation * 0.6f));
            m.SetColor("_ColorA", v.colorA);
            m.SetColor("_ColorB", v.colorB);
            m.SetFloat("_TouchWeight", s.TouchWeight);
            m.SetVector("_ContactPoint", s.ContactPoint);
            m.SetFloat("_ContactFlash", s.ContactFlash);
            m.SetFloat("_StreamFraction", v.streamFraction);
            m.SetFloat("_StreamSpeed", v.streamSpeed);
            m.SetFloat("_StreamTip", v.streamTipRadius);
            m.SetFloat("_HugWeight", s.HugWeight);
            m.SetVector("_HugCenter", s.HugCenter);
            m.SetVector("_HugRight", s.HugRight);
            m.SetVector("_HugUp", s.HugUp);
            m.SetVector("_HugForward", s.HugForward);
            m.SetFloat("_HugRadius", v.hugRadius);
            m.SetFloat("_HugHeight", v.hugHeight);
            m.SetFloat("_HugClear", v.hugFrontClear * Mathf.Deg2Rad);
            m.SetVector("_HeadPos", s.HeadPosition);
            m.SetFloat("_HeadClear", v.headClearance);
            m.SetVector("_LeftHand", s.LeftHand);
            m.SetVector("_RightHand", s.RightHand);
            m.SetFloat("_HandForce", s.HandForce);
        }

        static Mesh BuildMesh(int count, int seed)
        {
            var rng = new System.Random(seed);
            var pos = new List<Vector3>(count * 4);
            var uv0 = new List<Vector2>(count * 4);
            var uv1 = new List<Vector4>(count * 4);
            var idx = new List<int>(count * 6);
            for (int i = 0; i < count; i++)
            {
                // Uniform point in the unit ball (position attribute holds the seed, not a location).
                Vector3 p;
                do { p = new Vector3(R(rng) * 2f - 1f, R(rng) * 2f - 1f, R(rng) * 2f - 1f); } while (p.sqrMagnitude > 1f);
                var rnd = new Vector4(R(rng), R(rng), R(rng), R(rng));
                int b = pos.Count;
                for (int k = 0; k < 4; k++) { pos.Add(p); uv1.Add(rnd); }
                uv0.Add(new Vector2(-1f, -1f)); uv0.Add(new Vector2(1f, -1f)); uv0.Add(new Vector2(1f, 1f)); uv0.Add(new Vector2(-1f, 1f));
                idx.Add(b); idx.Add(b + 1); idx.Add(b + 2); idx.Add(b); idx.Add(b + 2); idx.Add(b + 3);
            }
            var mesh = new Mesh { name = "CloudParticles", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(pos);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetTriangles(idx, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 200f); // positions come from the shader
            mesh.UploadMeshData(true);
            return mesh;
        }

        static float R(System.Random r) { return (float)r.NextDouble(); }

        public void Dispose()
        {
            if (root != null) Object.Destroy(root);
        }
    }
}
