Shader "Kimchily/Soft Toy"
{
    Properties { _Color ("Color", Color) = (1,1,1,1) _Glow ("Glow", Range(0,1)) = 0 }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct v2f { float4 pos:SV_POSITION; float3 normal:TEXCOORD0; SHADOW_COORDS(1) UNITY_FOG_COORDS(2) };
            fixed4 _Color;
            float _Glow;
            v2f vert(appdata v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);TRANSFER_SHADOW(o);UNITY_TRANSFER_FOG(o,o.pos);return o; }
            fixed4 frag(v2f i):SV_Target
            {
                float3 n=normalize(i.normal);
                float light=saturate(dot(n,normalize(_WorldSpaceLightPos0.xyz)));
                float shadow=SHADOW_ATTENUATION(i);
                float shade=(.67+.28*light)*lerp(.77,1,shadow);
                fixed4 col=fixed4(_Color.rgb*lerp(shade,1,_Glow),1);
                UNITY_APPLY_FOG(i.fogCoord,col);
                return col;
            }
            ENDCG
        }
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
    FallBack "Diffuse"
}
