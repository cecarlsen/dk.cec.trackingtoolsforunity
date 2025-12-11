/*
	Copyright © Carl Emil Carlsen 2024-2025
	http://cec.dk
*/

using UnityEngine;
using UnityEngine.Events;

namespace TrackingTools
{
	public class IntrinsicsLoader : MonoBehaviour
	{
		[SerializeField] string _intrinsicsFileName = "DefaultCamera";
		[SerializeField] AutoLoadTime _loadTime = AutoLoadTime.Awake;
		[SerializeField] bool _logActions = true;
		[SerializeField,Tooltip("We use an arbitrary focal length to derive the sensor size.")] float _focalLength = 50f;

		[Header("Output")]
		[SerializeField,Tooltip("Focal length, sensorsize, lens shift." )] UnityEvent<float,Vector2,Vector2> _intrinsicsEvent = new();
		[SerializeField,Tooltip("Just forwarding the focal length as defined above." )] UnityEvent<float> _focalLengthEvent = new();
		[SerializeField,Tooltip("Sensor size as defined in Unity camera with 'physicalCamera' enabled.")] UnityEvent<Vector2> _derivedSensorSizeEvent = new();
		[SerializeField,Tooltip("Lens shift as defined in Unity camera with 'physicalCamera' enabled.")] UnityEvent<Vector2> _lensShiftEvent = new();
		[SerializeField,Tooltip("Degrees")] UnityEvent<float> _verticalFieldOfViewEvent = new();
		[SerializeField,Tooltip("Degrees")] UnityEvent<float> _horizontalFieldOfViewEvent = new();

		[Header("Debug")]
		[SerializeField] GizmoMode _displayFrustumGizmo = GizmoMode.Never;
		[SerializeField] float _frustumGizmoNear = 0.1f;
		[SerializeField] float _frustumGizmoFar = 5f;

		[System.Serializable] enum AutoLoadTime { Awake, OnEnable, Start, Off }
		[System.Serializable] enum GizmoMode { Never, Always, OnSelected }

		Intrinsics _intrinsics;

		static string logPrepend = "<b>[" + nameof( IntrinsicsLoader ) + "]</b> ";

		public Intrinsics intrinsics => _intrinsics;


		void Awake()
		{
			if( _loadTime == AutoLoadTime.Awake ) LoadAndOutput();
		}


		void OnEnable()
		{
			if( _loadTime == AutoLoadTime.OnEnable ) LoadAndOutput();
		}


		void Start()
		{
			if( _loadTime == AutoLoadTime.Start ) LoadAndOutput();
		}


		public void SetIntrinsicsFileName( string intrinsicsFileName )
		{
			_intrinsicsFileName = intrinsicsFileName;
		}


		public void LoadAndOutput()
		{
			if( !Intrinsics.TryLoadFromFile( _intrinsicsFileName, out _intrinsics ) ){
				Debug.LogError( logPrepend + "Intrinsics file '" + _intrinsicsFileName + "' does not exist.\n" );
				return;
			}

			if( _logActions ) Debug.Log( logPrepend + "Loaded intrinsics from file at '" + TrackingToolsHelper.GetIntrinsicsFilePath( _intrinsicsFileName ) + "'.\n" );

			var sensorSize = _intrinsics.GetDerivedSensorSize( _focalLength );
			var lensShift = _intrinsics.lensShift;
			_intrinsicsEvent.Invoke( _focalLength, sensorSize, lensShift );
			_focalLengthEvent.Invoke( _focalLength );
			_derivedSensorSizeEvent.Invoke( sensorSize );
			_lensShiftEvent.Invoke( lensShift );
			_verticalFieldOfViewEvent.Invoke( _intrinsics.verticalFieldOfView );
			_horizontalFieldOfViewEvent.Invoke( _intrinsics.horizontalFieldOfView );
		}


		public void OnDrawGizmos()
		{
			if( _displayFrustumGizmo == GizmoMode.Always ) DrawFrustumGizmo();
		}


		public void OnDrawGizmosSelected()
		{
			if( _displayFrustumGizmo == GizmoMode.OnSelected ) DrawFrustumGizmo();
		}


		void DrawFrustumGizmo()
		{
			if( _intrinsics == null ) return;

			TrackingToolsGizmos.DrawWireFrustum( transform.worldToLocalMatrix, _intrinsics.ToProjetionMatrix( _frustumGizmoNear, _frustumGizmoFar ), worldToCameraMatrixIsWorldToLocalMatrix: true );
		}
	}
}