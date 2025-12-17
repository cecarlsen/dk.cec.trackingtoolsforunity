/*
	Copyright © Carl Emil Carlsen 2025
	http://cec.dk

	Projector calibration using OpenCV calibrateCamera() function. Given a known geometry we feed two sets of points to the function,
	one set in world space, and one in image space.

	This is essentially what tools like Mapamok (Kyle McDonald) and camSchnappr (TouchDesigner) does.
*/

using System.IO;
using UnityEngine;
using UnityEngine.UI;
using OpenCVForUnity.CoreModule;
using System;
using UnityEngine.InputSystem;
using UnityEngine.Experimental.Rendering;

namespace TrackingTools
{
	public class ProjectorFromWorldPointsIntrinsicsExtrinsicsEstimator : MonoBehaviour
	{
		[SerializeField] bool _interactable = false;
		
		[Header("Input")]
		[SerializeField,Tooltip("Used for calibrateCamere() intrinsic guess.")] float _throwRatioGuess = 1.2f;
		[SerializeField,Tooltip("Used for calibrateCamere() intrinsic guess.")] Vector2 _lensshiftGuess = Vector2.zero;
		[SerializeField,Tooltip("Used for initial point positioning.")] Transform _extrinsicGuess = null;
		[SerializeField] Vector2Int _resolution = new Vector2Int( 1920, 1080 );
		[SerializeField] Transform[] _worldPointTransforms = null;

		[Header("Output")]
		[SerializeField,Tooltip("Without extension name (.json)")] string _calibrationPointsFileName = "DefaultProjector";
		[SerializeField,Tooltip("Without extension name (.json)")] string _projectorIntrinsicsFileName = "DefaultProjector";
		[SerializeField,Tooltip("Without extension name (.json)")] string _projectorExtrinsicsFileName = "DefaultProjector";

		[Header("UI")]
		[SerializeField] Camera _calibrationCamera = null;
		[SerializeField] RectTransform _canvasContainerRect;
		[SerializeField] Text _rmsErrorText;
		[SerializeField] Key _interactableHotKey = Key.Digit1;
		[SerializeField] Key _resetHotKey = Key.Backspace;
		[SerializeField] Key _precisionHoldHotKey = Key.LeftCtrl;
		[SerializeField] Font _font = null;
		[SerializeField] int _fontSize = 12;
		[SerializeField,Range(0f,1f)] float _virtualAlpha = 0.8f;
		[SerializeField] Color _pointIdleColor = Color.cyan;
		[SerializeField] Color _pointFocusedColor = Color.magenta;
		[SerializeField] Color _pointActiveColor = Color.yellow;

		[Header("Gizmos")]
		[SerializeField] bool _drawGizmosAlways = true;
		[SerializeField] bool _drawPointIndexLabelGizmos = true;
		[SerializeField] Vector3 _pointLabelOffset = Vector3.zero;
		[SerializeField] bool _drawPointGizmos = true;
		[SerializeField] Color _gizmoColor = Color.white;
		[SerializeField] float _gizmoPointRadius = 0.005f;

		CalibrateCameraOperation _operation;
		Intrinsics _intrinsicGuess;

		bool _dirtyCalibration = true;
		int _controlDisplayIndex;

		RenderTexture _calibrationTexture = null;
		
		// UI.
		Canvas _canvas;
		AspectRatioFitter _aspectFitter;
		RectTransform[] _userPointRects;
		Image[] _userPointImages;
		RawImage _calibrationImage;
		int _focusedPointIndex = -1;
		bool _isPointActive;
		Vector2 _mouseHitAnchoredPosition;
		Vector3[] _worldPoints;
		Vector2[] _imagePoints;

		Color _cameraInitialBackgroundColor;
		bool _cameraInitialActiveState;

		const float mouseHitDistanceMinNormalized = 0.2f; // Normalized to image height
		

		static readonly string logPrepend = "<b>[" + nameof( CameraFromWorldPointsExtrinsicsEstimator ) + "]</b> ";


		public bool interactable {
			get { return _interactable; }
			set { _interactable = value; }
		}

