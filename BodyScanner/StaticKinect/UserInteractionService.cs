// 
// Based on code from https://github.com/baSSiLL/BodyScanner
//
using System.Windows;

namespace BodyScanner
{
    /// <summary>
    /// A class for user interactions.
    /// </summary>
    class UserInteractionService
    {
        /// <summary>
        ///  Shows error to the user.
        /// </summary>
        /// <param name="message">The error message</param>
        public void ShowError(string message)
        {
            MessageBox.Show(message, Properties.Resources.ApplicationName,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
