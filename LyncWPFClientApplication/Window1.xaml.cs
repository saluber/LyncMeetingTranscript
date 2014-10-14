using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using Microsoft.Lync.Model;
using Microsoft.Lync.Model.Conversation;
using Microsoft.Lync.Model.Extensibility;

using LyncWPFClientApplication.Model;
using LyncWPFClientApplication.ViewModels;

namespace LyncWPFClientApplication
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Window1 : Window
    {
        private MainViewModel _viewModel;
        private LyncClient _lyncClient;
        private Conversation _conversation;
        private ApplicationRegistration _myApplicationRegistration;

        public Window1()
        {
            InitializeComponent();
            _viewModel = App.MainViewModel;
            this.DataContext = _viewModel;
            //scrollViewerMessageLog.DataContext = _viewModel;
            //listBoxHistory.DataContext = _viewModel.MessageHistory;

            try
            {
                // Get the instance of LyncClient and subscribe to outgoing/incoming conversation events
                _lyncClient = LyncClient.GetClient();
                _lyncClient.StateChanged += new EventHandler<ClientStateChangedEventArgs>(LyncClient_StateChanged);
                _lyncClient.ConversationManager.ConversationAdded += ConversationManager_ConversationAdded;
                _lyncClient.DelegatorClientAdded += _lyncClient_DelegatorClientAdded;
                _lyncClient.DelegatorClientRemoved += _lyncClient_DelegatorClientRemoved;
                foreach (DelegatorClient dc in _lyncClient.DelegatorClients)
                {
                    dc.ConversationManager.ConversationAdded += ConversationManager_ConversationAdded;

                    foreach (Conversation c in dc.ConversationManager.Conversations)
                    {
                        // Subscribe to conversation events
                        c.InitialContextReceived += Conversation_InitialContextReceived;
                        c.ContextDataReceived += Conversation_ContextDataReceived;
                        c.StateChanged += Conversation_StateChanged;
                    }
                }
                if (_lyncClient.ConversationManager.Conversations.Count > 0)
                {
                    _conversation = (Conversation)_lyncClient.ConversationManager.Conversations[0];

                    foreach (Conversation c in _lyncClient.ConversationManager.Conversations)
                    {
                        // Subscribe to conversation events
                        c.InitialContextReceived += Conversation_InitialContextReceived;
                        c.ContextDataReceived += Conversation_ContextDataReceived;
                        c.StateChanged += Conversation_StateChanged;
                    }
                }
                else
                {
                    // throw new InvalidOperationException("There must be at least one active Lync Conversation instance running to record a meeting transcript");
                }                
            }
            catch (ClientNotFoundException) { Console.WriteLine("Lync client was not found on startup"); }
            catch (LyncClientException lce) { MessageBox.Show("Lyncclientexception: " + lce.Message); }
        }

        void _lyncClient_DelegatorClientAdded(object sender, DelegatorClientCollectionEventArgs e)
        {
            e.DelegatorClient.ConversationManager.ConversationAdded += ConversationManager_ConversationAdded;

            foreach (Conversation c in e.DelegatorClient.ConversationManager.Conversations)
            {
                if (_conversation == null)
                {
                    _conversation = c;
                }

                // Subscribe to conversation events
                c.InitialContextReceived += Conversation_InitialContextReceived;
                c.ContextDataReceived += Conversation_ContextDataReceived;
                c.StateChanged += Conversation_StateChanged;
            }
        }

        void _lyncClient_DelegatorClientRemoved(object sender, DelegatorClientCollectionEventArgs e)
        {
            e.DelegatorClient.ConversationManager.ConversationAdded -= ConversationManager_ConversationAdded;

            foreach (Conversation c in e.DelegatorClient.ConversationManager.Conversations)
            {
                if (_conversation == c)
                {
                    _conversation = null;
                }

                // Subscribe to conversation events
                c.InitialContextReceived += Conversation_InitialContextReceived;
                c.ContextDataReceived += Conversation_ContextDataReceived;
                c.StateChanged += Conversation_StateChanged;
            }
        }

        private void ShowIncomingTranscriptMessage(Message message)
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
                if (_lyncClient.ConversationManager.Conversations.Count > 0)
                {
                    if (_conversation == null)
                    {
                        _conversation = (Conversation)_lyncClient.ConversationManager.Conversations[0];
                    }

                    foreach (Conversation c in _lyncClient.ConversationManager.Conversations)
                    {
                        // Subscribe to conversation events
                        c.InitialContextReceived += Conversation_InitialContextReceived;
                        c.ContextDataReceived += Conversation_ContextDataReceived;
                        c.StateChanged += Conversation_StateChanged;
                    }
                }
                else
                {
                    throw new InvalidOperationException("There must be at least one active Lync Conversation instance running to record a meeting transcript"); 
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.ToString());
            }
        }

        /// <summary>
        /// New conversation is added because the user added it or was invited to a conversation by another user
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ConversationManager_ConversationAdded(object sender, Microsoft.Lync.Model.Conversation.ConversationManagerEventArgs e)
        {
            if (_conversation == null)
            {
                _conversation = e.Conversation;
            }

            e.Conversation.InitialContextReceived += Conversation_InitialContextReceived;
            e.Conversation.ContextDataReceived += Conversation_ContextDataReceived;
            e.Conversation.StateChanged += Conversation_StateChanged;
        }

        void LyncClient_StateChanged(object sender, ClientStateChangedEventArgs e)
        {
            if (e.NewState == ClientState.ShuttingDown)
            {
                if (_lyncClient != null)
                {
                    _lyncClient.StateChanged -= LyncClient_StateChanged;
                    _lyncClient.ConversationManager.ConversationAdded -= ConversationManager_ConversationAdded;
                    _lyncClient.DelegatorClientAdded -= _lyncClient_DelegatorClientAdded;
                    _lyncClient.DelegatorClientRemoved -= _lyncClient_DelegatorClientRemoved;
                }
                if (_conversation != null)
                {
                    _conversation.InitialContextReceived -= Conversation_InitialContextReceived;
                    _conversation.ContextDataReceived -= Conversation_ContextDataReceived;
                    _conversation.StateChanged -= Conversation_StateChanged;
                }
                
                if (this.IsActive)
                {
                    this.Close();
                    App.Current.Shutdown();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Conversation_StateChanged(object sender, Microsoft.Lync.Model.Conversation.ConversationStateChangedEventArgs e)
        {
            Conversation conv = sender as Conversation;
            if (e.NewState == ConversationState.Terminated)
            {
                if (conv != null)
                {
                    //conv.InitialContextReceived -= Conversation_InitialContextReceived;
                    //conv.ContextDataReceived -= Conversation_ContextDataReceived;
                    //conv.StateChanged -= Conversation_StateChanged;

                    if (conv == _conversation)
                    {
                        _conversation = null;
                    }
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
            if (!e.ApplicationId.Equals(App.AppId))
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
            if (!e.ApplicationId.Equals(App.AppId))
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
