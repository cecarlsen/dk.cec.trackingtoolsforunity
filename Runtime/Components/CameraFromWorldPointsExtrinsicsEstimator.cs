/*
	Copyright © Carl Emil Carlsen 2018-2025
	http://cec.dk

	Given the intrisics of a camera and a set of points in model space and in camera image space we can use Calib3d.solvePnP
	to find the extrinsics (position and rotation) of the camera relative to the model.

	Trouble shooting
	- If you get 'Calib3d.solvePnP failed' warnings, then try to add more points. The method does not seem to like single points in one dimension.

*/

using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using OpenCVForUnity.CoreModule;
using System;

namespace TrackingTools
{
	public class CameraFromWorldPointsExtrinsicsEstimator : MonoBehaviour
	{
		[SerializeField] bool _interactable = false;
		
		[Header("Input")]
		[SerializeField] Transform[] _worldPointTransforms = null;
		[SerializeField] Texture _physicalCameraTexture = null;
		[SerializeField] bool _preFlipPhysicalCameraTextureY = true;
		[SerializeField] bool _undistortPhysicalCameraTexture = true;
		[SerializeField] string _physicalCameraIntrinsicsFileName = "DefaultCamera";
		[SerializeField,Tooltip("Without extension name (which is .json)")] string _calibrationPointsFileName = "DefaultPoints";

		[Header("Output")]
		[SerializeField] Camera _virtualCamera = null;

		[Header("UI")]
		[SerializeField] RectTransform _containerRect = null;
		[SerializeField] Key _interactableHotKey = Key.Digit1;
		[SerializeField] Key _resetHotKey = Key.Backspace;
		[SerializeField] Key _precisionHoldHotKey = Key.LeftCtrl;
		[SerializeField] Font _font = null;
		[SerializeField] int _fontSize = 12;
		[SerializeField,Range(0f,1f)] float _virtualAlpha = 0.8f;
		[SerializeField,Range(1f,25f)] float _physicalBrightness = 1f;
		//[SerializeField,Tooltip("Optional")] RectTransform _containerUI;

		[Header("Gizmos")]
		[SerializeField] bool _drawGizmosAlways = true;
		[SerializeField] bool _drawPointIndexLabelGizmos = true;
		[SerializeField] Vector3 _pointLabelOffset = Vector3.zero;
		[SerializeField] bool _drawPointGizmos = true;
		[SerializeField] float _pointGizmoRadius = 0.005f;

		[Header("Debug")]
		[SerializeField] bool _logWarnings = false;

		//[Header("Other")]
		//[SerializeField] bool _undistortOnGpu = true;

		ExtrinsicsCalibrator _extrinsicsCalibrator;
		Intrinsics _intrinsics;
		Flipper _flipper;
		LensUndistorter _lensUndistorter;
		RenderTexture _processedPhysicalCameraTexture;
		
		bool _dirtyTexture = true;
		bool _needsResetImageCalibrationPoints = false;
		bool _dirtyCalibration = true;
		
		// UI.
		RenderTexture _virtualCameraRenderTexture;
		Material _uiMaterial;
		AspectRatioFitter _aspectFitterUI;
		RawImage _virtualCameraImageUI;
		RawImage _physicalCameraImageUI;
		RectTransform _physicalCameraImageRect;
		CanvasGroup _virtualAlphaGroup;
		RectTransform[] _userPointRects;
		Image[] _userPointImages;
		int _focusedPointIndex = -1;
		bool _isPointActive;
		Vector2 _mouseHitAnchoredPosition;

		// OpenCV.
		Point[] _calibrationPointsImage;
		Point3[] _calibrationPointsWorld;
		MatOfPoint2f _calibrationPointsImageMat;
		MatOfPoint3f _calibrationPointsWorldMat;

		// THESE ARE FOR THE ALTERNATIVE CPU-BASED OPENCV UNDISTORTION.
		//Texture2D _tempTransferTexture; // For conversion from RenderTexture input.
		//Color32[] _tempTransferColors;
		//Mat _cameraMatrix;
		//MatOfDouble _distCoeffs;
		//Mat _camTexMat;
		//Mat _camTexGrayMat;
		//Mat _camTexGrayUndistortMat;
		//Texture2D _undistortedCameraTexture2D;
		
