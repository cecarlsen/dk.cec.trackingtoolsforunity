/*
	Copyright © Carl Emil Carlsen 2022-2024
	http://cec.dk
*/

using UnityEngine;

namespace TrackingTools
{
	[RequireComponent( typeof( Camera) ) ]
	public class CameraIntrinsicsSaver : MonoBehaviour
	{
		[SerializeField] string _intrinsicsFileName = "DefaultCamera";
		[SerializeField] bool _saveOnDestroy = true;
		[SerializeField] bool _saveOnDisable = false;
		[SerializeField] bool _logActions = true;

		static string logPrepend = "<b>[" + nameof( CameraIntrinsicsSaver ) + "]</b> ";


		void OnDisable()
		{
			if( _saveOnDisable ) Save();
		}

		void OnDestroy()
		{
			if( _saveOnDestroy ) Save();
		}


		public void Save()
		{
			Camera cam = GetComponent<Camera>();

			Intrinsics intrinsics = new Intrinsics();
			intrinsics.UpdateFromUnityCamera( cam );
			string filePath = intrinsics.SaveToFile( _intrinsicsFileName );

			if( _logActions ) Debug.Log( logPrepend + "Saved intrinsics to file at '" + filePath + "'.\n" );
		}
	}
}