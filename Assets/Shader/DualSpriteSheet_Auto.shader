Shader "Custom/DualSpriteSheet_Auto"
{
    Properties
    {
        // 第一组序列帧（例如：Idle空闲动画）
        _MainTex ("Idle Sheet", 2D) = "white" {}
        _Rows1 ("Idle Rows", Float) = 4
        _Columns1 ("Idle Columns", Float) = 4
        _Speed1 ("Idle Speed", Float) = 15

        // 第二组序列帧（例如：Player动作动画）
        _SecondTex ("Player Sheet", 2D) = "white" {}
        _Rows2 ("Player Rows", Float) = 4
        _Columns2 ("Player Columns", Float) = 4
        _Speed2 ("Player Speed", Float) = 20

        // 混合控制
        _Blend ("Blend (0=纯Idle, 1=纯Player)", Range(0, 1)) = 0

        // 镜像功能（左右镜像）
        _Flip ("Flip Horizontal", Range(0, 1)) = 0

        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

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
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            // 第一组属性
            sampler2D _MainTex;
            float _Rows1;
            float _Columns1;
            float _Speed1;

            // 第二组属性
            sampler2D _SecondTex;
            float _Rows2;
            float _Columns2;
            float _Speed2;

            // 通用属性
            fixed4 _Color;
            float _Blend;
            float _Flip; // 左右镜像 (0=正常, 1=镜像)

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            // 计算单张序列帧的UV
            float2 getFrameUV(float2 baseUV, float time, float rows, float columns, float speed)
            {
                float totalFrames = rows * columns;
                float timeInFrames = _Time.y * speed;
                float currentFrame = floor(timeInFrames);
                currentFrame = fmod(currentFrame, totalFrames);

                float column = fmod(currentFrame, columns);
                float row = floor(currentFrame / columns);

                float2 frameSize = float2(1.0 / columns, 1.0 / rows);
                float2 frameUV = baseUV * frameSize;
                frameUV.x += column * frameSize.x;
                frameUV.y += (rows - 1 - row) * frameSize.y;

                return frameUV;
            }

            // 应用左右镜像
            float2 applyFlip(float2 uv)
            {
                uv.x = lerp(uv.x, 1.0 - uv.x, _Flip);
                return uv;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 应用镜像到基础UV
                float2 baseUV = applyFlip(i.uv);
                
                // 分别计算两组序列帧的当前UV
                float2 uv1 = getFrameUV(baseUV, _Time.y, _Rows1, _Columns1, _Speed1);
                float2 uv2 = getFrameUV(baseUV, _Time.y, _Rows2, _Columns2, _Speed2);

                // 采样两套纹理
                fixed4 col1 = tex2D(_MainTex, uv1);
                fixed4 col2 = tex2D(_SecondTex, uv2);

                // 根据混合因子混合最终颜色
                fixed4 finalColor = lerp(col1, col2, _Blend);
                finalColor *= i.color;

                return finalColor;
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}