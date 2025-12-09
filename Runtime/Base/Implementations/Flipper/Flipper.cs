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


		static class ShaderIDs
		{
			public static readonly int _FlipFlags = Shader.PropertyToID( nameof( _FlipFlags ) );
		}


		public Flipper()
		{
			_material = new Material( Shader.Find( "Hidden/" + nameof( Flipper ) ) );
		}


		public void Flip( RenderTexture texture, bool vertically = true, bool horizontally = false )
		{
			_material.SetVector( ShaderIDs._FlipFlags, new Vector4( horizontally ? 1f : 0f, vertically ? 1f : 0f ) );
			RenderTexture tempTexture = RenderTexture.GetTemporary( texture.descriptor );
			Graphics.Blit( texture, tempTexture, _material );
			Graphics.CopyTexture( tempTexture, texture );
			RenderTexture.active = null; // Otherwise Unity does not like us to release tempTexture.
			RenderTexture.ReleaseTemporary( tempTexture );
		}


		public void Flip( Texture sourceTexture, RenderTexture destinationTexture, bool vertically = true, bool horizontally = false )
		{
			_material.SetVector( ShaderIDs._FlipFlags, new Vector4( horizontally ? 1f : 0f, vertically ? 1f : 0f ) );
			Graphics.Blit( sourceTexture, destinationTexture, _material );
		}


		public void Release()
		{
			Object.Destroy( _material );
		}
	}
}