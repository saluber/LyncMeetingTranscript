using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Lync.Model;
using Microsoft.Lync.Model.Conversation;

using LyncMeetingTranscriptClientApplication.Model;
using LyncMeetingTranscriptClientApplication.Service;
using LyncMeetingTranscriptClientApplication.ViewModels;

namespace LyncMeetingTranscriptClientApplication
{
    public partial class MainPage : UserControl
    {
        private MainViewModel _viewModel;
        private TranslationService _translationService;
        private ConversationService _conversationService;

        private LyncClient _lyncClient;
        private Conversation _conversation;

        public MainPage()
        {
            InitializeComponent();
            _viewModel = App.MainViewModel;

            try
            {
                _translationService = new TranslationService();
                _conversationService = new ConversationService();

                // Get the instance of LyncClient and subscribe to outgoing/incoming conversation events
                _lyncClient = LyncClient.GetClient();
                _lyncClient.StateChanged += new EventHandler<ClientStateChangedEventArgs>(LyncClient_StateChanged);
            }
            catch (ClientNotFoundException) { Console.WriteLine("Lync client was not found on startup"); }
            catch (LyncClientException lce) { MessageBox.Show("Lyncclientexception: " + lce.Message); }
        }

        // TODO: Make private later and use ConversationContext channel to write data
        void ShowIncomingTranscriptMessage(Message message)
        {
            WriteMessageToTranscript(message);
        }

        private void WriteMessageToTranscript(Message message)
        {
            //adds a line for the received or sent message
            TranscriptItem lineItem = new TranscriptItem(message.TimeStamp, message.SenderDisplayName, message.SenderUri, message.Modality.ToString(), message.Content);
            _viewModel.MessageHistory.Add(lineItem);

            //manually scrows down to the last added message
            listBoxHistory.UpdateLayout();
            scrollViewerMessageLog.ScrollToVerticalOffset(listBoxHistory.ActualHeight);
        }

        #region Event Handlers

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                //gets the conversation this translator is associated with
                _conversation = (Conversation)LyncClient.GetHostingConversation();

                // Subscribe to conversation events
                _conversation.InitialContextReceived += Conversation_InitialContextReceived;
                _conversation.ContextDataReceived += Conversation_ContextDataReceived;
                _conversation.StateChanged += Conversation_StateChanged;

            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.ToString());
            }
        }

        void LyncClient_StateChanged(object sender, ClientStateChangedEventArgs e)
        {
            if (e.NewState == ClientState.ShuttingDown)
            {
                // TODO: Handle Lync Client state changes
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Conversation_StateChanged(object sender, Microsoft.Lync.Model.Conversation.ConversationStateChangedEventArgs e)
        {
            if (e.NewState == ConversationState.Terminated)
            {
                // TODO: Handle conversation state change events
            }
        }

        /// <summary>
        /// Displays the initial context received string in the UI list box.  
        /// This event is not raised in this code when Lync is not in UI suppressed mode.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Conversation_InitialContextReceived(object sender, Microsoft.Lync.Model.Conversation.InitialContextEventArgs e)
        {
            if (e.ApplicationId != App.AppId)
            {
                return;
            }

            // Do app setup stuff
        }

        /// <summary>
        /// Displays the received context data string in the UI list box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Conversation_ContextDataReceived(object sender, ContextEventArgs e)
        {
            // Does the application Id sent with the context data match the application GUID of this 
            // application?
            if (e.ApplicationId != App.AppId)
            {
                return;
            }

            // TODO: Parse transcript item(s) from context contents
            /*
            this.Dispatcher.BeginInvoke(() =>
            {
                ShowIncomingTranscriptMessage(message);
            });
            */
        }

        #endregion // Event Handlers
    }
}
