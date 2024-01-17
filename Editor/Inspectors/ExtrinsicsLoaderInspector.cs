/*
	Copyright © Carl Emil Carlsen 2022-2024
	http://cec.dk
*/

using UnityEngine;
using UnityEditor;

namespace TrackingTools
{
	[CustomEditor(typeof( ExtrinsicsLoader ) )]
	public class ExtrinsicsLoaderInspector : Editor
	{
		ExtrinsicsLoader _component;


		void OnEnable()
		{
			_component = target as ExtrinsicsLoader;
		}



		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			DrawDefaultInspector();

			EditorGUILayout.Space();

			if( GUILayout.Button( "Load!" ) ) _component.LoadAndApply();

			serializedObject.ApplyModifiedProperties();
		}
	}
}