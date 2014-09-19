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
using LyncMeetingTranscriptClientApplication.ViewModel;

namespace LyncMeetingTranscriptClientApplication
{
    public partial class MainPage : UserControl
    {
        private static string _appId = "{97AD7B8A-3220-4855-8D1E-E70BB0973C4D}";
        private ConversationService _conversationService;
        private LyncClient _lyncClient;
        private Conversation _conversation;
        private Boolean _startConversation = false;
        private Contact _remoteContact;

        private MainViewModel _viewModel;

        public MainPage()
        {
            InitializeComponent();
            _viewModel = App.MainViewModel;

            try
            {
                // Get the instance of LyncClient and subscribe to outgoing/incoming conversation events
                _lyncClient = LyncClient.GetClient();
                _lyncClient.StateChanged += new EventHandler<ClientStateChangedEventArgs>(LyncClient_StateChanged);
                _lyncClient.ConversationManager.ConversationAdded +=
                    new EventHandler<Microsoft.Lync.Model.Conversation.ConversationManagerEventArgs>(
                    ConversationManager_ConversationAdded);
            }
            catch (ClientNotFoundException) { Console.WriteLine("Lync client was not found on startup"); }
            catch (LyncClientException lce) { MessageBox.Show("Lyncclientexception: " + lce.Message); }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_conversation == null)
                {
                    _conversation = _lyncClient.ConversationManager.Conversations[0];
                }

                _conversationService = new ConversationService();
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.ToString());
            }
        }

        void LyncClient_StateChanged(object sender, ClientStateChangedEventArgs e)
        {
            if (_startConversation == true && e.NewState == ClientState.SignedIn)
            {
                _conversation = _lyncClient.ConversationManager.AddConversation();
            }
        }

        void ConversationManager_ConversationAdded(object sender, Microsoft.Lync.Model.Conversation.ConversationManagerEventArgs e)
        {
            /*
            if (_conversation == null)
            {
                _conversation = e.Conversation;
            }

            _conversation.ParticipantAdded += Conversation_ParticipantAdded;
            if (_conversation.Modalities[ModalityTypes.InstantMessage].State != ModalityState.Notified)
            {
                // Get the Contact object for the person initiating the conversation.
                Contact inviter = e.Conversation.Properties[ConversationProperty.Inviter] as Contact;
                _remoteContact = _lyncClient.ContactManager.GetContactByUri(inviter.Uri);
                _conversation.AddParticipant(_remoteContact);
            }

            e.Conversation.InitialContextReceived += Conversation_InitialContextReceived;
            e.Conversation.ContextDataReceived += Conversation_ContextDataReceived;
            e.Conversation.StateChanged += Conversation_StateChanged;
            ((InstantMessageModality)e.Conversation.Modalities[ModalityTypes.InstantMessage]).InstantMessageReceived += MainWindow_InstantMessageReceived;
             */
        }

        /// <summary>
        /// Displays the initial context received string in the UI list box.  
        /// This event is not raised in this code when Lync is not in UI suppressed mode.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Conversation_InitialContextReceived(object sender, Microsoft.Lync.Model.Conversation.InitialContextEventArgs e)
        {
            if (e.ApplicationId != _appId)
            {
                return;
            }
            /*
            this.Dispatcher.Invoke(
                new ControlContentUpdateDelegate(ControlContentUpdate),
                new object[] { Inbound_Listbox, e.ApplicationData.ToString() });
            */
        }

        /// <summary>
        /// Displays the received context data string in the UI list box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Conversation_ContextDataReceived(object sender, ContextEventArgs e)
        {
            /*
            //Does the application Id sent with the context data match the application GUID of this 
            //application?
            if (e.ApplicationId != _AppId)
            {
                return;
            }

            if (e.ContextDataType.ToLower() == "text/plain")
            {
                this.Dispatcher.Invoke(
                    new ControlContentUpdateDelegate(ControlContentUpdate),
                    new object[] { Inbound_Listbox, e.ContextData.ToString() });
            }
             */
        }

        private void conversationService_MessageRecived(MessageContext message)
        {
            this.Dispatcher.BeginInvoke(() =>
            {
                ShowIncomingTranscriptMessage(message);
            });

            /*
            if (_viewModel.ReadInstantMessages && (message.Modality == MessageModality.InstantMessage)
                && (message.Direction == MessageDirection.Incoming))
            {
                // Use Speech Synthesizer to speak message text
            }
             */
        }

        private void ShowIncomingTranscriptMessage(MessageContext message)
        {
            WriteMessageToTranscript(message);
        }

        private void WriteMessageToTranscript(MessageContext message)
        {
            //adds a line for the received or sent message
            TranscriptItem lineItem = new TranscriptItem(message.MessageTime, message.ParticipantName, message.ParticipantUri, message.Modality.ToString(), message.Message);
            _viewModel.MessageHistory.Add(lineItem);
            listBoxHistory.UpdateLayout();
            scrollViewerMessageLog.ScrollToVerticalOffset(listBoxHistory.ActualHeight);

            //manually scrows down to the last added message
            listBoxHistory.UpdateLayout();
            scrollViewerMessageLog.ScrollToVerticalOffset(listBoxHistory.ActualHeight);
        }
    }
}
