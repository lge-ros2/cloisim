Shader "FullScreen/DepthCapture"
{
    Properties
    {
        [Toggle] _ReverseData ("reverse depth data", int) = 0
        [Toggle] _FlipX ("horizontal flip", int) = 1
    }

    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 vulkan metal switch

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

    int _ReverseData;
    int _FlipX;

    struct Attributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 texcoord   : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
        output.texcoord   = GetFullScreenTriangleTexCoord(input.vertexID);
        return output;
    }

    float4 Frag(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        // Compute pixel coords, optionally flipping X
        uint2 pixelCoords = uint2(input.positionCS.xy);
        if (_FlipX > 0)
        {
            // _ScreenSize.x contains render target width
            pixelCoords.x = (uint)_ScreenSize.x - 1 - pixelCoords.x;
        }

        // LoadCameraDepth handles Tex2DArray correctly via LOAD_TEXTURE2D_X
        float rawDepth = LoadCameraDepth(pixelCoords);
        float linearDepth = Linear01Depth(rawDepth, _ZBufferParams);

        // If depth is at or beyond far clip, return zero (no valid depth)
        if (linearDepth > 0.999999)
            return float4(0, 0, 0, 0);

        float nearClip = _ProjectionParams.y;
        float farClip  = _ProjectionParams.z;

        float eyeSpaceDepth = linearDepth * (farClip - nearClip) + nearClip;
        float normalizedDepth = saturate((eyeSpaceDepth - nearClip) / (farClip - nearClip));

        float finalDepth = (_ReverseData > 0) ? (1.0 - normalizedDepth) : normalizedDepth;
        return float4(finalDepth, 0, 0, 0);
    }

    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }

        Pass
        {
            Name "DepthCapture"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }

    Fallback Off
}
