// 
// Based on code from https://github.com/baSSiLL/BodyScanner
//
using Microsoft.Kinect;
using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace BodyScanner
{
    /// <summary>
    /// A helper class for AppvViewModel.cs and StaticKinectWindow.xaml.cs. Creates kinect frame renderer
    /// </summary>
    class KinectFrameRenderer
    {
        /// <summary>
        /// Depth to Color Converter. Is used in FillBitMap and UnmirrorAndFillBitmap functions.
        /// </summary>
        private readonly DepthToColorConverter converter;

        /// <summary>
        /// Reader for depth frames.
        /// </summary>
        private readonly DepthFrameReader reader;

        /// <summary>
        /// Pointer to the DepthFrame image data.
        /// </summary>
        private readonly ushort[] frameData;

        /// <summary>
        /// Provides the basic functionality for propagating a synchronization context in
        /// various synchronization models.
        /// </summary>
        private readonly SynchronizationContext syncContext;

        /// <summary>
        /// Directly accesses the underlying image buffer of the DepthFrame to 
        /// create a displayable bitmap.
        /// This function requires the /unsafe compiler option as we make use of direct
        /// access to the native memory pointed to by the depthFrameData pointer.
        /// </summary>
        /// <param name="sensor">Active kinect sensor.</param>
        /// <param name="converter">Depth to color converter.</param>
        public KinectFrameRenderer(KinectSensor sensor, DepthToColorConverter converter)
        {
            Contract.Requires(sensor != null);
            Contract.Requires(converter != null);

            this.syncContext = SynchronizationContext.Current;
            this.converter = converter;

            // Frame description for the format.
            var depthFrameDesc = sensor.DepthFrameSource.FrameDescription;

            // Gets the pixel count.
            var pixelCount = depthFrameDesc.Width * depthFrameDesc.Height;
            frameData = new ushort[pixelCount];
            Bitmap = new ThreadSafeBitmap(depthFrameDesc.Width, depthFrameDesc.Height);

            reader = sensor.DepthFrameSource.OpenReader();
            reader.FrameArrived += Reader_FrameArrived;
        }

        /// <summary>
        /// Bool for Mirror condition. Is used in Reader_FrameArrived function.
        /// </summary>
        public bool Mirror
        {
            get { return mirror; }
            set { mirror = value; }
        }
        private volatile bool mirror;

        /// <summary>
        /// Bitmap object.
        /// </summary>
        public ThreadSafeBitmap Bitmap { get; }

        /// <summary>
        /// EventHandler for updating bitmap.
        /// </summary>
        public event EventHandler BitmapUpdated;

        /// <summary>
        /// Invokes bitmap event handler
        /// </summary>
        private void RaiseBitmapUpdated()
        {
            BitmapUpdated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Handles the depth frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">Event arguments</param>
        private void Reader_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            var frame = e.FrameReference.AcquireFrame();
            if (frame != null)
            {
                using (frame)
                {
                    frame.CopyFrameDataToArray(frameData);
                }

                var fillAction = mirror ? new Action<int[]>(FillBitmap) : new Action<int[]>(UnmirrorAndFillBitmap);
                Task.Run(() => Bitmap.Access(fillAction)).
                    ContinueWith(_ => AfterRender());
            }
        }

        /// <summary>
        /// Fills the Bitmap.
        /// </summary>
        /// <param name="bitmapData">The bitmap data</param>
        private void FillBitmap(int[] bitmapData)
        {
            for (int iDepth = 0, iBitmap = 0; iDepth < frameData.Length; iDepth++, iBitmap++)
            {
                var color = converter.Convert(frameData[iDepth]);
                WritePixel(bitmapData, iBitmap, color);
            }
        }

        /// <summary>
        /// Unmirrors then fills the Bitmap.
        /// </summary>
        /// <param name="bitmapData">The bitmap data</param>
        private void UnmirrorAndFillBitmap(int[] bitmapData)
        {
            var iBitmap = 0;
            while (iBitmap < bitmapData.Length)
            {
                var iDepth = iBitmap + Bitmap.Width - 1;
                for (var x = 0; x < Bitmap.Width; x++)
                {
                    var color = converter.Convert(frameData[iDepth--]);
                    WritePixel(bitmapData, iBitmap++, color);
                }
            }
        }

        /// <summary>
        /// Writes pixels to the bitmapData.
        /// </summary>
        /// <param name="bitmapData">The bitmap data</param>
        /// <param name="iBitmap">bitmap values</param>
        /// <param name="color">ARGB Color</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WritePixel(int[] bitmapData, int iBitmap, Color color)
        {
            bitmapData[iBitmap] = color.B + (color.G << 8) + (color.R << 16) + (color.A << 24);
        }

        /// <summary>
        /// Dispatches an asynchronous message to a synchronization
        /// context.
        /// </summary>
        private void AfterRender()
        {
            syncContext.Post(RaiseBitmapUpdated);
        }
    }
}
