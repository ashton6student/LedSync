Shader "Unlit/ColorFrameSubtract"
{
    Properties
    {
        _MainTex ("ON Frame", 2D) = "white" {}
        _PrevTex ("OFF Frame", 2D) = "white" {}
        _Threshold ("Threshold", Range(0, 0.3)) = 0.05
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _PrevTex;
            float _Threshold;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 on = tex2D(_MainTex, i.uv);
                fixed4 off = tex2D(_PrevTex, i.uv);
                fixed4 diff = abs(on - off);

                float brightness = max(diff.r, max(diff.g, diff.b));
                float mask = step(_Threshold, brightness);

                fixed4 col = on * mask;
                col.a=1;
                return col;
            }
            ENDCG
        }
    }
}
