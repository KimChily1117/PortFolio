Shader "Kimchily/Chili Portal"
{
    Properties { _Tint ("Portal tint", Color) = (0.55,0.91,0.83,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            fixed4 _Tint;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 p=i.uv*2-1;
                float r=length(p);
                float swirl=sin(r*18-atan2(p.y,p.x)*2-_Time.y*1.7)*.5+.5;
                float rings=pow(swirl,6)*.2;
                float core=1-smoothstep(0,1.15,r);
                float rim=pow(saturate(r),6)*.65;
                float3 col=lerp(float3(.12,.39,.47),_Tint.rgb,core*.7+rings+rim);
                col+=pow(core,5)*float3(.27,.24,.12);
                return fixed4(col,.94);
            }
            ENDCG
        }
    }
}
