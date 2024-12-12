// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/Unlit/VideoTexture"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;

				o.vertex = UnityObjectToClipPos(v.vertex);

                // rotating UV
                const float Deg2Rad = (UNITY_PI * 2.0) / 360.0;
				const float Rotation = 90.0;

                float rotationRadians = Rotation * Deg2Rad; // convert degrees to radians
                float s = sin(rotationRadians); // sin and cos take radians, not degrees
                float c = cos(rotationRadians);

                float2x2 rotationMatrix = float2x2( c, -s, s, c); // construct simple rotation matrix
                v.uv -= 0.5; // offset UV so we rotate around 0.5 and not 0.0
                v.uv = mul(rotationMatrix, v.uv); // apply rotation matrix
                v.uv += 0.5; // offset UV again so UVs are in the correct location

                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}


// Shader "Example/URPUnlitShaderTexture"
// {
//     // The _BaseMap variable is visible in the Material's Inspector, as a field
//     // called Base Map.
//     Properties
//     {
//         _BaseMap("Base Map", 2D) = "white"
//     }

//     SubShader
//     {
//         Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalRenderPipeline" }

//         Pass
//         {
//             HLSLPROGRAM
//             #pragma vertex vert
//             #pragma fragment frag

//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

//             struct Attributes
//             {
//                 float4 positionOS   : POSITION;
//                 // The uv variable contains the UV coordinate on the texture for the
//                 // given vertex.
//                 float2 uv           : TEXCOORD0;
//             };

//             struct Varyings
//             {
//                 float4 positionHCS  : SV_POSITION;
//                 // The uv variable contains the UV coordinate on the texture for the
//                 // given vertex.
//                 float2 uv           : TEXCOORD0;
//             };

//             // This macro declares _BaseMap as a Texture2D object.
//             TEXTURE2D(_BaseMap);
//             // This macro declares the sampler for the _BaseMap texture.
//             SAMPLER(sampler_BaseMap);

//             CBUFFER_START(UnityPerMaterial)
//                 // The following line declares the _BaseMap_ST variable, so that you
//                 // can use the _BaseMap variable in the fragment shader. The _ST
//                 // suffix is necessary for the tiling and offset function to work.
//                 float4 _BaseMap_ST;
//             CBUFFER_END

//             Varyings vert(Attributes IN)
//             {
//                 Varyings OUT;
//                 OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
//                 // The TRANSFORM_TEX macro performs the tiling and offset
//                 // transformation.
//                 OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
//                 return OUT;
//             }

//             half4 frag(Varyings IN) : SV_Target
//             {
//                 // The SAMPLE_TEXTURE2D marco samples the texture with the given
//                 // sampler.
//                 half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
//                 return color;
//             }
//             ENDHLSL
//         }
//     }
// }