		static readonly Color pointIdleColor = Color.cyan;
		static readonly Color pointFocusedColor = Color.magenta;
		static readonly Color pointActiveColor = Color.white;
		const int worldPointTransformCountMin = 4;
		const float mouseHitDistanceMinNormalized = 0.2f; // Normalized to image height

		static readonly string logPrepend = $"<b>[{nameof( CameraFromWorldPointsExtrinsicsEstimator )}]</b>";


		public Texture physicalCameraTexture {
			get { return _physicalCameraTexture; }
			set {
				_physicalCameraTexture = value;
				_dirtyTexture = true;
			}
		}
		
		public bool interactable {
			get { return _interactable; }
			set {
				if( value && !CheckCanCalibrate() ) return;
				_interactable = value;
				if( _physicalCameraImageUI && Application.isPlaying ) _physicalCameraImageUI.gameObject.SetActive( _interactable );
			}
		}

		public float alpha {
			get { return _virtualAlpha; }
			set { _virtualAlpha = Mathf.Clamp01( value ); }
		}

		public float brightness {
			get { return _physicalBrightness; }
			set {
				_physicalBrightness = value;
				if( _uiMaterial ) _uiMaterial.SetFloat( ShaderIDs._Brightness, _physicalBrightness );
			}
		}

		public bool preFlipPhysicalCameraTextureY {
			get { return _preFlipPhysicalCameraTextureY; }
			set {
				_preFlipPhysicalCameraTextureY = value;
				_dirtyTexture = true;
			}
		}

		public bool undistortPhysicalCameraTexture {
			get { return _undistortPhysicalCameraTexture; }
			set {
				_undistortPhysicalCameraTexture = value;
				_dirtyTexture = true;
			}
		}

		public Camera virtualCamera => _virtualCamera;


		static class ShaderIDs
		{
			public static readonly int _Brightness = Shader.PropertyToID( nameof( _Brightness ) );
		}


		public void SetWorldPointTransforms( Transform[] transforms )
		{
			if( transforms == null ){
				Debug.LogWarning( $"{logPrepend} Ignored SetWorldPointTransforms(). The transforms array is null.\n" );
				return;
			}

			if( transforms.Length < worldPointTransformCountMin ){
				Debug.LogWarning( $"{logPrepend} Ignored SetWorldPointTransforms(). You must supply at least {worldPointTransformCountMin} transforms.\n" );
				return;
			}

			_worldPointTransforms = new Transform[ transforms.Length ];
			Array.Copy( transforms, _worldPointTransforms, _worldPointTransforms.Length );

			AdaptWorldPointTransformResources();
			_needsResetImageCalibrationPoints = true;
			_dirtyCalibration = true;
		}


		/// <summary>
		/// /// Reset calibration points to fit current camera perspective.
		/// </summary>
		public void ResetImageCalibrationPoints()
		{
			int pointCount = _worldPointTransforms.Length;
			for( int p = 0; p < pointCount; p++ ) {
				Vector2 viewportPoint = _virtualCamera.WorldToViewportPoint( _worldPointTransforms[ p ].position );
				SetAnchoredPosition( _userPointRects[ p ], viewportPoint );
			}
			_dirtyCalibration = true;
			_needsResetImageCalibrationPoints = false;
		}


		/// <summary>
		/// Set physical camera intrinsics file name without extension.
		/// </summary>
		public void SetPhysicalCameraIntrinsicsFileName( string physicalCameraIntrinsicsFileName )
		{
			_physicalCameraIntrinsicsFileName = physicalCameraIntrinsicsFileName;
			TryLoadPhysicalCameraIntrinsics();
		}


		/// <summary>
		/// Set calibration point file name without extention. You must first make sure that WorldPointTransforms are set with matching count.
		/// </summary>
		public void SetCalibrationPointFileName( string calibrationPointsFileName )
		{
			_calibrationPointsFileName = calibrationPointsFileName;
			TryLoadCalibrationPoints();
		}


