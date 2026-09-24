// ART-DIRECTION PROTOTYPE ONLY. Not installed in Assets or bound to a Canvas.
// Built-in Render Pipeline / uGUI. Preview counterpart: effects.js (WebGL).
Shader "UI/Concept/PixelFrostOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Alpha", 2D) = "white" {}
        _ColdColor ("Ice shadow", Color) = (0.18, 0.33, 0.43, 1)
        _LightColor ("Ice facet", Color) = (0.73, 0.89, 0.93, 1)
        _FrostAmount ("Frost amount", Range(0,1)) = 0
        _EdgeWidth ("Edge width (short-side fraction)", Range(0.005,0.15)) = 0.055
        _PixelStep ("Pixel grid size", Range(1,8)) = 4
        _RectSize ("Overlay pixel size", Vector) = (1920,1080,0,0)
        _Opacity ("Maximum opacity", Range(0,1)) = 0.75
        _Thaw ("Thaw", Range(0,1)) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            Name "FROST_OVERLAY"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata_t { float4 vertex:POSITION; float4 color:COLOR; float2 texcoord:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 worldPosition:TEXCOORD1; };
            sampler2D _MainTex;
            fixed4 _TextureSampleAdd, _ColdColor, _LightColor;
            float4 _ClipRect, _RectSize;
            float _FrostAmount, _EdgeWidth, _PixelStep, _Opacity, _Thaw;
            v2f vert(appdata_t v)
            {
                v2f o; o.worldPosition=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex);
                o.uv=v.texcoord; o.color=v.color; return o;
            }
            float2 hash22(float2 p) { p=frac(p*float2(.1031,.1030));p+=dot(p,p.yx+33.33);return frac((p.xx+p.yx)*p.xy); }
            float hash21(float2 p) { return hash22(p).x; }
            float noise21(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3.0-2.0*f);
                return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),lerp(hash21(i+float2(0,1)),hash21(i+float2(1,1)),f.x),f.y);
            }
            float2 crystalCell(float2 p)
            {
                float2 ip=floor(p), fp=frac(p);float a=8.0,b=8.0;
                [unroll] for(int y=-1;y<=1;y++)
                {
                    [unroll] for(int x=-1;x<=1;x++)
                    {
                        float2 g=float2(x,y), d=g+hash22(ip+g)-fp;float t=dot(d,d);
                        if(t<a){b=a;a=t;}else if(t<b){b=t;}
                    }
                }
                return float2(a,b);
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 size=max(_RectSize.xy,1.0);
                float2 uv=floor(i.uv*size/max(_PixelStep,1.0))*max(_PixelStep,1.0)/size;
                float aspect=size.x/size.y;
                float edge=min(min(uv.x,1.0-uv.x)*aspect,min(uv.y,1.0-uv.y));
                float corner=pow(abs(uv.x-.5)*2.0,3.0)*pow(abs(uv.y-.5)*2.0,3.0);
                // Noise never changes with time: no sparkling or crawling pixels on text.
                float n=noise21(uv*float2(24,14))*.65+noise21(uv*float2(83,47))*.35;
                float width=(.006+_FrostAmount*_EdgeWidth)*(.30+n*.95+corner*.85);
                float frost=(1.0-smoothstep(width*.22,width,edge))*_FrostAmount;
                float2 crystal=crystalCell(uv*float2(52,30));
                float facet=1.0-smoothstep(.015,.13,crystal.y-crystal.x);
                float highlight=saturate(facet*.72+n*.18+step(.79,n)*.18);
                fixed4 col=lerp(_ColdColor,_LightColor,highlight);
                // One continuous frost surface. Put readable text above it instead
                // of punching opaque rectangular holes around individual labels.
                float spriteAlpha=(tex2D(_MainTex,i.uv)+_TextureSampleAdd).a;
                col.a=frost*(.18+n*.32+facet*.65)*_Opacity*spriteAlpha*i.color.a*lerp(1.0,.7,_Thaw);
                col.rgb*=i.color.rgb;
                #ifdef UNITY_UI_CLIP_RECT
                col.a*=UnityGet2DClipping(i.worldPosition.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a-.001);
                #endif
                return col;
            }
            ENDCG
        }
    }
}
