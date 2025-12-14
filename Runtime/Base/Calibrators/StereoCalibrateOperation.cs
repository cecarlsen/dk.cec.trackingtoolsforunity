/*
	Copyright © Carl Emil Carlsen 2020-2024
	http://cec.dk

	To compute the intrinsics of a camera you need multiple pairs of sampled image space and real space pattern points.
	Either the image space points or the real space points need to change for each sample.
	Image space is measured in pixels and "real space" is measured in millimeters.
*/

using System.Collections.Generic;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Calib3dModule;

namespace TrackingTools
{
	public class StereoCalibrateOperation
	{
		Mat _sensorMatA;
		Mat _sensorMatB;
		MatOfDouble _noDistCoeffs;
		List<Mat> _patternWorldSamples;
		List<Mat> _cameraPatternImageSamplesA;
		List<Mat> _cameraPatternImageSamplesB;
		Mat _rotation3x3Mat;
		Mat _translationVecMat;
		Mat _essentialMat;
		Mat _fundamentalMat;
		Size _zeroSize;
		Extrinsics _extrinsics;

		public int sampleCount { get { return _cameraPatternImageSamplesA.Count; } }
		public Extrinsics extrinsics { get { return _extrinsics; } }


		public StereoCalibrateOperation()
		{
			const int regularSampleCount = 8;

			_sensorMatA = new Mat();
			_sensorMatB = new Mat();
			_noDistCoeffs = new MatOfDouble( new double[ 5 ] );
			_patternWorldSamples = new List<Mat>( regularSampleCount );
			_cameraPatternImageSamplesB = new List<Mat>( regularSampleCount );
			_cameraPatternImageSamplesA = new List<Mat>( regularSampleCount );
			_rotation3x3Mat = new Mat();
			_translationVecMat = new Mat();
			_essentialMat = new Mat();
			_fundamentalMat = new Mat();
			_extrinsics = new Extrinsics();
			_zeroSize = new Size();
		}


		/// <summary>
		/// Add a pair of real space + image space points. Points must be undistorted. THe Mat's are cloned.
		/// </summary>
		/// <param name="patternRealSample">Must be measured in millimeters</param>
		/// <param name="patternImageSample"></param>
		public void AddSample( MatOfPoint3f patternWorldSample, MatOfPoint2f cameraPatternImageSampleA, MatOfPoint2f cameraPatternImageSampleB )
		{
			_patternWorldSamples.Add( patternWorldSample.clone() );
			_cameraPatternImageSamplesA.Add( cameraPatternImageSampleA.clone() );
			_cameraPatternImageSamplesB.Add( cameraPatternImageSampleB.clone() );
		}


		public void RemovePreviousSample()
		{
			int index = _patternWorldSamples.Count - 1;
			_patternWorldSamples.RemoveAt( index );
			_cameraPatternImageSamplesA.RemoveAt( index );
			_cameraPatternImageSamplesB.RemoveAt( index );
		}


		/// <summary>
		/// Update the extrinsics of camera B relative to camera A.
		/// </summary>
		public void Update( Intrinsics cameraA, Intrinsics cameraB )
		{
			cameraA.ApplyToOpenCV( ref _sensorMatA );
			cameraB.ApplyToOpenCV( ref _sensorMatB );

			// In order to match OpenCV's pixel space (zero at top-left) and Unity's camera space (up is positive), we flip the sensor matrix vertically.
			_sensorMatA.WriteValue( - _sensorMatA.ReadValue( 1, 1 ), 1, 1 ); // fy
			_sensorMatB.WriteValue( - _sensorMatB.ReadValue( 1, 1 ), 1, 1 ); // fy

			// Set flags.
			int flag = 0;
			flag |= Calib3d.CALIB_FIX_INTRINSIC;	// Don't recompute and change intrinsics parameters.
			flag |= Calib3d.CALIB_FIX_PRINCIPAL_POINT | Calib3d.CALIB_FIX_FOCAL_LENGTH | Calib3d.CALIB_FIX_ASPECT_RATIO; // These should probably also be fixed.
			flag |=									// Don't recompute distortions, ignore them. We assume the incoming points have already bee undistorted.
				Calib3d.CALIB_FIX_TANGENT_DIST |
				Calib3d.CALIB_FIX_K1 |
				Calib3d.CALIB_FIX_K2 |
				Calib3d.CALIB_FIX_K3 |
				Calib3d.CALIB_FIX_K4 |
				Calib3d.CALIB_FIX_K5;

			// Compute!
			Calib3d.stereoCalibrate
			(
				_patternWorldSamples, _cameraPatternImageSamplesA, _cameraPatternImageSamplesB,
				_sensorMatA, _noDistCoeffs,
				_sensorMatB, _noDistCoeffs,
				_zeroSize, // This is fine. Texture size is ignored when using CALIB_FIX_INTRINSIC https://stackoverflow.com/questions/35128281/different-image-size-opencv-stereocalibrate
				_rotation3x3Mat, _translationVecMat, _essentialMat, _fundamentalMat,
				flag
			);
			
			_extrinsics.UpdateFromOpenCvStereoCalibrate( _rotation3x3Mat, _translationVecMat );
		}
		

		public void Release()
		{
			foreach( Mat mat in _patternWorldSamples ) mat.release();
			foreach( Mat mat in _cameraPatternImageSamplesB ) mat.release();
			foreach( Mat mat in _cameraPatternImageSamplesA ) mat.release();
			_patternWorldSamples.Clear();
			_cameraPatternImageSamplesB.Clear();
			_cameraPatternImageSamplesA.Clear();
			_sensorMatA.release();
			_sensorMatB.release();
			_noDistCoeffs.release();
			_rotation3x3Mat.release();
			_translationVecMat.release();
			_essentialMat.release();
			_fundamentalMat.release();
		}
	}
}