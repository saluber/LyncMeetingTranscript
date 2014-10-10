﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Rtc.Collaboration;
using Microsoft.Rtc.Collaboration.AudioVideo;
using Microsoft.Rtc.Signaling;

using LyncMeetingTranscriptBotApplication.TranscriptRecorders;

namespace LyncMeetingTranscriptBotApplication
{
    public class TranscriptRecorderSessionShutdownEventArgs : EventArgs
    {
        private ConferenceSession _conference;
        private Conversation _conversation;
        private TranscriptRecorderSession _transcriptRecorderSession;
        private Guid _sessionId;
        private List<Message> _messages;

        public TranscriptRecorderSessionShutdownEventArgs(TranscriptRecorderSession trs)
        {
            _conference = trs.Conference;
            _conversation = trs.Conversation;
            _transcriptRecorderSession = trs;
            _sessionId = trs.SessionId;
            _messages = trs.Messages;
        }

        public ConferenceSession Conference
        {
            get { return _conference; }
        }

        public Conversation Conversation
        {
            get { return _conversation; }
        }

        public TranscriptRecorderSession TranscriptRecorderSession
        {
            get { return _transcriptRecorderSession; }
        }

        public Guid SessionId
        {
            get { return _sessionId; }
        }

        public List<Message> TranscriptMessages
        {
            get { return _messages; }
        }
    }

    public class TranscriptRecorderSessionChangedEventArgs : EventArgs
    {
        private ConferenceSession _conference;
        private Conversation _conversation;
        private Guid _sessionId;

        public TranscriptRecorderSessionChangedEventArgs(TranscriptRecorderSession trs)
        {
            _conference = trs.Conference;
            _conversation = trs.Conversation;
            _sessionId = trs.SessionId;
        }

        public ConferenceSession Conference
        {
            get { return _conference; }
        }

        public Conversation Conversation
        {
            get { return _conversation; }
        }

        public Guid SessionId
        {
            get { return _sessionId; }
        }
    }

    public class TranscriptRecorderSession
    {
        #region Events
        public event EventHandler<TranscriptRecorderSessionShutdownEventArgs> TranscriptRecorderSessionShutdown;

        public event EventHandler<TranscriptRecorderSessionChangedEventArgs> TranscriptRecorderSessionChanged;
        #endregion // Events

        #region Fields

        private Guid _sessionId;
        private TranscriptRecorderState _state = TranscriptRecorderState.Initialized;

        // TODO: Transmit messages to Lync client app over ConversationContextChannel
        // private ConversationContextChannel _channel;

        /// <summary>
        /// 'Main conversation' for user endpoint recording session
        /// </summary>
        private Conversation _conversation;
        private ConversationTranscriptRecorder _conversationTranscriptRecorder;

        // TODO: wtf is this data structure choice? fix it.
        private List<MediaTranscriptRecorder> _transcriptRecorders;
        private Dictionary<ConversationTranscriptRecorder, List<MediaTranscriptRecorder>> _conversationToCallTranscriptMapping;
        private List<Message> _messages;

        private AutoResetEvent _waitForConversationTerminated = new AutoResetEvent(false);

        #endregion // Fields

        #region Properties
        public Guid SessionId
        {
            get { return _sessionId; }
        }

        public TranscriptRecorderState State
        {
            get { return _state; }
        }

        public Conversation Conversation
        {
            get { return _conversation; }
        }

        public ConferenceSession Conference
        {
            get
            {
                if (_conversation == null)
                {
                    return null;
                }
                else
                {
                    return _conversation.ConferenceSession;
                }
            }
        }

        public List<Message> Messages
        {
            get { return _messages; }
        }
        #endregion // Properties

        #region Consturctors

        public TranscriptRecorderSession(ConferenceInvitationReceivedEventArgs e)
        {
            _sessionId = new Guid();
            _transcriptRecorders = new List<MediaTranscriptRecorder>();
            _conversationToCallTranscriptMapping = new Dictionary<ConversationTranscriptRecorder, List<MediaTranscriptRecorder>>();
            _messages = new List<Message>();
            _state = TranscriptRecorderState.Active;

            // Log Conference Invitation Recv
            ConversationParticipant caller = e.RemoteParticipant;
            Message m = new Message("Conference Started Invitation Recieved.", caller.DisplayName, caller.UserAtHost, caller.Uri, DateTime.Now,
                _conversation.Id, _conversation.ConferenceSession.ConferenceUri,
                MessageModality.ConferenceInfo, MessageDirection.Outgoing);
            this.OnMessageReceived(m);

            AddIncomingInvitedConferece(e);
        }

