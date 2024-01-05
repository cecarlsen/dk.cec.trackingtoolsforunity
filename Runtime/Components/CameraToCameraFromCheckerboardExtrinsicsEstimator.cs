/*
	Copyright © Carl Emil Carlsen 2024
	http://cec.dk
*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Experimental.Rendering;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Calib3dModule;
using OpenCVForUnity.ImgprocModule;


namespace TrackingTools
{
	/// <summary>
	/// Finds the relative extrinsics between camera A and camera B using a checkerboard as shared reference.
	/// Also known as stereo calibration.
	/// </summary>
	public class CameraToCameraFromCheckerboardExtrinsicsEstimator : MonoBehaviour
	{
		[SerializeField] Camera _targetCameraA = null;
		[SerializeField] Camera _targetCameraB = null;

		[Header("Input")]
		[SerializeField] Texture _cameraSourceTextureA = null;
		[SerializeField] Texture _cameraSourceTextureB = null;
		[SerializeField] bool _flipSourceTextureAVertically = false;
		[SerializeField] bool _flipSourceTextureBVertically = false;
		[SerializeField] string _intrinsicsAFileName = "DefaultCameraA";
		[SerializeField] string _intrinsicsBFileName = "DefaultCameraB";
		[SerializeField] Checkerboard _checkerboard = null;
		[SerializeField,Tooltip("Only use when you cannot control lighting conditions.")] bool _normalizeSourceATexture = false;
		[SerializeField,Tooltip("Only use when you cannot control lighting conditions.")] bool _normalizeSourceBTexture = false;

		[Header("Options")]
		[SerializeField] bool _fastAndImprecise = false;

		[Header("Output")]
		[SerializeField,Tooltip("Name used when SaveToFile is called and no file name is provided.")] string _defaultExtrinsicsFileName = "DefaultCameraFromChessboard";

		[Header("UI")]
		[SerializeField] RawImage _processedCameraImageA = null;
		[SerializeField] RawImage _processedCameraImageB = null;
		[SerializeField] Button _actionButton = null;


		Intrinsics _intrinsicsA, _intrinsicsB;
		ExtrinsicsCalibrator _extrinsicsCalibratorA, _extrinsicsCalibratorB;

		Mat _sensorMatA, _sensorMatB;
		MatOfDouble _distortionCoeffsMatA, _distortionCoeffsMatB;

		Mat _camTexMatA, _camTexMatB;
		Mat _camTexGrayMatA, _camTexGrayMatB;
		Mat _camTexGrayUndistortMatA, _camTexGrayUndistortMatB;
		Texture2D _processedCameraTextureA, _processedCameraTextureB;
		Texture2D _tempTransferTextureA, _tempTransferTextureB; // For conversion from RenderTexture input.
		Color32[] _tempTransferColorsA, _tempTransferColorsB;

		Mat _undistortMapA1, _undistortMapB1;
		Mat _undistortMapA2, _undistortMapB2;

		MatOfPoint2f _chessCornersImageMatA, _chessCornersImageMatB;
		MatOfPoint3f _chessCornersWorldMat;
		MatOfDouble _noDistCoeffs;

		List<Mat> _chessCornersWorldSamplesMat, _chessCornersImageSamplesMatA, _chessCornersImageSamplesMatB;

		Mat _rotation3x3Mat;
		Mat _translationVecMat;
		Mat _essentialMat;
		Mat _fundamentalMat;

		Material _previewMaterialA, _previewMaterialB;
		Material _patternRenderMaterial;
		RenderTexture _chessPatternTexture;
		RenderTexture _arTextureA, _arTextureB;

		AspectRatioFitter _aspectFitterA, _aspectFitterB;
		RawImage _arImageA, _arImageB;

		Transform _targetCheckerboardTransform;

		bool _dirtyCameraTextureA, _dirtyCameraTextureB;
		bool _foundBoardA, _foundBoardB;
		bool _initiated;

		const int targetSampleCount = 4;
		const string logPrepend = "<b>[" + nameof( CameraToCameraFromCheckerboardExtrinsicsEstimator ) + "]</b> ";


		public Texture cameraSourceTextureA {
			get { return _cameraSourceTextureA; }
			set {
				_cameraSourceTextureA = value;
				_dirtyCameraTextureA = true;
			}
		}

		public Texture cameraSourceTextureB {
			get { return _cameraSourceTextureB; }
			set {
				_cameraSourceTextureB = value;
				_dirtyCameraTextureB = true;
			}
		}


		void Awake()
		{
			if( !Intrinsics.TryLoadFromFile( _intrinsicsAFileName, out _intrinsicsA ) ) {
				Debug.LogError( logPrepend + "Loading intrinsics A file '" + _intrinsicsAFileName + "' failed.\n" );
				enabled = false;
				return;
			}
			if( !Intrinsics.TryLoadFromFile( _intrinsicsBFileName, out _intrinsicsB ) ) {
				Debug.LogError( logPrepend + "Loading intrinsics B file '" + _intrinsicsBFileName + "' failed.\n" );
				enabled = false;
				return;
			}

			if( !_checkerboard ){
				Debug.LogError( logPrepend + "Missing calibration bord reference\n" );
				enabled = false;
				return;
			}

			_undistortMapA1 = new Mat();
			_undistortMapA2 = new Mat();
			_undistortMapB1 = new Mat();
			_undistortMapB2 = new Mat();
			_noDistCoeffs = new MatOfDouble( new double[ 5 ] );
			_rotation3x3Mat = new Mat();
			_translationVecMat = new Mat();
			_essentialMat = new Mat();
			_fundamentalMat = new Mat();

			_chessCornersWorldSamplesMat = new List<Mat>();
			_chessCornersImageSamplesMatA = new List<Mat>();
			_chessCornersImageSamplesMatB = new List<Mat>();

			_extrinsicsCalibratorA = new ExtrinsicsCalibrator();
			_extrinsicsCalibratorB = new ExtrinsicsCalibrator();

			TrackingToolsHelper.RenderPattern( _checkerboard.checkerPatternSize, TrackingToolsHelper.PatternType.Checkerboard, 1024, ref _chessPatternTexture, ref _patternRenderMaterial );

			// Prepare UI.
			PrepareUIResources( ref _aspectFitterA, _processedCameraImageA, ref _previewMaterialA, ref _arImageA, _targetCameraA );
			PrepareUIResources( ref _aspectFitterB, _processedCameraImageB, ref _previewMaterialB, ref _arImageB, _targetCameraB );
			
			if( !_targetCheckerboardTransform ) _targetCheckerboardTransform = new GameObject( "CalibrationBoard" ).transform;
			_targetCheckerboardTransform.localScale = new Vector3( ( _checkerboard.checkerPatternSize.x - 1 ) * _checkerboard.checkerTileSize * 0.001f, ( _checkerboard.checkerPatternSize.y - 1 ) * _checkerboard.checkerTileSize * 0.001f, 0 );
			MeshFilter meshFilter;
			if( !( meshFilter = _targetCheckerboardTransform.GetComponent<MeshFilter>() ) ) meshFilter = _targetCheckerboardTransform.gameObject.AddComponent<MeshFilter>();
			meshFilter.sharedMesh = PrimitiveFactory.Quad();
			MeshRenderer meshRenderer;
			if( !( meshRenderer = _targetCheckerboardTransform.GetComponent<MeshRenderer>() ) ) meshRenderer = _targetCheckerboardTransform.gameObject.AddComponent<MeshRenderer>();
			Shader unlitTextureShader = Shader.Find( "Hidden/UnlitTexture" );
			Material calibrationBoardMaterial = new Material( unlitTextureShader );
			calibrationBoardMaterial.mainTexture = _chessPatternTexture;
			meshRenderer.sharedMaterial = calibrationBoardMaterial;

			_actionButton.GetComponentInChildren<Text>().text = "Sample";
			_actionButton.onClick.AddListener( () => Sample() );

			// Update world points.
			TrackingToolsHelper.UpdateWorldSpacePatternPoints( _checkerboard.checkerPatternSize, _targetCheckerboardTransform.localToWorldMatrix, TrackingToolsHelper.PatternType.Checkerboard, Vector2.zero, ref _chessCornersWorldMat );
		}


		static void PrepareUIResources( ref AspectRatioFitter aspectFitter, RawImage processedCameraImage, ref Material previewMaterial, ref RawImage arImage, Camera targetCamera )
		{
			aspectFitter = processedCameraImage.GetComponent<AspectRatioFitter>();
			if( !aspectFitter ) aspectFitter = processedCameraImage.gameObject.AddComponent<AspectRatioFitter>();
			aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
			previewMaterial = new Material( Shader.Find( TrackingToolsConstants.previewShaderName ) );
			processedCameraImage.material = previewMaterial;
			processedCameraImage.color = Color.white;
			arImage = new GameObject( "ARImage" ).AddComponent<RawImage>();
			arImage.transform.SetParent( processedCameraImage.transform );
			arImage.rectTransform.FitParent();
			arImage.gameObject.SetActive( false );
			targetCamera.backgroundColor = Color.clear;
		}


		void OnDestroy()
		{
			if( _patternRenderMaterial ) Destroy( _patternRenderMaterial );
			if( _tempTransferTextureA ) Destroy( _tempTransferTextureA );
			if( _tempTransferTextureB ) Destroy( _tempTransferTextureB );
			if( _previewMaterialA ) Destroy( _previewMaterialA );
			if( _previewMaterialB ) Destroy( _previewMaterialB );
			_chessPatternTexture?.Release();
			_chessCornersImageMatA?.release();
			_chessCornersImageMatB?.release();
			_camTexMatA?.release();
			_camTexMatB?.release();
			_arTextureA?.Release();
			_arTextureB?.Release();
			_extrinsicsCalibratorA?.Release();
			_extrinsicsCalibratorB?.Release();
			_rotation3x3Mat?.release() ;
			_translationVecMat?.release();
			_essentialMat?.release();
			_fundamentalMat?.release();
			_undistortMapA1?.release();
			_undistortMapA2?.release();
			_undistortMapB1?.release();
			_undistortMapB2?.release();

			Reset();
		}
		

		void Update()
		{
			if( _cameraSourceTextureA && _dirtyCameraTextureA )
			{
				if( !AdaptResources(
					_cameraSourceTextureA, _intrinsicsA, _processedCameraImageA, _arImageA, _aspectFitterA, _targetCameraA, _undistortMapA1, _undistortMapA2,
					ref _processedCameraTextureA, ref _sensorMatA, ref _distortionCoeffsMatA, ref _camTexGrayMatA, ref _camTexGrayUndistortMatA, ref _arTextureA
				) ) return;

				UpdatePreviewForCamera(
					_cameraSourceTextureA, _intrinsicsA, _flipSourceTextureAVertically, _normalizeSourceATexture, _targetCameraA, _arImageA, _processedCameraTextureA, ref _foundBoardA,
					ref _tempTransferColorsA, _tempTransferTextureA, ref _camTexMatA, ref _chessCornersImageMatA, _camTexGrayMatA, _camTexGrayUndistortMatA, _undistortMapA1, _undistortMapA2, _extrinsicsCalibratorA
				);

				_dirtyCameraTextureA = false;
			}

			if( _cameraSourceTextureB && _dirtyCameraTextureB ) {
				if( !AdaptResources(
					_cameraSourceTextureB, _intrinsicsB, _processedCameraImageB, _arImageB, _aspectFitterB, _targetCameraB, _undistortMapB1, _undistortMapB2,
					ref _processedCameraTextureB, ref _sensorMatB, ref _distortionCoeffsMatB, ref _camTexGrayMatB, ref _camTexGrayUndistortMatB, ref _arTextureB
				) ) return;

				UpdatePreviewForCamera(
					_cameraSourceTextureB, _intrinsicsB, _flipSourceTextureBVertically, _normalizeSourceBTexture, _targetCameraB, _arImageB, _processedCameraTextureB, ref _foundBoardB,
					ref _tempTransferColorsB, _tempTransferTextureB, ref _camTexMatB, ref _chessCornersImageMatB, _camTexGrayMatB, _camTexGrayUndistortMatB, _undistortMapB1, _undistortMapB2, _extrinsicsCalibratorB
				);

				_dirtyCameraTextureB = false;
			}

			bool buttonVisible = _chessCornersWorldSamplesMat.Count == targetSampleCount || ( _foundBoardA && _foundBoardB );
			if( buttonVisible != _actionButton.gameObject.activeSelf ) _actionButton.gameObject.SetActive( buttonVisible );
		}


		void UpdatePreviewForCamera
		(
			Texture cameraSourceTexture, Intrinsics intrinsics, bool flipSourceTextureAVertically, bool normalizeSourceTexture, Camera targetCmaera, RawImage arImage, Texture2D processedCameraTexture, ref bool foundBoard,
			ref Color32[] tempTransferColors, Texture2D tempTransferTexture, ref Mat camTexMat, ref MatOfPoint2f chessCornersImageMat, Mat camTexGrayMat, Mat camTexGrayUndistortMat, Mat undistortMap1, Mat undistortMap2, ExtrinsicsCalibrator extrinsicsCalibrator
		) {
			// Update mat texture (If the texture looks correct in Unity, then it needs to be flipped for OpenCV).
			TrackingToolsHelper.TextureToMat( cameraSourceTexture, !flipSourceTextureAVertically, ref camTexMat, ref tempTransferColors, ref tempTransferTexture );

			// Convert to grayscale if more than one channel, else copy (and convert bit rate if necessary).
			TrackingToolsHelper.ColorMatToLumanceMat( camTexMat, camTexGrayMat );

			// Sometimes normalization makes it easier for FindChessboardCorners.
			if( normalizeSourceTexture ) Core.normalize( camTexGrayMat, camTexGrayMat, 0, 255, Core.NORM_MINMAX, CvType.CV_8U );

			// Undistort (TODO: move undistortion to GPU as last step and work on distorted image instead).
			//Calib3d.undistort( _camTexGrayMat, _camTexGrayUndistortMat, _sensorMat, _distortionCoeffsMat );
			Imgproc.remap( camTexGrayMat, camTexGrayUndistortMat, undistortMap1, undistortMap2, Imgproc.INTER_LINEAR );

			// Find chessboard.
			foundBoard = TrackingToolsHelper.FindChessboardCorners( camTexGrayUndistortMat, _checkerboard.checkerPatternSize, ref chessCornersImageMat, _fastAndImprecise );

			if( foundBoard ) {
				// Draw chessboard.
				//if( _showFoundPointsInImage ) TrackingToolsHelper.DrawFoundPattern( _camTexGrayUndistortMatA, _calibrationBoard.checkerPatternSize, _chessCornersImageMat );

				// Update and apply extrinsics.
				bool foundExtrinsics = extrinsicsCalibrator.UpdateExtrinsics( _chessCornersWorldMat, chessCornersImageMat, intrinsics ); // TODO: Problem could be here
				if( foundExtrinsics ) {
					extrinsicsCalibrator.extrinsics.ApplyToTransform( targetCmaera.transform, _targetCheckerboardTransform );
				}
				arImage.gameObject.SetActive( foundExtrinsics );
			} else {
				arImage.gameObject.SetActive( false );
			}

			// UI.
			Utils.fastMatToTexture2D( camTexGrayUndistortMat, processedCameraTexture ); // Will flip back to Unity orientation by default.
		}


		void Sample()
		{
			// If we are in fast and imprecise mode, then detect and update again with higher precision before saving.
			if( _fastAndImprecise ) {
				TrackingToolsHelper.FindChessboardCorners( _camTexGrayUndistortMatA, _checkerboard.checkerPatternSize, ref _chessCornersImageMatA );
				_extrinsicsCalibratorA.UpdateExtrinsics( _chessCornersWorldMat, _chessCornersImageMatA, _intrinsicsA );

				TrackingToolsHelper.FindChessboardCorners( _camTexGrayUndistortMatB, _checkerboard.checkerPatternSize, ref _chessCornersImageMatB );
				_extrinsicsCalibratorB.UpdateExtrinsics( _chessCornersWorldMat, _chessCornersImageMatB, _intrinsicsB );
			}

			if( !_extrinsicsCalibratorA.isValid || !_extrinsicsCalibratorB.isValid ) {
				Debug.LogWarning( "Save extrinsics to file failed. No chessboard was found in camera image.\n" );
				return;
			}

			_chessCornersWorldSamplesMat.Add( _chessCornersWorldMat.clone() );
			_chessCornersImageSamplesMatA.Add( _chessCornersImageMatA.clone() );
			_chessCornersImageSamplesMatB.Add( _chessCornersImageMatB.clone() );

			// Allow saving after 4 samples.
			if( _chessCornersWorldSamplesMat.Count == targetSampleCount ) {
				_actionButton.GetComponentInChildren<Text>().text = "Save To File";
				_actionButton.onClick.RemoveAllListeners();
				_actionButton.onClick.AddListener( () => SaveToFile() );
			}
		}
		

		void SaveToFile( string optionalFileName = null )
		{
			if( !string.IsNullOrEmpty( optionalFileName ) ) _defaultExtrinsicsFileName = optionalFileName;

			// Set flags.
			int flag = 0;
			flag |= Calib3d.CALIB_FIX_INTRINSIC;    // Don't recompute and change intrinsics parameters.
			flag |=                                 // Don't recompute distortions, ignore them. We assume the incoming points have already bee undistorted.
				Calib3d.CALIB_FIX_TANGENT_DIST |
				Calib3d.CALIB_FIX_K1 |
				Calib3d.CALIB_FIX_K2 |
				Calib3d.CALIB_FIX_K3 |
				Calib3d.CALIB_FIX_K4 |
				Calib3d.CALIB_FIX_K5;
			
			// Compute!
			Calib3d.stereoCalibrate
			(
				_chessCornersWorldSamplesMat, _chessCornersImageSamplesMatA, _chessCornersImageSamplesMatB,
				_sensorMatA, _noDistCoeffs,
				_sensorMatB, _noDistCoeffs,
				new Size(),
				_rotation3x3Mat, _translationVecMat, _essentialMat, _fundamentalMat,
				flag
			);

			// Save.
			Extrinsics extrinsics = new Extrinsics();
			extrinsics.UpdateFromOpenCvStereoCalibrate( _rotation3x3Mat, _translationVecMat );
			extrinsics.SaveToFile( _defaultExtrinsicsFileName );

			Debug.Log( logPrepend + "Saved extrinsics to file.\n" + _defaultExtrinsicsFileName );
		}


		static bool AdaptResources
		(
			Texture cameraSourceTexture, Intrinsics intrinsics, RawImage processedCameraImage, RawImage arImage, AspectRatioFitter aspectFitter, Camera targetCamera, Mat undistortMap1, Mat undistortMap2,
			ref Texture2D processedCameraTexture, ref Mat sensorMat, ref MatOfDouble distortionCoeffsMat, ref  Mat camTexGrayMat, ref Mat camTexGrayUndistortMat, ref RenderTexture arTexture
		) {
			int w = cameraSourceTexture.width;
			int h = cameraSourceTexture.height;
			if( processedCameraTexture != null && processedCameraTexture.width == w && processedCameraTexture.height == h ) return true; // Already adapted.

			// Get and apply intrinsics.
			bool success = intrinsics.ApplyToToOpenCV( ref sensorMat, ref distortionCoeffsMat );//, w, h );
			if( !success ) return false;

			intrinsics.ApplyToUnityCamera( targetCamera );

			// Create mats and textures.
			camTexGrayMat = new Mat( h, w, CvType.CV_8UC1 );
			camTexGrayUndistortMat = new Mat( h, w, CvType.CV_8UC1 );
			processedCameraTexture = new Texture2D( w, h, GraphicsFormat.R8_UNorm, 0, TextureCreationFlags.None );
			processedCameraTexture.name = "UndistortedCameraTex";
			processedCameraTexture.wrapMode = TextureWrapMode.Repeat;
			arTexture = new RenderTexture( w, h, 16, GraphicsFormat.R8G8B8A8_UNorm );
			arTexture.name = "AR Texture";

			// Create undistort map (sensorMat remains unchanged even through it is passed as newCameraMatrix).
			Calib3d.initUndistortRectifyMap( sensorMat, distortionCoeffsMat, new Mat(), sensorMat, new Size( w, h ), CvType.CV_32FC1, undistortMap1, undistortMap2 );

			// Update UI.
			aspectFitter.aspectRatio = w / (float) h;
			processedCameraImage.texture = processedCameraTexture;
			arImage.texture = arTexture;
			targetCamera.targetTexture = arTexture;

			return true;
		}


		void Reset()
		{
			_sensorMatA?.Dispose();
			_sensorMatB?.Dispose();
			_distortionCoeffsMatA?.Dispose();
			_distortionCoeffsMatB?.Dispose();
			_camTexGrayMatA?.Dispose();
			_camTexGrayMatB?.Dispose();
			_camTexGrayUndistortMatA?.Dispose();
			_camTexGrayUndistortMatB?.Dispose();
			if( _processedCameraTextureA ) Destroy( _processedCameraTextureA );
			if( _processedCameraTextureB ) Destroy( _processedCameraTextureB );
		}
	}
}