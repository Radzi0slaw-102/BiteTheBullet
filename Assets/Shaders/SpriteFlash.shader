// Shader sprite'a z możliwością "błysku": miesza kolor każdego piksela z _FlashColor
// w proporcji _FlashAmount (0 = normalny sprite, 1 = cała sylwetka w kolorze błysku).
// Przezroczystość (alfa) zostaje z tekstury, więc błyska tylko sama postać, nie prostokąt.
//
// Shader jest nieoświetlony (unlit) – na scenie jest jedno Global Light 2D o intensywności 1,
// więc wygląda identycznie jak domyślny Sprite-Lit. Pass bez tagu LightMode jest rysowany
// zarówno przez Renderer 2D, jak i zwykły renderer URP.
Shader "BiteTheBullet/Sprite Flash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        _FlashAmount ("Flash Amount", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;      // SpriteRenderer.color trafia tutaj
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _FlashColor;
                half   _FlashAmount;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                color.rgb = lerp(color.rgb, _FlashColor.rgb, _FlashAmount * _FlashColor.a);
                return color;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
