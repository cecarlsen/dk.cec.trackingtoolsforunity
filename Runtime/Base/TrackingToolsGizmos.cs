/*
	Copyright © Carl Emil Carlsen 2024
	http://cec.dk
*/

using UnityEngine;

namespace TrackingTools
{
	public static class TrackingToolsGizmos
	{
		/// <summary>
		/// Draw a view projection matrix frustum.
		/// </summary>
		public static void DrawWireFrustum( Matrix4x4 worldToCameraMatrix, Matrix4x4 projectionMatrix, bool worldToCameraMatrixIsWorldToLocalMatrix = false )
		{
			// Unity's view matrix is negated on the z-axis to reflect GL view space. https://forum.unity.com/threads/reproducing-cameras-worldtocameramatrix.365645/
			if( worldToCameraMatrixIsWorldToLocalMatrix )
			{
				worldToCameraMatrix.m20 *= -1f;
				worldToCameraMatrix.m21 *= -1f;
				worldToCameraMatrix.m22 *= -1f;
				worldToCameraMatrix.m23 *= -1f;
			}

			Matrix4x4 worldToNdc = projectionMatrix * worldToCameraMatrix;

			DrawWireFrustum( worldToNdc );
		}


		/// <summary>
		/// Draw a projection matrix frustum.
		/// </summary>
		public static void DrawWireFrustum( Matrix4x4 projectionMatrix )
		{
			Matrix4x4 ndcToWorld = projectionMatrix.inverse;

			Vector3 n0 = ndcToWorld.MultiplyPoint( new Vector3( -1f, -1f, -1f ) );
			Vector3 n1 = ndcToWorld.MultiplyPoint( new Vector3( -1f, 1f, -1f ) );
			Vector3 n2 = ndcToWorld.MultiplyPoint( new Vector3( 1f, 1f, -1f ) );
			Vector3 n3 = ndcToWorld.MultiplyPoint( new Vector3( 1f, -1f, -1f ) );
			Vector3 f0 = ndcToWorld.MultiplyPoint( new Vector3( -1f, -1f, 1f ) );
			Vector3 f1 = ndcToWorld.MultiplyPoint( new Vector3( -1f, 1f, 1f ) );
			Vector3 f2 = ndcToWorld.MultiplyPoint( new Vector3( 1f, 1f, 1f ) );
			Vector3 f3 = ndcToWorld.MultiplyPoint( new Vector3( 1f, -1f, 1f ) );

			// Near rect.
			Gizmos.DrawLine( n0, n1 );
			Gizmos.DrawLine( n1, n2 );
			Gizmos.DrawLine( n2, n3 );
			Gizmos.DrawLine( n3, n0 );

			// Far rect.
			Gizmos.DrawLine( f0, f1 );
			Gizmos.DrawLine( f1, f2 );
			Gizmos.DrawLine( f2, f3 );
			Gizmos.DrawLine( f3, f0 );

			// Frustum vectors.
			Gizmos.DrawLine( n0, f0 );
			Gizmos.DrawLine( n1, f1 );
			Gizmos.DrawLine( n2, f2 );
			Gizmos.DrawLine( n3, f3 );
		}
	}
}