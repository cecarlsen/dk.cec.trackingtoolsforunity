/*
	Copyright © Carl Emil Carlsen 2023
	http://cec.dk
*/

Shader "Hidden/Flipper"
{
	Properties
	{
		_MainTex( "Whatever", 2D ) = "black" {} // Needed for Graphics.Blit to work.
	}

	CGINCLUDE

		#include "UnityCG.cginc"

		struct ToVert
		{
			float4 vertex : POSITION;
			float2 uv : TEXCOORD0;
		};

		struct ToFrag
		{
			float4 vertex : SV_POSITION;
			float2 uv : TEXCOORD0;
		};

		sampler2D _MainTex;
		float4 _MainTex_TexelSize;


		ToFrag Vert( ToVert v )
		{
			ToFrag o;
			o.vertex = UnityObjectToClipPos( v.vertex );
			o.uv = v.uv;
			return o;
		}


		float4 Frag( ToFrag i ) : SV_Target
		{
			i.uv.y = 1.0 - i.uv.y;
			return tex2D( _MainTex, i.uv );
		}

	ENDCG


	SubShader
	{
		Pass
		{
			Cull Off
			ZWrite Off
			ZTest Always

			CGPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			ENDCG
		}
	}
}
