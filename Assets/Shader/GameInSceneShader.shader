Shader "Custom/GameInSceneShader"
{
    Properties
    {
        // ========== 基础参数 ==========
        _MainTex ("Main Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)
        _Flip ("Flip Horizontal", Range(0, 1)) = 0
        _LockFlip ("Lock Flip", Range(0, 1)) = 0
        
        // ========== 闪烁叠加图片参数 ==========
        _BlinkTex ("Blink Overlay Texture", 2D) = "white" {}
        _BlinkColor ("Blink Overlay Color", Color) = (1,1,1,1)
        _BlinkEnabled ("Enable Blink", Range(0, 1)) = 0
        _BlinkInterval ("Blink Interval", Float) = 0.5
        _BlinkOffset ("Blink Offset", Float) = 0
        _BlinkOffsetX ("Blink Offset X", Float) = 0
        _BlinkOffsetY ("Blink Offset Y", Float) = 0
        _BlinkScale ("Blink Scale", Float) = 1
        
        // ========== 序列帧动画参数 ==========
        _SpriteSheetEnabled ("Sprite Sheet Enabled", Range(0, 1)) = 0
        _Rows ("Rows", Float) = 1
        _Columns ("Columns", Float) = 4
        _Speed ("Speed", Float) = 15

        // ========== 纹理过渡参数 ==========
        _NextTex ("Next Texture", 2D) = "white" {}
        _Transition ("Transition Progress", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags
        {
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
            sampler2D _NextTex;
            sampler2D _BlinkTex;
            
            float4 _Color;
            float4 _BlinkColor;
            
            float _Flip;
            float _LockFlip;
            float _BlinkEnabled;
            float _BlinkInterval;
            float _BlinkOffset;
            float _BlinkOffsetX;
            float _BlinkOffsetY;
            float _BlinkScale;
            
            // ========== 序列帧属性 ==========
            float _SpriteSheetEnabled;
            float _Rows;
            float _Columns;
            float _Speed;

            float _Transition;

            // 计算单张序列帧的UV
            float2 getFrameUV(float2 baseUV, float rows, float columns, float speed)
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

            float2 applyFlip(float2 uv, float flip, float lockFlip)
            {
                float actualFlip = flip * (1.0 - lockFlip);
                uv.x = lerp(uv.x, 1.0 - uv.x, actualFlip);
                return uv;
            }

            fixed4 applyBlink(fixed4 mainColor, float2 blinkUv)
            {
                if (_BlinkEnabled > 0.5)
                {
                    fixed4 blinkCol = tex2D(_BlinkTex, blinkUv) * _BlinkColor;
                    
                    float blinkLuminance = blinkCol.r * 0.299 + blinkCol.g * 0.587 + blinkCol.b * 0.114;
                    float blinkValid = step(0.001, blinkCol.a * max(blinkLuminance, 0.001));
                    
                    if (blinkValid > 0.5)
                    {
                        float time = _Time.y + _BlinkOffset;
                        float period = max(_BlinkInterval * 2.0, 0.001);
                        float cycleTime = fmod(time, period);
                        float blinkAlpha = step(cycleTime, _BlinkInterval);
                        
                        float overlayAlpha = blinkCol.a * blinkAlpha;
                        
                        mainColor.rgb = lerp(mainColor.rgb, blinkCol.rgb, overlayAlpha);
                        mainColor.a = max(mainColor.a, overlayAlpha);
                    }
                }
                return mainColor;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                float2 uv = applyFlip(v.uv, _Flip, _LockFlip);
                o.uv = uv;
                
                float2 blinkUv = v.uv;
                blinkUv.x += _BlinkOffsetX;
                blinkUv.y += _BlinkOffsetY;
                blinkUv = (blinkUv - 0.5) / max(_BlinkScale, 0.001) + 0.5;
                o.blinkUv = blinkUv;
                
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 baseUV = i.uv;
                fixed4 spriteCol;
                
                // ========== 判断是否启用序列帧动画 ==========
                if (_SpriteSheetEnabled > 0.5)
                {
                    // 序列帧模式：计算当前帧UV
                    float2 frameUV = getFrameUV(baseUV, _Rows, _Columns, _Speed);
                    spriteCol = tex2D(_MainTex, frameUV);
                }
                else
                {
                    // 单纹理模式：直接采样主纹理
                    spriteCol = tex2D(_MainTex, baseUV);
                }
                
                // 纹理过渡
                fixed4 nextCol = tex2D(_NextTex, baseUV);
                fixed4 finalSpriteCol = lerp(spriteCol, nextCol, _Transition);
                
                finalSpriteCol *= i.color;
                finalSpriteCol = applyBlink(finalSpriteCol, i.blinkUv);
                
                return finalSpriteCol;
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}