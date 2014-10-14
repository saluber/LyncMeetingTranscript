using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using LyncWPFClientApplication.Model;
using LyncWPFClientApplication.ViewModels;

namespace LyncWPFClientApplication
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private const string _appName = "Lync Meeting Transcript";
        private static Guid _appId = new Guid("5c25bcb7-4df6-4746-8b71-740ed37ab47f");
        private static string _outputFolderPath = System.Environment.CurrentDirectory;
        private static MainViewModel _viewModel = new MainViewModel();

        public static string AppName
        {
            get { return _appName; }
        }

        public static Guid AppId
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
    }
}