		void OnEnable()
		{
			//if( _worldPointTransforms == null || _worldPointTransforms.Length < worldPointTransformCountMin ){
			//	Debug.LogWarning( $"{logPrepend} World points missing. At least {worldPointTransformCountMin} are required.\n" );
			//	enabled = false;
			//	return;
			//}

			_extrinsicsCalibrator = new ExtrinsicsCalibrator();

			// Create UI.
			//if( !_containerUI ) {
			//	_containerUI = new GameObject( "CameraPoser" ).AddComponent<RectTransform>();
			//	_containerUI.transform.SetParent( _containerRect );
			//}
			_physicalCameraImageUI = new GameObject( "PhysicalCameraImage" ).AddComponent<RawImage>();
			_physicalCameraImageUI.transform.SetParent( _containerRect );
			_physicalCameraImageUI.transform.localScale = Vector3.one;
			_physicalCameraImageUI.rectTransform.FitParent();
			_physicalCameraImageRect = _physicalCameraImageUI.GetComponent<RectTransform>();
			_virtualCameraImageUI = new GameObject( "VirtualCameraImage" ).AddComponent<RawImage>();
			_virtualCameraImageUI.transform.SetParent( _containerRect );
			_virtualCameraImageUI.transform.localScale = Vector3.one;
			_virtualCameraImageUI.rectTransform.FitParent();
			_virtualAlphaGroup = _virtualCameraImageUI.gameObject.AddComponent<CanvasGroup>();
			_uiMaterial = new Material( Shader.Find( "UI/ScalarTexture" ) );
			_physicalCameraImageUI.material = _uiMaterial;
			_uiMaterial.SetColor( "_BurnoutColor", new Color( 0.5f, 0f, 0f, 1f ) );
			_aspectFitterUI = _containerRect.gameObject.GetComponent<AspectRatioFitter>();
			if( !_aspectFitterUI ) _aspectFitterUI = _containerRect.gameObject.AddComponent<AspectRatioFitter>();
			_aspectFitterUI.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
			//ExpandRectTransform( _containerRect );
			ExpandRectTransform( _physicalCameraImageRect );

			// Adapt world point transform resources (if any).
			AdaptWorldPointTransformResources();

			// Hide.
			if( !_interactable ) _physicalCameraImageUI.transform.gameObject.SetActive( false );

			// Load files.
			TryLoadPhysicalCameraIntrinsics();
			if( !TryLoadCalibrationPoints() ) ResetImageCalibrationPoints();

			// Processing.
			_flipper = new Flipper();

			// Update variables.
			OnValidate();
		}


		


		void OnDisable()
		{
			ReleaseWorldPointTransformResources();

			if( _physicalCameraImageUI != null && _physicalCameraImageUI.gameObject ) Destroy( _physicalCameraImageUI.gameObject );
			if( _virtualCameraImageUI != null && _virtualCameraImageUI.gameObject ) Destroy( _virtualCameraImageUI.gameObject );
			if( _aspectFitterUI ) Destroy( _aspectFitterUI );
			_physicalCameraImageUI = null;
			_virtualCameraImageUI = null;
			_aspectFitterUI = null;

			_extrinsicsCalibrator?.Release();
			_lensUndistorter?.Release();
			_flipper?.Release();
			_extrinsicsCalibrator = null;
			_lensUndistorter = null;
			_flipper = null;

			_virtualCameraRenderTexture?.Release();
			_processedPhysicalCameraTexture?.Release();
			_virtualCameraRenderTexture = null;
			_processedPhysicalCameraTexture = null;

			_isPointActive = false;
			_focusedPointIndex = -1;

			//_distCoeffs?.Dispose();
			//_cameraMatrix?.Dispose();
			//_camTexMat?.Dispose();
			//_camTexGrayMat?.Dispose();
			//_camTexGrayUndistortMat?.Dispose();
			//if( _tempTransferTexture ) Destroy( _tempTransferTexture );
		}



