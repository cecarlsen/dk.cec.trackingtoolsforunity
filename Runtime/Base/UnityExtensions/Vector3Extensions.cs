/*
	Copyright © Carl Emil Carlsen 2026
	http://cec.dk
*/

using UnityEngine;
using System.Runtime.CompilerServices;

namespace TrackingTools
{
	public static class Vector3Extensions
	{
		/// <summary>
		/// Returns the maximum of the vector components.
		/// </summary>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public static float Max( this Vector3 v )
		{
			return v.x > v.y ? ( v.x > v.z ? v.x : v.z ) : ( v.y > v.z ? v.y : v.z );
		}


		/// <summary>
		/// Returns the minimum of the vector components.
		/// </summary>
		[MethodImpl( MethodImplOptions.AggressiveInlining )]
		public static float Min( this Vector3 v )
		{
			return v.x < v.y ? ( v.x < v.z ? v.x : v.z ) : ( v.y < v.z ? v.y : v.z );
		}
	}
}