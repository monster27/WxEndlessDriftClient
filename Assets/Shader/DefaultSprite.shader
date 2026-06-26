Shader "Custom/DefaultSprite"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)
        _Flip ("Flip Horizontal", Range(0, 1)) = 0
        
        // ========== 闪烁叠加图片参数 ==========
        [Header(Blink Overlay)]
        _BlinkTex ("Blink Overlay Texture", 2D) = "white" {}
        _BlinkColor ("Blink Overlay Color", Color) = (1,1,1,1)
        _BlinkEnabled ("Enable Blink", Range(0, 1)) = 0
        _BlinkInterval ("Blink Interval (seconds)", Float) = 0.5
        _BlinkOffset ("Blink Offset (seconds)", Float) = 0
        
        // 叠加图片的偏移和缩放（用于调整位置和大小）
        _BlinkOffsetX ("Blink Offset X", Float) = 0
        _BlinkOffsetY ("Blink Offset Y", Float) = 0
        _BlinkScale ("Blink Scale", Float) = 1
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
                float2 blinkUv : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Flip;

            sampler2D _BlinkTex;
            fixed4 _BlinkColor;
            float _BlinkEnabled;
            float _BlinkInterval;
            float _BlinkOffset;
            float _BlinkOffsetX;
            float _BlinkOffsetY;
            float _BlinkScale;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // 主纹理UV（应用翻转）
                float2 uv = v.uv;
                uv.x = lerp(uv.x, 1.0 - uv.x, _Flip);
                o.uv = uv;
                
                // ========== 叠加纹理UV（独立变换，不受翻转影响，可以偏移和缩放） ==========
                float2 blinkUv = v.uv;
                // 偏移
                blinkUv.x += _BlinkOffsetX;
                blinkUv.y += _BlinkOffsetY;
                // 缩放（以中心为基准）
                blinkUv = (blinkUv - 0.5) / max(_BlinkScale, 0.001) + 0.5;
                o.blinkUv = blinkUv;
                
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 主纹理采样（使用主纹理UV）
                fixed4 mainCol = tex2D(_MainTex, i.uv) * i.color;
                
                // ========== 闪烁叠加图片处理 ==========
                // 只有同时满足以下条件才执行闪烁：
                // 1. _BlinkEnabled > 0.5（开启了闪烁）
                // 2. _BlinkTex 不是空纹理（有叠加图）
                // 3. _BlinkTex 的采样结果不是全透明（实际有内容）
                if (_BlinkEnabled > 0.5)
                {
                    // 采样叠加纹理
                    fixed4 blinkCol = tex2D(_BlinkTex, i.blinkUv) * _BlinkColor;
                    
                    // 检查叠加纹理是否有效（不是全透明）
                    // 使用亮度判断，如果叠加纹理是全透明的，则跳过闪烁
                    float blinkLuminance = blinkCol.r * 0.299 + blinkCol.g * 0.587 + blinkCol.b * 0.114;
                    float blinkValid = step(0.001, blinkCol.a * max(blinkLuminance, 0.001));
                    
                    // 如果叠加纹理无效，不执行闪烁
                    if (blinkValid > 0.5)
                    {
                        // 计算闪烁时间
                        float time = _Time.y + _BlinkOffset;
                        float period = max(_BlinkInterval * 2.0, 0.001);
                        float cycleTime = fmod(time, period);
                        float blinkAlpha = step(cycleTime, _BlinkInterval);
                        
                        // 叠加纹理的透明度（包含闪烁控制）
                        float overlayAlpha = blinkCol.a * blinkAlpha;
                        
                        // ========== 图片叠加（完全覆盖，超出部分也显示） ==========
                        // 当叠加纹理不透明时，完全显示叠加纹理
                        // 当叠加纹理透明时，显示主纹理
                        mainCol.rgb = lerp(mainCol.rgb, blinkCol.rgb, overlayAlpha);
                        
                        // 透明度取最大值，确保不透明部分正确显示
                        mainCol.a = max(mainCol.a, overlayAlpha);
                    }
                }
                
                return mainCol;
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}