		void Update()
		{
			// Check basic needs.
			if( _intrinsics == null ) return;
			
			// Only allow calibration if we have a physical camera source texture.
			if( _interactableHotKey != Key.None && Keyboard.current[ _interactableHotKey ].wasReleasedThisFrame ){
				//if( !_interactable && !CheckCanCalibrate() ) return;
				interactable = !interactable;
			}

			// Adapt resources.
			AutoAdaptTextureResources();
			if( _needsResetImageCalibrationPoints ) ResetImageCalibrationPoints();

			if( _interactable )
			{
				if( _dirtyTexture )
				{
					//if( _undistortOnGpu ){

						if( _undistortPhysicalCameraTexture ){
							_lensUndistorter.Undistort( _physicalCameraTexture, _processedPhysicalCameraTexture, _preFlipPhysicalCameraTextureY );
						} else if( _preFlipPhysicalCameraTextureY ) {
							_flipper.Flip( _physicalCameraTexture, _processedPhysicalCameraTexture );
						}
				
						_physicalCameraImageUI.texture = _preFlipPhysicalCameraTextureY || _undistortPhysicalCameraTexture ? _processedPhysicalCameraTexture : _physicalCameraTexture;

						/*
					} else {

						// WE KEEP THIS HERE SO WE CAN COMPARE THE TWO IMPLEMENTATIONS.

						// Update mat texture ( If the texture looks correct in Unity, then it needs to be flipped for OpenCV ).
						TrackingToolsHelper.TextureToMat( _physicalCameraTexture, !_isPhysicalCameraTextureFlipped, ref _camTexMat, ref _tempTransferColors, ref _tempTransferTexture );

						// Convert to black & white.
						TrackingToolsHelper.ColorMatToLumanceMat( _camTexMat, _camTexGrayMat );

						// Undistort.
						if( _isPhysicalCameraTextureDistorted ) Calib3d.undistort( _camTexGrayMat, _camTexGrayUndistortMat, _cameraMatrix, _distCoeffs );

						// Back to Unity.
						Utils.fastMatToTexture2D( _isPhysicalCameraTextureDistorted ? _camTexGrayUndistortMat : _camTexGrayMat, _undistortedCameraTexture2D ); // Will flip back to Unity orientation by default.

						_physicalCameraImageUI.texture = _undistortedCameraTexture2D;
					}
					*/

					_dirtyTexture = false;
				}
				UpdateInteraction();

				// UI.
				//_virtualCameraImageUI.color = new Color( 1f, 1f, 1f, _virtualAlpha );
				_virtualAlphaGroup.alpha = _virtualAlpha;
			}

			if( _worldPointTransforms?.Length >= worldPointTransformCountMin && _dirtyCalibration ) UpdateAndSaveCalibration();
		}


		void OnDrawGizmos() => DrawGizmos( selected: false );
		void OnDrawGizmosSelected() => DrawGizmos( selected: true );


		void OnValidate()
		{
			interactable = _interactable;
			physicalCameraTexture = _physicalCameraTexture;
			alpha = _virtualAlpha;
			brightness = _physicalBrightness;

			_fontSize = Mathf.Max( 0, _fontSize );
		}


		void DrawGizmos( bool selected )
		{
			if( !selected && !_drawGizmosAlways ) return;

			if( !_drawPointGizmos && !_drawPointIndexLabelGizmos ) return;

			if( _worldPointTransforms != null && _worldPointTransforms.Length > 0 ){
				for( int i = 0; i < _worldPointTransforms.Length; i++ ) {
					var t = _worldPointTransforms[ i ];
					if( t ){
						Vector3 p = t.position;
						if( _drawPointGizmos ) Gizmos.DrawWireSphere( p, _pointGizmoRadius );
						#if UNITY_EDITOR
							if( _drawPointIndexLabelGizmos ) UnityEditor.Handles.Label( p + _pointLabelOffset, i.ToString() );
						#endif
					}
				}
			}
		}



		bool CheckCanCalibrate()
		{
			if( !_physicalCameraTexture ){
				if( _logWarnings ) Debug.LogWarning( $"{logPrepend} Cannot calibrate. Missing camera texture.\n" );
				return false;
			}

			if( _intrinsics.width != _physicalCameraTexture.width || _intrinsics.height !=_physicalCameraTexture.height ){
				if( _logWarnings ) Debug.LogWarning( $"{logPrepend} Cannot calibrate. Intrinsics ({_intrinsics.width}x{_intrinsics.height}) and Physical Camera Texture ({_physicalCameraTexture.width}x{_physicalCameraTexture.height}) sizes must match.\n" );
				return false;
			}

			if( _worldPointTransforms?.Length < worldPointTransformCountMin ){
				if( _logWarnings ) Debug.LogWarning( $"{logPrepend}Cannot calibrate. You must supply at least {worldPointTransformCountMin} transforms.\n" );
				return false;
			}

			return true;
		}
		


