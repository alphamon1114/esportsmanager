Shader "FpsManager/SourceMapPreview" {
 Properties { _Color ("Color",Color)=(0.45,0.55,0.65,1) }
 SubShader { Tags { "RenderType"="Opaque" } Cull Off
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct V { float4 vertex:POSITION; };
 struct F { float4 position:SV_POSITION;float3 world:TEXCOORD0; };
 fixed4 _Color;
 F vert(V v){F o;o.position=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;}
 fixed4 frag(F i):SV_Target {float3 n=normalize(cross(ddx(i.world),ddy(i.world)));float light=.28+.72*abs(dot(n,normalize(float3(.4,.8,.25))));return fixed4(_Color.rgb*light,1);}
 ENDCG }
 }
}
