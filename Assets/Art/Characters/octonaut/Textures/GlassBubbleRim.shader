Shader "Custom/GlassBubbleRim"
{
    Properties
    {
        _OutlineColor   ("Outline Color",    Color)  = (1, 1, 1, 1)
        _OutlinePower   ("Outline Power",    Float)  = 2.0
        _OutlineStrength("Outline Strength", Float)  = 1.0
        _Cutoff         ("Alpha Cutoff",     Float)  = 0.15
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "RenderType"        = "Transparent"
            "IgnoreProjector"   = "True"
        }

        Pass
        {
            Name "RIM_OUTLINE"
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlinePower;
                float  _OutlineStrength;
                float  _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalVS    : TEXCOORD0;   // normale en view space
                float3 viewDirVS   : TEXCOORD1;   // direction vue en view space
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // Position en clip space
                float4 posWS = mul(UNITY_MATRIX_M, IN.positionOS);
                OUT.positionHCS = mul(UNITY_MATRIX_VP, posWS);

                // Normale en view space (= normalMatrix * normal en GLSL)
                OUT.normalVS  = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, IN.normalOS));

                // View direction en view space (= -mvPosition.xyz normalisé)
                float4 mvPos  = mul(UNITY_MATRIX_MV, IN.positionOS);
                OUT.viewDirVS = normalize(-mvPos.xyz);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalVS);
                float3 V = normalize(IN.viewDirVS);

                // Rim = 1 sur les bords, 0 au centre — identique au Three.js
                float rim = 1.0 - max(dot(N, V), 0.0);
                rim = pow(rim, _OutlinePower);

                float alpha = rim * _OutlineStrength;

                // Coupe les zones quasi transparentes (évite le voile gris)
                clip(alpha - _Cutoff);

                return half4(_OutlineColor.rgb, alpha * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}