		void UpdateInteraction()
		{
			if( Keyboard.current[ _resetHotKey ].wasPressedThisFrame ){
				ResetImageCalibrationPoints();
			}

			// Get anchored mouse position within image rect.
			Vector2 mousePos;
			var canvas = _containerRect.GetComponentInParent<Canvas>();
			RectTransformUtility.ScreenPointToLocalPointInRectangle( _physicalCameraImageRect, Mouse.current.position.value, canvas.worldCamera, out mousePos );
			Vector2 mouseAnchoredPos = LocalPixelPositionToAnchoredPosition( mousePos, _physicalCameraImageRect ); // lower left is (0,0), upper right is (1,1)

			// Update point.
			if( _isPointActive ){
				float precisionFactor = ( _precisionHoldHotKey != Key.None && Keyboard.current[_precisionHoldHotKey].isPressed ) ? 0.1f : 1f;
				var newAnchoredPosition = _mouseHitAnchoredPosition + ( mouseAnchoredPos - _mouseHitAnchoredPosition ) * precisionFactor;
				SetAnchoredPosition( _userPointRects[_focusedPointIndex], newAnchoredPosition );
				_dirtyCalibration = true;
			}

			if( _isPointActive ){
				// Check for deselection.
				if( Mouse.current.leftButton.wasReleasedThisFrame && _focusedPointIndex != -1){
					_userPointImages[_focusedPointIndex].color = pointFocusedColor;
					_isPointActive = false;
				} 
			} else {
				// Handle selection.
				// Find nearest point.
				float sqrDistMin = mouseHitDistanceMinNormalized * mouseHitDistanceMinNormalized;
				int nearestPointIndex = -1;
				int pointCount = _worldPointTransforms.Length;
				for( int p = 0; p < pointCount; p++ ){
					Vector2 towardsPoint = _userPointRects[ p ].anchorMin - mouseAnchoredPos;
					towardsPoint.x *= _aspectFitterUI.aspectRatio;
					float sqrDist = Vector2.Dot( towardsPoint, towardsPoint );
					if( sqrDist < sqrDistMin ) {
						nearestPointIndex = p;
						sqrDistMin = sqrDist;
					}
				}
				if( nearestPointIndex != -1 ){
					if( _focusedPointIndex != -1 ){
						if( Mouse.current.leftButton.wasPressedThisFrame ) {
							// Make active.
							_userPointImages[nearestPointIndex].color = pointActiveColor;
							_mouseHitAnchoredPosition = mouseAnchoredPos;
							_isPointActive = true;
						} else {
							// Indicate focused.
							_userPointImages[nearestPointIndex].color = pointFocusedColor;
						}
					}
				} else if( _focusedPointIndex != -1 ){
					// Defocus.
					_isPointActive = false;
					_userPointImages[ _focusedPointIndex ].color = pointIdleColor;
				}
				_focusedPointIndex = nearestPointIndex;
			}
		}
	
	
		void UpdateAndSaveCalibration()
		{
			// Update points.
			for( int p = 0; p < _worldPointTransforms.Length; p++ )
			{
				Vector2 posImage = _userPointRects[ p ].anchorMin; // Min and max should be the same.
				posImage.y = 1f - posImage.y; // OpenCv pixels are flipped vertically so (0,0) is at the top left corner.
				posImage.Scale( new Vector2( _intrinsics.width, _intrinsics.height ) );
				_calibrationPointsImage[ p ].set( new double[]{ posImage.x, posImage.y } );
				Vector3 posWorld = _worldPointTransforms[ p ].position;
				_calibrationPointsWorld[ p ].set( new double[]{ posWorld.x, posWorld.y, posWorld.z } );
				//Debug.Log( posWorld + " -> " + _calibrationPointsWorld[ p ] );
			}
			_calibrationPointsImageMat.fromArray( _calibrationPointsImage );
			_calibrationPointsWorldMat.fromArray( _calibrationPointsWorld );

			//Debug.Log( _calibrationPointsWorldMat.dump() );

			_extrinsicsCalibrator.UpdateExtrinsics( _calibrationPointsWorldMat, _calibrationPointsImageMat, _intrinsics );
			
			_extrinsicsCalibrator.extrinsics.ApplyToTransform( _virtualCamera.transform );

			// Save.
			SaveAnchorPoints();

			_dirtyCalibration = false;
		}


