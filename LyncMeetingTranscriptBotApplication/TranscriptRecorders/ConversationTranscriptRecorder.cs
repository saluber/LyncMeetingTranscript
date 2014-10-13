using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Rtc.Collaboration;
using Microsoft.Rtc.Collaboration.AudioVideo;
using Microsoft.Rtc.Signaling;

namespace LyncMeetingTranscriptBotApplication.TranscriptRecorders
{
    class ConversationTranscriptRecorder : MediaTranscriptRecorder
    {
        private static TranscriptRecorderType _type = TranscriptRecorderType.Conversation;
        private TranscriptRecorderState _state = TranscriptRecorderState.Initialized;
        
        private TranscriptRecorderSession _transcriptRecorder;
        private Conversation _conversation;
        private bool _isSubConversation = false;

        private AutoResetEvent _waitForConversationTerminated = new AutoResetEvent(false);
        private AutoResetEvent _waitForConversationJoined = new AutoResetEvent(false);

        #region Properties
        public override TranscriptRecorderType RecorderType
        {
            get { return _type; }
        }

        public override TranscriptRecorderState State
        {
            get { return _state; }
        }

        public TranscriptRecorderSession TranscriptRecorder
        {
            get { return _transcriptRecorder; }
        }

        public bool IsSubConversation
        {
            get { return _isSubConversation; }
        }

        public Conversation Conversation
        {
            get { return _conversation; }
        }

        internal AutoResetEvent ConversationTerminatedEventWaiter
        {
            get { return _waitForConversationTerminated; }
        }

        internal AutoResetEvent ConversationJoinedEventWaiter
        {
            get { return _waitForConversationJoined; }
        }

        #endregion // Properties

        public ConversationTranscriptRecorder(TranscriptRecorderSession transcriptRecorder, Conversation conversation, bool isSubConversation = false)
        {
            if (transcriptRecorder == null)
            {
                throw new ArgumentNullException("transcriptRecorder");
            }
            if (conversation == null)
            {
                throw new ArgumentNullException("conversation");
            }

            _transcriptRecorder = transcriptRecorder;
            _conversation = conversation;
            _isSubConversation = isSubConversation;

            RegisterConversationEvents();
        }

        public void TerminateConversation()
        {
            if (_conversation != null)
            {
                _conversation.BeginTerminate(ConversationTerminated, _conversation);
                UnregisterConversationEvents();
                _transcriptRecorder.OnConversationTerminated(_conversation, this);

                _conversation = null;
            }
            else
            {
                _waitForConversationTerminated.Set();
            }

            _waitForConversationJoined.Reset();
        }

        public override void Shutdown()
        {
            if (_state == TranscriptRecorderState.Terminated)
            {
                return;
            }
            _state = TranscriptRecorderState.Terminated;

            if (this.IsSubConversation)
            {
                _transcriptRecorder.OnSubConversationRemoved(this.Conversation, this);
            }
            else
            {
                TerminateConversation();
                _transcriptRecorder.OnMediaTranscriptRecorderTerminated(this);
            }
            _transcriptRecorder = null;
        }

        #region Private Methods

        private void RegisterConversationEvents()
        {
            // Register for Conversation events
            _conversation.StateChanged += new EventHandler<StateChangedEventArgs<ConversationState>>(Conversation_StateChanged);
            _conversation.PropertiesChanged += new EventHandler<PropertiesChangedEventArgs<ConversationProperties>>(Conversation_PropertiesChanged);
            _conversation.RemoteParticipantAttendanceChanged += new EventHandler<ParticipantAttendanceChangedEventArgs>(Conversation_ParticipantEndpointAttendanceChanged);
            _conversation.ParticipantPropertiesChanged += new EventHandler<ParticipantPropertiesChangedEventArgs>(Conversation_ParticipantPropertiesChanged);
            _conversation.EscalateToConferenceRequested += new EventHandler<EscalateToConferenceRequestedEventArgs>(Conversation_EscalateToConferenceRequested);
        }

        private void UnregisterConversationEvents()
        {
            // Unregister for Conversation events
            _conversation.StateChanged -= Conversation_StateChanged;
            _conversation.PropertiesChanged -= Conversation_PropertiesChanged;
            _conversation.RemoteParticipantAttendanceChanged -= Conversation_ParticipantEndpointAttendanceChanged;
            _conversation.ParticipantPropertiesChanged -= Conversation_ParticipantPropertiesChanged;
            _conversation.EscalateToConferenceRequested -= Conversation_EscalateToConferenceRequested;
        }
        #endregion // Private Methods

        #region Callbacks

        private void ConversationTerminated(IAsyncResult ar)
        {
            Conversation conv = ar.AsyncState as Conversation;

            try
            {
                // End terminating the conversation.
                conv.EndTerminate(ar);
            }
            finally
            {
                _waitForConversationTerminated.Set();
            }
        }

        #endregion // Private Methods

        #region Conversation Event Handlers

