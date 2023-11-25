/*
	Copyright © Carl Emil Carlsen 2020-2023
	http://cec.dk
*/

﻿using System;
using UnityEngine;

namespace TrackingTools
{
	[Serializable]
	public class PointSetData
	{
		public Vector2[] points;


		public PointSetData( int capacity )
		{
			points = new Vector2[ capacity ];
		}
	}
}