		public float alpha {
			get { return _virtualAlpha; }
			set { _virtualAlpha = Mathf.Clamp01( value ); }
		}

		public Vector2Int resolution
		{
			get { return _resolution; }
			set {
				_resolution = value;
				if( _calibrationTexture && ( _calibrationTexture.width != _resolution.x || _calibrationTexture.height != _resolution.y ) ){
					RecreateCalibrationTexture();
					_dirtyCalibration = true;
				}
			}
		}


		static class ShaderIDs
		{
			public static readonly int _Brightness = Shader.PropertyToID( nameof( _Brightness ) );
		}


		public void SetWorldPointTransforms( Transform[] transforms )
		{
			if( transforms == null ) return;
			if( Application.isPlaying ){
				Debug.LogWarning( $"{logPrepend} Ignored OverrideCalibrationPointTransforms(). Only supported in Editor edit mode for now.\n" );
				return;
			}

			_worldPointTransforms = new Transform[ transforms.Length ];
			Array.Copy( transforms, _worldPointTransforms, _worldPointTransforms.Length );
		}


		/// <summary>
		/// Reset calibration points to fit current camera perspective.
		/// </summary>
		public void ResetImageCalibrationPoints()
		{
			int pointCount = _worldPointTransforms.Length;
			for( int p = 0; p < pointCount; p++ ) {
				Vector2 viewportPoint = _calibrationCamera.WorldToViewportPoint( _worldPointTransforms[ p ].position );
				SetAnchoredPosition( _userPointRects[ p ], viewportPoint );
			}
			_dirtyCalibration = true;
		}


		/// <summary>
		/// Set physical camera intrinsics file name without extension.
		/// </summary>
		public void SetPhysicalCameraIntrinsicsFileName( string physicalCameraIntrinsicsFileName )
		{
			_projectorIntrinsicsFileName = physicalCameraIntrinsicsFileName;
		}


		/// <summary>
		/// Set calibration point file name without extention.
		/// </summary>
		public void SetCalibrationPointFileName( string calibrationPointsFileName )
		{
			_calibrationPointsFileName = calibrationPointsFileName;
			TryLoadCalibrationPoints();
		}


