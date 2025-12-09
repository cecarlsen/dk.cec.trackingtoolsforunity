/*
	Copyright © Carl Emil Carlsen 2023
	http://cec.dk
*/

Shader "Hidden/LensUndistorter"
{
	Properties
	{
		_MainTex( "Whatever", 2D ) = "black" {} // Needed for Graphics.Blit to work.
	}

	CGINCLUDE

		#include "UnityCG.cginc"

		#pragma multi_compile_local __ _PRE_FLIP_Y
		#pragma multi_compile_local __ _POST_FLIP_Y
		
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
		sampler2D _UndistMapTex;
		
		cbuffer _Constants
		{
			int _W;
			int _H;
			float _CX;
			float _CY;

			float _FX;
			float _FY;
			float _K1;
			float _K2;

			float _K3;
			float _K4;
			float _K5;
			float _K6;

			float _P1;
			float _P2;
			float _M01;
			float _M11;

			float _M21;
			float _M02;
			float _M12;
			float _M22;

			float _M00;
			float _M10;
			float _M20;
			float _Pad;
			
			//float3 m01_m11_m21;
			//float3 m02_m12_m22;
			//float3 m00_m10_m20;
		};


		ToFrag Vert( ToVert v )
		{
			ToFrag o;
			o.vertex = UnityObjectToClipPos( v.vertex );
			o.uv = v.uv;
			return o;
		}


		float2 FragGenerateMap( ToFrag i ) : SV_Target
		{
			float px = i.uv.x * _W;
			float py = i.uv.y * _H;
			
			float3 xyw = float3(_M01,_M11,_M21) * py + float3(_M02,_M12,_M22) + float3(_M00,_M10,_M20) * px;
			
			float w = 1.0 / xyw.z;
			float x = xyw.x * w;
			float y = xyw.y * w;
			float x2 = x * x;
			float y2 = y * y;
			float r2 = x2 + y2;
			float _2xy = 2.0 * x * y;
			float kr = ( 1.0 + ( ( _K3 * r2 + _K2 ) * r2 + _K1 ) * r2 ) / ( 1.0 + ( ( _K6*r2 + _K5 ) * r2 + _K4 ) * r2 );
			float u = _FX * ( x * kr + _P1 * _2xy + _P2 * ( r2 + 2.0 * x2 ) ) + _CX;
			float v = _FY * ( y * kr + _P1 * (r2 + 2 * y2 ) + _P2 * _2xy ) + _CY;
			
			
			float2 uv = float2( u / (float) _W, v / (float) _H );
			
			// DEBUG. Visualize distortion.
			//return length( i.uv - uv );
			//return ( ( i.uv - uv ) * 2 + 0.5 );
			
			return uv;
		}
		
		
		float4 FragUndistort( ToFrag i ) : SV_Target
		{
			#ifdef _PRE_FLIP_Y
				i.uv.y = 1.0 - i.uv.y;
			#endif

			float2 distortUV = tex2D( _UndistMapTex, i.uv ).xy;
			
			#ifdef _POST_FLIP_Y
				distortUV.y = 1.0 - distortUV.y;
			#endif
			
			return tex2D( _MainTex, distortUV );
		}

	ENDCG


	SubShader
	{
		Pass
		{
			Name "_GenerateMapKernel"
			ColorMask RG
		
			Cull Off
			ZWrite Off
			ZTest Always

			CGPROGRAM
			#pragma vertex Vert
			#pragma fragment FragGenerateMap
			ENDCG
		}
		
		Pass
		{
			Name "_UndistortKernel"
		
			Cull Off
			ZWrite Off
			ZTest Always

			CGPROGRAM
			#pragma vertex Vert
			#pragma fragment FragUndistort
			ENDCG
		}
	}
}
