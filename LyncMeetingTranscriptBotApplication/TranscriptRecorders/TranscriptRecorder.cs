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
    public abstract class MediaTranscriptRecorder
    {
        public abstract void Shutdown();
    }

    public class TranscriptRecorder
    {
        private AutoResetEvent _waitForConversationTerminated = new AutoResetEvent(false);

        private List<MediaTranscriptRecorder> _mediaCallRecorders;
        private List<Message> _messages;

        private Conversation _conversation;

        // TODO: Transmit messages to Lync client app over ConversationContextChannel
        private ConversationContextChannel _channel;

        public List<Message> Messages
        {
            get { return _messages; }
        }

        public Conversation Conversation
        {
            get { return _conversation; }
        }

        #region Consturctors

        public TranscriptRecorder(ConferenceInvitationReceivedEventArgs e)
        {
            // TODO
        }

        public TranscriptRecorder(CallReceivedEventArgs<AudioVideoCall> e)
        {
            _mediaCallRecorders = new List<MediaTranscriptRecorder>();
            _messages = new List<Message>();
            _conversation = e.Call.Conversation;

            RegisterConversationEvents();

            ConversationParticipant caller = e.RemoteParticipant;
            Message m = new Message("AudioVideo Conversation/Conference Started.", caller.Uri, caller.DisplayName, DateTime.Now,
                _conversation.Id, _conversation.ConferenceSession.ConferenceUri,
                MessageModality.ConferenceInfo, MessageDirection.Outgoing);
            this.AddMessage(m);

            AddAVIncomingCall(e);
        }

        public TranscriptRecorder(CallReceivedEventArgs<InstantMessagingCall> e)
        {
            _mediaCallRecorders = new List<MediaTranscriptRecorder>();
            _messages = new List<Message>();
            _conversation = e.Call.Conversation;

            RegisterConversationEvents();

            ConversationParticipant caller = e.RemoteParticipant;
            Message m = new Message("InstantMessaging Conversation/Conference Started.", caller.Uri, caller.DisplayName, DateTime.Now,
                _conversation.Id, _conversation.ConferenceSession.ConferenceUri,
                MessageModality.ConferenceInfo, MessageDirection.Outgoing);
            this.AddMessage(m);

            AddIMIncomingCall(e);
        }

        #endregion // Constructors

        #region Public Methods

        public void AddAVIncomingCall(CallReceivedEventArgs<AudioVideoCall> e)
        {
            // TODO: Swap if AV call already set on this conversation?
            /*
            if (_avTranscriptRecorder != null)
            {
                _avTranscriptRecorder.TerminateCall();
            }
            else
            {
                _avTranscriptRecorder = new AudioVideoCallTranscriptRecorder(this);
            }
            */
            AVTranscriptRecorder a = new AVTranscriptRecorder(this);

            ConversationParticipant caller = e.RemoteParticipant;
            Message m = new Message("AudioVideo Conversation Participant Added.", caller.Uri, caller.DisplayName, DateTime.Now,
                _conversation.Id, _conversation.ConferenceSession.ConferenceUri,
                MessageModality.ConversationInfo, MessageDirection.Outgoing);
            this.AddMessage(m);

            _mediaCallRecorders.Add(a);

            a.AudioVideoCall_Received(e);
        }

        public void AddIMIncomingCall(CallReceivedEventArgs<InstantMessagingCall> e)
        {
            // TODO: Swap if IM call already set on this conversation?
            /*
            if (_imTranscriptRecorder != null)
            {
                _imTranscriptRecorder.TerminateCall();
            }
            else
            {
                _imTranscriptRecorder = new InstantMessagingTranscriptRecorder(this);
            }
            */

            IMTranscriptRecorder i = new IMTranscriptRecorder(this);

            ConversationParticipant caller = e.RemoteParticipant;
            Message m = new Message("InstantMessaging Conversation Participant Added.", caller.Uri, caller.DisplayName, DateTime.Now,
                _conversation.Id, _conversation.ConferenceSession.ConferenceUri,
                MessageModality.ConversationInfo, MessageDirection.Outgoing);
            this.AddMessage(m);

            _mediaCallRecorders.Add(i);

            i.On_InstantMessagingCall_Received(e);
        }

        public void AddMessage(Message m)
        {
            Console.WriteLine("Message logged: " + m.ToString());

            // TODO: Write message to Lync client app or output file

            _messages.Add(m);
        }

        public string GetFullTranscript()
        {
            String transcript = "";
            foreach (Message m in _messages)
            {
                transcript += m.ToString() + "\n";
            }

            return transcript;
        }

        public void Shutdown()
        {
            /*
            if (_avTranscriptRecorder != null)
            {
                _avTranscriptRecorder.Shutdown();
                _avTranscriptRecorder = null;
            }

            if (_imTranscriptRecorder != null)
            {
                _imTranscriptRecorder.Shutdown();
                _imTranscriptRecorder = null;
            }
             */

            foreach (MediaTranscriptRecorder m in _mediaCallRecorders)
            {
                m.Shutdown();
            }

            _mediaCallRecorders.Clear();

            if (_conversation != null)
            {
                UnregisterConversationEvents();

                _conversation.BeginTerminate(ConversationTerminated, _conversation);

                Message m = new Message("Conversation shutting down.", _conversation.LocalParticipant.Uri,
                    _conversation.LocalParticipant.DisplayName, DateTime.Now,
                    _conversation.Id, _conversation.ConferenceSession.ConferenceUri,
                    MessageModality.ConversationInfo, MessageDirection.Outgoing);

                this.AddMessage(m);

                //_waitForConversationTerminated.WaitOne();
            }
        }

        #endregion // Public Methods

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

            AddMessage(m);
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

        #region ConferenceSession Event Handlers

        //Just to record the state transitions in the console.
        void ConferenceSession_StateChanged(object sender, StateChangedEventArgs<ConferenceSessionState> e)
        {
            ConferenceSession confSession = sender as ConferenceSession;

            //Session participants allow for disambiguation.
            Console.WriteLine("The conference session with Local Participant: " +
                confSession.Conversation.LocalParticipant + " has changed state. " +
                "The previous conference state was: " + e.PreviousState +
                " and the current state is: " + e.State);
            Console.WriteLine();

            Message m = new Message("ConferenceSession state changed from " + e.PreviousState.ToString()
                + " to new value: " + e.State.ToString() + ".",
        confSession.Conversation.LocalParticipant.DisplayName, confSession.Conversation.LocalParticipant.Uri,
        DateTime.Now, confSession.Conversation.Id, confSession.ConferenceUri, MessageModality.ConferenceInfo, MessageDirection.Outgoing);

            AddMessage(m);
        }

        //Just to record the state transitions in the console.
        void ConferenceSession_ParticipantEndpointAttendanceChanged(object sender,
            ParticipantEndpointAttendanceChangedEventArgs<ConferenceParticipantEndpointProperties> e)
        {
            ConferenceSession confSession = sender as ConferenceSession;

            // Log each participant as s/he gets added/deleted from the ConferenceSession's roster.
            foreach (KeyValuePair<ParticipantEndpoint, ConferenceParticipantEndpointProperties> pair in e.Joined)
            {
                Console.WriteLine("{0} is notified of participant joining the conference: {1}",
                    confSession.Conversation.LocalParticipant.UserAtHost,
                    pair.Key.Participant.UserAtHost);

            }

            foreach (KeyValuePair<ParticipantEndpoint, ConferenceParticipantEndpointProperties> pair in e.Left)
            {
                Console.WriteLine("{0} is notified of participant leaving the conference: {1}",
                    confSession.Conversation.LocalParticipant.UserAtHost,
                    pair.Key.Participant.UserAtHost);
            }

            Console.WriteLine();
        }

        // Just to record the state transitions in the console.
        void ConferenceSession_ParticipantEndpointPropertiesChanged(object sender,
            ParticipantEndpointPropertiesChangedEventArgs<ConferenceParticipantEndpointProperties> e)
        {
            ConferenceSession confSession = sender as ConferenceSession;

            Console.WriteLine(
                "{0} is notified of ConferenceSession participant property change for user: {1}. Role:{2}, CanManageLobby:{3}, InLobby:{4}",
                confSession.Conversation.LocalParticipant.UserAtHost,
                e.ParticipantEndpoint.Participant.UserAtHost,
                e.Properties.Role,
                e.Properties.CanManageLobby,
                e.Properties.IsInLobby);

            Console.WriteLine();

            // TODO: Conference message
        }

        // Just to record the state transitions in the console.
        void ConferenceSession_PropertiesChanged(object sender,
            PropertiesChangedEventArgs<ConferenceSessionProperties> e)
        {
            ConferenceSession confSession = sender as ConferenceSession;
            string propertyValue = null;

            foreach (string property in e.ChangedPropertyNames)
            {
                // Record all ConferenceSession property changes.
                switch (property)
                {
                    case "AccessLevel":
                        propertyValue = e.Properties.AccessLevel.ToString();
                        break;
                    case "AutomaticLeaderAssignment":
                        propertyValue = e.Properties.AutomaticLeaderAssignment.ToString();
                        break;
                    case "ConferenceUri":
                        propertyValue = e.Properties.ConferenceUri;
                        break;
                    case "Disclaimer":
                        propertyValue = e.Properties.Disclaimer;
                        break;
                    case "DisclaimerTitle":
                        propertyValue = e.Properties.DisclaimerTitle;
                        break;
                    case "HostingNetwork":
                        propertyValue = e.Properties.HostingNetwork.ToString();
                        break;
                    case "LobbyBypass":
                        propertyValue = e.Properties.LobbyBypass.ToString();
                        break;
                    case "Organizer":
                        propertyValue = e.Properties.Organizer.UserAtHost;
                        break;
                    case "ParticipantData":
                        propertyValue = e.Properties.ParticipantData;
                        break;
                    case "RecordingPolicy":
                        propertyValue = e.Properties.RecordingPolicy.ToString();
                        break;
                    case "SchedulingTemplate":
                        propertyValue = e.Properties.SchedulingTemplate.ToString();
                        break;
                    case "Subject":
                        propertyValue = e.Properties.Subject;
                        break;
                }

                Console.WriteLine("{0} is notified of ConferenceSession property change. {1}: {2}",
                    confSession.Conversation.LocalParticipant.UserAtHost,
                    property,
                    propertyValue);

                Message m = new Message("ConferenceSession property " + property + " changed to new value: " + propertyValue + ".",
                    confSession.Conversation.LocalParticipant.DisplayName, confSession.Conversation.LocalParticipant.Uri,
                    DateTime.Now, confSession.Conversation.Id, confSession.ConferenceUri, MessageModality.ConferenceInfo, MessageDirection.Outgoing);

                AddMessage(m);
            }

            Console.WriteLine();
        }

        #endregion // Conference Event Handlers


        #region Callbacks

        /// <summary>
        /// Occurs when bot joined the conference.
        /// </summary>
        /// <param name="argument">The argument.</param>
        /// <remarks></remarks>
        private void EndJoinConference(IAsyncResult argument)
        {
            ConferenceSession conferenceSession = argument.AsyncState as ConferenceSession;
            Exception exception = null;
            try
            {
                Console.WriteLine("Joined the conference");
                conferenceSession.EndJoin(argument);
                Console.WriteLine(string.Format(
                                              "Conference Url: conf:{0}%3Fconversation-id={1}",
                                              conferenceSession.ConferenceUri,
                                              conferenceSession.Conversation.Id));
            }
            catch (ConferenceFailureException conferenceFailureException)
            {
                // ConferenceFailureException may be thrown on failures due to MCUs being absent or unsupported, or due to malformed parameters.
                // It is left to the application to perform real error handling here.
                Console.WriteLine(conferenceFailureException.ToString());
                exception = conferenceFailureException;
            }
            catch (RealTimeException realTimeException)
            {
                // It is left to the application to perform real error handling here.
                Console.WriteLine(realTimeException.ToString());
                exception = realTimeException;
            }
            finally
            {
                // Again, for sync. reasons.
                //this.applicationConferenceJoinCompletedEvent.Set();

                if (exception != null)
                {
                    string originator = string.Format("Error when joining the conference.");
                    Console.WriteLine(originator);
                }

                // In case Bot was dragged into existing conversation or someone was dragged into existing conversation with Bot; 
                // it will create ad-hoc conference and here is the place where we need to escalate current call into conference.

                //if (this.escalateToConference)
                //{
                //    this.escalateToConference = false;
                conferenceSession.Conversation.BeginEscalateToConference(this.EndEscalateConference, conferenceSession.Conversation);
                //}
            }
        }

        /// <summary>
        /// Ends the escalation to conference.
        /// </summary>
        /// <param name="argument">The argument.</param>
        /// <remarks></remarks>
        private void EndEscalateConference(IAsyncResult argument)
        {
            Conversation conversation = argument.AsyncState as Conversation;
            Exception exception = null;
            try
            {
                conversation.EndEscalateToConference(argument);
                Console.WriteLine("Conversation was escalated into conference");

                RegisterConversationEvents();
            }
            catch (OperationFailureException operationFailureException)
            {
                // OperationFailureException: Indicates failure to connect the call to the remote party.
                // It is left to the application to perform real error handling here.
                Console.WriteLine(operationFailureException.ToString());
                exception = operationFailureException;
            }
            catch (RealTimeException realTimeException)
            {
                // RealTimeException may be thrown on media or link-layer failures.
                // It is left to the application to perform real error handling here.
                Console.WriteLine(realTimeException.ToString());
                exception = realTimeException;
            }
            finally
            {
                //Again, just to sync the completion of the code.
                if (exception != null)
                {
                    string originator = string.Format("Error when escalating to conference.");
                    Console.WriteLine(originator);
                }
            }
        }


        private void ConversationTerminated(IAsyncResult ar)
        {
            Conversation conv = ar.AsyncState as Conversation;

            // End terminating the conversation.
            conv.EndTerminate(ar);

            //Again, just to sync the completion of the code.
            _waitForConversationTerminated.Set();
        }

        #endregion // Callbacks
    }
}
