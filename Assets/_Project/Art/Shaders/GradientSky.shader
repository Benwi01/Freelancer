// Stylized sky: a three-colour vertical gradient with a soft sun disc and glow.
// Sun direction is passed in as a property so it doesn't depend on render-pipeline light variables.
Shader "Freelancer/GradientSky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.33, 0.56, 0.9, 1)
        _HorizonColor ("Horizon", Color) = (0.98, 0.84, 0.7, 1)
        _BottomColor ("Below horizon", Color) = (0.62, 0.56, 0.52, 1)
        _Exponent ("Gradient sharpness", Range(0.2, 4)) = 1.3
        _SunDir ("Direction to sun", Vector) = (0.4, 0.6, -0.6, 0)
        _SunColor ("Sun", Color) = (1, 0.93, 0.78, 1)
        _SunSize ("Sun size", Range(0.0005, 0.05)) = 0.006
        _SunGlow ("Sun glow", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TopColor, _HorizonColor, _BottomColor, _SunColor;
            float4 _SunDir;
            float _Exponent, _SunSize, _SunGlow;

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float up = d.y;
                float3 col;
                if (up >= 0)
                    col = lerp(_HorizonColor.rgb, _TopColor.rgb, pow(saturate(up), 1.0 / _Exponent));
                else
                    col = lerp(_HorizonColor.rgb, _BottomColor.rgb, saturate(-up * 4.0));

                float3 sun = normalize(_SunDir.xyz);
                float cosAngle = dot(d, sun);
                float disc = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.6, cosAngle);
                float glow = pow(saturate(cosAngle), 64.0) * _SunGlow;
                col += _SunColor.rgb * (disc + glow);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
