/*
	Copyright © Carl Emil Carlsen 2020-2025
	http://cec.dk

	To compute the intrinsics of a camera you need multiple pairs of sampled image space and real space pattern points.
	Either the image space points or the real space points need to change for each sample.
	Image space is measured in pixels and "real space" is measured in millimeters.
*/

using UnityEngine;
using System.Collections.Generic;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Calib3dModule;

namespace TrackingTools
{
	public class CalibrateCameraOperation
	{
		Size _resolutionSize;
		List<Mat> _patternRealSamples;
		List<Mat> _patternImageSamples;

		Mat _sensorResultMat;
		MatOfDouble _distortionCoeffsResultMat;
		List<Mat> _rotationResults;
		List<Mat> _translationResults;
		Intrinsics _intrinsicsResult;
		List<Extrinsics> _extrinsicsResults;
		float _rmsErrorResult = 1;
		bool _hasResult = false;

		public Size textureSize => _resolutionSize;
		public int resolutionX => (int) _resolutionSize.width;
		public int resolutionY => (int) _resolutionSize.height;
		public Vector2Int resolution => new Vector2Int( (int) _resolutionSize.width, (int) _resolutionSize.height );
		public int sampleCount => _patternRealSamples.Count;
		
		public bool hasResult => _hasResult;
		public Mat sensorResultMat => _sensorResultMat;
		public MatOfDouble distortionCoeffsREsultMat => _distortionCoeffsResultMat;
		public Intrinsics intrinsicsResult => _intrinsicsResult;
		public List<Extrinsics> extrinsicsResults => _extrinsicsResults;
		public float rmsErrorResult => _rmsErrorResult;



		public CalibrateCameraOperation( Vector2Int resolution )
		{
			const int defaultSampleCount = 4; // Typical for camera calibration.

			_intrinsicsResult = new Intrinsics();
			_patternImageSamples = new List<Mat>( defaultSampleCount );
			_patternRealSamples = new List<Mat>( defaultSampleCount );
			_distortionCoeffsResultMat = new MatOfDouble();
			_rotationResults = new List<Mat>( defaultSampleCount );
			_translationResults = new List<Mat>( defaultSampleCount );
			_sensorResultMat = Mat.eye( 3, 3, CvType.CV_64FC1 );
			_resolutionSize = new Size( resolution.x, resolution.y );
			_extrinsicsResults = new List<Extrinsics>();
		}


		/// <summary>
		/// Add a pair of real space + image space points.
		/// Beware that calibration can fail if pattern is not rotated to face forward, so that z is zero.
		/// Also ensure that the point order in the the two point sets are matching.
		/// </summary>
		/// <param name="patternRealModelSample">Must be measured in millimeters</param>
		/// <param name="patternImageSample"></param>
		public void AddSample( MatOfPoint3f patternRealModelSample, MatOfPoint2f patternImageSample )
		{
			//Debug.Log( "patternRealModelSample\n" + patternRealModelSample.dump() );
			//Debug.Log( "patternImageSample\n" + patternImageSample.dump() );

			_patternRealSamples.Add( patternRealModelSample.clone() );
			_patternImageSamples.Add( patternImageSample.clone() );
		}


		public void ClearSamples()
		{
			_patternRealSamples.Clear();
			_patternImageSamples.Clear();
		}


		public void SetResolution( Vector2Int resolution )
		{
			_resolutionSize = new Size( resolution.x, resolution.y );
		}


		public bool Update( bool samplesHaveDistortion = true, bool useAspect = false, Intrinsics intrinsicGuess = null )
		{
			if( _patternRealSamples.Count == 0 ||  _patternImageSamples.Count == 0 ) return false;

			int flags = 0;

			// This is useful in the case of a projector where the incoming points are already undistorted and we can asume no distortion in the view frustrum.
			if( !samplesHaveDistortion ) {
				flags = 
					Calib3d.CALIB_FIX_TANGENT_DIST |
					Calib3d.CALIB_FIX_K1 |
					Calib3d.CALIB_FIX_K2 |
					Calib3d.CALIB_FIX_K3 |
					Calib3d.CALIB_FIX_K4 |
					Calib3d.CALIB_FIX_K5;
			}

			// This is useful in case of projectors.
			if( useAspect ) flags |= Calib3d.CALIB_FIX_ASPECT_RATIO;

			if( intrinsicGuess != null)
			{
				flags |= Calib3d.CALIB_USE_INTRINSIC_GUESS;
				intrinsicGuess.ApplyToOpenCV( ref _sensorResultMat );
			}

			// NOTE ON ERROR: CvType.CV_32SC2 != m.type() ||  m.cols()!=1 in calibrateCamera() because rvecs.rows() == 0
			// This is what happens when calibrateCamera fails to find a solution. The reasons could be:
			// - Not enough points.
			// - Symmetric point pattern.
			// - Invalid combination of flags and perhaps input.

			// Example: https://forum.unity.com/threads/released-opencv-for-unity.277080/page-8#post-2348856
			//var terminationCriteria = new TermCriteria( TermCriteria.EPS + TermCriteria.MAX_ITER, 60, 0.001 );
			_rmsErrorResult = (float) Calib3d.calibrateCamera(
				_patternRealSamples, _patternImageSamples, _resolutionSize,
				_sensorResultMat, _distortionCoeffsResultMat, _rotationResults, _translationResults, // Out.
				flags //, terminationCriteria // Using termination critera screws up projector calibration.
			);

			// About RMS Error
			// It's the average re-projection error. This number gives a good estimation of precision of the found parameters. 
			// This should be as close to zero as possible. Given the intrinsic, distortion, rotation and translation matrices 
			// we may calculate the error for one view by using the projectPoints to first transform the object point to image 
			// point. Then we calculate the absolute norm between what we got with our transformation and the corner/circle 
			// finding algorithm. To find the average error we calculate the arithmetical mean of the errors calculated for 
			// all the calibration images.
			// https://docs.opencv.org/2.4/doc/tutorials/calib3d/camera_calibration/camera_calibration.html

			// Update Unity friendly objects.
			_intrinsicsResult.UpdateFromOpenCV( _sensorResultMat, _distortionCoeffsResultMat, new Vector2Int( resolutionX, resolutionY ), _rmsErrorResult );
			_extrinsicsResults.Clear();
			for( int i = 0; i < _patternRealSamples.Count; i++ ){
				var extrinsics = new Extrinsics();
				extrinsics.UpdateFromOpenCv( _rotationResults[i], _translationResults[i] );
				_extrinsicsResults.Add( extrinsics );
			}

			_hasResult = true;
			return true;
		}


		public void Release()
		{
			foreach( Mat mat in _patternImageSamples ) mat.release();
			foreach( Mat mat in _patternRealSamples ) mat.release();
			foreach( Mat mat in _rotationResults ) mat.release();
			foreach( Mat mat in _translationResults ) mat.release();
			_patternImageSamples.Clear();
			_patternRealSamples.Clear();
			_rotationResults.Clear();
			_translationResults.Clear();
		}
	}

}