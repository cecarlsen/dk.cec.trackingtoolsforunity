/*
	Copyright © Carl Emil Carlsen 2024
	http://cec.dk
*/

using UnityEngine;

namespace TrackingTools
{
	public class ExtrinsicsSaver : MonoBehaviour
	{
		[SerializeField] string _extrinsicsFileName = "Default";
		[SerializeField] bool _logActions = true;

		public string extrinsicsFileName {
			get => _extrinsicsFileName;
			set => _extrinsicsFileName = value;
		}


		static string logPrepend = "<b>[" + nameof( ExtrinsicsSaver ) + "]</b> ";



		public void Save()
		{
			var extrinsics = new Extrinsics();
			extrinsics.UpdateFromTransform( transform );
			string filePath = extrinsics.SaveToFile( _extrinsicsFileName );

			if( _logActions ) Debug.Log( logPrepend + "Saved extrinsics to file at '" + filePath + "'.\n" );
		}
	}
}