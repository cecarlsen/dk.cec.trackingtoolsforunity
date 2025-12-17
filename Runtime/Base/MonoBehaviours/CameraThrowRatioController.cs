/*
	Copyright © Carl Emil Carlsen 2025
	http://cec.dk
*/

using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraThrowRatioController : MonoBehaviour
{
	[SerializeField,Range(0.1f,3f)] float _throwRatio = 1.2f;

	Camera _camera;

	public float throwRatio
	{
		get { return _throwRatio; }
		set {
			_throwRatio = value;
			if( !_camera ) _camera = GetComponent<Camera>();
			if( !_camera ) return;
			_camera.fieldOfView = Mathf.Atan2( 0.5f / _camera.aspect, _throwRatio ) * Mathf.Rad2Deg * 2f;
		}
	}


	void OnEnable()
	{
		OnValidate();
	}


	void OnValidate()
	{
		throwRatio = _throwRatio;
	}
}