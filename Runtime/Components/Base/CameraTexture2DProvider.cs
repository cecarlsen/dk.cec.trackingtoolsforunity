/*
	Copyright © Carl Emil Carlsen 2024
	http://cec.dk
*/

using UnityEngine;

namespace TrackingTools
{
	public abstract class CameraTexture2DProvider : CameraTextureProvider
	{
		/// <summary>
		/// Latest texture aquired from the camera.
		/// </summary>
		/// <returns></returns>
		public abstract Texture2D GetLatestTexture2D();


		/// <summary>
		/// Get texture at history index. History index 0 is the latest frame while index 1 is the previous frame.
		/// </summary>
		public abstract Texture2D GetHistoryTexture2D( int historyIndex );
	}
}