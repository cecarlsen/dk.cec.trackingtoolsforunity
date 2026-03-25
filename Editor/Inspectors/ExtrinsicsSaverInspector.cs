/*
	Copyright © Carl Emil Carlsen 2026
	http://cec.dk
*/

using UnityEngine;
using UnityEditor;

namespace TrackingTools
{
	[CustomEditor(typeof( ExtrinsicsSaver ) )]
	[CanEditMultipleObjects]
	public class ExtrinsicsSaverInspector : Editor
	{
		ExtrinsicsSaver _component;


		void OnEnable()
		{
			_component = target as ExtrinsicsSaver;
		}



		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			DrawDefaultInspector();

			EditorGUILayout.Space();

			if( GUILayout.Button( "Save!" ) ) _component.Save();

			serializedObject.ApplyModifiedProperties();
		}
	}
}