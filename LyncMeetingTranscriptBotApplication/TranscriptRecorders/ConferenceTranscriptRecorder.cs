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
    class ConferenceTranscriptRecorder : MediaTranscriptRecorder
    {
        private static TranscriptRecorderType _type = TranscriptRecorderType.Conference;
        private TranscriptRecorderState _state = TranscriptRecorderState.Initialized;

        private AutoResetEvent _waitForConferenceSessionTerminated = new AutoResetEvent(false);
        private AutoResetEvent _waitForInvitedConferenceJoined = new AutoResetEvent(false);
        private AutoResetEvent _waitForInvitedConferenceActiveMediaTypeCallEstablished = new AutoResetEvent(false);
        private AutoResetEvent _waitForEscalatedConferenceJoined = new AutoResetEvent(false);
        private AutoResetEvent _waitForConferenceEscalationCompleted = new AutoResetEvent(false);

        private TranscriptRecorderSession _transcriptRecorder;
        private Conversation _conversation;
        private ConferenceSession _conference;

        // TODO: Event handler delegates, create transcriptrecorders for added modalities
        // Add accept invite, begin join, and begin escalate methods

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

        public ConferenceSession ConferenceSession
        {
            get { return _conference; }
        }

        public ConferenceTranscriptRecorder(TranscriptRecorderSession transcriptRecorder, Conversation conversation)
        {
            _transcriptRecorder = transcriptRecorder;
            _conversation = conversation;

            // TODO: TranscriptRecorderSession should check if new conversation is joined to a conference and do full
            // Begin/EndJoin with an End async method in ConferenceTranscriptRecorder (as is done for Invite or Escalate)
            if (_conversation.ConferenceSession != null)
            {
                _conference = _conversation.ConferenceSession;
                _state = TranscriptRecorderState.Active;
                _waitForInvitedConferenceJoined.Set();
                RegisterConferenceEvents();
            }
        }

        public void ConferenceInviteAccepted(IAsyncResult result)
        {
            try
            {
                ConferenceInvitation invite = result.AsyncState as ConferenceInvitation;
                // ConferenceInvite already accepted in TranscriptRecorder.ConferenceInvitation_AcceptCompleted()

                ConferenceJoinOptions cjo = new ConferenceJoinOptions();
                //cjo.JoinAsTrustedApplication = false;
                _conversation.ConferenceSession.BeginJoin(cjo, EndJoinInvitedConference, invite);
            }
            catch (RealTimeException ex)
            {
                Console.WriteLine("invite.EndAccept failed. Exception: {0}", ex.ToString());
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine("m_conversation.ConferenceSession.BeginJoin failed. Exception: {0}", ex.ToString());
            }
        }

        public void EscalateToConferenceRequested()
        {
            // The conference session of the escalating conversation must be joined.
            _conversation.ConferenceSession.BeginJoin(default(ConferenceJoinOptions), EndJoinEscalatedConference, _conversation.ConferenceSession);
        }

        public override void Shutdown()
        {
            if (_state == TranscriptRecorderState.Terminated)
            {
                return;
            }
            _state = TranscriptRecorderState.Terminated;
            // TODO: Shutdown message

            if (_conference != null)
            {
                UnregisterConferenceEvents();
            }

            _transcriptRecorder.OnMediaTranscriptRecorderTerminated(this);
            _transcriptRecorder = null;
        }

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
        confSession.Conversation.LocalParticipant.DisplayName, confSession.Conversation.LocalParticipant.UserAtHost, confSession.Conversation.LocalParticipant.Uri,
        DateTime.Now, confSession.Conversation.Id, confSession.ConferenceUri, MessageModality.ConferenceInfo, MessageDirection.Outgoing);

            _transcriptRecorder.OnMessageReceived(m);

            if (e.State == ConferenceSessionState.Disconnecting || e.State == ConferenceSessionState.Disconnected)
            {
                this.Shutdown();
            }
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
                    confSession.Conversation.LocalParticipant.DisplayName, confSession.Conversation.LocalParticipant.UserAtHost, confSession.Conversation.LocalParticipant.Uri,
                    DateTime.Now, confSession.Conversation.Id, confSession.ConferenceUri, MessageModality.ConferenceInfo, MessageDirection.Outgoing);

                _transcriptRecorder.OnMessageReceived(m);
            }

            Console.WriteLine();

            // TODO: If modalities added, establish calls on new modalities
        }

        private void InstantMessagingMcuSession_ParticipantEndpointPropertiesChanged(object sender, ParticipantEndpointPropertiesChangedEventArgs<InstantMessagingMcuParticipantEndpointProperties> e)
        {
            throw new NotImplementedException();
        }

        private void InstantMessagingMcuSession_ParticipantEndpointAttendanceChanged(object sender, ParticipantEndpointAttendanceChangedEventArgs<InstantMessagingMcuParticipantEndpointProperties> e)
        {
            throw new NotImplementedException();
        }

        private void InstantMessagingMcuSession_StateChanged(object sender, StateChangedEventArgs<McuSessionState> e)
        {
            throw new NotImplementedException();
        }

        private void AudioVideoMcuSession_ParticipantEndpointPropertiesChanged(object sender, ParticipantEndpointPropertiesChangedEventArgs<AudioVideoMcuParticipantEndpointProperties> e)
        {
            throw new NotImplementedException();
        }

        private void AudioVideoMcuSession_ParticipantEndpointAttendanceChanged(object sender, ParticipantEndpointAttendanceChangedEventArgs<AudioVideoMcuParticipantEndpointProperties> e)
        {
            throw new NotImplementedException();
        }

        private void AudioVideoMcuSession_StateChanged(object sender, StateChangedEventArgs<McuSessionState> e)
        {
            throw new NotImplementedException();
        }

        #endregion // ConferenceSession Event Handlers

        #region Callbacks
        /// <summary>
        /// Occurs when bot joined the conference.
        /// </summary>
        /// <param name="result">The argument.</param>
        /// <remarks></remarks>
        public void EndJoinInvitedConference(IAsyncResult result)
        {
            ConferenceInvitation invite = result.AsyncState as ConferenceInvitation;
            Exception exception = null;
            List<String> activeMediaTypes = new List<string>();
            try
            {
                Console.WriteLine("Joined the invited conference");
                activeMediaTypes = invite.AvailableMediaTypes.ToList();

                _conversation.ConferenceSession.EndJoin(result);
                _conference = _conversation.ConferenceSession;

                Console.WriteLine(string.Format(
                                              "Conference Url: conf:{0}%3Fconversation-id={1}",
                                              _conversation.ConferenceSession.ConferenceUri,
                                              _conversation.ConferenceSession.Conversation.Id));

                RegisterConferenceEvents();

                // Raise event on TranscriptRecorderSession
                _transcriptRecorder.RaiseTranscriptRecorderSessionChanged(_conference);

                // Establish Calls for Conference's supported modalities
                if (activeMediaTypes.Contains(MediaType.Audio))
                {
                    _transcriptRecorder.OnActiveMediaTypeCallToEstablish(_conversation, TranscriptRecorderType.AudioVideo);
                }
                if (activeMediaTypes.Contains(MediaType.Message))
                {
                    _transcriptRecorder.OnActiveMediaTypeCallToEstablish(_conversation, TranscriptRecorderType.InstantMessage);
                }

                _waitForInvitedConferenceActiveMediaTypeCallEstablished.Set();
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
                _state = TranscriptRecorderState.Active;
                _waitForInvitedConferenceJoined.Set();

                if (exception != null)
                {
                    string originator = string.Format("Error when joining the invited conference: {0}", exception.ToString());
                    Console.WriteLine(originator);
                }
            }
        }

        /// <summary>
        /// Occurs when bot joined the conference.
        /// </summary>
        /// <param name="argument">The argument.</param>
        /// <remarks></remarks>
        public void EndJoinEscalatedConference(IAsyncResult argument)
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

                _conference = conferenceSession;

                RegisterConferenceEvents();

                // Raise event on TranscriptRecorderSession
                _transcriptRecorder.RaiseTranscriptRecorderSessionChanged(_conference);

                // In case Bot was dragged into existing conversation or someone was dragged into existing conversation with Bot; 
                // it will create ad-hoc conference and here is the place where we need to escalate current call into conference.
                conferenceSession.Conversation.BeginEscalateToConference(EndEscalateConversation, conferenceSession.Conversation);

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
                if (exception != null)
                {
                    string originator = string.Format("Error when joining the escalated conference.");
                    Console.WriteLine(originator);
                }

                _waitForEscalatedConferenceJoined.Set();
            }
        }

        /// <summary>
        /// Ends the escalation to conference.
        /// </summary>
        /// <param name="argument">The argument.</param>
        /// <remarks></remarks>
        private void EndEscalateConversation(IAsyncResult argument)
        {
            Conversation conversation = argument.AsyncState as Conversation;
            Exception exception = null;
            try
            {
                conversation.EndEscalateToConference(argument);
                Console.WriteLine("Conversation was escalated into conference");
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
                _state = TranscriptRecorderState.Active;
                _waitForConferenceEscalationCompleted.Set();

                //Again, just to sync the completion of the code.
                if (exception != null)
                {
                    string originator = string.Format("Error when escalating to conference.");
                    Console.WriteLine(originator);
                }
            }
        }

        #endregion // Callbacks

        #region Private Methods

        private void RegisterConferenceEvents()
        {
            // Register ConversationSession events
            _conference.StateChanged += new EventHandler<StateChangedEventArgs<ConferenceSessionState>>(this.ConferenceSession_StateChanged);
            _conference.PropertiesChanged += new EventHandler<PropertiesChangedEventArgs<ConferenceSessionProperties>>(this.ConferenceSession_PropertiesChanged);
            _conference.ParticipantEndpointAttendanceChanged += new EventHandler<ParticipantEndpointAttendanceChangedEventArgs<ConferenceParticipantEndpointProperties>>(this.ConferenceSession_ParticipantEndpointAttendanceChanged);
            _conference.ParticipantEndpointPropertiesChanged += new EventHandler<ParticipantEndpointPropertiesChangedEventArgs<ConferenceParticipantEndpointProperties>>(this.ConferenceSession_ParticipantEndpointPropertiesChanged);

            // Register for AudioVieoMcuSession state changes.
            _conference.AudioVideoMcuSession.StateChanged += new EventHandler<StateChangedEventArgs<McuSessionState>>(AudioVideoMcuSession_StateChanged);
            // Monitor AudioVideo MCU roster.
            _conference.AudioVideoMcuSession.ParticipantEndpointAttendanceChanged += new EventHandler<ParticipantEndpointAttendanceChangedEventArgs<AudioVideoMcuParticipantEndpointProperties>>(AudioVideoMcuSession_ParticipantEndpointAttendanceChanged);
            _conference.AudioVideoMcuSession.ParticipantEndpointPropertiesChanged += new EventHandler<ParticipantEndpointPropertiesChangedEventArgs<AudioVideoMcuParticipantEndpointProperties>>(AudioVideoMcuSession_ParticipantEndpointPropertiesChanged);

            // Register for InstantMessagingMcuSession state changes.
            _conference.InstantMessagingMcuSession.StateChanged += new EventHandler<StateChangedEventArgs<McuSessionState>>(InstantMessagingMcuSession_StateChanged);
            // Monitor Instant Messaging MCU roster.
            _conference.InstantMessagingMcuSession.ParticipantEndpointAttendanceChanged += new EventHandler<ParticipantEndpointAttendanceChangedEventArgs<InstantMessagingMcuParticipantEndpointProperties>>(InstantMessagingMcuSession_ParticipantEndpointAttendanceChanged);
            _conference.InstantMessagingMcuSession.ParticipantEndpointPropertiesChanged += new EventHandler<ParticipantEndpointPropertiesChangedEventArgs<InstantMessagingMcuParticipantEndpointProperties>>(InstantMessagingMcuSession_ParticipantEndpointPropertiesChanged);
        }

        private void UnregisterConferenceEvents()
        {
            // Unregister ConferenceSession events
            _conference.StateChanged -= this.ConferenceSession_StateChanged;
            _conference.PropertiesChanged -= this.ConferenceSession_PropertiesChanged;
            _conference.ParticipantEndpointAttendanceChanged -= this.ConferenceSession_ParticipantEndpointAttendanceChanged;
            _conference.ParticipantEndpointPropertiesChanged -= this.ConferenceSession_ParticipantEndpointPropertiesChanged;
            
            // Unregister for AudioVieoMcuSession state changes.
            _conference.AudioVideoMcuSession.StateChanged -= (AudioVideoMcuSession_StateChanged);
            // Monitor AudioVideo MCU roster.
            _conference.AudioVideoMcuSession.ParticipantEndpointAttendanceChanged -= (AudioVideoMcuSession_ParticipantEndpointAttendanceChanged);
            _conference.AudioVideoMcuSession.ParticipantEndpointPropertiesChanged -= (AudioVideoMcuSession_ParticipantEndpointPropertiesChanged);

            // Unregister for InstantMessagingMcuSession state changes.
            _conference.InstantMessagingMcuSession.StateChanged -= (InstantMessagingMcuSession_StateChanged);
            // Monitor Instant Messaging MCU roster.
            _conference.InstantMessagingMcuSession.ParticipantEndpointAttendanceChanged -= (InstantMessagingMcuSession_ParticipantEndpointAttendanceChanged);
            _conference.InstantMessagingMcuSession.ParticipantEndpointPropertiesChanged -= (InstantMessagingMcuSession_ParticipantEndpointPropertiesChanged);
        }

        #endregion // Private Methods
    }
}
