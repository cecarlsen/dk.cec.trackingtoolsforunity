/*
	Copyright © Carl Emil Carlsen 2020-2024
	http://cec.dk
*/

using UnityEngine;

namespace TrackingTools
{
	public class ExtrinsicsLoader : MonoBehaviour
	{
		[SerializeField] string _extrinsicsFileName = "DefaultCamera";
		[SerializeField,Tooltip("Optional")] Transform _anchorTransform = null;
		[SerializeField] AutoLoadTime _autoLoadTime = AutoLoadTime.Awake;
		[SerializeField] bool _inverse = false;
		[SerializeField] bool _isMirrored = false;
		[SerializeField] Vector3 _postOffsetLocal = Vector3.zero;
		[SerializeField] Vector3 _postOffsetGlobal = Vector3.zero;
		[SerializeField] bool _embedInAnchorTransform = false;
		[SerializeField] bool _updateContinously = false;
		[SerializeField] bool _logActions = true;

		[System.Serializable] enum AutoLoadTime { Awake, OnEnable, Start, Off }

		Extrinsics _extrinsics;

		static string logPrepend = "<b>[" + nameof( ExtrinsicsLoader ) + "]</b> ";


		void Awake()
		{
			if( _autoLoadTime == AutoLoadTime.Awake ) LoadAndApply();
		}


		void OnEnable()
		{
			if( _autoLoadTime == AutoLoadTime.OnEnable ) LoadAndApply();
		}


		void Start()
		{
			if( _autoLoadTime == AutoLoadTime.Start ) LoadAndApply();
		}


		void Update()
		{
			if( _updateContinously ) Apply();
		}


		public void LoadAndApply()
		{
			if( !Extrinsics.TryLoadFromFile( _extrinsicsFileName, out _extrinsics ) ) {
				enabled = false;
				return;
			}

			Apply();

			if( _logActions ) Debug.Log( logPrepend + "Loaded extrinsics from file at '" + TrackingToolsHelper.GetExtrinsicsFilePath( _extrinsicsFileName ) + "'.\n" + _extrinsics.ToString() );
		}


		void Apply()
		{
			_extrinsics.ApplyToTransform( transform, _anchorTransform, _inverse, _isMirrored );

			if( _anchorTransform && _embedInAnchorTransform ) transform.SetParent( _anchorTransform );

			transform.Translate( _postOffsetLocal, Space.Self );
			transform.Translate( _postOffsetGlobal, Space.World );
		}


		void OnValidate()
		{
			if( Application.isPlaying && _extrinsics != null ) Apply();
		}
	}
}