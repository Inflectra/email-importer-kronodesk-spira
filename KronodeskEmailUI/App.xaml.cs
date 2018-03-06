using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

using Inflectra.KronoDesk.Service.Email.UI.Classes;

namespace KronodeskEmailUI
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
        /// <summary>
        /// Constructor
        /// </summary>
        public App() : base()
        {
            this.DispatcherUnhandledException += new DispatcherUnhandledExceptionEventHandler(App_DispatcherUnhandledException);
        }

        void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            //Log the event
            Logger logger = new Logger();
            logger.WriteMessage(e.Exception.Message + ": " + e.Exception.StackTrace, System.Diagnostics.EventLogEntryType.Error, 0);
            if (e.Exception.InnerException != null)
            {
                logger.WriteMessage(e.Exception.InnerException.Message + ": " + e.Exception.InnerException.StackTrace, System.Diagnostics.EventLogEntryType.Error, 0);
            }
            MessageBox.Show(e.Exception.Message, "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            
            // Prevent default unhandled exception processing
            e.Handled = true;

            //Exit
            this.Shutdown();
        }
	}
}
