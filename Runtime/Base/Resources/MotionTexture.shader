Shader "UI/MotionTexture"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_Brightness ("Brightness", Float) = 1.0
		[Toggle(FLIP_VERTICALLY)] _FlipVertically("Flip Vertically", Int) = 0
	}
	SubShader
	{
		Tags{ "Queue" = "Transparent" "RenderType" = "Transparent" }
		Blend SrcAlpha OneMinusSrcAlpha
		Cull Off

		Pass
		{
			CGPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

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

			sampler2D _MainTex;
			float4 _MainTex_ST;
			half _Brightness;
			int _FlipVertically;


			v2f Vert( appdata v )
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				if( _FlipVertically > 0 ) v.uv.y = 1.0 - v.uv.y;
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.color = v.color;
				return o;
			}

			half4 Frag( v2f i ) : SV_Target
			{
				half2 motion = ( tex2D( _MainTex, i.uv ).rg * _Brightness ) + 0.5;
				return half4( motion , 0.5, 1 ) * i.color;
			}
			ENDCG
		}
	}
}