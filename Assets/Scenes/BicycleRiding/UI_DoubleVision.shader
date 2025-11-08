Shader "UI/DoubleVision"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Offset ("Offset (texels)", Range(0,10)) = 0
        _Mix    ("Ghost Mix", Range(0,1)) = 0.5
        _TimeFactor ("Drift Speed", Range(0,2)) = 0.35
    }
    SubShader
    {
        Tags{ "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float2 texel:TEXCOORD1; };
            sampler2D _MainTex; float4 _MainTex_TexelSize;
            float _Offset, _Mix, _TimeFactor;

            v2f vert(appdata v){
                v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv;
                o.texel = _MainTex_TexelSize.xy * _Offset;
                return o;
            }

            fixed4 frag(v2f i):SV_Target
            {
                // gentle wandering direction
                float ang = _Time.y * _TimeFactor;
                float2 dir = float2(cos(ang), sin(ang));
                float2 off = dir * i.texel;

                fixed4 a = tex2D(_MainTex, i.uv);
                fixed4 b = tex2D(_MainTex, i.uv + off);
                return lerp(a, b, _Mix);
            }
            ENDCG
        }
    }
}
