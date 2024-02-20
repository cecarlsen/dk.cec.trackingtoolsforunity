/*
	Copyright © Carl Emil Carlsen 2024
	http://cec.dk
*/

using UnityEngine;

namespace TrackingTools
{
	public abstract class CameraTextureProvider : MonoBehaviour
	{
		[SerializeField,Range(1,120)] protected int _frameHistoryCapacity = 1;


		/// <summary>
		/// Number of history frames that can be stored.
		/// </summary>
		public int frameHistoryCapacity => _frameHistoryCapacity;

		/// <summary>
		/// Number of history frames that is stored.
		/// </summary>
		public abstract int frameHistoryCount { get; }

		/// <summary>
		/// Number of frames counted since last Unity update.
		/// </summary>
		public abstract int framesSinceLastUnityUpdate { get; }

		/// <summary>
		/// Number of frames aquired and available since last Unity update.
		/// </summary>
		public abstract int framesAquiredSinceLastUnityUpdate { get; }

		/// <summary>
		/// Interval between two latest frames in seconds.
		/// </summary>
		public abstract double latestFrameInterval { get; }

		/// <summary>
		/// Get latest frame number.
		/// </summary>
		public abstract long latestFrameNumber { get; }

		/// <summary>
		/// Aspect
		/// </summary>
		public float aspect {
			get {
				Texture texture = GetLatestTexture();
				if( !texture ) return 1f;
				return texture.width / (float) texture.height;
			}
		}


		/// <summary>
		/// Latest texture aquired from the camera.
		/// </summary>
		/// <returns></returns>
		public abstract Texture GetLatestTexture();


		/// <summary>
		/// Get texture at history index. History index 0 is the latest frame while index 1 is the previous frame.
		/// </summary>
		public abstract Texture GetHistoryTexture( int historyIndex );


		/// <summary>
		/// Get latest frame time in seconds, measured relative to capture begin time. 
		/// </summary>
		public abstract double GetLatestFrameTime();


		/// <summary>
		/// Get history frame time in seconds, measured relative to capture begin time. History index 0 is the latest frame while index 1 is the previous frame.
		/// </summary>
		public abstract double GetHistoryFrameTime( int historyIndex );
	}
}