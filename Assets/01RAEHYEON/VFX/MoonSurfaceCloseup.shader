Shader "NITROZERO/Moon Surface Closeup"
{
    Properties
    {
        _BaseMap("Lunar dust", 2D) = "gray" {}
        _BaseColor("Surface tint", Color) = (0.65,0.65,0.65,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionOS:TEXCOORD0; float3 normalWS:TEXCOORD1; float3 normalOS:TEXCOORD2; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.positionOS=v.positionOS.xyz;
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                o.normalOS=v.normalOS;
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float3 n=normalize(i.normalWS);
                float3 weights=pow(abs(normalize(i.normalOS)),4);
                weights/=max(dot(weights,float3(1,1,1)),0.001);
                float3 p=i.positionOS*0.32;
                half3 dust=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.yz).rgb*weights.x
                    +SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xz).rgb*weights.y
                    +SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xy).rgb*weights.z;
                float light=0.10+0.90*saturate(dot(n,normalize(float3(-0.65,0.7,-0.4))));
                Light earthlight = GetMainLight();
                half3 earthFill = earthlight.color * saturate(dot(n, earthlight.direction));
                return half4(dust*_BaseColor.rgb*(light + earthFill),1);
            }
            ENDHLSL
        }
    }
}
