Shader "Unlit/DiffOverlay"
{
    Properties
    {
        _MainTex ("Original Frame", 2D) = "black" {}
        _DiffTex ("Diff Texture", 2D) = "black" {}
        _Gain ("Diff Gain", Range(0, 8)) = 2
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

            sampler2D _MainTex;
            sampler2D _DiffTex;
            float _Gain;

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
                float3 baseCol = tex2D(_MainTex, i.uv).rgb;
                float3 diffCol = tex2D(_DiffTex, i.uv).rgb;

                float3 outCol = saturate(baseCol + _Gain * diffCol);
                return float4(outCol, 1.0);
            }
            ENDHLSL
        }
    }
}