		void OnEnable()
		{
			if( _worldPointTransforms == null || _worldPointTransforms.Length < 3 ){
				Debug.LogWarning( $"{logPrepend} Abort. World points missing. At least three is required.\n" );
				enabled = false;
				return;
			}

			_canvas = _canvasContainerRect.GetComponentInParent<Canvas>();
			_controlDisplayIndex = _canvas.targetDisplay;

			int pointCount = _worldPointTransforms.Length;
			_worldPoints = new Vector3[ pointCount ];
			_imagePoints = new Vector2[ pointCount ];

			_intrinsicGuess = new Intrinsics();
			_intrinsicGuess.UpdateFromProjectorParameters( _throwRatioGuess, _resolution, _lensshiftGuess );
			_intrinsicGuess.ApplyToUnityCamera( _calibrationCamera );

			_calibrationCamera.transform.position = _extrinsicGuess.position;
			_calibrationCamera.transform.rotation = _extrinsicGuess.rotation;

			// Create UI.
			if( !_canvasContainerRect ) {
				_canvasContainerRect = new GameObject( "CameraPoser" ).AddComponent<RectTransform>();
				_canvasContainerRect.transform.SetParent( _canvas.transform );
			}

			ExpandRectTransform( _canvasContainerRect );
			
			_aspectFitter = _canvasContainerRect.gameObject.AddComponent<AspectRatioFitter>();
			_aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
			_aspectFitter.aspectRatio = _resolution.x / (float) _resolution.y;
			
			_calibrationImage = new GameObject("CalibrationTexture").AddComponent<RawImage>();
			_calibrationImage.transform.SetParent( _canvasContainerRect );
			ExpandRectTransform( _calibrationImage.rectTransform );
			

			_userPointRects = new RectTransform[pointCount];
			_userPointImages = new Image[pointCount];
			for( int p = 0; p < pointCount; p++ )
			{
				GameObject pointObject = new GameObject( "Point" + p );
				pointObject.transform.SetParent( _canvasContainerRect );
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

			RecreateCalibrationTexture();
			ResetImageCalibrationPoints();

			_cameraInitialBackgroundColor = _calibrationCamera.backgroundColor;
			_calibrationCamera.backgroundColor = Color.black;
			_cameraInitialActiveState = _calibrationCamera.gameObject.activeSelf;
			_calibrationCamera.gameObject.SetActive( true );

			// Load files.
			TryLoadCalibrationPoints();

			// Update variables.
			OnValidate();
		}


		void OnDisable()
		{
			if( !_userPointRects[0] ) return;

			// Save.
			if( _operation.hasResult ){
				_operation.intrinsicsResult.SaveToFile( _projectorIntrinsicsFileName );
				_operation.extrinsicsResults[0].SaveToFile( _projectorExtrinsicsFileName );
			}
			SaveAnchorPoints();

			// CLean up after the party.
			foreach( var pointRect in _userPointRects ) if( pointRect?.gameObject ) Destroy( pointRect.gameObject );
			_operation?.Release();
			Destroy( _aspectFitter );
			_calibrationTexture?.Release();

			_calibrationTexture = null;

			_calibrationCamera.targetTexture = null;

			_calibrationCamera.backgroundColor = _cameraInitialBackgroundColor;
			_calibrationCamera.gameObject.SetActive( _cameraInitialActiveState );

			_isPointActive = false;
			_focusedPointIndex = -1;
		}


		void Update()
		{
			if( !_calibrationCamera ) return;

			var resolution = new Vector2Int( _calibrationCamera.pixelWidth, _calibrationCamera.pixelHeight );
			if( _operation == null ){
				_operation = new CalibrateCameraOperation( resolution );
			} else if( _operation.resolution != resolution ){
				_operation.SetResolution( resolution );
				_dirtyCalibration = true;
			}

			if( _interactableHotKey != Key.None && Keyboard.current[_interactableHotKey].wasPressedThisFrame ) interactable = !interactable;

			if( _interactable ) UpdateInteraction();

			if( _dirtyCalibration ) UpdateCalibration();
		}


		void OnDrawGizmos() => DrawGizmos( selected: false );
		void OnDrawGizmosSelected() => DrawGizmos( selected: true );


		void OnValidate()
		{
			interactable = _interactable;
			alpha = _virtualAlpha;

			_fontSize = Mathf.Max( 0, _fontSize );
		}


		void DrawGizmos( bool selected )
		{
			if( !selected && !_drawGizmosAlways ) return;

			if( !_drawPointGizmos && !_drawPointIndexLabelGizmos ) return;

			Gizmos.color = _gizmoColor;
			if( _worldPointTransforms != null && _worldPointTransforms.Length > 0 ){
				for( int i = 0; i < _worldPointTransforms.Length; i++ ) {
					var t = _worldPointTransforms[ i ];
					if( t ){
						Vector3 p = t.position;
						if( _drawPointGizmos ) Gizmos.DrawWireSphere( p, _gizmoPointRadius );
						#if UNITY_EDITOR
							if( _drawPointIndexLabelGizmos ) UnityEditor.Handles.Label( p + _pointLabelOffset, i.ToString() );
						#endif
					}
				}
			}
		}



		void UpdateInteraction()
		{
			if( Keyboard.current[ _resetHotKey ].wasPressedThisFrame ){
				ResetImageCalibrationPoints();
			}

			// Only update when interactin with the target display.
			if( Mouse.current.displayIndex.value != _controlDisplayIndex ) return;

			// Get anchored mouse position within image rect.
			Vector2 mousePos = Mouse.current.position.value;
			var canvasRectTransform = _canvas.GetComponent<RectTransform>();
			RectTransformUtility.ScreenPointToLocalPointInRectangle( canvasRectTransform, mousePos, _canvas.worldCamera, out mousePos );
			Vector2 mouseAnchoredPos = LocalPixelPositionToAnchoredPosition( mousePos, canvasRectTransform );

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
					_userPointImages[_focusedPointIndex].color = _pointFocusedColor;
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
					float aspect = _calibrationCamera.pixelWidth / (float) _calibrationCamera.pixelHeight;
					towardsPoint.x *= aspect;
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
							_userPointImages[nearestPointIndex].color = _pointActiveColor;
							_mouseHitAnchoredPosition = mouseAnchoredPos;
							_isPointActive = true;
						} else {
							// Indicate focused.
							_userPointImages[nearestPointIndex].color = _pointFocusedColor;
						}
					}
				} else if( _focusedPointIndex != -1 ){
					// Defocus.
					_isPointActive = false;
					_userPointImages[ _focusedPointIndex ].color = _pointIdleColor;
				}
				_focusedPointIndex = nearestPointIndex;
			}
		}

	
	
		void UpdateCalibration()
		{
			// Update points.
			for( int p = 0; p < _worldPointTransforms.Length; p++ )
			{
				Vector2 posImage = _userPointRects[ p ].anchorMin; // Min and max should be the same.
				posImage.y = 1f - posImage.y; // OpenCv pixels are flipped vertically.
				posImage.Scale( _operation.resolution );
				_imagePoints[ p ] = posImage;
				_worldPoints[ p ] = _worldPointTransforms[ p ].position;
			}

			_operation.ClearSamples();
			_operation.AddSample( _worldPoints, _imagePoints );
			_operation.Update( samplesHaveDistortion: false, useAspect: true, _intrinsicGuess );
			
			// Apply.
			_operation.intrinsicsResult.ApplyToUnityCamera( _calibrationCamera );
			_operation.extrinsicsResults[0].ApplyToTransform( _calibrationCamera.transform );

			if( _rmsErrorText ) _rmsErrorText.text = _operation.rmsErrorResult.ToString( "F2" );

			// Done.
			_dirtyCalibration = false;
		}


		void TryLoadCalibrationPoints()
		{
			string filePath = TrackingToolsConstants.worldPointSetsDirectoryPath + "/" + _calibrationPointsFileName + ".json";
			if( !File.Exists( filePath ) ) return;

			string json = File.ReadAllText( filePath );
			PointSetData data = JsonUtility.FromJson<PointSetData>( json );
			if( data.points.Length != _worldPointTransforms.Length ){
				Debug.LogWarning( logPrepend + "Ignored stored point set. Number of points does not match number of transforms in 'worldPointTransforms'.\n" );
				return;
			}

			for( int p = 0; p < _worldPointTransforms.Length; p++ ) SetAnchoredPosition( _userPointRects[ p ], data.points[ p ] );
			_dirtyCalibration = true;
		}


		void SaveAnchorPoints()
		{
			if( !_userPointRects[ 0 ] ) return;

			if( !Directory.Exists( TrackingToolsConstants.worldPointSetsDirectoryPath ) ) Directory.CreateDirectory( TrackingToolsConstants.worldPointSetsDirectoryPath );
			string filePath = TrackingToolsConstants.worldPointSetsDirectoryPath + "/" + _calibrationPointsFileName + ".json";


			PointSetData data = new PointSetData( _worldPointTransforms.Length );
			for( int p = 0; p < _worldPointTransforms.Length; p++ ) data.points[p] = _userPointRects[ p ].anchorMin;

			File.WriteAllText( filePath, JsonUtility.ToJson( data ) );

			//Debug.Log( logPrepend + "Saved anchor points to file.\n" + filePath );
		}


		void RecreateCalibrationTexture()
		{
			if( _calibrationTexture ) _calibrationTexture.Release();
			_calibrationTexture = new RenderTexture( _resolution.x, _resolution.y, 32, GraphicsFormat.R8G8B8A8_UNorm );
			_calibrationTexture.name = "CalibrationTexture";
			if( _calibrationImage ) _calibrationImage.texture = _calibrationTexture;
			if( _calibrationCamera ) _calibrationCamera.targetTexture = _calibrationTexture;
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