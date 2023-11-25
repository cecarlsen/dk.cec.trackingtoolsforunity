/*
	Copyright © Carl Emil Carlsen 2023
	http://cec.dk
*/

using TrackingTools;
using UnityEngine;
using UnityEngine.Events;

public class LensUndistorterExample : MonoBehaviour
{
	[SerializeField] UnityEvent<RenderTexture> _undistortionMapEvent = new UnityEvent<RenderTexture>();
	[SerializeField] bool _showReferenceImplementationAsGizmos;

	LensUndistorter _generator;

	// Azure Kinect depth map distortion as example.
	const int w = 640;
	const int h = 576;
	const float cx = 322.4581f;
	const float cy = 330.6768f;
	const float fx = 503.973f;
	const float fy = 504.083f;
	const float k1 = 8.359803f;
	const float k2 = 5.312581f;
	const float k3 = 0.2633835f;
	const float k4 = 8.685491f;
	const float k5 = 8.125207f;
	const float k6 = 1.4104f;
	const float p1 = 5.207402E-05f;
	const float p2 = -6.796281E-05f;


	void Start()
	{
		Intrinsics intrinsics = new Intrinsics();
		intrinsics.UpdateRaw(  w, h, cx, cy, fx, fy, k1, k2, k3, k4, k5, k6, p1, p2 );

		_generator = new LensUndistorter( intrinsics );

		if( _undistortionMapEvent != null ) _undistortionMapEvent.Invoke( _generator.undistortionMap );
	} 


	void OnDestroy()
	{
		_generator?.Release();
	}


	void OnDrawGizmos()
	{
		if( !_showReferenceImplementationAsGizmos ) return;

		for( int y = 0; y < h; y += 10 ) {
			for( int x = 0; x < w; x+= 10 )
			{
				Vector2 originalPoint = new Vector3( x, y );
				Vector2 distPoint = LensUndistorter.ReferenceImplementation( x, y, cx, cy, fx, fy, k1, k2, k3, k4, k5, k6, p1, p2 );
		
				Gizmos.color = Color.red;
				Gizmos.DrawCube( originalPoint, Vector3.one );
				Gizmos.color = Color.green;
				Gizmos.DrawCube( distPoint, Vector3.one );
			}
		}
	}
}