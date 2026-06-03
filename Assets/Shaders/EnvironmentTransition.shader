Shader "Custom/EnvironmentTransition"
{
    Properties
    {
        _MainTex ("Current Texture", 2D) = "white" {}
        _NextTex ("Next Texture", 2D) = "white" {}
        _Transition ("Transition Progress", Range(0, 1)) = 0
        _Color ("Tint Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags
        {
            "Queue"="Background"
            "IgnoreProjector"="True"
            "RenderType"="Opaque"
            "PreviewType"="Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _NextTex;
            float4 _MainTex_ST;
            float4 _NextTex_ST;
            fixed4 _Color;
            float _Transition;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col1 = tex2D(_MainTex, i.uv);
                fixed4 col2 = tex2D(_NextTex, i.uv);
                fixed4 col = lerp(col1, col2, _Transition);
                return col * _Color;
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}