        void Conversation_StateChanged(object sender, StateChangedEventArgs<ConversationState> e)
        {
            Conversation conv = sender as Conversation;
            Console.WriteLine("Conversation {0} state changed from " + e.PreviousState + " to " + e.State, conv.LocalParticipant.UserAtHost);

            Message m = new Message("Conversation state changed from " + e.PreviousState.ToString() + " to " + e.State.ToString(),
                MessageType.ConversationInfo, _conversation.Id);
            _transcriptRecorder.OnMessageReceived(m);

            if (e.State == ConversationState.Established || e.State == ConversationState.Conferenced)
            {
                _waitForConversationJoined.Set();
            }
            else if (e.State == ConversationState.Terminating || e.State == ConversationState.Terminated)
            {
                _waitForConversationTerminated.Set();
                this.Shutdown();
            }
        }

        //Just to record the state transitions in the console.
        void Conversation_ParticipantEndpointAttendanceChanged(object sender,
            ParticipantAttendanceChangedEventArgs e)
        {
            Conversation conv = sender as Conversation;

            // Log each participant as s/he gets added/deleted from the Conversation's roster.
            foreach (ConversationParticipant p in e.Added)
            {
                Console.WriteLine("{0} is notified of participant joining the conversation: {1}",
                    conv.LocalParticipant.UserAtHost,
                    p.UserAtHost);

                Message m = new Message("Participant joined conversation.", p.DisplayName, p.UserAtHost,
                    p.Uri, MessageType.ConversationInfo, _conversation.Id, MessageDirection.Incoming);
                _transcriptRecorder.OnMessageReceived(m);
            }

            foreach (ConversationParticipant p in e.Removed)
            {
                Console.WriteLine("{0} is notified of participant leaving the conversation: {1}",
                    conv.LocalParticipant.UserAtHost,
                    p.UserAtHost);

                Message m = new Message("Participant left conversation.", p.DisplayName, p.UserAtHost,
                    p.Uri, MessageType.ConversationInfo, _conversation.Id, MessageDirection.Incoming);
                _transcriptRecorder.OnMessageReceived(m);
            }

            Console.WriteLine();
        }

        // Just to record the state transitions in the console.
        /// <summary>
        /// // Monitor main changes to participant properties
        /// (including local participant) such as active media types and conferencing role changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Conversation_ParticipantPropertiesChanged(object sender, ParticipantPropertiesChangedEventArgs e)
        {
            Conversation conv = sender as Conversation;
            
            Console.WriteLine(
                "{0} is notified of Conversation participant property change for user: {1}. Role:{2}",
                conv.LocalParticipant.UserAtHost,
                e.Participant.UserAtHost,
                e.Properties.Role);

            Console.WriteLine();

            Message m = new Message("Conversation Participant Properties changed. Properties changed: " + e.ChangedPropertyNames.ToString()
                + ". Participant Property Values: " + e.Properties.ToString() + ".",
                e.Participant.DisplayName, e.Participant.UserAtHost, e.Participant.Uri,
                MessageType.ConversationInfo, conv.Id, MessageDirection.Incoming);
            _transcriptRecorder.OnMessageReceived(m);
        }

        private void Conversation_PropertiesChanged(object sender, PropertiesChangedEventArgs<ConversationProperties> e)
        {
            if (e.ChangedPropertyNames != null)
            {
                //Update the property from property changed event
                foreach (string propertyName in e.ChangedPropertyNames)
                {
                    switch (propertyName)
                    {
                        case ConversationProperties.ConversationIdPropertyName:
                            Console.WriteLine("Conversation id changed to {0}", e.Properties.Id);
                            break;
                        case ConversationProperties.ConversationPriorityPropertyName:
                            Console.WriteLine("Conversation priority changed to {0}", e.Properties.Priority);
                            break;
                        case ConversationProperties.ConversationSubjectPropertyName:
                            Console.WriteLine("Conversation subject changed to {0}", e.Properties.Subject);
                            break;
                        case ConversationProperties.ConversationActiveMediaTypesPropertyName:
                            Console.WriteLine("Conversation active media types property name changed to");
                            foreach (string activeMedia in e.Properties.ActiveMediaTypes)
                            {
                                Console.WriteLine(activeMedia);
                                // TODO: Add calls for new active media types (and terminate calls for nonactive media types)
                            }
                            break;
                        default:
                            //Should not reach here
                            //Debug.Assert(false, "property name not expected");
                            break;
                    }
                }

                Message m = new Message("Conversation Properties changed. Properties changed: " + e.ChangedPropertyNames.ToString()
                    + ". Participant Property Values: " + e.Properties.ToString() + ".",
                    MessageType.ConversationInfo, _conversation.Id);
                _transcriptRecorder.OnMessageReceived(m);
            }
        }

    private void Conversation_EscalateToConferenceRequested(object sender, EscalateToConferenceRequestedEventArgs e)
    {
        Message m = new Message("Conversation EscalateToConferenceRequested.",
            MessageType.ConversationInfo, _conversation.Id);
        _transcriptRecorder.OnMessageReceived(m);

        _transcriptRecorder.OnEscalatedConferenceJoinRequested(_conversation);
    }

        #endregion // Conversation Event Handlers
    }
}
