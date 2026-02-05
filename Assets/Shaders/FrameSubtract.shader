Shader "Unlit/FrameSubtract"
{
    Properties
    {
        _MainTex ("Current Frame", 2D) = "black" {}
        _PrevTex ("Previous Frame", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;   // current frame
            sampler2D _PrevTex;   // previous frame

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 curr = tex2D(_MainTex, i.uv).rgb;
                float3 prev = tex2D(_PrevTex, i.uv).rgb;

                float3 diff = abs(curr - prev);

                // grayscale output so it's easy to see
                float intensity = (diff.r + diff.g + diff.b) / 3.0;

                return float4(intensity, intensity, intensity, 1.0);
            }
            ENDHLSL
        }
    }
}
