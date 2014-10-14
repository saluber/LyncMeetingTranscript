using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.IO.IsolatedStorage;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace LyncWPFClientApplication.ViewModels
{
    public class MainViewModel
    {
        // Creates an instance of IsolatedStorageSettings used to store the preferred languages
        //private IsolatedStorageSettings userSettings = IsolatedStorageSettings.ApplicationSettings;

        public MainViewModel()
        {
            ConferenceUri = "Null";
            LyncClientState = "Active";
            ConversationStatus = "Active";
            MessageHistory = new ObservableCollection<TranscriptItem>();
        }

        public string ConferenceUri { get; set; }

        public string LyncClientState { get; set; }

        public string ConversationStatus { get; set; }

        // MediaTextMessages
        /// <summary>
        /// Stores the in-memory history of the conversation.
        /// </summary>
        public ObservableCollection<TranscriptItem> MessageHistory { get; set; }
    }
}
