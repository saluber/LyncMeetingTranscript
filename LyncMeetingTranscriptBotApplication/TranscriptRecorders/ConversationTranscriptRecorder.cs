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
        private TranscriptRecorder _transcriptRecorder;
        private Conversation _conversation;

        public ConversationTranscriptRecorder(Conversation conversation)
        {
            _conversation = conversation;
            RegisterConversationEvents();
        }

        public override void Shutdown()
        {
            if (_conversation != null)
            {
                UnregisterConversationEvents();
            }
        }

        #region Private Methods

        private void RegisterConversationEvents()
        {
            // Register for Conversation events
            _conversation.StateChanged += new EventHandler<StateChangedEventArgs<ConversationState>>(Conversation_StateChanged);
            _conversation.PropertiesChanged += new EventHandler<PropertiesChangedEventArgs<ConversationProperties>>(Conversation_PropertiesChanged);
            _conversation.EscalateToConferenceRequested += new EventHandler<EscalateToConferenceRequestedEventArgs>(Conversation_EscalateToConferenceRequested);
            _conversation.RemoteParticipantAttendanceChanged += new EventHandler<ParticipantAttendanceChangedEventArgs>(Conversation_ParticipantEndpointAttendanceChanged);
            _conversation.ParticipantPropertiesChanged += new EventHandler<ParticipantPropertiesChangedEventArgs>(Conversation_ParticipantEndpointPropertiesChanged);

            // Register ConversationSession events
            if (_conversation.ConferenceSession != null)
            {
                _conversation.ConferenceSession.StateChanged += new EventHandler<StateChangedEventArgs<ConferenceSessionState>>(this.ConferenceSession_StateChanged);
                _conversation.ConferenceSession.PropertiesChanged += new EventHandler<PropertiesChangedEventArgs<ConferenceSessionProperties>>(this.ConferenceSession_PropertiesChanged);
                _conversation.ConferenceSession.ParticipantEndpointAttendanceChanged += new EventHandler<ParticipantEndpointAttendanceChangedEventArgs<ConferenceParticipantEndpointProperties>>(this.ConferenceSession_ParticipantEndpointAttendanceChanged);
                _conversation.ConferenceSession.ParticipantEndpointPropertiesChanged += new EventHandler<ParticipantEndpointPropertiesChangedEventArgs<ConferenceParticipantEndpointProperties>>(this.ConferenceSession_ParticipantEndpointPropertiesChanged);
            }
        }

        private void UnregisterConversationEvents()
        {
            // Unregister for Conversation events
            _conversation.StateChanged -= Conversation_StateChanged;
            _conversation.PropertiesChanged -= Conversation_PropertiesChanged;
            _conversation.EscalateToConferenceRequested -= Conversation_EscalateToConferenceRequested;
            _conversation.RemoteParticipantAttendanceChanged -= Conversation_ParticipantEndpointAttendanceChanged;
            _conversation.ParticipantPropertiesChanged -= Conversation_ParticipantEndpointPropertiesChanged;

            // Unregister ConferenceSession events
            if (_conversation.ConferenceSession != null)
            {
                _conversation.ConferenceSession.StateChanged -= this.ConferenceSession_StateChanged;
                _conversation.ConferenceSession.PropertiesChanged -= this.ConferenceSession_PropertiesChanged;
                _conversation.ConferenceSession.ParticipantEndpointAttendanceChanged -= this.ConferenceSession_ParticipantEndpointAttendanceChanged;
                _conversation.ConferenceSession.ParticipantEndpointPropertiesChanged -= this.ConferenceSession_ParticipantEndpointPropertiesChanged;
            }
        }

        #endregion // Private Methods

        #region Conversation Event Handlers

        void Conversation_StateChanged(object sender, StateChangedEventArgs<ConversationState> e)
        {
            Conversation conv = sender as Conversation;
            Console.WriteLine("Conversation {0} state changed from " + e.PreviousState + " to " + e.State, conv.LocalParticipant.UserAtHost);
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
                e.Participant.DisplayName, e.Participant.Uri,
                DateTime.Now, conv.Id, conv.ConferenceSession.ConferenceUri,
                MessageModality.ConversationInfo, MessageDirection.Outgoing);

            _transcriptRecorder.AddMessage(m);
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

        /// <summary>
        /// Handler for the Conversation_EscalateToConferenceRequested event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The EventArgs instance containing the event data.</param>
        private void Conversation_EscalateToConferenceRequested(object sender, EventArgs e)
        {
            Conversation conversation = sender as Conversation;

            // Note that it will not cause any new conference to be created.
            // First, we bind the conference state changed event handler, largely for logging reasons.
            conversation.ConferenceSession.StateChanged += new EventHandler<StateChangedEventArgs<ConferenceSessionState>>(this.ConferenceSession_StateChanged);

            // Next, the prepare the session to escalate, by calling ConferenceSession.BeginJoin on the conversation that received the escalation request.
            // This prepares the calls for actual escalation by binding the appropriate conference multipoint Control Unit (MCU) sessions.
            // You cannot escalate directly in response to an escalation request.

            conversation.ConferenceSession.BeginJoin(null as ConferenceJoinOptions, this.EndJoinConference, conversation.ConferenceSession);
        }

        #endregion // Conversation Event Handlers


    }
}
