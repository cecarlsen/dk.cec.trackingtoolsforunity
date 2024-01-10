/*
	Copyright © Carl Emil Carlsen 2024
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

		[Header("Output")]
		[SerializeField,Tooltip("Lens shift as defined in Unity camera with 'physicalCamera' enabled.")] UnityEvent<Vector2> _lensShift = new UnityEvent<Vector2>();
		[SerializeField,Tooltip("Degrees")] UnityEvent<float> _verticalFieldOfView = new UnityEvent<float>();

		[Header("Debug")]
		[SerializeField] bool _displayFrustumGizmo = false;
		[SerializeField] float _frustumGizmoNear = 0.1f;
		[SerializeField] float _frustumGizmoFar = 5f;

		[System.Serializable] enum AutoLoadTime { Awake, OnEnable, Start, Off }

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


		public void LoadAndOutput()
		{
			if( !Intrinsics.TryLoadFromFile( _intrinsicsFileName, out _intrinsics ) ){
				Debug.LogError( logPrepend + "Intrinsics file '" + _intrinsicsFileName + "' does not exist.\n" );
				return;
			}

			_lensShift.Invoke( _intrinsics.lensShiftNormalized );
			_verticalFieldOfView.Invoke( _intrinsics.verticalFieldOfView );
		}


		public void OnDrawGizmos()
		{
			
		}
	}
}