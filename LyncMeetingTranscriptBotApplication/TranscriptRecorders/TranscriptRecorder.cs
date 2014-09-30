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
    public enum TranscriptRecorderType { AudioVideo, InstantMessage, Conversation, Conference }

    public enum TranscriptRecorderState { Initialized, Active, Terminated }

    public abstract class MediaTranscriptRecorder
    {
        public abstract void Shutdown();

        public abstract TranscriptRecorderType RecorderType { get; }

        public abstract TranscriptRecorderState State { get; }
    }

    public class TranscriptRecorder
    {
        private TranscriptRecorderState _state = TranscriptRecorderState.Initialized;

        // TODO: Transmit messages to Lync client app over ConversationContextChannel
        private ConversationContextChannel _channel;
        private Conversation _conversation;

        private List<MediaTranscriptRecorder> _transcriptRecorders;
        private List<Message> _messages;

        private AutoResetEvent _waitForConversationTerminated = new AutoResetEvent(false);

        public TranscriptRecorderState State
        {
            get { return _state; }
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
            _transcriptRecorders = new List<MediaTranscriptRecorder>();
            _messages = new List<Message>();
            _state = TranscriptRecorderState.Active;

            // TODO
        }

        public TranscriptRecorder(CallReceivedEventArgs<AudioVideoCall> e)
        {
            _transcriptRecorders = new List<MediaTranscriptRecorder>();
            _messages = new List<Message>();
            _state = TranscriptRecorderState.Active;
            _conversation = e.Call.Conversation;

            // Log AV conversation started
            ConversationParticipant caller = e.RemoteParticipant;
            Message m = new Message("AudioVideo Conversation/Conference Started.", caller.DisplayName, caller.UserAtHost, caller.Uri, DateTime.Now,
                _conversation.Id, _conversation.ConferenceSession.ConferenceUri,
                MessageModality.ConferenceInfo, MessageDirection.Outgoing);
            this.OnMessageReceived(m);

            AddAVIncomingCall(e);
        }

        public TranscriptRecorder(CallReceivedEventArgs<InstantMessagingCall> e)
        {
            _transcriptRecorders = new List<MediaTranscriptRecorder>();
            _messages = new List<Message>();
            _state = TranscriptRecorderState.Active;
            _conversation = e.Call.Conversation;

            // Log IM conversation started
            ConversationParticipant caller = e.RemoteParticipant;
            Message m = new Message("InstantMessaging Conversation/Conference Started.", caller.DisplayName, caller.UserAtHost, caller.Uri, DateTime.Now,
                _conversation.Id, _conversation.ConferenceSession.ConferenceUri,
                MessageModality.ConferenceInfo, MessageDirection.Outgoing);
            this.OnMessageReceived(m);

            AddIMIncomingCall(e);
        }

        #endregion // Constructors

        #region Public Methods

        public void AddAVIncomingCall(CallReceivedEventArgs<AudioVideoCall> e)
        {
            AVTranscriptRecorder a = new AVTranscriptRecorder(this);

            ConversationParticipant caller = e.RemoteParticipant;
            Message m = new Message("AudioVideo Conversation Participant Added.", caller.DisplayName,
                caller.UserAtHost, caller.Uri, DateTime.Now,
                _conversation.Id, _conversation.ConferenceSession.ConferenceUri,
                MessageModality.ConversationInfo, MessageDirection.Outgoing);
            this.OnMessageReceived(m);

            _transcriptRecorders.Add(a);

            a.AudioVideoCall_Received(e);
        }

        public void AddIMIncomingCall(CallReceivedEventArgs<InstantMessagingCall> e)
        {
            if (_state != TranscriptRecorderState.Active)
            {
                Console.WriteLine("Warn: Unexpected TranscriptRecorder state: " + _state.ToString());
            }

            IMTranscriptRecorder i = new IMTranscriptRecorder(this);

            ConversationParticipant caller = e.RemoteParticipant;
            Message m = new Message("InstantMessaging Conversation Participant Added.", caller.DisplayName, 
                caller.UserAtHost, caller.Uri, DateTime.Now,
                _conversation.Id, _conversation.ConferenceSession.ConferenceUri,
                MessageModality.ConversationInfo, MessageDirection.Outgoing);
            this.OnMessageReceived(m);

            _transcriptRecorders.Add(i);

            i.InstantMessagingCall_Received(e);
        }

        internal void JoinIncomingInvitedConference()
        {

        }

        internal void JoinIncomingEscalatedConference()
        {

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
            if (_state == TranscriptRecorderState.Terminated)
            {
                return;
            }
            _state = TranscriptRecorderState.Terminated;

            lock (_transcriptRecorders)
            {
                foreach (MediaTranscriptRecorder m in _transcriptRecorders)
                {
                    m.Shutdown();
                }
                _transcriptRecorders.Clear();
            }

            if (_conversation != null)
            {
                Message m = new Message("Conversation shutting down.", _conversation.LocalParticipant.DisplayName,
                    _conversation.LocalParticipant.UserAtHost, _conversation.LocalParticipant.Uri, DateTime.Now,
                    _conversation.Id, _conversation.ConferenceSession.ConferenceUri,
                    MessageModality.ConversationInfo, MessageDirection.Outgoing);
                this.OnMessageReceived(m);

                _conversation.BeginTerminate(EndTerminateConversation, _conversation);
                //_waitForConversationTerminated.WaitOne();
                _conversation = null;
            }
        }

        #endregion // Public Methods

        internal void OnMessageReceived(Message m)
        {
            Console.WriteLine("Message logged: " + m.ToString());

            // TODO: Write message to Lync client app or output file

            _messages.Add(m);
        }

        internal void OnMediaTranscriptRecorderError()
        {
            // TODO: Logic to handle errors that can happen in indiviual recorders/modality calls
        }

        internal void OnMediaTranscriptRecorderTerminated(MediaTranscriptRecorder terminatedRecorder)
        {
            if (_transcriptRecorders.Contains(terminatedRecorder))
            {
                _transcriptRecorders.Remove(terminatedRecorder);
            }

            if (_transcriptRecorders.Count == 0)
            {
                this.Shutdown();
            }
        }

        internal void OnSubConversationAdded(Conversation subConversation, ConversationTranscriptRecorder addedRecorder)
        {
            // TODO: Start conversation recorder on sub conversation
            _transcriptRecorders.Add(addedRecorder);
        }

        internal void OnTranscriptModalityAdded(TranscriptRecorderType addedModality)
        {

        }

        #region Callbacks

        /// <summary>
        /// Occurs when bot joined the conference.
        /// </summary>
        /// <param name="argument">The argument.</param>
        /// <remarks></remarks>
        public void EndJoinConference(IAsyncResult argument)
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
                conferenceSession.Conversation.BeginEscalateToConference(this.EndEscalateConversation, conferenceSession.Conversation);
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

                //this.JoinIncomingEscalatedConference(conversation.ConferenceSession);
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

        private void EndTerminateConversation(IAsyncResult ar)
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
