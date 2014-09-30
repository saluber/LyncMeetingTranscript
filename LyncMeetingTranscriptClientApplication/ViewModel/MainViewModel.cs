using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace LyncMeetingTranscriptClientApplication.ViewModel
{
    public class MainViewModel
    {
        // Creates an instance of IsolatedStorageSettings used to store the conference/conversation transcript history
        //private IsolatedStorageFile _userSettings;
        private const string TranscriptOutputFolderPathKey = "OutputFolderPath";
        private const string ReadInstantMessagesKey = "ReadInstantMessagesKey";

        public MainViewModel()
        {
            //_userSettings = IsolatedStorageFile.GetUserStoreForApplication();
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

        /// <summary>
        /// Adds the message to log.
        /// </summary>
        /// <param name="message">The message.</param>
        public static void AddMessageToLog(Message message)
        {
            if (message != null)
            {
                _messages.Add(message);
            }
        }

        public static void ClearMessages()
        {
            _messages.Clear();
        }

        public string TranscriptOutputFolderPath
        {
            get
            {
                // Use default output path for now
                return "C:\\LyncMeetingTranscriptLogs";
            }
        }
    }
}
