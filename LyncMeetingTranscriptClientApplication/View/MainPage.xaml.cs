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
using Microsoft.Lync.Model.Extensibility;

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

        ApplicationRegistration _myApplicationRegistration;
        ConversationWindow _cWindow;

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

                _conversation = (Conversation)Microsoft.Lync.Model.LyncClient.GetHostingConversation();

                // Perform run-time registration using the ApplicationRegistration class.
                _myApplicationRegistration = _lyncClient.CreateApplicationRegistration(App.AppId, App.AppName);
                this._myApplicationRegistration.AddRegistration();
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

                if (_myApplicationRegistration != null)
                {
                    // Unregister Run-Time Registration for application context.
                    _myApplicationRegistration.RemoveRegistration();
                }

                if (_cWindow != null)
                {
                    this._cWindow.Close();
                }
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

            List<Message> messages = ParseMessagesFromContextData(e.ApplicationData);
            foreach (Message m in messages)
            {
                ShowIncomingTranscriptMessage(m);
            }
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

            List<Message> messages = ParseMessagesFromContextData(e.ContextData);
            foreach (Message m in messages)
            {
                ShowIncomingTranscriptMessage(m);
            }
        }

        #endregion // Event Handlers

        /// <summary>
        /// Parses context data into List of Messages based on format:
        /// [SenderDisplayName (senderAlias@senderHost.com)(sip:senderAlias@senderHost.com)][ConversationId:12345678][ConferenceUri:senderAlias@senderHost.com;conf][TimeStamp][MessageType][MessageDirection][MessageContent];;
        /// </summary>
        /// <param name="contextData"></param>
        /// <returns></returns>
        private List<Message> ParseMessagesFromContextData(string contextData)
        {
            List<Message> transcriptMessages = new List<Message>();

            // Split messages
            char[] transcriptMessagesDelimiter = { ';', ';' };
            string[] transcriptMessageData = contextData.Split(transcriptMessagesDelimiter);

            char[] messagePropertyDelimiter = { ']', '[' };
            char[] messageSenderInfoDelimiter = { '(' };
            foreach (string s in transcriptMessageData)
            {
                // Split message content
                string senderInfo = "null";
                string senderDisplayName = "null";
                string senderAlias = "null";
                string senderUri = "null";
                string conversationId = "null";
                string conferenceUri = "null";
                string timeStamp = DateTime.Now.ToString();
                string messageType = "Info";
                string messageDirection = "Incoming";
                string messageContent = "null";

                string[] messageProperties = s.Split(messagePropertyDelimiter);
                // Verify 7 items in messageProperties
                if (messageProperties.Length == 7)
                {
                    senderInfo = messageProperties[0].Substring(1);
                    conversationId = messageProperties[1].Substring(1);
                    conferenceUri = messageProperties[2].Substring(1);
                    timeStamp = messageProperties[3].Substring(1);
                    messageType = messageProperties[4].Substring(1);
                    messageDirection = messageProperties[5].Substring(1);
                    messageContent = messageProperties[6].Substring(1);

                    if (senderInfo.Contains('('))
                    {
                        string[] senderInfoItems = senderInfo.Split(messageSenderInfoDelimiter);
                        senderDisplayName = senderInfoItems[0].Trim();
                        senderAlias = senderInfoItems[1].Substring(0, senderInfoItems[1].Length - 1).Trim();
                        senderAlias = senderInfoItems[2].Substring(0, senderInfoItems[2].Length - 1).Trim();
                    }
                    else
                    {
                        senderDisplayName = senderInfo.Trim();
                    }
                }
                else
                {
                    // TODO: Error
                    continue;
                }

                MessageModality modality = MessageModality.Info;
                Enum.TryParse<MessageModality>(messageType, true, out modality);
                MessageDirection direction = MessageDirection.Incoming;
                Enum.TryParse<MessageDirection>(messageDirection, true, out direction);
                Message m = new Message(messageContent, senderDisplayName, senderAlias, senderUri, DateTime.Parse(timeStamp), conversationId, conferenceUri, modality, direction);
                transcriptMessages.Add(m);
            } // loop

            return transcriptMessages;
        }
    }
}
