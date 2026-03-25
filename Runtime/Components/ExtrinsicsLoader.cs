/*
	Copyright © Carl Emil Carlsen 2020-2026
	http://cec.dk
*/

using UnityEngine;

namespace TrackingTools
{
	public class ExtrinsicsLoader : MonoBehaviour
	{
		[SerializeField] string _extrinsicsFileName = "Default";
		[SerializeField,Tooltip( "Optionally make extrinsics relative to anchor." )] Transform _anchorTransform = null;
		[SerializeField] AutoLoadTime _autoLoadTime = AutoLoadTime.Awake;
		[SerializeField] bool _inverse = false;
		[SerializeField,Tooltip("When enabled, anchorTransform.localScale.Max() will be applied to extrinsics translation")] bool _applyUniformAnchorScale = false;
		[SerializeField] bool _isMirroredRelativeToAnchor = false;
		[SerializeField] Vector3 _postRotationLocal = Vector3.zero;
		[SerializeField] Vector3 _postRotationGlobal = Vector3.zero;
		[SerializeField] Vector3 _postOffsetLocal = Vector3.zero;
		[SerializeField] Vector3 _postOffsetGlobal = Vector3.zero;
		[SerializeField] bool _embedInAnchorTransform = false;
		[SerializeField] bool _updateContinously = false;
		[SerializeField] bool _logActions = true;

		[System.Serializable] enum AutoLoadTime { Awake, OnEnable, Start, Off }

		Extrinsics _extrinsics;

		static string logPrepend = "<b>[" + nameof( ExtrinsicsLoader ) + "]</b> ";


		public Vector3 postRotationLocal {
			get => _postRotationLocal;
			set {
				_postRotationLocal = value;
				if( _extrinsics != null ) Apply();
			}
		}


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
			if( _autoLoadTime == AutoLoadTime.Start ) LoadAndApply();
		}


		void Update()
		{
			if( _updateContinously ) Apply();
		}


		public void LoadAndApply()
		{
			if( !Extrinsics.TryLoadFromFile( _extrinsicsFileName, out _extrinsics ) ) {
				return;
			}

			Apply();

			if( _logActions ) Debug.Log( logPrepend + "Loaded extrinsics from file at '" + TrackingToolsHelper.GetExtrinsicsFilePath( _extrinsicsFileName ) + "'.\n" + _extrinsics.ToString() );
		}


		void Apply()
		{
			if( _extrinsics == null ) return;

			_extrinsics.ApplyToTransform( transform, _anchorTransform, _inverse, _isMirroredRelativeToAnchor, _applyUniformAnchorScale );

			if( _anchorTransform && _embedInAnchorTransform ) transform.SetParent( _anchorTransform );

			transform.Rotate( _postRotationLocal, Space.Self );
			transform.Rotate( _postRotationGlobal, Space.World );
			transform.Translate( _postOffsetLocal, Space.Self );
			transform.Translate( _postOffsetGlobal, Space.World );
		}


		public bool FileExists()
		{
			if( string.IsNullOrEmpty( _extrinsicsFileName ) ) return false;
			return Extrinsics.FileExists( _extrinsicsFileName );
		}


		void OnValidate()
		{
			if( Application.isPlaying && _extrinsics != null ) Apply();
		}
	}
}