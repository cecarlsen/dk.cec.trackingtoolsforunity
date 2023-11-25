/*
	Copyright © Carl Emil Carlsen 2023
	http://cec.dk
*/

using UnityEngine;

namespace TrackingTools
{
	public class Flipper
	{
		Material _material;


		public Flipper()
		{
			_material = new Material( Shader.Find( "Hidden/" + nameof( Flipper ) ) );
		}


		public void Flip( RenderTexture texture )
		{
			RenderTexture tempTexture = RenderTexture.GetTemporary( texture.descriptor );
			Graphics.Blit( texture, tempTexture, _material );
			Graphics.CopyTexture( tempTexture, texture );
			RenderTexture.active = null; // Otherwise Unity does not like us to release tempTexture.
			RenderTexture.ReleaseTemporary( tempTexture );
		}


		public void Flip( Texture sourceTexture, RenderTexture destinationTexture )
		{
			Graphics.Blit( sourceTexture, destinationTexture, _material );
		}


		public void Release()
		{
			Object.Destroy( _material );
		}
	}
}