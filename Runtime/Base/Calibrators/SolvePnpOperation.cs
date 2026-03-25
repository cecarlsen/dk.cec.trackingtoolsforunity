/*
	Copyright © Carl Emil Carlsen 2020-2025
	http://cec.dk
*/

using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Calib3dModule;
using UnityEngine;

namespace TrackingTools
{
	public class SolvePnpOperation
	{
		Extrinsics _extrinsics;

		Mat _sensorMatrix;
		MatOfDouble _noDistCoeffs;

		Point3[] _worldPoints;
		Point[] _imagePoints;
		MatOfPoint3f _worldPointsMat;
		MatOfPoint2f _imagePointsMat;

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


		public bool UpdateExtrinsics( MatOfPoint3f pointsWorldMat, MatOfPoint2f pointsImageMat, Intrinsics intrinsics )
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
				pointsWorldMat, pointsImageMat, _sensorMatrix, _noDistCoeffs,
				_rotationVecMat, _translationVecMat // Out.
			);

			if( _isValid ) {
				_extrinsics.UpdateFromOpenCv( _rotationVecMat, _translationVecMat );
			} else {
				Debug.LogWarning( "Calib3d.solvePnP failed\n" );
			}

			return _isValid;
		}


		public bool UpdateExtrinsics( Vector3[] pointsWorld, Vector2[] pointsImage, Intrinsics intrinsics, float physicalScale = 1f )
		{
			// Check.
			int pointCount = pointsWorld.Length;
			if( pointCount != pointsImage.Length ){
				Debug.Log( "Abort. Array lengths must match!\n");
				return false;
			}

			// Ensure CV resources.
			if( _imagePoints == null || _imagePoints.Length != pointCount )
			{
				_worldPointsMat?.release();
				_imagePointsMat?.release();
				_worldPoints = new Point3[pointCount];
				_imagePoints = new Point[pointCount];
				_worldPointsMat = new MatOfPoint3f();
				_imagePointsMat = new MatOfPoint2f();
				_worldPointsMat.alloc( pointCount );
				_imagePointsMat.alloc( pointCount );
				for( int p = 0; p < pointCount; p++ ) {
					_worldPoints[p] = new Point3();
					_imagePoints[p] = new Point();
				}
			}

			// Copy and scale.
			for( int p = 0; p < pointCount; p++ )
			{
				Vector3 posWorld = pointsWorld[ p ] * physicalScale;
				_worldPoints[ p ].set( new double[]{ posWorld.x, posWorld.y, posWorld.z } );
				Vector2 posImage = pointsImage[ p ];
				_imagePoints[ p ].set( new double[]{ posImage.x, posImage.y } );
			}
			_worldPointsMat.fromArray( _worldPoints );
			_imagePointsMat.fromArray( _imagePoints );

			// Update intrinsics. OpenCV's solvePnP will find a result in OpenCV camera space, where Y is flipped. So to compute the result for Unity we flip the sensor matrix vertically.
			intrinsics.ApplyToOpenCV( ref _sensorMatrix );
			_sensorMatrix.WriteValue( - _sensorMatrix.ReadValue( 1, 1 ), 1, 1 ); // fy

			// Find pattern pose, relative to camera (at zero position) using solvePnP.
			_isValid = Calib3d.solvePnP(
				_worldPointsMat, _imagePointsMat, _sensorMatrix, _noDistCoeffs,
				_rotationVecMat, _translationVecMat // Out.
			);

			if( _isValid ) {
				// Update extrinsics and apply optional scale.
				_extrinsics.UpdateFromOpenCv( _rotationVecMat, _translationVecMat, physicalScale );
			} else {
				Debug.LogWarning( "Calib3d.solvePnP failed\n" );
			}

			return _isValid;
		}


		public void Release()
		{
			_noDistCoeffs.release();
			_rotationVecMat.release();
			_translationVecMat.release();
			_worldPointsMat?.release();
			_imagePointsMat?.release();
		}
	}
}