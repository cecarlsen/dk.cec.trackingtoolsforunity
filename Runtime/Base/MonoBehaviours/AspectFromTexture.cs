/*
	Copyright © Carl Emil Carlsen 2023
	http://cec.dk
*/

using UnityEngine;
using UnityEngine.Events;

public class AspectFromTexture : MonoBehaviour
{
	[SerializeField] Texture _texture;

	[SerializeField] UnityEvent<float> _aspectEvent = new UnityEvent<float>();

	public Texture texture {
		get => _texture;
		set => _texture = value;
	}

	void Update()
	{
		if( !texture || _aspectEvent == null ) return;
		
		_aspectEvent.Invoke( texture.width / (float) texture.height );
	}
}