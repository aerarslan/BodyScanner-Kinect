using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BodyScanner
{
    /// <summary>
    /// A writeable bitmap holder class
    /// </summary>
    class WriteableBitmapHolder
    {
        /// <summary>
        /// Provides a System.Windows.Media.Imaging.BitmapSource that can be written to and updated.
        /// </summary>
        public WriteableBitmap Bitmap { get; private set; }

        /// <summary>
        /// Updates the pixels in the specified region of the bitmap.
        /// </summary>
        /// <param name="width">Width value</param>
        /// <param name="height">Height value</param>
        /// <param name="data">The pixel array used to update the bitmap.</param>
        public bool WritePixels(int width, int height, Array data)
        {
            var bitmapChanged = EnsureBitmapSize(width, height);

            var rect = new Int32Rect(0, 0, width, height);
            Bitmap.WritePixels(rect, data, width * Bitmap.Format.BitsPerPixel / 8, 0);

            return bitmapChanged;
        }

        /// <summary>
        /// Checks the bitmap size.
        /// </summary>
        /// <param name="width">Width value</param>
        /// <param name="height">Height value</param>
        private bool EnsureBitmapSize(int width, int height)
        {
            if (Bitmap != null && Bitmap.PixelWidth == width && Bitmap.PixelHeight == height)
                return false;

            Bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
            return true;
        }
    }
}
