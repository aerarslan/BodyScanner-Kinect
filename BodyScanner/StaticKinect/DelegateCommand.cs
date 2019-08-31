// 
// Based on code from https://github.com/baSSiLL/BodyScanner
//
using System;
using System.Diagnostics.Contracts;
using System.Windows;
using System.Windows.Input;

namespace BodyScanner
{
    /// <summary>
    /// A helper class of AppViewModel.cs. Is used while controlling the start of the scan and saving mesh.
    /// </summary>
    public class DelegateCommand : ICommand
    {
        /// <summary>
        /// Encapsulates a method that has a single parameter and does not return a value.
        /// </summary>
        private readonly Action<object> execute;

        /// <summary>
        /// Encapsulates a method that has one parameter and returns a value of the type
        /// specified by the TResult parameter.
        /// </summary>
        private readonly Func<object, bool> canExecute;

        /// <summary>
        /// Checks the condition then executes the given action.
        /// </summary>
        /// <param name="execute">Action will be executed.</param>
        /// <param name="canExecute">Condition to check the Action will be executed or not</param>
        public DelegateCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            Contract.Requires(execute != null);

            this.execute = execute;
            this.canExecute = canExecute;
        }

        /// <summary>
        /// Checks the condition then executes the given action.
        /// </summary>
        /// <param name="execute">Action will be executed.</param>
        /// <param name="canExecute">Condition to check the Action will be executed or not</param>
        public DelegateCommand(Action execute, Func<bool> canExecute = null)
            : this(execute != null ? new Action<object>(p => execute.Invoke()) : null,
                   canExecute != null ? new Func<object, bool>(p => canExecute.Invoke()) : null)
        {
            Contract.Requires(execute != null);
        }

        /// <summary>
        /// If the calling thread is not the thread associated with this System.Windows.Threading.Dispatcher calls a new action.
        /// </summary>
        public void InvalidateCanExecute()
        {
            if (Application.Current != null &&
                !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(
                    new Action(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty)));
            }
            else
            {
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }


        #region ICommand members
        /// <summary>
        /// Checks for the parameter will be executed or not.
        /// </summary>
        public bool CanExecute(object parameter)
        {
            return canExecute == null ? true : canExecute.Invoke(parameter);
        }

        /// <summary>
        /// Event handler to check execute changes.
        /// </summary>
        public event EventHandler CanExecuteChanged;

        /// <summary>
        /// Invokes the parameter If CanExecute is True.
        /// </summary>
        public void Execute(object parameter)
        {
            if (CanExecute(parameter))
            {
                execute.Invoke(parameter);
            }
        }

        #endregion
    }
}
