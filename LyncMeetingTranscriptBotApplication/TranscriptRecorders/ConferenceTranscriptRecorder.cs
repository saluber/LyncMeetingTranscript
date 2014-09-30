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
    class ConferenceTranscriptRecorder : MediaTranscriptRecorder
    {
        private static TranscriptRecorderType _type = TranscriptRecorderType.Conference;
        private TranscriptRecorderState _state = TranscriptRecorderState.Initialized;

        private TranscriptRecorder _transcriptRecorder;
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

        public TranscriptRecorder TranscriptRecorder
        {
            get { return _transcriptRecorder; }
        }

        public ConferenceSession Conference
        {
            get { return _conference; }
        }

        public ConferenceTranscriptRecorder(TranscriptRecorder transcriptRecorder, ConferenceSession conference)
        {
            _transcriptRecorder = transcriptRecorder;
            _conference = conference;

            RegisterConferenceEvents();
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
        }

        #endregion // ConferenceSession Event Handlers

        #region Private Methods

        private void RegisterConferenceEvents()
        {
            // Register ConversationSession events
            _conference.StateChanged += new EventHandler<StateChangedEventArgs<ConferenceSessionState>>(this.ConferenceSession_StateChanged);
            _conference.PropertiesChanged += new EventHandler<PropertiesChangedEventArgs<ConferenceSessionProperties>>(this.ConferenceSession_PropertiesChanged);
            _conference.ParticipantEndpointAttendanceChanged += new EventHandler<ParticipantEndpointAttendanceChangedEventArgs<ConferenceParticipantEndpointProperties>>(this.ConferenceSession_ParticipantEndpointAttendanceChanged);
            _conference.ParticipantEndpointPropertiesChanged += new EventHandler<ParticipantEndpointPropertiesChangedEventArgs<ConferenceParticipantEndpointProperties>>(this.ConferenceSession_ParticipantEndpointPropertiesChanged);
        }

        private void UnregisterConferenceEvents()
        {
            // Unregister ConferenceSession events
            _conference.StateChanged -= this.ConferenceSession_StateChanged;
            _conference.PropertiesChanged -= this.ConferenceSession_PropertiesChanged;
            _conference.ParticipantEndpointAttendanceChanged -= this.ConferenceSession_ParticipantEndpointAttendanceChanged;
            _conference.ParticipantEndpointPropertiesChanged -= this.ConferenceSession_ParticipantEndpointPropertiesChanged;
        }

        #endregion // Private Methods
    }
}
