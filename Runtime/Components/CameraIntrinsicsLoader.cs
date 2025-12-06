/*
	Copyright © Carl Emil Carlsen 2020-2024
	http://cec.dk
*/

using UnityEngine;

namespace TrackingTools
{
	[RequireComponent( typeof( Camera) ) ]
	public class CameraIntrinsicsLoader : MonoBehaviour
	{
		[SerializeField] string _intrinsicsFileName = "DefaultCamera";
		[SerializeField] AutoLoadTime _autoLoadTime = AutoLoadTime.Awake;
		[SerializeField] bool _logActions = true;
		[SerializeField] bool _drawFrustumAlways = false;
		[SerializeField] Color _frustumGizmoColor = new Color( 1f, 1f, 1f, 0.5f );

		[System.Serializable] enum AutoLoadTime { Awake, OnEnable, Start, Off }

		Camera _cam;

		bool _gizmoSelected;

		static string logPrepend = "<b>[" + nameof( CameraIntrinsicsLoader) + "]</b> ";


		void Awake()
		{
			if( enabled && _autoLoadTime == AutoLoadTime.Awake ) LoadAndApply();

		}


		void OnEnable()
		{
			if( _autoLoadTime == AutoLoadTime.OnEnable ) LoadAndApply();
		}


		void Start()
		{
			if( enabled && _autoLoadTime == AutoLoadTime.Start ) LoadAndApply();
		}


		public void LoadAndApply()
		{
			Intrinsics intrinsics;
			if( !Intrinsics.TryLoadFromFile( _intrinsicsFileName, out intrinsics ) ){
				Debug.LogError( logPrepend + "Intrinsics file '" + _intrinsicsFileName + "' does not exist.\n" );
				return;
			}

			if( !_cam ) _cam = GetComponent<Camera>();
			if( !_cam ) return;

			intrinsics.ApplyToUnityCamera( _cam );

			if( _logActions ) Debug.Log( logPrepend + "Loaded intrinsics from file at '" + TrackingToolsHelper.GetIntrinsicsFilePath( _intrinsicsFileName ) + "'.\n" );
		}


		void OnDrawGizmos()
		{
			if( !_drawFrustumAlways ) return;

			if( !_cam ) _cam = GetComponent<Camera>();
			if( !_cam ) return;
			
			Gizmos.color = _frustumGizmoColor;
			TrackingToolsGizmos.DrawWireFrustum( _cam.worldToCameraMatrix, _cam.projectionMatrix );
		}
	}
}