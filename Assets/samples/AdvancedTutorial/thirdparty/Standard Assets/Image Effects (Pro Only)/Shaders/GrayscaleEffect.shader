Shader "Hidden/Grayscale Effect" {
Properties {
	_MainTex ("Base (RGB)", 2D) = "white" {}
}

SubShader {
	Pass {
		ZTest Always Cull Off ZWrite Off
		Fog { Mode off }
				
CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma fragmentoption ARB_precision_hint_fastest 
#include "UnityCG.cginc"

uniform half _Ramp;
uniform sampler2D _MainTex;

fixed4 frag (v2f_img i) : SV_Target
{
	fixed4 original = tex2D(_MainTex, i.uv);
	fixed grayscale = Luminance(original.rgb);

	fixed4 output = lerp(original, grayscale, _Ramp);
	output.a = original.a;
	return output;
}
ENDCG

	}
}

Fallback off

}