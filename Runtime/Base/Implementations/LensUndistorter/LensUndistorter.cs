/*
	Copyright © Carl Emil Carlsen 2023
	http://cec.dk
*/

using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrackingTools
{
	public class LensUndistorter
	{
		RenderTexture _undistortionMap;
		Material _material;
		ComputeBuffer _constantBuffer;

		int _GenerateMapKernel, _UndistortKernel;
		LocalKeyword _PRE_FLIP_Y;
		LocalKeyword _POST_FLIP_Y;
		//LocalKeyword _FLIP_X;

		public RenderTexture undistortionMap => _undistortionMap;

		const string logPrepend = "<b>[" + nameof( LensUndistorter ) + "]</b> ";


		[System.Serializable]
		struct Constants
		{
			// 6 x sizeof( float4 ).
			// Note. Unity seems to fuck up struct types, so we stick to primitives (2023.1).
			public int _W;
			public int _H;
			public float _CX;
			public float _CY;

			public float _FX;
			public float _FY;
			public float _K1;
			public float _K2;

			public float _K3;
			public float _K4;
			public float _K5;
			public float _K6;

			public float _P1;
			public float _P2;
			public float _M01;
			public float _M11;

			public float _M21;
			public float _M02;
			public float _M12;
			public float _M22;

			public float _M00;
			public float _M10;
			public float _M20;
			public float _Pad;
		}


		static class ShaderIDs
		{
			public static readonly int _Constants = Shader.PropertyToID( nameof( _Constants ) );
			public static readonly int _UndistMapTex = Shader.PropertyToID( nameof( _UndistMapTex ) );
		}


		public LensUndistorter( Intrinsics intrinsics )
		{
			const string shaderName = "Hidden/" + nameof( LensUndistorter );
			Shader shader = Shader.Find( shaderName );
			if( !shader ){
				Debug.LogWarning( logPrepend + "Missing shader: '" + shaderName + "'.\n" );
				return;
			}
			_material = new Material( shader );
			_GenerateMapKernel = _material.FindPass( nameof( _GenerateMapKernel ) );
			_UndistortKernel = _material.FindPass( nameof( _UndistortKernel ) );
			_PRE_FLIP_Y = new LocalKeyword( shader, nameof( _PRE_FLIP_Y ) );
			_POST_FLIP_Y = new LocalKeyword( shader, nameof( _POST_FLIP_Y ) );

			_undistortionMap = new RenderTexture( intrinsics.resolution.x, intrinsics.resolution.y, 0, RenderTextureFormat.RGFloat );
			_undistortionMap.name = "LensDistortionMap";

			Matrix4x4 camMatrix = new Matrix4x4(
				new Vector4( (float) intrinsics.fx, 0f, 0, 0f ),
				new Vector4( 0f, (float) intrinsics.fy, 0f, 0f ),
				new Vector4( (float) intrinsics.cx, (float) intrinsics.cy, 1f, 0f ),
				new Vector4( 0f, 0f, 0f, 1f )
			);
			camMatrix = camMatrix.inverse;

			Constants c = new Constants();
			c._W = intrinsics.resolution.x;
			c._H = intrinsics.resolution.y;
			c._CX = (float) intrinsics.cx;
			c._CY = (float) intrinsics.cy;
			c._FX = (float) intrinsics.fx;
			c._FY = (float) intrinsics.fy;
			c._K1 = (float) intrinsics.k1;
			c._K2 = (float) intrinsics.k2;
			c._K3 = (float) intrinsics.k3;
			c._K4 = (float) intrinsics.k4;
			c._K5 = (float) intrinsics.k5;
			c._K6 = (float) intrinsics.k6;
			c._P1 = (float) intrinsics.p1;
			c._P2 = (float) intrinsics.p2;
			c._M01 = camMatrix.m01;
			c._M11 = camMatrix.m11;
			c._M21 = camMatrix.m21;
			c._M02 = camMatrix.m02;
			c._M12 = camMatrix.m12;
			c._M22 = camMatrix.m22;
			c._M00 = camMatrix.m00;
			c._M10 = camMatrix.m10;
			c._M20 = camMatrix.m20;

			int stride = Marshal.SizeOf( typeof( Constants ) );
			_constantBuffer = new ComputeBuffer( count: 1, stride, ComputeBufferType.Constant );
			_constantBuffer.SetData( new Constants[]{ c } );
			_material.SetConstantBuffer( ShaderIDs._Constants, _constantBuffer, offset: 0, _constantBuffer.stride );

			Graphics.Blit( null, _undistortionMap, _material, _GenerateMapKernel );

			_material.SetTexture( ShaderIDs._UndistMapTex, _undistortionMap );
		}

		/*
		public void Undistort( RenderTexture distortedTexture, bool flipY = false )
		{
			if( _material.IsKeywordEnabled( _FLIP_Y ) != flipY ) _material.SetKeyword( _FLIP_Y, flipY );
			RenderTexture tempTexture = RenderTexture.GetTemporary( distortedTexture.descriptor );
			Graphics.Blit( distortedTexture, tempTexture, _material, _UndistortKernel );
			Graphics.CopyTexture( tempTexture, distortedTexture );
			RenderTexture.active = null;
			RenderTexture.ReleaseTemporary( tempTexture );
		}
		*/


		/// <summary>
		/// Undistort a lens distorted image.
		/// </summary>
		public void Undistort( Texture distortedTexture, RenderTexture undistortedTexture, bool preFlipY = false, bool postFlipY = false )
		{
			if( _material.IsKeywordEnabled( _PRE_FLIP_Y ) != preFlipY ) _material.SetKeyword( _PRE_FLIP_Y, preFlipY );
			if( _material.IsKeywordEnabled( _POST_FLIP_Y ) != postFlipY ) _material.SetKeyword( _POST_FLIP_Y, postFlipY );
			Graphics.Blit( distortedTexture, undistortedTexture, _material, _UndistortKernel );

			// Always flip after.
			// ...
		}


		public void Release()
		{
			_undistortionMap?.Release();
			_constantBuffer?.Release();
			if( _material ) Object.Destroy( _material );
		}

		/// <summary>
		/// Compute lens distortion value for pixel, using OpenCV's initUndistortRectifyMap implementation.
		/// </summary>
		/// <param name="x">Pixel position to remap</param>
		/// <param name="y">Pixel position to remap</param>
		/// <param name="cx">Prinicple point (optical center)</param>
		/// <param name="cy">Prinicple point (optical center)</param>
		/// <param name="fx">Prinicple point (optical center)</param>
		/// <param name="fy">Prinicple point (optical center)</param>
		/// <param name="k1">Radial distortion</param>
		/// <param name="k2">Radial distortion</param>
		/// <param name="k3">Radial distortion</param>
		/// <param name="k4">Radial distortion</param>
		/// <param name="k5">Radial distortion</param>
		/// <param name="k6">Radial distortion</param>
		/// <param name="p1">Tangential distortion</param>
		/// <param name="p2">Tangential distortion</param>
		/// <returns>Distorted pixel position.</returns>
		public static Vector2 ReferenceImplementation( float px, float py, float cx, float cy, float fx, float fy, float k1, float k2, float k3, float k4, float k5, float k6, float p1, float p2 )
		{
			// https://github.com/egonSchiele/OpenCV/blob/master/modules/imgproc/src/undistort.cpp
			// https://amroamroamro.github.io/mexopencv/matlab/cv.initUndistortRectifyMap.html
			//Simplex.Matrix3x3 camMatrix = new Simplex.Matrix3x3(
			//	fx, 0f, cx,
			//	0f, fy, cy,
			//	0f, 0f, 1f );
			//camMatrix.Invert();
			Matrix4x4 camMatrix = new Matrix4x4(
				new Vector4( fx, 0f, 0, 0f ),
				new Vector4( 0f, fy, 0f, 0f ),
				new Vector4( cx, cy, 1f, 0f ),
				new Vector4( 0f, 0f, 0f, 1f )
			);
			camMatrix = camMatrix.inverse;

			float _x = py * camMatrix.m01 + camMatrix.m02 + px * camMatrix.m00;
			float _y = py * camMatrix.m11 + camMatrix.m12 + px * camMatrix.m10;
			float _w = py * camMatrix.m21 + camMatrix.m22 + px * camMatrix.m20;

			float w = 1f / _w;
			float x = _x * w;
			float y = _y * w;
			float x2 = x * x;
			float y2 = y * y;
			float r2 = x2 + y2;
			float _2xy = 2 * x * y;
			float kr = ( 1f + ( ( k3 * r2 + k2 ) * r2 + k1 ) * r2 ) / ( 1f + ( ( k6*r2 + k5 ) * r2 + k4 ) * r2 );
			float u = fx * ( x * kr + p1 * _2xy + p2 * ( r2 + 2 * x2 ) ) + cx;
			float v = fy * ( y * kr + p1 * (r2 + 2 * y2 ) + p2 * _2xy ) + cy;
			return new Vector2( u, v );
		}
	}
}