Shader "EmotionalEntity/ProceduralCloud"
{
    Properties { _VolumeLayer ("Volume layer", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "Cloud"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _VolumeLayer, _EntityTime, _Radius, _Cohesion, _Stretch, _Droop, _Turbulence, _FlowFreq, _FlowSpeed;
            float _PulseRate, _PulseDepth, _PulseScatter, _Size, _Opacity, _Additive, _Brightness;
            float _TouchWeight, _ContactFlash, _StreamFraction, _StreamSpeed, _StreamTip;
            float _HugWeight, _HugRadius, _HugHeight, _HugClear, _HeadClear, _HandForce;
            float4 _Center, _ContactPoint, _HugCenter, _HugRight, _HugUp, _HugForward, _HeadPos, _LeftHand, _RightHand;
            float4 _ColorA, _ColorB;

            struct Attributes
            {
                float3 seed : POSITION;
                float2 corner : TEXCOORD0;
                float4 rnd : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 corner : TEXCOORD0;
                float4 data : TEXCOORD1; // x colour mix, y glow, z fade, w random
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 Flow(float3 q)
            {
                float3 a = sin(q * 1.3 + sin(q.yzx * 1.7 + 1.1));
                float3 b = sin(q.zxy * 2.9 + a * 1.5 + 2.3) * 0.5;
                float3 c = sin(q.yzx * 5.3 + b * 2.0 + 4.1) * 0.25;
                return (a + b + c) / 1.75;
            }

            float3 Repel(float3 p, float3 from, float force)
            {
                float3 v = p - from;
                float d = max(length(v), 1e-3);
                return p + v / d * force * exp(-d * d / 0.045);
            }

            float3 ParticlePosition(float3 seed, float4 rnd, out float mixT, out float glow)
            {
                float t = _EntityTime;
                float l = max(length(seed), 1e-4);
                float3 dir = seed / l;
                float shell = pow(l, lerp(0.55, 1.7, _Cohesion));
                float pulse = 1.0 + _PulseDepth * sin(6.28318 * _PulseRate * t + rnd.x * _PulseScatter);
                float3 local = dir * shell * _Radius * pulse;
                local.y *= _Stretch;
                float3 q = local / _Radius * _FlowFreq + float3(0.0, t * _FlowSpeed, t * _FlowSpeed * 0.7) + rnd.w * 0.5;
                float3 cloud = _Center.xyz + local + Flow(q) * _Turbulence * _Radius * (0.35 + 0.65 * shell);
                cloud.y -= _Droop * _Radius * shell * shell;
                float3 pos = cloud;

                // Touch: a share of particles forms a stream that runs to the contact point.
                float stream = step(rnd.x, _StreamFraction) * _TouchWeight;
                if (stream > 0.0)
                {
                    float u = frac(rnd.y + t * _StreamSpeed * (0.7 + 0.6 * rnd.z));
                    float3 a = _Center.xyz, c = _ContactPoint.xyz;
                    float3 m = (a + c) * 0.5 + float3(0.0, 0.12, 0.0);
                    float3 s = lerp(lerp(a, m, u), lerp(m, c, u), u);
                    float tube = lerp(_Radius * 0.35, _StreamTip, u);
                    s += normalize(Flow(seed * 3.0 + t * 0.7) + dir * 0.5 + 1e-3) * tube * sqrt(l);
                    pos = lerp(pos, s, stream);
                }

                // Hug: particles travel in waves to an open ring around the torso. The gap in front keeps
                // particles out of the chest area, which a projection CAVE cannot show correctly.
                float h = saturate(_HugWeight * 1.6 - rnd.y * 0.6);
                h = h * h * (3.0 - 2.0 * h);
                if (h > 0.0)
                {
                    float angle = _HugClear + (6.28318 - 2.0 * _HugClear) * rnd.x;
                    float r = _HugRadius * (1.0 + 0.1 * sin(6.28318 * 0.25 * t + rnd.z * 6.28318)) + (rnd.w - 0.5) * 0.14;
                    float3 ring = _HugCenter.xyz + _HugRight.xyz * sin(angle) * r + _HugForward.xyz * cos(angle) * r
                                + _HugUp.xyz * (rnd.z - 0.5) * _HugHeight;
                    ring += Flow(seed * _FlowFreq + t * _FlowSpeed) * (0.03 + _Turbulence * 0.1);
                    pos = lerp(pos, ring, h);
                }

                if (_HandForce != 0.0)
                {
                    pos = Repel(pos, _LeftHand.xyz, _HandForce);
                    pos = Repel(pos, _RightHand.xyz, _HandForce);
                }
                float3 fromHead = pos - _HeadPos.xyz;
                float hd = max(length(fromHead), 1e-3);
                if (hd < _HeadClear) pos = _HeadPos.xyz + fromHead / hd * _HeadClear;

                float cd = length(pos - _ContactPoint.xyz);
                glow = _ContactFlash * exp(-cd * cd / 0.02);
                mixT = saturate(shell * 0.75 + rnd.z * 0.35 + glow);
                return pos;
            }

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float mixT, glow;
                float3 p = ParticlePosition(v.seed, v.rnd, mixT, glow);

                // Face the eye, not the wall, so a particle looks the same across a CAVE seam.
                float3 eye = GetCameraPositionWS();
                float3 n = normalize(eye - p);
                float3 r = normalize(cross(abs(n.y) > 0.98 ? float3(1.0, 0.0, 0.0) : float3(0.0, 1.0, 0.0), n));
                float3 u = cross(n, r);
                float size = _Size * (_VolumeLayer > 0.5 ? (0.6 + 0.8 * v.rnd.z) : (0.45 + 1.1 * v.rnd.z * v.rnd.z));
                float3 world = p + (r * v.corner.x + u * v.corner.y) * size;

                float eyeDist = length(p - eye);
                o.positionCS = TransformWorldToHClip(world);
                o.corner = v.corner;
                o.data = float4(mixT, glow, smoothstep(0.12, 0.4, eyeDist), v.rnd.w);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float d2 = dot(i.corner, i.corner);
                if (d2 > 1.0) discard;
                float a = exp(-d2 * 3.5) * (1.0 - d2);
                if (_VolumeLayer > 0.5)
                {
                    float2 q = i.corner * 2.2 + i.data.w * 17.0;
                    float n = sin(q.x + sin(q.y * 1.7 + _EntityTime * 0.2)) * sin(q.y * 1.3 + sin(q.x * 2.1));
                    a *= 0.65 + 0.35 * n;
                }
                a *= _Opacity * i.data.z;
                float3 col = lerp(_ColorA.rgb, _ColorB.rgb, i.data.x);
                col = col * _Brightness + i.data.y * (col + 0.6) * 2.0;
                return float4(col * a, a * (1.0 - _Additive));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
