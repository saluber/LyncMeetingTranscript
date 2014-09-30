using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        private TranscriptRecorder _transcriptRecorder;
        private Conversation _conversation;

        private MediaTranscriptRecorder _parentMediaTranscriptRecorder;
        private bool _isSubConversation = false;

        #region Properties
        public TranscriptRecorder TranscriptRecorder
        {
            get { return _transcriptRecorder; }
        }

        public override TranscriptRecorderType RecorderType
        {
            get { return _type; }
        }

        public override TranscriptRecorderState State
        {
            get { return _state; }
        }

        public MediaTranscriptRecorder ParentMediaTranscriptRecorder
        {
            get { return _parentMediaTranscriptRecorder; }
        }

        public bool IsSubConversation
        {
            get { return _isSubConversation; }
        }

        public Conversation Conversation
        {
            get { return _conversation; }
        }

        #endregion // Properties

        public ConversationTranscriptRecorder(TranscriptRecorder transcriptRecorder, Conversation conversation, MediaTranscriptRecorder parentRecorder = null)
        {
            _transcriptRecorder = transcriptRecorder;
            _conversation = conversation;

            if (parentRecorder != null)
            {
                _parentMediaTranscriptRecorder = parentRecorder;
                _isSubConversation = true;
            }

            RegisterConversationEvents();
            _state = TranscriptRecorderState.Active;
        }

        public override void Shutdown()
        {
            if (_state == TranscriptRecorderState.Terminated)
            {
                return;
            }
            _state = TranscriptRecorderState.Terminated;

            // TODO: Shutdown message
            if (_conversation != null)
            {
                UnregisterConversationEvents();
            }

            if (_parentMediaTranscriptRecorder != null)
            {
                // TODO: Remove subconversation from parent
            }

            _transcriptRecorder.OnMediaTranscriptRecorderTerminated(this);
            _transcriptRecorder = null;
        }

        #region Private Methods

        private void RegisterConversationEvents()
        {
            // Register for Conversation events
            _conversation.StateChanged += new EventHandler<StateChangedEventArgs<ConversationState>>(Conversation_StateChanged);
            _conversation.PropertiesChanged += new EventHandler<PropertiesChangedEventArgs<ConversationProperties>>(Conversation_PropertiesChanged);
            _conversation.RemoteParticipantAttendanceChanged += new EventHandler<ParticipantAttendanceChangedEventArgs>(Conversation_ParticipantEndpointAttendanceChanged);
            _conversation.ParticipantPropertiesChanged += new EventHandler<ParticipantPropertiesChangedEventArgs>(Conversation_ParticipantEndpointPropertiesChanged);
            _conversation.EscalateToConferenceRequested += new EventHandler<EscalateToConferenceRequestedEventArgs>(conversation_EscalateToConferenceRequested);
        }

        private void UnregisterConversationEvents()
        {
            // Unregister for Conversation events
            _conversation.StateChanged -= Conversation_StateChanged;
            _conversation.PropertiesChanged -= Conversation_PropertiesChanged;
            _conversation.RemoteParticipantAttendanceChanged -= Conversation_ParticipantEndpointAttendanceChanged;
            _conversation.ParticipantPropertiesChanged -= Conversation_ParticipantEndpointPropertiesChanged;
            _conversation.EscalateToConferenceRequested -= conversation_EscalateToConferenceRequested;
        }

        #endregion // Private Methods

        #region Conversation Event Handlers

        void Conversation_StateChanged(object sender, StateChangedEventArgs<ConversationState> e)
        {
            Conversation conv = sender as Conversation;
            Console.WriteLine("Conversation {0} state changed from " + e.PreviousState + " to " + e.State, conv.LocalParticipant.UserAtHost);

            if (e.State == ConversationState.Terminating || e.State == ConversationState.Terminated)
            {
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

            }

            foreach (ConversationParticipant p in e.Removed)
            {
                Console.WriteLine("{0} is notified of participant leaving the conversation: {1}",
                    conv.LocalParticipant.UserAtHost,
                    p.UserAtHost);
            }

            Console.WriteLine();
        }

        // Just to record the state transitions in the console.
        void Conversation_ParticipantEndpointPropertiesChanged(object sender, ParticipantPropertiesChangedEventArgs e)
        {
            Conversation conv = sender as Conversation;

            Console.WriteLine(
                "{0} is notified of Conversation participant property change for user: {1}. Role:{2}",
                conv.LocalParticipant.UserAtHost,
                e.Participant.UserAtHost,
                e.Properties.Role);

            Console.WriteLine();

            Message m = new Message("Conversation_ParticipantEndpointPropertiesChanged for user: " + e.Participant.DisplayName,
                e.Participant.DisplayName, e.Participant.UserAtHost, e.Participant.Uri,
                DateTime.Now, conv.Id, conv.ConferenceSession.ConferenceUri,
                MessageModality.ConversationInfo, MessageDirection.Outgoing);

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
            }
        }

    public void conversation_EscalateToConferenceRequested(object sender, EscalateToConferenceRequestedEventArgs e)
    {

    }

        #endregion // Conversation Event Handlers
    }
}