        public TranscriptRecorderSession(CallReceivedEventArgs<AudioVideoCall> e)
        {
            _sessionId = new Guid();
            _transcriptRecorders = new List<MediaTranscriptRecorder>();
            _conversationToCallTranscriptMapping = new Dictionary<ConversationTranscriptRecorder, List<MediaTranscriptRecorder>>();
            _messages = new List<Message>();
            _state = TranscriptRecorderState.Active;
            _conversation = e.Call.Conversation;
            _conversationTranscriptRecorder = new ConversationTranscriptRecorder(this, _conversation);
            _transcriptRecorders.Add(_conversationTranscriptRecorder);
            _conversationToCallTranscriptMapping.Add(_conversationTranscriptRecorder, new List<MediaTranscriptRecorder>());

            // Log AV conversation started
            ConversationParticipant caller = e.RemoteParticipant;
            Message m = new Message("AudioVideo Conversation/Conference Started.", caller.DisplayName, caller.UserAtHost, caller.Uri, DateTime.Now,
                _conversation.Id, _conversation.ConferenceSession.ConferenceUri,
                MessageModality.ConferenceInfo, MessageDirection.Outgoing);
            this.OnMessageReceived(m);

            AddAVIncomingCall(e);
        }

        public TranscriptRecorderSession(CallReceivedEventArgs<InstantMessagingCall> e)
        {
            _sessionId = new Guid();
            _transcriptRecorders = new List<MediaTranscriptRecorder>();
            _conversationToCallTranscriptMapping = new Dictionary<ConversationTranscriptRecorder, List<MediaTranscriptRecorder>>();
            _messages = new List<Message>();
            _state = TranscriptRecorderState.Active;
            _conversation = e.Call.Conversation;
            _conversationTranscriptRecorder = new ConversationTranscriptRecorder(this, _conversation);
            _transcriptRecorders.Add(_conversationTranscriptRecorder);
            _conversationToCallTranscriptMapping.Add(_conversationTranscriptRecorder, new List<MediaTranscriptRecorder>());

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

        public void AddIncomingInvitedConferece(ConferenceInvitationReceivedEventArgs e)
        {
            // Log Conference Invitation Recv
            ConversationParticipant caller = e.RemoteParticipant;
            Message m = new Message("Conference Started Invite Accept Started.", caller.DisplayName, caller.UserAtHost, caller.Uri, DateTime.Now,
                _conversation.Id, _conversation.ConferenceSession.ConferenceUri,
                MessageModality.ConferenceInfo, MessageDirection.Outgoing);
            this.OnMessageReceived(m);

            e.Invitation.BeginAccept(ConferenceInvitation_AcceptCompleted, e.Invitation);
        }

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
            _conversationToCallTranscriptMapping[_conversationTranscriptRecorder].Add(a);

            a.AudioVideoCall_Received(e);
        }

