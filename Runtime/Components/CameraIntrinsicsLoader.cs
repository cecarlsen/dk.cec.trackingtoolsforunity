/*
	Copyright © Carl Emil Carlsen 2020-2022
	http://cec.dk
*/

using UnityEngine;

namespace TrackingTools
{
	[RequireComponent( typeof( Camera) ) ]
	public class CameraIntrinsicsLoader : MonoBehaviour
	{
		[SerializeField] string _intrinsicsFileName = "DefaultCamera";
		[SerializeField] AutoLoadTime _loadTime = AutoLoadTime.Awake;

		[System.Serializable] enum AutoLoadTime { Awake, OnEnable, Start, Off }


		static string logPrepend = "<b>[" + nameof( CameraIntrinsicsLoader) + "]</b> ";


		void Awake()
		{
			if( _loadTime == AutoLoadTime.Awake ) LoadAndApply();
		}


		void OnEnable()
		{
			if( _loadTime == AutoLoadTime.OnEnable ) LoadAndApply();
		}


		void Start()
		{
			if( _loadTime == AutoLoadTime.Start ) LoadAndApply();
		}


		public void LoadAndApply()
		{
			Intrinsics intrinsics;
			if( !Intrinsics.TryLoadFromFile( _intrinsicsFileName, out intrinsics ) ){
				Debug.LogError( logPrepend + "Intrinsics file '" + _intrinsicsFileName + "' does not exist.\n" );
				return;
			}

			Camera cam = GetComponent<Camera>();
			intrinsics.ApplyToUnityCamera( cam );

			//Debug.Log( intrinsics );
		}
	}
}