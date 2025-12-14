/*
	Copyright © Carl Emil Carlsen 2020-2025
	http://cec.dk
*/

using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Calib3dModule;

namespace TrackingTools
{
	public class SolvePnpOperation
	{
		Extrinsics _extrinsics;

		Mat _sensorMatrix;
		MatOfDouble _noDistCoeffs;

		Mat _rotationVecMat;
		Mat _translationVecMat;

		bool _isValid;

		public Extrinsics extrinsics { get { return _extrinsics; } }
		public bool isValid { get { return _isValid; } }


		public SolvePnpOperation()
		{
			_noDistCoeffs = new MatOfDouble( new double[5] );
			_rotationVecMat = new Mat();
			_translationVecMat = new Mat();
			_extrinsics = new Extrinsics();
		}


		public bool UpdateExtrinsics( MatOfPoint3f patternPointsWorldMat, MatOfPoint2f patternPointsImageMat, Intrinsics intrinsics )
		{

			// OpenCV's solvePnP will find a result in OpenCV camera space, where Y is flipped. So to compute the result for Unity we flip the sensor matrix vertically.
			intrinsics.ApplyToOpenCV( ref _sensorMatrix );
			_sensorMatrix.WriteValue( - _sensorMatrix.ReadValue( 1, 1 ), 1, 1 ); // fy
			//_sensorMatrix.WriteValue( - _sensorMatrix.ReadValue( 0, 0 ), 0, 0 ); // fx
			//_sensorMatrix.WriteValue( intrinsics.width - _sensorMatrix.ReadValue( 0, 2 ), 0, 2 ); // cx
			

			//UnityEngine.Debug.Log( "_sensorMatrix:\n" + _sensorMatrix.dump() );
			//UnityEngine.Debug.Log( "patternPointsWorldMat:\n" + patternPointsWorldMat.dump() );
			//UnityEngine.Debug.Log( "patternPointsImageMat:\n" + patternPointsImageMat.dump() );

			// Find pattern pose, relative to camera (at zero position) using solvePnP.
			_isValid = Calib3d.solvePnP(
				patternPointsWorldMat, patternPointsImageMat, _sensorMatrix, _noDistCoeffs,
				_rotationVecMat, _translationVecMat // Out.
			);

			if( _isValid ) {
				_extrinsics.UpdateFromOpenCv( _rotationVecMat, _translationVecMat );
			} else {
				UnityEngine.Debug.LogWarning( "Calib3d.solvePnP failed\n" );
			}

			return _isValid;
		}


		public void Release()
		{
			_noDistCoeffs.release();
			_rotationVecMat.release();
			_translationVecMat.release();
		}
	}
}