﻿/*
	Copyright © Carl Emil Carlsen 2020-2022
	http://cec.dk

	Camera intriniscs values stored as defined by OpenCV.

	Beware that values are NOT independent from aspect.
	Imagine you crop a 16:9 camera to 4:3. This will most
	certainly change the distortion coefficents.

	OpenCV camera intrinsics matrix
	
		[ fx,  0, cx ]
		[  0, fy, cy ]
		[  0,  0,  1 ]
		
		https://docs.opencv.org/4.x/dc/dbb/tutorial_py_calibration.html
*/

using System.IO;
using UnityEngine;
using OpenCVForUnity.CoreModule;

namespace TrackingTools
{
	[System.Serializable]
	public class Intrinsics
	{
		[SerializeField] double _cx, _cy;
		[SerializeField] double _fx, _fy;
		[SerializeField] double[] _distortionCoeffs; // Radial and tangential distortion coefficients as defined by OpenCV: [k1,k2,p1,p2,k3,k4,k5,k6,s1,s2,s3,s4,taux,tauy] of 4, 5, 8, 12 or 14 elements.
		[SerializeField] Vector2Int _referenceResolution;
		[SerializeField] double _rmsError; // Root mean square error.


		const int defaultDistortionCoeffCount = 5;

		/// <summary>
		/// The horizontal principal point (px, optical center) as defined by OpenCV; an offset measure in pixels from upper-left corner (right is positive).
		/// If camera sensor is placed perfectly on-axis the value will be: ( referenceResolution.x * 0.5 ).
		/// Lens shift can be derived: ( shiftX = 0.5 - cx / referenceResolution.x )
		/// </summary>
		public double cx => _cx;

		/// <summary>
		/// The vertical principal point (py, optical center) as defined by OpenCV; an offset measure in pixels from upper-left corner (down is positive).
		/// If camera sensor is placed perfectly on-axis the value will be: ( referenceResolution.y * 0.5 ).
		/// Lens shift can be derived: ( shiftX = 0.5 - cx / referenceResolution.y )
		/// </summary>
		public double cy => _cy;

		/// <summary>
		/// Horizontal focal length as defined by OpenCV.
		/// Product of the physical focal length of the lens (F measured in mm) and the horizontal size of the (potentially non-square) individual imager elements (sx measured in px/mm). Equation: ( fx = F * sx ).
		/// </summary>
		public double fx => _fx;

		/// <summary>
		/// Vertical focal length as defined by OpenCV.
		/// Product of the physical focal length of the lens (F measured in mm) and the horizontal size of the (potentially non-square) individual imager elements (sx measured in px/mm). Equation: ( fy = F * sy ).
		/// </summary>
		public double fy => _fy;

		/// <summary>
		/// Radial distortion K1.
		/// </summary>
		public double k1 => _distortionCoeffs?.Length > 0 ? _distortionCoeffs[ 0 ] : 0.0;

		/// <summary>
		/// Radial distortion K1.
		/// </summary>
		public double k2 => _distortionCoeffs?.Length > 1 ? _distortionCoeffs[ 1 ] : 0.0;

		/// <summary>
		/// Radial distortion K3.
		/// </summary>
		public double k3 => _distortionCoeffs?.Length > 4 ? _distortionCoeffs[ 4 ] : 0.0;

		/// <summary>
		/// Radial distortion K4.
		/// </summary>
		public double k4 => _distortionCoeffs?.Length > 5 ? _distortionCoeffs[ 5 ] : 0.0;

		/// <summary>
		/// Radial distortion K5.
		/// </summary>
		public double k5 => _distortionCoeffs?.Length > 6 ? _distortionCoeffs[ 6 ] : 0.0;

		/// <summary>
		/// Radial distortion K6.
		/// </summary>
		public double k6 => _distortionCoeffs?.Length > 7 ? _distortionCoeffs[ 7 ] : 0.0;

		/// <summary>
		/// Tangential distortion P1.
		/// </summary>
		public double p1 => _distortionCoeffs?.Length > 2 ? _distortionCoeffs[ 2 ] : 0.0;

		/// <summary>
		/// Tangential distortion P1.
		/// </summary>
		public double p2 => _distortionCoeffs?.Length > 3 ? _distortionCoeffs[ 3 ] : 0.0;

		/// <summary>
		/// Reference image pixel resolution. The values of cx, cy, fx, fy must be relative to this.
		/// </summary>
		public Vector2Int referenceResolution => _referenceResolution;

		public int width => _referenceResolution.x;
		public int height => _referenceResolution.y;
		public float aspect => _referenceResolution.x / (float) _referenceResolution.y;

		/// <summary>
		/// Returns false if the Json file failed to load.
		/// </summary>
		public bool isValid => _distortionCoeffs != null; // If json deserialization failed _distortionCoeffs will be null.
		
