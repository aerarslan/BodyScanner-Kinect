// 
// Based on code from https://github.com/baSSiLL/BodyScanner
//
using System;
using System.Diagnostics.Contracts;
using System.Threading;

namespace BodyScanner
{
    /// <summary>
    ///  SynchronizationContext provides the basic functionality for propagating a synchronization context in
    ///  various synchronization models. If context is not null, invokes the action.
    /// </summary>
    static class SynchronizationContextExtensions
    {

        /// <summary>
        ///  Dispatches an asynchronous message to a synchronization context.
        /// </summary>
        /// <param name="context">The synchronization context</param>
        /// <param name="action">The action</param>
        public static void Post(this SynchronizationContext context, Action action)
        {
            Contract.Requires(context != null);

            context.Post(_ => action.Invoke(), null);
        }

        /// <summary>
        ///  When overridden in a derived class, dispatches a synchronous message to a synchronization context.
        /// </summary>
        /// <param name="context">The synchronization context</param>
        /// <param name="action">The action</param>
        public static void Send(this SynchronizationContext context, Action action)
        {
            Contract.Requires(context != null);

            context.Send(_ => action.Invoke(), null);
        }
    }
}