		void TryLoadPhysicalCameraIntrinsics()
		{
			if( string.IsNullOrEmpty( _physicalCameraIntrinsicsFileName ) ) return;

			if( !Intrinsics.TryLoadFromFile( _physicalCameraIntrinsicsFileName, out _intrinsics ) ) {
				enabled = false;
				Debug.LogError( logPrepend + "Missing instrinsics file: '" + _physicalCameraIntrinsicsFileName + "'\n" );
				return;
			}

			// Apply to camera.
			_intrinsics.ApplyToUnityCamera( _virtualCamera );

			// Recreate lens undistorter.
			_lensUndistorter?.Release();
			_lensUndistorter = new LensUndistorter( _intrinsics );

			// Apply intrinsics.
			_intrinsics.ApplyToUnityCamera( _virtualCamera );
			_aspectFitterUI.aspectRatio = _intrinsics.aspect;
			//_intrinsics.ApplyToToOpenCV( ref _cameraMatrix, ref _distCoeffs );
		}


		bool TryLoadCalibrationPoints()
		{
			string filePath = TrackingToolsConstants.worldPointSetsDirectoryPath + "/" + _calibrationPointsFileName + ".json";
			if( !File.Exists( filePath ) ) return false;

			string json = File.ReadAllText( filePath );
			PointSetData data = JsonUtility.FromJson<PointSetData>( json );
			if( data.points.Length != _worldPointTransforms.Length ){
				if( _logWarnings ) Debug.LogWarning( $"{logPrepend} Ignored stored point set. Number of points does not match number of transforms in 'worldPointTransforms'.\n" );
				return false;
			}

			for( int p = 0; p < _worldPointTransforms.Length; p++ ) SetAnchoredPosition( _userPointRects[ p ], data.points[ p ] );
			
			_needsResetImageCalibrationPoints = false;
			_dirtyCalibration = true;

			return true;
		}


		void SaveAnchorPoints()
		{
			if( !Directory.Exists( TrackingToolsConstants.worldPointSetsDirectoryPath ) ) Directory.CreateDirectory( TrackingToolsConstants.worldPointSetsDirectoryPath );
			string filePath = TrackingToolsConstants.worldPointSetsDirectoryPath + "/" + _calibrationPointsFileName + ".json";

			PointSetData data = new PointSetData( _worldPointTransforms.Length );
			for( int p = 0; p < _worldPointTransforms.Length; p++ ) data.points[p] = _userPointRects[ p ].anchorMin;

			File.WriteAllText( filePath, JsonUtility.ToJson( data ) );

			//Debug.Log( logPrepend + "Saved anchor points to file.\n" + filePath );
		}


		void AdaptWorldPointTransformResources()
		{
			ReleaseWorldPointTransformResources();

			int pointCount = _worldPointTransforms?.Length ?? 0;
			if( pointCount < worldPointTransformCountMin ) return;

			// UI.
			_userPointRects = new RectTransform[pointCount];
			_userPointImages = new Image[pointCount];
			for( int p = 0; p < pointCount; p++ )
			{
				GameObject pointObject = new GameObject( "Point" + p );
				pointObject.transform.SetParent( _containerRect );
				Image pointImage = pointObject.AddComponent<Image>();
				pointImage.color = Color.cyan;
				RectTransform pointRect = pointObject.GetComponent<RectTransform>();
				pointRect.sizeDelta = Vector2.one * 3;
				pointRect.anchoredPosition = Vector3.zero;
				Text pointLabel = new GameObject( "Label" ).AddComponent<Text>();
				pointLabel.text = p.ToString();
				pointLabel.transform.SetParent( pointRect );
				pointLabel.rectTransform.anchoredPosition = Vector2.zero;
				pointLabel.rectTransform.sizeDelta = new Vector2( _fontSize, _fontSize ) * 2;
				pointLabel.font = _font;
				pointLabel.fontSize = _fontSize;
				_userPointRects[p] = pointRect;
				_userPointImages[p] = pointImage;
			}

			// OpenCV.
			_calibrationPointsImage = new Point[pointCount];
			_calibrationPointsWorld = new Point3[pointCount];
			_calibrationPointsImageMat = new MatOfPoint2f();
			_calibrationPointsWorldMat = new MatOfPoint3f();
			_calibrationPointsImageMat.alloc( pointCount );
			_calibrationPointsWorldMat.alloc( pointCount );
			for( int p = 0; p < pointCount; p++ ) {
				_calibrationPointsImage[p] = new Point();
				_calibrationPointsWorld[p] = new Point3();
			}
		}


