// 
// Based on code from https://github.com/baSSiLL/BodyScanner
//
using System;
using System.Windows.Media;


namespace BodyScanner
{
    /// <summary>
    /// A class to convert depth to color. Is used with KinectFrameRenderer
    /// </summary
    class DepthToColorConverter
    {
        /// <summary>
        /// The minimum reliable depth value for the frame.
        /// </summary>
        private const ushort minDepth = 500;

        /// <summary>
        /// The maximum reliable depth value for the frame.
        /// </summary>
        private const ushort maxDepth = 5000;

        /// <summary>
        /// The palette to use while converting depth to color.
        /// Describes a color in terms of alpha, red, green, and blue channels.
        /// </summary>
        private readonly Color[] palette;

        /// <summary>
        /// Constructor function. 
        /// </summary>
        public DepthToColorConverter()
        {
            palette = CreatePalette();
        }

        /// <summary>
        /// Creates the palette that represents depth values.
        /// </summary>
        private static Color[] CreatePalette()
        {
            var coeff = 255f / (maxDepth - minDepth);
            var palette = new Color[maxDepth - minDepth + 1];
            for (var depth = minDepth; depth <= maxDepth; depth++)
            {
                var grey = (byte)(coeff * (depth - minDepth));
                palette[depth - minDepth] = Color.FromArgb(255, grey, grey, grey);
            }
            return palette;
        }

        /// <summary>
        /// Converts the depth into color. This function is used in KinectFrameRenderer FillBitmap and UnmirrorAndFillBitmap functions.
        /// </summary>
        /// <param name="depth">The depth pixel count.</param>
        public Color Convert(ushort depth)
        {
            return palette[Math.Max(minDepth, Math.Min(maxDepth, depth)) - minDepth];
        }
    }
}