		/// <summary>
		/// Lens shift value, as applied to Unity cameras.
		/// </summary>
		public Vector2 lensShiftNormalized =>
			new Vector2(
				(float) -( ( _cx / (double) _referenceResolution.x ) - 0.5 ),
				(float) ( ( _cy / (double) _referenceResolution.y ) - 0.5 )
			);

		/// <summary>
		/// Vertical field of view (fov) angle in degrees as applied to Unity cameras.
		/// </summary>
		public float verticalFieldOfView => Mathf.Atan2( (float) _cy, _referenceResolution.y ) * 2f * Mathf.Rad2Deg;


		static readonly string logPrepend = "<b>[" + nameof( Intrinsics ) + "]</b> ";
		



		public string SaveToFile( string fileName )
		{
			if( !Directory.Exists( TrackingToolsConstants.intrinsicsDirectoryPath ) ) Directory.CreateDirectory( TrackingToolsConstants.intrinsicsDirectoryPath );
			string filePath = TrackingToolsConstants.intrinsicsDirectoryPath + "/" + fileName;
			if( !fileName.EndsWith( ".json" ) ) filePath += ".json";
			File.WriteAllText( filePath, JsonUtility.ToJson( this ) );
			return filePath;
		}
		

		public static bool TryLoadFromFile( string fileName, out Intrinsics intrinsics )
		{
			intrinsics = null;

			if( !Directory.Exists( TrackingToolsConstants.intrinsicsDirectoryPath ) ) {
				Debug.LogError( logPrepend + "Directory missing.\n" + TrackingToolsConstants.intrinsicsDirectoryPath );
				return false;
			}

			string filePath = TrackingToolsConstants.intrinsicsDirectoryPath + "/" + fileName;
			if( !fileName.EndsWith( ".json" ) ) filePath += ".json";
			if( !File.Exists( filePath ) ) {
				Debug.LogError( logPrepend + "File missing.\n" + filePath );
				return false;
			}

			string jsonText = File.ReadAllText( filePath );
			intrinsics = JsonUtility.FromJson<Intrinsics>( jsonText );

			if( !intrinsics.isValid ) {
				Debug.LogError( logPrepend + "Failed to load json text:\n" + jsonText );
				return false;
			}

			return true;
		}


		public void UpdateFromOpenCV( Mat cameraIntrinsicsMat, MatOfDouble distCoeffsMat, Vector2Int referenceResolution, float rmsError )
		{
			if( _distortionCoeffs == null || distCoeffsMat.IsDisposed || _distortionCoeffs.Length != distCoeffsMat.total() ){
				_distortionCoeffs = new double[ distCoeffsMat.total() ];
			}

			_referenceResolution = referenceResolution;

			UpdateFromOpenCVCameraIntrinsicsMatrix( cameraIntrinsicsMat );

			for( int i = 0; i < _distortionCoeffs.Length; i++ ) _distortionCoeffs[ i ] = distCoeffsMat.ReadValue( i );

			_rmsError = rmsError;
		}


		public void UpdateFromOpenCV( Mat cameraIntrinsicsMat, Vector2Int referenceResolution, float rmsError )
		{
			_referenceResolution = referenceResolution;

			UpdateFromOpenCVCameraIntrinsicsMatrix( cameraIntrinsicsMat );

			_rmsError = rmsError;
		}


		public void UpdateRaw( int resX, int resY, double cx, double cy, double fx, double fy, double[] distortionCoeffs = null )
		{
			_referenceResolution = new Vector2Int( resX, resY );

			_cx = cx;
			_cy = cy;
			_fx = fx;
			_fy = fy;

			_distortionCoeffs = distortionCoeffs == null ? new double[ defaultDistortionCoeffCount ] : distortionCoeffs; // No distortion.

			_rmsError = 0; // No error.
		}


		public void UpdateRaw( int resX, int resY, double cx, double cy, double fx, double fy, double k1, double k2, double k3, double p1, double p2 )
		{
			_referenceResolution = new Vector2Int( resX, resY );

			_cx = cx;
			_cy = cy;
			_fx = fx;
			_fy = fy;

			_distortionCoeffs = new double[]{ k1, k2, p1, p2, k3 };

			_rmsError = 0; // No error.
		}


		public void UpdateRaw( int resX, int resY, double cx, double cy, double fx, double fy, double k1, double k2, double k3, double k4, double k5, double k6, double p1, double p2 )
		{
			_referenceResolution = new Vector2Int( resX, resY );

			_cx = cx;
			_cy = cy;
			_fx = fx;
			_fy = fy;

			_distortionCoeffs = new double[]{ k1, k2, p1, p2, k3, k4, k5, k6 };

			_rmsError = 0; // No error.
		}


