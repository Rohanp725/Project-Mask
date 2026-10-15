// PS1-style lit shader for URP: vertex snapping (jitter), affine texture warping,
// simple Lambert lighting from the main light + additional lights (Forward and Forward+), and fog.
Shader "Semblance/PS1 Lit"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 0)
        _FogPierce ("Emission Fog Pierce", Range(0, 1)) = 0
        [Toggle] _WorldUV ("World-Space Texture Mapping", Float) = 0
        _WorldScale ("World Texture Scale (tiles per meter)", Float) = 0.5
        _SnapResolution ("Vertex Snap Resolution", Float) = 160
        _AffineStrength ("Affine Warp", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _OffsetFactor ("Depth Offset Factor", Float) = 0
        _OffsetUnits ("Depth Offset Units", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _EmissionColor;
            half _FogPierce;
            float _WorldUV;
            float _WorldScale;
            float _SnapResolution;
            float _AffineStrength;
        CBUFFER_END

        // Snap clip-space position to a coarse screen grid, like the PS1's integer vertex coordinates.
        float4 SnapClipPos(float4 positionCS)
        {
            // Vertices at or behind the camera can't be snapped stably; snapping them makes
            // large polygons (ground, roads) jump around, so leave them alone.
            if (positionCS.w < 0.5)
                return positionCS;
            float2 grid = float2(_SnapResolution * (_ScreenParams.x / _ScreenParams.y), _SnapResolution);
            float4 snapped = positionCS;
            snapped.xy = positionCS.xy / positionCS.w;
            snapped.xy = floor(snapped.xy * grid + 0.5) / grid;
            snapped.xy *= positionCS.w;
            return snapped;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            Offset [_OffsetFactor], [_OffsetUnits]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                noperspective float3 affineUV : TEXCOORD1; // uv * w, w
                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                float4 positionCS = SnapClipPos(pos.positionCS);
                float2 uv = TRANSFORM_TEX(input.uv, _BaseMap);
                // World-space mapping: textures keep their scale on stretched boxes (walls, ground, roads).
                float3 nWS = abs(TransformObjectToWorldNormal(input.normalOS));
                float2 worldUV = nWS.y > max(nWS.x, nWS.z) ? pos.positionWS.xz : (nWS.x > nWS.z ? pos.positionWS.zy : pos.positionWS.xy);
                uv = lerp(uv, worldUV * _WorldScale, _WorldUV);

                output.positionCS = positionCS;
                output.uv = uv;
                output.affineUV = float3(uv * positionCS.w, positionCS.w);
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 affine = input.affineUV.xy / input.affineUV.z;
                float2 uv = lerp(input.uv, affine, _AffineStrength);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
                float3 normalWS = normalize(input.normalWS);

                // Ambient from spherical harmonics (scene ambient color).
                half3 lighting = SampleSH(normalWS);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                lighting += mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation * saturate(dot(normalWS, mainLight.direction)));

                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, input.positionWS);
                    lighting += light.color * (light.distanceAttenuation * saturate(dot(normalWS, light.direction) * 0.75 + 0.25));
                LIGHT_LOOP_END
                #endif

                // Fog per pixel: large ground/road polygons would get wrong fog from per-vertex values.
                float fogFactor = ComputeFogFactor(TransformWorldToHClip(input.positionWS).z);
                half3 color = MixFog(albedo.rgb * lighting + _EmissionColor.rgb, fogFactor);
                // Let bright emissive objects (lamps, lit windows) glow through the fog as distant beacons.
                color += _EmissionColor.rgb * (1.0h - ComputeFogIntensity(fogFactor)) * _FogPierce;
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]
            Offset [_OffsetFactor], [_OffsetUnits]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = SnapClipPos(TransformObjectToHClip(input.positionOS.xyz));
                return output;
            }

            half frag(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]
            Offset [_OffsetFactor], [_OffsetUnits]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = SnapClipPos(TransformObjectToHClip(input.positionOS.xyz));
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                half3 packedNormalWS = PackFloat2To888(saturate(octNormalWS * 0.5 + 0.5));
                return half4(packedNormalWS, 0);
                #else
                return half4(normalWS, 0);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