		void ReleaseWorldPointTransformResources()
		{
			// UI.
			if( _userPointRects != null ) foreach( var pointRect in _userPointRects ) if( pointRect && pointRect.gameObject ) Destroy( pointRect.gameObject );
			_userPointRects = null;

			// OpenCV.
			_calibrationPointsImageMat?.release();
			_calibrationPointsWorldMat?.Dispose();
			_calibrationPointsImageMat = null;
			_calibrationPointsWorldMat = null;
		}


		void AutoAdaptTextureResources()
		{
			if( !_physicalCameraTexture ) return;

			int w = _physicalCameraTexture.width;
			int h = _physicalCameraTexture.height;
			if( _undistortPhysicalCameraTexture && ( _processedPhysicalCameraTexture == null || _processedPhysicalCameraTexture.width != w || _processedPhysicalCameraTexture.height != h ) ){
				_processedPhysicalCameraTexture?.Release();
				_processedPhysicalCameraTexture = new RenderTexture( w, h, 0, _physicalCameraTexture.graphicsFormat );
				_processedPhysicalCameraTexture.name = "PhysicalCameraTexture";
			}

			if( !_virtualCameraRenderTexture || _virtualCameraRenderTexture.width != w || _virtualCameraRenderTexture.height != h ){
				if( _virtualCameraRenderTexture ) _virtualCameraRenderTexture.Release();
				_virtualCameraRenderTexture = new RenderTexture( w, h, 16 );
				_virtualCameraRenderTexture.name = "VirtualCameraTexture";
				_virtualCamera.targetTexture = _virtualCameraRenderTexture;
				_virtualCameraImageUI.texture = _virtualCameraRenderTexture;

				// OPEN CV ALTERNATIVE.
				//_camTexMat = TrackingToolsHelper.GetCompatibleMat( _physicalCameraTexture );
				//_camTexGrayMat = new Mat( h, w, CvType.CV_8UC1 );
				//_camTexGrayUndistortMat = new Mat( h, w, CvType.CV_8UC1 );
				//_undistortedCameraTexture2D = new Texture2D( w, h, GraphicsFormat.R8_UNorm, 0, TextureCreationFlags.None );
				//_undistortedCameraTexture2D.name = "UndistortedCameraTex";
			}
		}


		static void ExpandRectTransform( RectTransform rectTransform )
		{
			rectTransform.anchorMax = Vector2.one;
			rectTransform.anchorMin = Vector2.zero;
			rectTransform.anchoredPosition = Vector2.zero;
			rectTransform.sizeDelta = Vector2.zero;
		}


		static void SetAnchoredPosition( RectTransform rectTransform, Vector2 pos )
		{
			rectTransform.anchorMin = pos;
			rectTransform.anchorMax = pos;
			rectTransform.anchoredPosition = Vector3.zero;
		}


		static Vector2 LocalPixelPositionToAnchoredPosition( Vector2 pos, RectTransform rectTransform )
		{
			UnityEngine.Rect rect = rectTransform.rect;
			Vector2 anchoredPosition = new Vector2( ( pos.x - rect.x ) / rect.width, ( pos.y - rect.y ) / rect.height );
			//Debug.Log( "pos: " + pos + ", rectTransform: " + rectTransform.rect + ", anchoredPosition: " + anchoredPosition );
			return anchoredPosition;
		}
	}
}