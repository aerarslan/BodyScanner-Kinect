// 
// Based on code from https://github.com/baSSiLL/BodyScanner
//
using Microsoft.Kinect;
using Microsoft.Kinect.Fusion;
using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using System.Windows;

namespace BodyScanner
{
    /// <summary>
    /// Updates the scan, controls the reconstruction and scan duration.
    /// </summary>
    internal class ScanningEngine
    {
        /// <summary>
        /// The duration of scan in the type of seconds.
        /// </summary>
        public static TimeSpan SCAN_DURATION = TimeSpan.FromSeconds(20);

        /// <summary>
        /// The ending time of the scan.
        /// </summary>
        public static DateTime scanEndTime;

        /// <summary>
        /// Kinect sensor.
        /// </summary>
        private readonly KinectSensor sensor;

        /// <summary>
        /// ReconstructionController func. Invoke() function will be used in Run() task.
        /// </summary>
        private readonly Func<ReconstructionController> controllerFactory;

        /// <summary>
        /// ScanningEngine constructor. Scanning engine will be used as a parameter in AppViewModel constructor function. 
        /// </summary>
        /// <param name="sensor">Active kinect sensor.</param>
        /// <param name="controllerFactory">The reconstruction controller.</param>
        public ScanningEngine(KinectSensor sensor, Func<ReconstructionController> controllerFactory)
        {
            Contract.Requires(sensor != null);
            Contract.Requires(controllerFactory != null);

            this.sensor = sensor;
            this.controllerFactory = controllerFactory;
        }


        /// <summary>
        /// Scan bitmap that holds surface bitmap of reconstruction contoller.
        /// </summary>
        public ThreadSafeBitmap ScanBitmap { get; private set; }

        /// <summary>
        /// Mesh that holds the body mesh of reconstruction controller.
        /// </summary>
        public Mesh ScannedMesh { get; private set; }

        /// <summary>
        /// Vector3 that holds floor normal of reconstruction controller.
        /// </summary>
        public Vector3 FloorNormal { get; private set; }

        /// <summary>
        /// Event handler for scan update
        /// </summary>
        public event EventHandler ScanUpdated;

        /// <summary>
        /// Event handler for scan start
        /// </summary>
        public event EventHandler ScanStarted;

        /// <summary>
        /// Asynchronous method that updates the scan engine. Updates the scan bitmap and scan mesh. Ends when the time is up.
        /// </summary>
        public async Task Run()
        {
            if (!sensor.IsAvailable)
                //throw new ApplicationException(Properties.Resources.KinectNotAvailable);
                MessageBox.Show("Kinect sensor is not available!");

            ReconstructionController controller;
            try
            {
                controller = controllerFactory.Invoke();
            }
            catch
            {
                return;
            }

            using (controller)
            {
                ScanBitmap = controller.SurfaceBitmap;
                RaiseScanUpdated();

                controller.SurfaceBitmapUpdated += (_, __) => RaiseScanUpdated();
                controller.FrameAligned += Controller_FrameAligned;
                controller.ReconstructionStarted += Controller_ReconstructionStarted;

                scanEndTime = DateTime.MaxValue;
                controller.Start();

                // TODO: Should check for various sides of scanning instead
                while (DateTime.UtcNow < scanEndTime)
                {
                    await Task.Delay(1000);
                }

                ScannedMesh = controller.GetBodyMesh();
            }
        }

        /// <summary>
        /// Updates the floor normal and sends controller object. Calculates scan ending time; End Time = Now + Duration
        /// </summary>
        /// <param name="sender">Object sending the event</param>
        /// <param name="e">Event arguments</param>
        private void Controller_ReconstructionStarted(object sender, EventArgs e)
        {
            var controller = (ReconstructionController)sender;
            FloorNormal = controller.FloorNormal;

            scanEndTime = DateTime.UtcNow.Add(SCAN_DURATION);

            ScanStarted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Sends the controller and tells the mesh is started. 
        /// </summary>
        /// <param name="sender">Object sending the event</param>
        /// <param name="e">Event arguments</param>
        private void Controller_FrameAligned(object sender, EventArgs e)
        {
            var controller = (ReconstructionController)sender;
            WindowControl.startSavingSkeleton = true;
            WindowControl.meshStarted = true;
            RaiseScanUpdated();
        }

        /// <summary>
        /// Invokes scan updates event handler.
        /// </summary>
        private void RaiseScanUpdated()
        {
            ScanUpdated?.Invoke(this, EventArgs.Empty);
        }
    }
}