        public void AddIMIncomingCall(CallReceivedEventArgs<InstantMessagingCall> e)
        {
            if (_state != TranscriptRecorderState.Active)
            {
                Console.WriteLine("Warn: Unexpected TranscriptRecorderSession state: " + _state.ToString());
            }

            IMTranscriptRecorder i = new IMTranscriptRecorder(this);

            ConversationParticipant caller = e.RemoteParticipant;
            Message m = new Message("InstantMessaging Conversation Participant Added.", caller.DisplayName, 
                caller.UserAtHost, caller.Uri, DateTime.Now,
                _conversation.Id, _conversation.ConferenceSession.ConferenceUri,
                MessageModality.ConversationInfo, MessageDirection.Outgoing);
            this.OnMessageReceived(m);

            _transcriptRecorders.Add(i);
            _conversationToCallTranscriptMapping[_conversationTranscriptRecorder].Add(i);

            i.InstantMessagingCall_Received(e);
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

            TranscriptRecorderSessionShutdownEventArgs shutdownArgs = null;
            lock (_transcriptRecorders)
            {
                foreach (MediaTranscriptRecorder m in _transcriptRecorders)
                {
                    m.Shutdown();
                }
                _transcriptRecorders.Clear();
            }

            try
            {
                Message shutdownMessage = new Message("TranscriptRecorderSession is shutting down. SessionId: " + _sessionId.ToString(),
                    MessageModality.ConversationInfo, (_conversation == null) ? "null" : _conversation.Id);
                                
                this.OnMessageReceived(shutdownMessage);
                shutdownArgs = new TranscriptRecorderSessionShutdownEventArgs(this);

                // TODO: Conversation should already be shutdown because we shutdown ConversationTranscriptRecorder.
                if (_conversation != null)
                {
                    Message m = new Message("Conversation shutting down.", _conversation.LocalParticipant.DisplayName,
                        _conversation.LocalParticipant.UserAtHost, _conversation.LocalParticipant.Uri, DateTime.Now,
                        _conversation.Id, (this.Conference == null) ? "null" : this.Conference.ConferenceUri,
                        MessageModality.ConversationInfo, MessageDirection.Outgoing);

                   this.OnMessageReceived(m);

                    _conversation.BeginTerminate(EndTerminateConversation, _conversation);
                    //_waitForConversationTerminated.WaitOne();
                    _conversation = null;
                }
                else
                {
                    _waitForConversationTerminated.Set();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("TranscriptRecorderSession.Shutdown(). Exception occured during conversation shutdown: {0}",
                    e.ToString());
            }
            finally
            {
                // Raise Shutdown event to MeetingTranscriptSessionManager
                this.RaiseTranscriptRecorderSessionShutdown(shutdownArgs);
            }
        }

        #endregion // Public Methods

        private void RaiseTranscriptRecorderSessionShutdown(TranscriptRecorderSessionShutdownEventArgs args)
        {
            try
            {
                if (this.TranscriptRecorderSessionShutdown != null)
                {
                    this.TranscriptRecorderSessionShutdown.Invoke(this, args);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: Exception occured in RaiseTranscriptRecorderSessionShutdown(): {0}",
                    e.ToString());
            }
        }

        internal void RaiseTranscriptRecorderSessionChanged(ConferenceSession conference)
        {
            try
            {
                if (this.TranscriptRecorderSessionChanged != null)
                {
                    TranscriptRecorderSessionChangedEventArgs args = new TranscriptRecorderSessionChangedEventArgs(this);
                    this.TranscriptRecorderSessionChanged.Invoke(this, args);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: Exception occured in TranscriptRecorderSessionChanged(). Conference: {0}. {1}.",
                    conference.ConferenceUri,
                    e.ToString());
            }
        }

        internal void OnMessageReceived(Message m)
        {
            Console.WriteLine("Message logged: " + m.ToString());

            // TODO: Write message to Lync client app or output file

            _messages.Add(m);
        }

        internal void OnMediaTranscriptRecorderError(Message m)
        {
            Console.WriteLine("Error message loged: " + m.ToString());

            _messages.Add(m);

            // TODO: Logic to handle errors that can happen in indiviual recorders/modality calls
        }

        internal void OnMediaTranscriptRecorderTerminated(MediaTranscriptRecorder terminatedRecorder)
        {
            if (_transcriptRecorders.Contains(terminatedRecorder))
            {
                _transcriptRecorders.Remove(terminatedRecorder);
            }

            // TODO: Handle sub conversation transcript recorder shutdown

            if ((_transcriptRecorders.Count == 0) || (terminatedRecorder is ConversationTranscriptRecorder)
            || (terminatedRecorder is ConferenceTranscriptRecorder))
            {
                this.Shutdown();
            }
        }

        internal void OnSubConversationAdded(Conversation subConversation, MediaTranscriptRecorder addingTranscriptRecorder)
        {
            // Start conversation recorder on sub conversation
            ConversationTranscriptRecorder subConvRecorder = null;
            foreach (ConversationTranscriptRecorder c in _conversationToCallTranscriptMapping.Keys)
            {
                if (c.Conversation.Equals(subConversation))
                {
                    subConvRecorder = c;
                    break;
                }
            }

            if (subConvRecorder == null)
            {
                subConvRecorder = new ConversationTranscriptRecorder(this, subConversation, true);
                _transcriptRecorders.Add(subConvRecorder);
                _conversationToCallTranscriptMapping.Add(subConvRecorder, new List<MediaTranscriptRecorder>());
                _conversationToCallTranscriptMapping[subConvRecorder].Add(addingTranscriptRecorder);
            }
        }

        internal void OnSubConversationRemoved(Conversation subConversation, MediaTranscriptRecorder removingTranscriptRecorder)
        {
            // Terminate conversation recorder on sub conversation if no calls left under this sub conv
            ConversationTranscriptRecorder subConvRecorder = null;
            foreach (ConversationTranscriptRecorder c in _conversationToCallTranscriptMapping.Keys)
            {
                if (c.Conversation.Equals(subConversation))
                {
                    subConvRecorder = c;
                    break;
                }
            }

            if (subConvRecorder != null)
            {
                if (_conversationToCallTranscriptMapping[subConvRecorder].Contains(removingTranscriptRecorder))
                {
                    _conversationToCallTranscriptMapping[subConvRecorder].Remove(removingTranscriptRecorder);
                    if (_conversationToCallTranscriptMapping[subConvRecorder].Count == 0)
                    {
                        _conversationToCallTranscriptMapping.Remove(subConvRecorder);
                        _transcriptRecorders.Remove(subConvRecorder);
                    }
                }
            }
        }

        internal void OnConversationTerminated(Conversation conversation, ConversationTranscriptRecorder terminatedRecorder)
        {
            _transcriptRecorders.Remove(terminatedRecorder);

            if (!terminatedRecorder.IsSubConversation)
            {
                this.Shutdown();
            }
        }

        internal void OnEscalatedConferenceJoinRequested(Conversation conversation)
        {
            ConferenceTranscriptRecorder confRecorder = new ConferenceTranscriptRecorder(this, conversation);
            _transcriptRecorders.Add(confRecorder);
            _conversationToCallTranscriptMapping[_conversationTranscriptRecorder].Add(confRecorder);
            // TODO: log message for conf escalation events
            confRecorder.EscalateToConferenceRequested();

            // TODO: Raise event to TranscriptRecorerSessionManager that conversation has been escalated to conference
        }

        internal void OnActiveMediaTypeCallToEstablish(Conversation conversation, TranscriptRecorderType addedModality)
        {
            // TODO: Will be called from ConferenceTranscriptRecorder after joining an invited conf & added Calls for each supported modality
            // Note: Requires Established async methods to be added to AV and IM
            if (addedModality == TranscriptRecorderType.AudioVideo)
            {
                AVTranscriptRecorder avRecorder = new AVTranscriptRecorder(this);
                // TODO: Log call established and added to conv/conf
                _transcriptRecorders.Add(avRecorder);
                _conversationToCallTranscriptMapping[_conversationTranscriptRecorder].Add(avRecorder);
                avRecorder.EstablishAudioVideoCall(conversation);
            }
            else if (addedModality == TranscriptRecorderType.InstantMessage)
            {
                IMTranscriptRecorder imRecorder = new IMTranscriptRecorder(this);
                // TODO: Log call established and added to conv/conf
                _transcriptRecorders.Add(imRecorder);
                _conversationToCallTranscriptMapping[_conversationTranscriptRecorder].Add(imRecorder);
                imRecorder.EstablishInstantMessagingCall(conversation);
            }
        }

        #region Callbacks

        private void ConferenceInvitation_AcceptCompleted(IAsyncResult result)
        {
            try
            {
                ConferenceInvitation invite = result.AsyncState as ConferenceInvitation;
                invite.EndAccept(result);

                if (_conversation == null)
                {
                    _conversation = invite.Conversation;

                    _conversationTranscriptRecorder = new ConversationTranscriptRecorder(this, _conversation);
                    _transcriptRecorders.Add(_conversationTranscriptRecorder);
                    _conversationToCallTranscriptMapping.Add(_conversationTranscriptRecorder, new List<MediaTranscriptRecorder>());

                    // TODO: Handle case where we're already joined into a different meeting for this conv?

                    ConferenceTranscriptRecorder conferenceTranscriptRecorder = new ConferenceTranscriptRecorder(this, _conversation);
                    _transcriptRecorders.Add(conferenceTranscriptRecorder);
                    _conversationToCallTranscriptMapping[_conversationTranscriptRecorder].Add(conferenceTranscriptRecorder);

                    conferenceTranscriptRecorder.ConferenceInviteAccepted(result);

                    // Raise event to TranscriptRecorderSessionManager that Conference was joined
                    // TODO: 
                    // call top level event handler
                    
                }
                else
                {
                    Console.WriteLine("Warn: Already have a Conference/active conversation");
                    // Treat this as a sub conversation?
                    /*
                    subConvRecorder = new ConversationTranscriptRecorder(this, subConversation, true);
                    _transcriptRecorders.Add(subConvRecorder);
                    _conversationToCallTranscriptMapping.Add(subConvRecorder, new List<MediaTranscriptRecorder>());
                    _conversationToCallTranscriptMapping[subConvRecorder].Add(addingTranscriptRecorder);
                     */
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: Exception occurred during conference invite acceptance: " + e.ToString());
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