		public bool ApplyToToOpenCV( ref Mat cameraIntrinsicsMat, ref MatOfDouble distCoeffsMat )
		{
			if( cameraIntrinsicsMat == null || cameraIntrinsicsMat.IsDisposed || cameraIntrinsicsMat.rows() != 3 || cameraIntrinsicsMat.cols() != 3 ){
				cameraIntrinsicsMat = Mat.eye( 3, 3, CvType.CV_64F );
			}
			if( distCoeffsMat == null || distCoeffsMat.IsDisposed || distCoeffsMat.total() != defaultDistortionCoeffCount ) {
				distCoeffsMat = new MatOfDouble( new Mat( 1, _distortionCoeffs.Length, CvType.CV_64F ) ); // This seems to be the only way to get distCoeffs.Length columns.
			}

			ApplyToOpenCVCameraIntrinsicsMatrix( cameraIntrinsicsMat );

			for( int i = 0; i < _distortionCoeffs.Length; i++ ) distCoeffsMat.WriteValue( _distortionCoeffs[ i ], i );

			return true;
		}


		public bool ApplyToToOpenCV( ref Mat cameraIntrinsicsMat )
		{
			if( cameraIntrinsicsMat == null || cameraIntrinsicsMat.IsDisposed || cameraIntrinsicsMat.rows() != 3 || cameraIntrinsicsMat.cols() != 3 ) {
				cameraIntrinsicsMat = Mat.eye( 3, 3, CvType.CV_64F );
			}

			ApplyToOpenCVCameraIntrinsicsMatrix( cameraIntrinsicsMat );

			return true;
		}


		public void UpdateFromUnityCamera( Camera cam )
		{
			if( cam.orthographic ){
				Debug.LogError( logPrepend + " UpdateFromUnityCamera failed. Camera cannot be orthographic.\n" );
				return;
			}

			if( !cam.usePhysicalProperties ){
				Debug.LogError( logPrepend + " UpdateFromUnityCamera failed. Camera must use physical properties.\n" );
				return;
			}

			cam.gateFit = Camera.GateFitMode.None;

			_referenceResolution = new Vector2Int( cam.pixelWidth, cam.pixelHeight );

			_cx = ( -cam.lensShift.x + 0.5f ) * _referenceResolution.x;
			_cy = (  cam.lensShift.y + 0.5f ) * _referenceResolution.y;
			_fx = ( cam.focalLength / cam.sensorSize.x ) * _referenceResolution.x;
			_fy = ( cam.focalLength / cam.sensorSize.y ) * _referenceResolution.y;

			_distortionCoeffs = new double[ defaultDistortionCoeffCount ]; // No distortion.

			_rmsError = 0; // No error.
		}


		public void ApplyToUnityCamera( Camera cam )
		{
			// Great explanation by jungguswns:
			// https://forum.unity.com/threads/how-to-use-opencv-camera-calibration-to-set-physical-camera-parameters.704120/

			// Also, about sensor size and focal lengths.
			// https://answers.opencv.org/question/139166/focal-length-from-calibration-parameters/

			cam.orthographic = false;
			cam.usePhysicalProperties = true;
			cam.gateFit = Camera.GateFitMode.None;

			// Keep the current focal length. F can be arbitrary, as long as sensor size is resized to to make fx and fy consistient
			float focalLength = cam.focalLength;

			cam.lensShift =  new Vector2(
				(float) - ( ( _cx / (double) _referenceResolution.x ) - 0.5 ), 
				(float)   ( ( _cy / (double) _referenceResolution.y ) - 0.5 )
			);
			cam.sensorSize = new Vector2(
				(float) ( focalLength * _referenceResolution.x / _fx ),
				(float) ( focalLength * _referenceResolution.y / _fy )
			);
		}


		void UpdateFromOpenCVCameraIntrinsicsMatrix( Mat cameraIntrinsicsMat )
		{
			_fx = cameraIntrinsicsMat.ReadValue( 0, 0 );
			_fy = cameraIntrinsicsMat.ReadValue( 1, 1 );
			_cx = cameraIntrinsicsMat.ReadValue( 0, 2 );
			_cy = cameraIntrinsicsMat.ReadValue( 1, 2 );
		}


		void ApplyToOpenCVCameraIntrinsicsMatrix( Mat cameraIntrinsicsMat )
		{
			cameraIntrinsicsMat.WriteValue( _fx, 0, 0 );
			cameraIntrinsicsMat.WriteValue( _fy, 1, 1 );
			cameraIntrinsicsMat.WriteValue( _cx, 0, 2 );
			cameraIntrinsicsMat.WriteValue( _cy, 1, 2 );
		}


		public override string ToString()
		{
			if( !isValid ) return "Invalid";
			string text =  "(cx,cy,fx,fy): ( " + _cx + ", " + _cy + ", " + _fx + ", " + _fy + " )";
			text += " dist: ( " + string.Join( ',', _distortionCoeffs ) + " ).";
			return text;
		}
	}
}