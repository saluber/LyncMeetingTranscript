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
        // TODO: Transmit messages to Lync client app over ConversationContextChannel
        private ConversationContextChannel _channel;
        private Conference _conference;
        private Conversation _conversation;

        private List<MediaTranscriptRecorder> _mediaCallRecorders;
        private List<Message> _messages;

        private AutoResetEvent _waitForConversationTerminated = new AutoResetEvent(false);

        public Conference Conference
        {
            get { return _conference; }
        }

        public Conversation Conversation
        {
            get { return _conversation; }
        }

        public List<Message> Messages
        {
            get { return _messages; }
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

        public string GetFullTranscript(bool print = false)
        {
            String transcript = "";
            foreach (Message m in _messages)
            {
                transcript += m.ToString() + "\n";
            }

            if (print)
            {
                Console.WriteLine("Meeting Transcript:\n" + transcript);
            }

            return transcript;
        }

        public void Shutdown()
        {
            foreach (MediaTranscriptRecorder m in _mediaCallRecorders)
            {
                m.Shutdown();
            }

            _mediaCallRecorders.Clear();

            if (_conversation != null)
            {
                Message m = new Message("Conversation shutting down.", _conversation.LocalParticipant.Uri,
                    _conversation.LocalParticipant.DisplayName, DateTime.Now,
                    _conversation.Id, _conversation.ConferenceSession.ConferenceUri,
                    MessageModality.ConversationInfo, MessageDirection.Outgoing);
                this.AddMessage(m);

                _conversation.BeginTerminate(ConversationTerminated, _conversation);
                UnregisterConversationEvents();

                //_waitForConversationTerminated.WaitOne();
                _conversation = null;
            }

            _conference = null;
        }

        #endregion // Public Methods

        internal void OnMediaTranscriptRecorderError()
        {
            // TODO: Logic to handle errors that can happen in indiviual recorders/modality calls
        }

        internal void OnTerminated(MediaTranscriptRecorder terminatedRecorder)
        {
            if (_mediaCallRecorders.Contains(terminatedRecorder))
            {
                _mediaCallRecorders.Remove(terminatedRecorder);
            }

            if (_mediaCallRecorders.Count == 0)
            {
                this.Shutdown();
            }
        }

        internal void OnSubConversationAdded(Conversation subConversation)
        {
            // TODO: Start conversation recorder on sub conversation
        }

        internal void OnConferenceJoined(Conference conference)
        {
            // TODO: Start conference transcript recorder on conference
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
