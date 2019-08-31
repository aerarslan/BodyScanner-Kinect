// Based on code from Reconstruction.cs, ReconstructionParameters.cs, copyright (c) Microsoft 
// Based on code from https://github.com/baSSiLL/BodyScanner

using Microsoft.Kinect;
using Microsoft.Kinect.Fusion;
using System;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BodyScanner
{
    /// <summary>
    /// A class that is used to setup the volume parameters and encapsulates reconstruction volume creation updating,meshing functions
    /// and removing nonbody pixels to extract body.
    /// </summary>
    class ReconstructionController : IDisposable
    {
        /// <summary>
        /// Minimum Depth value can kinect sensor see.
        /// </summary>
        public static float MIN_DEPTH = 1.5f;

        /// <summary>
        /// Maximum Depth value can kinect sensor see.
        /// </summary>
        public static float MAX_DEPTH = 3.5f;

        /// <summary>
        /// Synchronization context. Post() function will be used. Dispatches an asynchronous message to a synchronization
        /// </summary>
        private readonly SynchronizationContext syncContext;

        /// <summary>
        ///  Notifies a waiting thread that an event has occurred.
        /// </summary>
        private readonly SharedCriticalSection syncProcessing = new SharedCriticalSection();

        /// <summary>
        /// Bool variable of Disposed() function.
        /// </summary>
        private bool isDisposed;

        /// <summary>
        /// The sensor.
        /// </summary>
        private readonly KinectSensor sensor;

        /// <summary>
        /// Multi Source Frame Reader.
        /// </summary>
        private MultiSourceFrameReader reader;

        /// <summary>
        /// Reconstruction object.
        /// </summary>
        private readonly Reconstruction reconstruction;

        /// <summary>
        /// Represents raw depth data.
        /// </summary>
        private readonly ushort[] rawDepthData;

        /// <summary>
        /// Represents body index data
        /// </summary>
        private readonly byte[] bodyIndexData;

        /// <summary>
        /// The object to access to the dimensions, format and pixel data for a depth frame. (float based images)
        /// </summary>
        private readonly FusionFloatImageFrame floatDepthFrame;

        /// <summary>
        /// The object to access to the dimensions, format and pixel data for a depth frame. (float based point cloud images)
        /// </summary>
        private readonly FusionPointCloudImageFrame pointCloudFrame;

        /// <summary>
        /// The object to access to the dimensions, format and pixel data for a depth frame. (32bit - RGBA based images)
        /// </summary>
        private readonly FusionColorImageFrame surfaceFrame;

        /// <summary>
        /// The best guess of the camera pose
        /// </summary>
        private Matrix4 worldToCameraTransform = Matrix4.Identity;

        /// <summary>
        ///  A Matrix4 instance, containing the world to volume transform.
        /// </summary>
        private Matrix4 worldToVolumeTransform;

        /// <summary>
        ///  The ID of reconstructed body tracking.
        /// </summary>
        private ulong reconstructedBodyTrackingId = ulong.MaxValue;

        /// <summary>
        ///  The place where reconstruction is done. Creates floatDepthFrame, pointCloudFrame and surfaceFrame 
        ///  to be used in ProcessFrame() function.
        /// </summary>
        /// <param name="sensor">The sensor.</param>
        public ReconstructionController(KinectSensor sensor)
        {
            Contract.Requires(sensor != null);

            this.syncContext = SynchronizationContext.Current;
            this.sensor = sensor;

            var rparams = new ReconstructionParameters(128, 256, 256, 256);
            reconstruction = Reconstruction.FusionCreateReconstruction(rparams, ReconstructionProcessor.Amp, -1, worldToCameraTransform);
            worldToVolumeTransform = reconstruction.GetCurrentWorldToVolumeTransform();
            worldToVolumeTransform.M43 -= MIN_DEPTH * rparams.VoxelsPerMeter;
            reconstruction.ResetReconstruction(worldToCameraTransform, worldToVolumeTransform);

            var depthFrameDesc = sensor.DepthFrameSource.FrameDescription;

            var totalPixels = depthFrameDesc.Width * depthFrameDesc.Height;
            rawDepthData = new ushort[totalPixels];
            bodyIndexData = new byte[totalPixels];
            SurfaceBitmap = new ThreadSafeBitmap(depthFrameDesc.Width, depthFrameDesc.Height);

            var intrinsics = sensor.CoordinateMapper.GetDepthCameraIntrinsics();
            var cparams = new CameraParameters(
                intrinsics.FocalLengthX / depthFrameDesc.Width, 
                intrinsics.FocalLengthY / depthFrameDesc.Height, 
                intrinsics.PrincipalPointX / depthFrameDesc.Width, 
                intrinsics.PrincipalPointY / depthFrameDesc.Height);
            floatDepthFrame = new FusionFloatImageFrame(depthFrameDesc.Width, depthFrameDesc.Height, cparams);
            pointCloudFrame = new FusionPointCloudImageFrame(depthFrameDesc.Width, depthFrameDesc.Height, cparams);
            surfaceFrame = new FusionColorImageFrame(depthFrameDesc.Width, depthFrameDesc.Height, cparams);
        }

        /// <summary>
        /// Responsible from disposing floatDepthFrame, pointCloudFrame, surfaceFrame abd reconstruction.
        /// </summary>
        public void Dispose()
        {
            if (!isDisposed)
            {
                syncProcessing.Enter();

                isDisposed = true;

                reader?.Dispose();

                floatDepthFrame?.Dispose();
                pointCloudFrame?.Dispose();
                surfaceFrame?.Dispose();

                reconstruction?.Dispose();
            }
        }

        /// <summary>
        /// Eventhandler for the start of reconstruction.
        /// </summary>
        public event EventHandler ReconstructionStarted;

        /// <summary>
        /// Variable to hold floor normal.
        /// </summary>
        public Vector3 FloorNormal { get; private set; }

        /// <summary>
        /// Surface Bitmap. Is used in ProcessFrame() function.
        /// </summary>
        public ThreadSafeBitmap SurfaceBitmap { get; }

        /// <summary>
        /// Eventhandler for updating surfacebitmap
        /// </summary>
        public event EventHandler SurfaceBitmapUpdated;

        /// <summary>
        /// Updates the multi source frame.
        /// </summary>
        public void Start()
        {
            reader = sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Body | FrameSourceTypes.BodyIndex);
            reader.MultiSourceFrameArrived += Reader_MultiSourceFrameArrived;
        }

        /// <summary>
        /// Calculates the body mesh.
        /// </summary>
        public Mesh GetBodyMesh()
        {
            return reconstruction.CalculateMesh(1);
        }

        /// <summary>
        /// Event handler for multiSourceFrame arrived event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            if (!syncProcessing.TryEnter())
                return;

            byte bodyIndex = 255;
            var frame = e.FrameReference.AcquireFrame();
            var isValidFrame = frame != null;
            if (isValidFrame && StaticKinectWindow.startRecording == true)
            {
                using (var bodyFrame = frame.BodyFrameReference.AcquireFrame())
                {
                    isValidFrame = bodyFrame != null;
                    if (isValidFrame)
                    {
                        if (!IsReconstructing)
                        {
                            SelectBodyToReconstruct(bodyFrame);
                            if (IsReconstructing)
                            {
                                var floorClipPlane = bodyFrame.FloorClipPlane;
                                syncContext.Post(() => OnReconstructionStarted(floorClipPlane));
                            }
                        }

                        if (IsReconstructing)
                        {
                            bodyIndex = GetReconstructedBodyIndex(bodyFrame);
                            isValidFrame = bodyIndex != byte.MaxValue;
                        }
                    }
                }

                if (isValidFrame && IsReconstructing)
                {
                    using (var depthFrame = frame.DepthFrameReference.AcquireFrame())
                    using (var bodyIndexFrame = frame.BodyIndexFrameReference.AcquireFrame())
                    {
                        isValidFrame = depthFrame != null && bodyIndexFrame != null;
                        if (isValidFrame)
                        {
                            depthFrame.CopyFrameDataToArray(rawDepthData);
                            bodyIndexFrame.CopyFrameDataToArray(bodyIndexData);
                        }
                    }
                }
            }

            if (isValidFrame && IsReconstructing)
            {
                Task.Run(() => ProcessFrame(bodyIndex)).
                    ContinueWith(_ => syncProcessing.Exit());
            }
            else
            {
                syncProcessing.Exit();
            }
        }

        #region Reconstruction

        /// <summary>
        /// Gets the X, Y, Z coordinates of given Vector4.
        /// </summary>
        /// <param name="floorClipPlane">4D vector</param>
        private void OnReconstructionStarted(Vector4 floorClipPlane)
        {
            FloorNormal = new Vector3 { X = floorClipPlane.X, Y = floorClipPlane.Y, Z = floorClipPlane.Z };
            ReconstructionStarted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// A float to receive a value describing how well the observed frame aligns to the model with
        /// the calculated pose. A larger magnitude value represent more discrepancy, and a lower value
        /// represent less discrepancy. Note that it is unlikely an exact 0 (perfect alignment) value 
        /// will ever be returned as every frame from the sensor will contain some sensor noise.
        /// </summary>
        public float LastFrameAlignmentEnergy => alignmentEnergy;
        private float alignmentEnergy;

        /// <summary>
        /// Event handler for FrameAligned.
        /// </summary>
        public event EventHandler FrameAligned;

        /// <summary>
        /// Returns the bool of Is reconstructing or not
        /// </summary>
        public bool IsReconstructing => reconstructedBodyTrackingId != ulong.MaxValue;

        /// <summary>
        /// A high-level function to process a depth frame through the Kinect Fusion pipeline.
        /// </summary>
        /// <param name="bodyIndex">The body index>
        private void ProcessFrame(byte bodyIndex)
        {
            try
            {
                RemoveNonBodyPixels(bodyIndex);

                reconstruction.DepthToDepthFloatFrame(rawDepthData, floatDepthFrame,
                    MIN_DEPTH, MAX_DEPTH,
                    false);

                var aligned = reconstruction.ProcessFrame(
                    floatDepthFrame,
                    FusionDepthProcessor.DefaultAlignIterationCount,
                    FusionDepthProcessor.DefaultIntegrationWeight,
                    out alignmentEnergy,
                    worldToCameraTransform);
                if (aligned)
                {
                    syncContext.Post(() => FrameAligned?.Invoke(this, EventArgs.Empty));
                    worldToCameraTransform = reconstruction.GetCurrentWorldToCameraTransform();
                }
            }
            catch (InvalidOperationException)
            {
            }

            try
            {
                reconstruction.CalculatePointCloud(pointCloudFrame, worldToCameraTransform);

                FusionDepthProcessor.ShadePointCloud(pointCloudFrame, worldToCameraTransform, surfaceFrame, null);
                SurfaceBitmap.Access(data => surfaceFrame.CopyPixelDataTo(data));

                syncContext.Post(() => SurfaceBitmapUpdated?.Invoke(this, EventArgs.Empty));
            }
            catch (InvalidOperationException)
            {
            }
        }

        #endregion

        #region Body procesing

        /// <summary>
        /// Select the body depanding on the position of Its Z coordinate of SpineBase joint.
        /// </summary>
        /// <param name="bodyFrame">The body frame>
        private void SelectBodyToReconstruct(BodyFrame bodyFrame)
        {
            if (bodyFrame.BodyCount == 0)
                return;

            var bodies = new Body[bodyFrame.BodyCount];
            bodyFrame.GetAndRefreshBodyData(bodies);

            var minBodyZ = float.PositiveInfinity;
            foreach (var body in bodies.Where(IsBodySuitableForReconstruction))
            {
                var z = body.Joints[JointType.SpineBase].Position.Z;
                if (z < minBodyZ)
                {
                    minBodyZ = z;
                    reconstructedBodyTrackingId = body.TrackingId;
                }
            }
        }

        /// <summary>
        /// Checks If the body is suitable for reconstruction. Returns a specific area that around body.
        /// </summary>
        /// <param name="body">The body.>
        private static bool IsBodySuitableForReconstruction(Body body)
        {
            if (!body.IsTracked) return false;

            var spineBase = body.Joints[JointType.SpineBase];
            if (spineBase.TrackingState != TrackingState.Tracked) return false;

            var middleZ = (MIN_DEPTH + MAX_DEPTH) / 2;
            return middleZ - 0.5f < spineBase.Position.Z && spineBase.Position.Z < middleZ + 0.5f &&
                -0.5f < spineBase.Position.X && spineBase.Position.X < 0.5f;
        }

        /// <summary>
        /// Gets the reconstructed body index.
        /// </summary>
        /// <param name="bodyFrame">The body frame.>
        private byte GetReconstructedBodyIndex(BodyFrame bodyFrame)
        {
            if (bodyFrame.BodyCount == 0)
                return byte.MaxValue;

            var bodies = new Body[bodyFrame.BodyCount];
            bodyFrame.GetAndRefreshBodyData(bodies);

            for (var i = 0; i < bodies.Length; i++)
            {
                if (bodies[i].IsTracked && bodies[i].TrackingId == reconstructedBodyTrackingId)
                    return (byte)i;
            }

            return byte.MaxValue;
        }

        /// <summary>
        /// Removes non body pixels
        /// </summary>
        /// <param name="bodyIndex">The body index.>
        private void RemoveNonBodyPixels(int bodyIndex)
        {
            for (var i = 0; i < rawDepthData.Length; i++)
            {
                if (bodyIndexData[i] != bodyIndex)
                {
                    rawDepthData[i] = 0;
                }
            }
        }

        #endregion
    }
}
