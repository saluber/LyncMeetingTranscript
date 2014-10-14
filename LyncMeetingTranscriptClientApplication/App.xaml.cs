using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

using LyncMeetingTranscriptClientApplication.ViewModels;

namespace LyncMeetingTranscriptClientApplication
{
    public partial class App : Application
    {
        private const string _appName = "Lync Meeting Transcript";
        private const string _appId = "{5c25bcb7-4df6-4746-8b71-740ed37ab47f}";

        /// <summary>
        /// Service name.
        /// </summary>
        private const string ServiceName = "LyncMeetingTranscriptService.svc";

        // Creates an instance of IsolatedStorageSettings used to store the conference/conversation transcript history
        //private IsolatedStorageFile _userSettings;
        private const string TranscriptOutputFolderPathKey = "OutputFolderPath";
        private const string ReadInstantMessagesKey = "ReadInstantMessagesKey";

        private static string _outputFolderPath = System.Environment.CurrentDirectory;

        private static MainViewModel _viewModel;

        public static string AppName
        {
            get { return _appName; }
        }

        public static string AppId
        {
            get { return _appId; }
        }

        public static string TranscriptOutputFolderPath
        {
            get { return _outputFolderPath; }
            set { _outputFolderPath = value; }
        }

        public static MainViewModel MainViewModel
        {
            get { return _viewModel; }
        }

        public App()
        {
            this.Startup += this.Application_Startup;
            this.Exit += this.Application_Exit;
            this.UnhandledException += this.Application_UnhandledException;

            InitializeComponent();
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            //creates the main view model
            _viewModel = new MainViewModel();

            this.RootVisual = new MainPage();
        }

        private void Application_Exit(object sender, EventArgs e)
        {

        }

        private void Application_UnhandledException(object sender, ApplicationUnhandledExceptionEventArgs e)
        {
            // If the app is running outside of the debugger then report the exception using
            // the browser's exception mechanism. On IE this will display it a yellow alert 
            // icon in the status bar and Firefox will display a script error.
            if (!System.Diagnostics.Debugger.IsAttached)
            {

                // NOTE: This will allow the application to continue running after an exception has been thrown
                // but not handled. 
                // For production applications this error handling should be replaced with something that will 
                // report the error to the website and stop the application.
                e.Handled = true;
                Deployment.Current.Dispatcher.BeginInvoke(delegate { ReportErrorToDOM(e); });
            }
        }

        private void ReportErrorToDOM(ApplicationUnhandledExceptionEventArgs e)
        {
            try
            {
                string errorMsg = e.ExceptionObject.Message + e.ExceptionObject.StackTrace;
                errorMsg = errorMsg.Replace('"', '\'').Replace("\r\n", @"\n");

                System.Windows.Browser.HtmlPage.Window.Eval("throw new Error(\"Unhandled Error in Silverlight Application " + errorMsg + "\");");
            }
            catch (Exception)
            {
            }
        }
    }
}
