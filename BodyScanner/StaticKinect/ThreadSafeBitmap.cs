// 
// Based on code from https://github.com/baSSiLL/BodyScanner
//
using System;
using System.Diagnostics.Contracts;

namespace BodyScanner
{
    /// <summary>
    /// The class for bitmap
    /// </summary>
    class ThreadSafeBitmap
    {
        /// <summary>
        /// Object for lock current thread.
        /// </summary>
        private readonly object sync = new object();

        /// <summary>
        /// Bitmap data. Is used while updating bitmaps.
        /// </summary>
        private readonly int[] data;

        /// <summary>
        /// Calculates the data of bitmap. (width * height)
        /// </summary>
        /// <param name="width">Width of bitmap</param>
        /// <param name="height">Height of bitmap</param>
        public ThreadSafeBitmap(int width, int height)
        {
            Contract.Requires(width > 0 && height > 0);

            Width = width;
            Height = height;
            data = new int[width * height];
        }

        /// <summary>
        /// Width holder
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Height holder
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// We can use C# lock keyword to execute program synchronously. It is used to get lock for the current thread, 
        /// execute the task and then release the lock. 
        /// It ensures that other thread does not interrupt the execution until the execution finish.
        /// Access the current data.
        /// </summary>
        /// <param name="accessor">Accessor for bitmap data</param>
        public void Access(Action<int[]> accessor)
        {
            Contract.Requires(accessor != null);

            lock (sync)
            {
                accessor.Invoke(data);
            }
        }
    }
}
