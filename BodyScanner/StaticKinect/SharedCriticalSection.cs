// 
// Based on code from https://github.com/baSSiLL/BodyScanner
//
using System;
using System.Threading;

namespace BodyScanner
{
    /// <summary>
    /// A helper class for ReconstructionController.cs 
    /// </summary>
    class SharedCriticalSection : IDisposable
    {

        /// <summary>
        /// AutoResetEvent object.
        /// </summary>
        private readonly AutoResetEvent notEntered = new AutoResetEvent(true);

        /// <summary>
        /// Construction function.
        /// </summary>
        public SharedCriticalSection()
        {
        }

        /// <summary>
        /// Releases all resources used by the current instance of the System.Threading.WaitHandle class.
        /// </summary>
        public void Dispose()
        {
            notEntered.Dispose();
        }

        /// <summary>
        /// Blocks the current thread until the current System.Threading.WaitHandle receives
        /// a signal. Returns true if the current instance receives a signal. If the current instance is never
        /// signaled, System.Threading.WaitHandle.WaitOne(System.Int32,System.Boolean) never
        /// returns.
        /// </summary>
        public bool Enter()
        {
            return notEntered.WaitOne();
        }

        /// <summary>
        /// Blocks the current thread until the current System.Threading.WaitHandle receives a signal, using a 32-bit signed integer to specify the time interval in milliseconds.
        /// </summary>
        public bool TryEnter()
        {
            return notEntered.WaitOne(0);
        }

        /// <summary>
        /// Sets the state of the event to signaled, allowing one or more waiting threads to proceed.
        /// </summary>
        public void Exit()
        {
            notEntered.Set();
        }
    }
}

