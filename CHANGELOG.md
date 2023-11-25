## 0.1.2  (2023/09/29)

- Added LensUndistorter, a GPU alternative to OpenCV's Calib3d.undistort, or combined Calib3d.initUndistortRectifyMap and Imgproc.remap.
- Renamed CameraFromCircleAnchorExtrinsicsEstimator to CameraFromWorldPointsExtrinsicsEstimator and updated it to accept any set of 3D points, as defined by Transforms.

## 0.1.1  (2023/02/01)

- Changed some components to use the 'fastAndImprecise' parameter in TrackingToolsHelper.FindChessboardCorners when possible.
- Fixed issues in ProjectorFromCameraExtrinsicsEstimator that produced wrong results when camera and projector had different aspect ratios.


## 0.1.0 (2020/06/01)

- Fixed camera intrinsics being applied without vertical flip.
- Added CameraIntrinsicsSaver.
- Integrated Checkerboard and ProjectorCheckerboard ScriptableObject.
- Tested with OpenCVForUnity 2.4.7 and Unity 2020.3.33.
- Added MultiDisplayFullscreenStarter.
- Fixed missing materials issue in builds.
- Rearranged file structure.


## 0.0.1 (0000/00/00)

- First change-logged version.

