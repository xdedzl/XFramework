Shader "Hidden/XFramework/AnimationPreviewGrid"
{
    Properties
    {
        _BGColor ("Background Color", Color) = (0.039216, 0.101961, 0.180392, 1)
        _GridColor ("Grid Color", Color) = (0.301961, 0.670588, 0.968627, 1)
        _MajorGridColor ("Major Grid Color", Color) = (0.301961, 0.670588, 0.968627, 1)
        _CenterLineColor ("Center Line Color", Color) = (0.518077, 0.684173, 0.974843, 1)
        _GridWidth ("Grid Width (Meters)", Float) = 0.01
        _MajorGridWidth ("Major Grid Width (Meters)", Float) = 0.01
        _CenterLineWidth ("Center Line Width (Meters)", Float) = 0.1
        _GridSpacing ("Grid Spacing (Meters)", Float) = 1
        _MajorGridInterval ("Major Grid Interval", Float) = 5
        _GridSize ("Grid Size (Meters)", Float) = 10
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "GridForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 gridUV : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BGColor;
                half4 _GridColor;
                half4 _MajorGridColor;
                half4 _CenterLineColor;
                float _GridWidth;
                float _MajorGridWidth;
                float _CenterLineWidth;
                float _GridSpacing;
                float _MajorGridInterval;
                float _GridSize;
            CBUFFER_END

            float DistanceToRepeatingLine(float coordMeters, float spacingMeters)
            {
                float normalized = coordMeters / max(spacingMeters, 0.0001);
                float cycle = abs(frac(normalized + 0.5) - 0.5);
                return cycle * spacingMeters;
            }

            float LineMask(float distanceMeters, float widthMeters)
            {
                float halfWidth = max(widthMeters * 0.5, 0.0001);
                float aa = max(fwidth(distanceMeters), 0.0001);
                return 1.0 - smoothstep(halfWidth - aa, halfWidth + aa, distanceMeters);
            }

            half4 ComputeGridColor(float2 gridUV)
            {
                float2 localMeters = (gridUV - 0.5) * _GridSize;

                // Edge fade
                float2 edgeDist = 1.0 - abs(gridUV - 0.5) * 2.0;
                float edgeFade = saturate(min(edgeDist.x, edgeDist.y) * 8.0);
                edgeFade = edgeFade * edgeFade * edgeFade;

                // Minor grid
                float xLine = LineMask(DistanceToRepeatingLine(localMeters.x, _GridSpacing), _GridWidth);
                float zLine = LineMask(DistanceToRepeatingLine(localMeters.y, _GridSpacing), _GridWidth);
                float minorMask = max(xLine, zLine);

                // Major grid
                float majorSpacing = _GridSpacing * _MajorGridInterval;
                float xMajor = LineMask(DistanceToRepeatingLine(localMeters.x, majorSpacing), _MajorGridWidth);
                float zMajor = LineMask(DistanceToRepeatingLine(localMeters.y, majorSpacing), _MajorGridWidth);
                float majorMask = max(xMajor, zMajor);

                // Center lines
                float centerX = LineMask(abs(localMeters.y), _CenterLineWidth);
                float centerZ = LineMask(abs(localMeters.x), _CenterLineWidth);
                float centerMask = max(centerX, centerZ);

                half4 color = _BGColor;
                color = lerp(color, _GridColor, saturate(minorMask));
                color = lerp(color, _MajorGridColor, saturate(majorMask));
                color = lerp(color, _CenterLineColor, saturate(centerMask));
                color.a *= edgeFade;
                return color;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.gridUV = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return ComputeGridColor(input.gridUV);
            }
            ENDHLSL
        }

        Pass
        {
            Name "SRPDefaultUnlit"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 gridUV : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BGColor;
                half4 _GridColor;
                half4 _MajorGridColor;
                half4 _CenterLineColor;
                float _GridWidth;
                float _MajorGridWidth;
                float _CenterLineWidth;
                float _GridSpacing;
                float _MajorGridInterval;
                float _GridSize;
            CBUFFER_END

            float DistanceToRepeatingLine(float coordMeters, float spacingMeters)
            {
                float normalized = coordMeters / max(spacingMeters, 0.0001);
                float cycle = abs(frac(normalized + 0.5) - 0.5);
                return cycle * spacingMeters;
            }

            float LineMask(float distanceMeters, float widthMeters)
            {
                float halfWidth = max(widthMeters * 0.5, 0.0001);
                float aa = max(fwidth(distanceMeters), 0.0001);
                return 1.0 - smoothstep(halfWidth - aa, halfWidth + aa, distanceMeters);
            }

            half4 ComputeGridColor(float2 gridUV)
            {
                float2 localMeters = (gridUV - 0.5) * _GridSize;
                float2 edgeDist = 1.0 - abs(gridUV - 0.5) * 2.0;
                float edgeFade = saturate(min(edgeDist.x, edgeDist.y) * 8.0);
                edgeFade = edgeFade * edgeFade * edgeFade;

                float xLine = LineMask(DistanceToRepeatingLine(localMeters.x, _GridSpacing), _GridWidth);
                float zLine = LineMask(DistanceToRepeatingLine(localMeters.y, _GridSpacing), _GridWidth);
                float minorMask = max(xLine, zLine);

                float majorSpacing = _GridSpacing * _MajorGridInterval;
                float xMajor = LineMask(DistanceToRepeatingLine(localMeters.x, majorSpacing), _MajorGridWidth);
                float zMajor = LineMask(DistanceToRepeatingLine(localMeters.y, majorSpacing), _MajorGridWidth);
                float majorMask = max(xMajor, zMajor);

                float centerX = LineMask(abs(localMeters.y), _CenterLineWidth);
                float centerZ = LineMask(abs(localMeters.x), _CenterLineWidth);
                float centerMask = max(centerX, centerZ);

                half4 color = _BGColor;
                color = lerp(color, _GridColor, saturate(minorMask));
                color = lerp(color, _MajorGridColor, saturate(majorMask));
                color = lerp(color, _CenterLineColor, saturate(centerMask));
                color.a *= edgeFade;
                return color;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.gridUV = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return ComputeGridColor(input.gridUV);
            }
            ENDHLSL
        }
    }
}
