Shader "UI/FullScreenKawaseBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Radius ("Radius", Range(0,10)) = 0
        _Alpha ("Alpha", Range(0,1)) = 1
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
            float _Radius, _Alpha;

            v2f vert(appdata v){
                v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv;
                o.texel = _MainTex_TexelSize.xy * _Radius;
                return o;
            }

            fixed4 frag(v2f i):SV_Target
            {
                float2 o = i.texel;
                fixed4 c =
                      tex2D(_MainTex, i.uv)
                    + tex2D(_MainTex, i.uv + float2( o.x,  0))
                    + tex2D(_MainTex, i.uv + float2(-o.x,  0))
                    + tex2D(_MainTex, i.uv + float2( 0,   o.y))
                    + tex2D(_MainTex, i.uv + float2( 0,  -o.y))
                    + tex2D(_MainTex, i.uv + float2( o.x,  o.y))
                    + tex2D(_MainTex, i.uv + float2(-o.x,  o.y))
                    + tex2D(_MainTex, i.uv + float2( o.x, -o.y))
                    + tex2D(_MainTex, i.uv + float2(-o.x, -o.y));
                c /= 9.0;
                c.a *= _Alpha;
                return c;
            }
            ENDCG
        }
    }
}
