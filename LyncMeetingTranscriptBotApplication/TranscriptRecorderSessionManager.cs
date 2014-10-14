using System;
using System.Configuration;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Rtc.Collaboration;
using Microsoft.Rtc.Collaboration.AudioVideo;
using Microsoft.Rtc.Signaling;
using LyncMeetingTranscriptBotApplication.TranscriptRecorders;

namespace LyncMeetingTranscriptBotApplication
{
    public class TranscriptRecorderSessionManager
    {
        private Guid _sessionManagerId;

        private UcmaHelper _helper;
        private UserEndpoint _userEndpoint;
        private Dictionary<Conversation, TranscriptRecorderSession> _activeConversationSessions;
        private Dictionary<ConferenceSession, TranscriptRecorderSession> _activeConferenceSessions;

        // Calls
        private AudioVideoCall _incomingAVCall;
        private AudioVideoCall _outgoingAVCall;
        private InstantMessagingCall _imCall;

        // Flows
        private AudioVideoFlow _audioVideoFlow;
        private InstantMessagingFlow _instantMessagingFlow;

        // The information for the conversation and the far end participant.
        // The target of the call (agent) in the format sip:user@host (should be logged on when the application is run). This could also be in the format tel:+1XXXYYYZZZZ
        private static String _calledParty;
        private static String _conversationPriority = ConversationPriority.Normal;

        // The conversations.
        // The conversation between the customer and the UCMA application.
        private Conversation _incomingConversation;
        // The conversation between the UCMA application and the agent.
        private Conversation _outgoingConversation;

        // BackToBackCall and associated fields
        private BackToBackCall _b2bCall;
        // _outgoingCallLeg contains the settings for the leg from the UCMA application to the agent.
        private BackToBackCallSettings _outgoingCallLeg;
        // _incomingCallLeg contains the settings for the leg from the customer to the UCMA application.
        private BackToBackCallSettings _incomingCallLeg;

        // Wait handles are used to synchronize the main thread and the worker thread that is
        // used for callbacks and event handlers.
        private AutoResetEvent _waitForConversationToTerminate = new AutoResetEvent(false);
        private AutoResetEvent _waitForCallToEstablish = new AutoResetEvent(false);
        private AutoResetEvent _waitForConferenceJoin = new AutoResetEvent(false);
        private AutoResetEvent _waitForCallorInviteRecv = new AutoResetEvent(false);
        private AutoResetEvent _waitForB2BCallToEstablish = new AutoResetEvent(false);
        private AutoResetEvent _waitUntilOneUserHangsUp = new AutoResetEvent(false);
        private AutoResetEvent _waitForB2BCallToTerminate = new AutoResetEvent(false);

        private CancellationTokenSource _cancelToken;

        public Guid Identity
        {
            get { return _sessionManagerId; }
        }

        public TranscriptRecorderSessionManager()
        {
            _sessionManagerId = new Guid();
            _activeConversationSessions = new Dictionary<Conversation, TranscriptRecorderSession>();
            _activeConferenceSessions = new Dictionary<ConferenceSession, TranscriptRecorderSession>();
            _cancelToken = new CancellationTokenSource();
        }

        public void Run()
        {
            _helper = new UcmaHelper();
            _userEndpoint = _helper.CreateEstablishedUserEndpoint("MeetingTranscriptBot");
            RegisterEndpointEvents();

            _waitForCallorInviteRecv.WaitOne();
            _waitForConversationToTerminate.WaitOne();
        }

        public async Task RunAsync()
        {
            _helper = new UcmaHelper();   
            _userEndpoint = _helper.CreateEstablishedUserEndpoint("MeetingTranscriptBot");
            RegisterEndpointEvents();

            List<Task> tasks = new List<Task>()
            {
                Task.Factory.StartNew(() => WaitForCallOrInviteReceieved(), TaskCreationOptions.LongRunning),
                Task.Factory.StartNew(() => WaitForConversationTerminated(), TaskCreationOptions.LongRunning)
            };

            await Task.WhenAll(tasks.ToArray());

            // _waitForCallorInviteRecv.WaitOne();
            // _waitForConversationToTerminate.WaitOne();
        }

        /// <summary>
        /// Join Conference and start TranscriptRecorderSession on Conference
        /// </summary>
        /// <param name="conferenceUri"></param>
        /// <param name="options"></param>
        public void StartConferenceTranscriptRecorderSession(string conferenceUri, ConferenceJoinOptions options)
        {
            Console.WriteLine("StartConferenceTranscriptRecorderSession - Entry. ConferenceUri: {0}", conferenceUri);
            Console.WriteLine("StartConferenceTranscriptRecorderSession - Exit. ConferenceUri: {0}", conferenceUri);
            throw new NotImplementedException();
        }

        /// <summary>
        /// Call remote user and start TranscriptRecorderSession on Conversation
        /// </summary>
        /// <param name="remoteUserUri"></param>
        /// <param name="options"></param>
        public void StartConversationTranscriptRecorderSession(string remoteUserUri, CallEstablishOptions options)
        {
            Console.WriteLine("StartConversationTranscriptRecorderSession - Entry. RemoteUserUri: {0}", remoteUserUri);
            Console.WriteLine("StartConversationTranscriptRecorderSession - Exit. RemoteUserUri: {0}", remoteUserUri);
            throw new NotImplementedException();
        }

        public async Task WaitForCallOrInviteReceieved()
        {
             await Task.Factory.StartNew(() =>
                {
                    _waitForCallorInviteRecv.WaitOne();
                }, _cancelToken.Token);
        }

        public async Task WaitForConversationTerminated()
        {
            await Task.Factory.StartNew(() =>
            {
                _waitForConversationToTerminate.WaitOne();
            }, _cancelToken.Token);
        }

        public void Shutdown()
        {
            Console.WriteLine("Shutdown - Entry");
            try
            {
                SaveTranscripts();
                SendTranscripts();

                if (_cancelToken.Token.CanBeCanceled)
                {
                    _cancelToken.Cancel();
                }

                lock (_activeConversationSessions)
                {
                    foreach (TranscriptRecorderSession t in _activeConversationSessions.Values)
                    {
                        t.TranscriptRecorderSessionChanged -= this.TranscriptRecorder_OnTranscriptRecorderSessionChanged;
                        t.TranscriptRecorderSessionShutdown -= this.TranscriptRecorder_OnTranscriptRecorderSessionShutdown;
                        t.Shutdown();
                    }

                    _activeConversationSessions.Clear();
                }

                lock (_activeConferenceSessions)
                {
                    foreach (TranscriptRecorderSession t in _activeConferenceSessions.Values)
                    {
                        t.TranscriptRecorderSessionChanged -= this.TranscriptRecorder_OnTranscriptRecorderSessionChanged;
                        t.TranscriptRecorderSessionShutdown -= this.TranscriptRecorder_OnTranscriptRecorderSessionShutdown;
                        t.Shutdown();
                    }

                    _activeConferenceSessions.Clear();
                }

                if (_userEndpoint != null)
                {
                    this.UnregisterEndpointEvents();
                    _userEndpoint.BeginTerminate(_userEndpoint.EndTerminate, _userEndpoint);
                    _userEndpoint = null;
                }
            }
            finally
            {
                // Clean up by shutting down the platform.
                if (_helper != null)
                {
                    _helper.ShutdownPlatform();
                }

                Console.WriteLine("Shutdown - Exit");
            }
        }

        #region Helper Methods

        private void SaveTranscript(TranscriptRecorderSession trs)
        {
            Console.WriteLine("SaveTranscript - Entry");

            string filename = "LyncMeetingTranscript_" + DateTime.Now.ToShortDateString();
            using (FileStream fs = new FileStream(filename, FileMode.OpenOrCreate))
            {
                using (BinaryWriter w = new BinaryWriter(fs))
                {
                    w.Write(trs.GetFullTranscript());
                }
            }

            Console.WriteLine("SaveTranscript - Exit");
        }

        // TODO: Save transcripts to share folder or upload to DB
        private void SaveTranscripts()
        {
            Console.WriteLine("SaveTranscripts - Entry");
            
            string filename = "LyncMeetingTranscript_" + DateTime.Now.ToShortDateString();
            using (FileStream fs = new FileStream(filename, FileMode.OpenOrCreate))
            {
                using (BinaryWriter w = new BinaryWriter(fs))
                {
                    foreach (TranscriptRecorderSession t in _activeConversationSessions.Values)
                    {
                        w.Write(t.GetFullTranscript());
                    }
                    foreach (TranscriptRecorderSession t in _activeConferenceSessions.Values)
                    {
                        w.Write(t.GetFullTranscript());
                    }
                }
            }

            Console.WriteLine("SaveTranscripts - Exit");
        }

        private void SendTranscript(TranscriptRecorderSession trs)
        {
            // TODO
            Console.WriteLine("SendTranscript - Entry");
            Console.WriteLine("SendTranscript - Exit");
        }

        private void SendTranscripts()
        {
            // TODO
            Console.WriteLine("SendTranscripts - Entry");
            Console.WriteLine("SendTranscripts - Exit");
        }

        private void RegisterEndpointEvents()
        {
            Console.WriteLine("RegisterEndpointEvents - Entry");
            if (_userEndpoint != null)
            {
                _userEndpoint.RegisterForIncomingCall<AudioVideoCall>(AudioVideoCall_Received);
                _userEndpoint.RegisterForIncomingCall<InstantMessagingCall>(InstantMessagingCall_Received);
                _userEndpoint.ConferenceInvitationReceived += new EventHandler<ConferenceInvitationReceivedEventArgs>(UserEndpoint_ConferenceInvitationReceived);
            }

            Console.WriteLine("RegisterEndpointEvents - Exit");
        }

        private void UnregisterEndpointEvents()
        {
            Console.WriteLine("UnregisterEndpointEvents - Entry");

            if (_userEndpoint != null)
            {
                _userEndpoint.UnregisterForIncomingCall<AudioVideoCall>(AudioVideoCall_Received);
                _userEndpoint.UnregisterForIncomingCall<InstantMessagingCall>(InstantMessagingCall_Received);
                _userEndpoint.ConferenceInvitationReceived -= UserEndpoint_ConferenceInvitationReceived;
            }

            Console.WriteLine("UnregisterEndpointEvents - Exit");
        }

        private void StopTranscriptRecorderSession(Guid sessionId)
        {
            Console.WriteLine("StopTranscriptRecorderSession - Entry. SessionId: {0}.", sessionId.ToString());
            TranscriptRecorderSession convSessionToStop = null;
            lock (_activeConversationSessions)
            {
                foreach (TranscriptRecorderSession trs in _activeConversationSessions.Values)
                {
                    if (trs.SessionId.Equals(sessionId))
                    {
                        convSessionToStop = trs;
                        break;
                    }
                }

                if (convSessionToStop != null)
                {
                    _activeConversationSessions.Remove(convSessionToStop.Conversation);
                }
            } // lock

            TranscriptRecorderSession confSessionToStop = null;
            lock (_activeConferenceSessions)
            {
                foreach (TranscriptRecorderSession trs in _activeConferenceSessions.Values)
                {
                    if (trs.SessionId.Equals(sessionId))
                    {
                        confSessionToStop = trs;
                        break;
                    }
                }

                if (confSessionToStop != null)
                {
                    _activeConferenceSessions.Remove(confSessionToStop.Conference);
                }
            } // lock

            // Only need to shutdown TranscriptRecorderSession once (if found)
            if (convSessionToStop != null)
            {
                SaveTranscript(convSessionToStop);
                convSessionToStop.Shutdown();
            }
            else if (confSessionToStop != null)
            {
                SaveTranscript(confSessionToStop);
                confSessionToStop.Shutdown();
            }
            else
            {
                Console.WriteLine("StopTranscriptRecorderSession: TranscriptRecorderSession {0} doesn't exist or was already shutdown", sessionId.ToString());
            }

            Console.WriteLine("StopTranscriptRecorderSession - Exit. SessionId: {0}.", sessionId.ToString());
        }

        #endregion // Helper Methods

        #region Event Handlers

        // Delegate that is called when an incoming AudioVideoCall arrives.
        void AudioVideoCall_Received(object sender, CallReceivedEventArgs<AudioVideoCall> e)
        {
            if (_activeConversationSessions.ContainsKey(e.Call.Conversation))
            {
                _activeConversationSessions[e.Call.Conversation].AddAVIncomingCall(e);
            }
            else if (e.IsNewConversation)
            {
                Conversation c = e.Call.Conversation;
                TranscriptRecorderSession t = new TranscriptRecorderSession(e);
                t.TranscriptRecorderSessionChanged += this.TranscriptRecorder_OnTranscriptRecorderSessionChanged;
                t.TranscriptRecorderSessionShutdown += this.TranscriptRecorder_OnTranscriptRecorderSessionShutdown;
                _activeConversationSessions.Add(c, t);
            }
            else if (e.IsConferenceDialOut)
            {
                // TODO: Join Conference then accept call
                /*
                 * McuDialOutOptions mcuDialOutOptions = new McuDialOutOptions();
                    mcuDialOutOptions.ParticipantUri = "sip:alice@contoso.com";
                    mcuDialOutOptions.ParticipantDisplayName = "Alice";
                    mcuDialOutOptions.PreferredLanguage = CultureInfo.GetCultureInfo("en-us");

                    conversation.ConferenceSession.AudioVideoMcuSession.BeginDialOut("tel:+14255551234", mcuDialOutOptions, dialOutCallback, state);

                 */
            }

            _waitForCallorInviteRecv.Set();

            /*
            //_waitForCallToBeReceived.Set();
            _audioVideoCall = e.Call;
            SetUserConversation(_audioVideoCall.Conversation);
            _audioVideoCall.AudioVideoFlowConfigurationRequested +=
                new EventHandler<AudioVideoFlowConfigurationRequestedEventArgs>(this.AudioVideoCall_FlowConfigurationRequested);

            // For logging purposes, register for notification of the StateChanged event on the call.
            _audioVideoCall.StateChanged +=
                      new EventHandler<CallStateChangedEventArgs>(AudioVideoCall_StateChanged);

            // Remote Participant URI represents the far end (caller) in this conversation. 
            Console.WriteLine("Call received from: " + e.RemoteParticipant.Uri);

            // Now, accept the call. CallAcceptCB will run on the same thread.
            _audioVideoCall.BeginAccept(AudioVideoCallAcceptedCallBack, _audioVideoCall);
             * */
        }

        // Delegate that is called when an incoming InstantMessagingCall arrives.
        void InstantMessagingCall_Received(object sender, CallReceivedEventArgs<InstantMessagingCall> e)
        {
            if (_activeConversationSessions.ContainsKey(e.Call.Conversation))
            {
                _activeConversationSessions[e.Call.Conversation].AddIMIncomingCall(e);
            }
            else if (e.IsNewConversation)
            {
                Conversation c = e.Call.Conversation;
                TranscriptRecorderSession t = new TranscriptRecorderSession(e);
                t.TranscriptRecorderSessionChanged += this.TranscriptRecorder_OnTranscriptRecorderSessionChanged;
                t.TranscriptRecorderSessionShutdown += this.TranscriptRecorder_OnTranscriptRecorderSessionShutdown;
                _activeConversationSessions.Add(c, t);
            }
            else if (e.IsConferenceDialOut)
            {
                // TODO: Join conference then accept call
                /*
                 * McuDialOutOptions mcuDialOutOptions = new McuDialOutOptions();
                mcuDialOutOptions.ParticipantUri = "sip:alice@contoso.com";
                mcuDialOutOptions.ParticipantDisplayName = "Alice";
                mcuDialOutOptions.PreferredLanguage = CultureInfo.GetCultureInfo("en-us");

                conversation.ConferenceSession.AudioVideoMcuSession.BeginDialOut("tel:+14255551234", mcuDialOutOptions, dialOutCallback, state); 
                 */
            }

            _waitForCallorInviteRecv.Set();

            /*
            //_waitForCallToBeReceived.Set();
            _instantMessagingCall = e.Call;

            SetUserConversation(_instantMessagingCall.Conversation);

            // Subscribe to InstantMessagingCall events
            _instantMessagingCall.InstantMessagingFlowConfigurationRequested +=
                new EventHandler<InstantMessagingFlowConfigurationRequestedEventArgs>(
                this.InstantMessagingCall_FlowConfigurationRequested);
            // For logging purposes, register for notification of the StateChanged event on the call.
            _instantMessagingCall.StateChanged +=
                      new EventHandler<CallStateChangedEventArgs>(InstantMessagingCall_StateChanged);

            // Remote Participant URI represents the far end (caller) in this conversation. 
            Console.WriteLine("Call received from: " + e.RemoteParticipant.Uri);

            // Now, accept the call. InstantMessagingCallAcceptedCallback will run on the same thread.
            _instantMessagingCall.BeginAccept(InstantMessagingCallAcceptedCallBack, _instantMessagingCall);
             */
        }

        void UserEndpoint_ConferenceInvitationReceived(object sender, ConferenceInvitationReceivedEventArgs e)
        {
            ConferenceInvitation invite = e.Invitation;
            Conversation conversation = invite.Conversation;
            // TODO: indexing by conv id doesn't work for "public meeting recording" scenario
            if (_activeConversationSessions.ContainsKey(conversation))
            {
                _activeConversationSessions[conversation].AddIncomingInvitedConference(e);
            }
            else
            {
                TranscriptRecorderSession t = new TranscriptRecorderSession(e);
                _activeConversationSessions.Add(conversation, t);
                /*
                if (e.IsConferenceDialOut)
                {
                    e.Invitation
                 *                 /*
                 * McuDialOutOptions mcuDialOutOptions = new McuDialOutOptions();
mcuDialOutOptions.ParticipantUri = "sip:alice@contoso.com";
mcuDialOutOptions.ParticipantDisplayName = "Alice";
mcuDialOutOptions.PreferredLanguage = CultureInfo.GetCultureInfo("en-us");

conversation.ConferenceSession.AudioVideoMcuSession.BeginDialOut("tel:+14255551234", mcuDialOutOptions, dialOutCallback, state);

                 */
                //}
                //else
                //{*/
                    //_activeConversationSessions.Add(conversation, t);
                //}
            }

            _waitForCallorInviteRecv.Set();
        }

        // TODO: Need to raise an event on MeetingTranscriptSession when TranscriptRecorder is shutdown (or conversation/conference ends)
        void TranscriptRecorder_OnTranscriptRecorderSessionShutdown(object sender, TranscriptRecorderSessionShutdownEventArgs e)
        {
            Console.WriteLine("TranscriptRecorder_OnTranscriptRecorderSessionShutdown event. SessionId: {0}. ConversationId: {1}. ConferenceId: {2}",
                e.SessionId.ToString(),
                (e.Conversation == null) ? "null" : e.Conversation.Id,
                (e.Conference == null) ? "null" : e.Conference.ConferenceUri);

            TranscriptRecorderSession shutdownSession = null;
            if (e.Conference != null)
            {
                lock (_activeConferenceSessions)
                {
                    if (_activeConferenceSessions.TryGetValue(e.Conference, out shutdownSession))
                    {
                        Console.WriteLine("TranscriptRecorder_OnTranscriptRecorderSessionShutdown: Removing TranscriptRecorderSession for Conference entry: {0}.",
                            e.Conference.ConferenceUri);

                        _activeConferenceSessions.Remove(e.Conference);
                    }
                } // lock
            }
            if (e.Conversation != null)
            {
                lock (_activeConversationSessions)
                {
                    if (_activeConversationSessions.ContainsKey(e.Conversation))
                    {
                        Console.WriteLine("TranscriptRecorder_OnTranscriptRecorderSessionShutdown: Removing TranscriptRecorderSession for Conversation entry: {0}.",
                            e.Conversation.Id);

                        if (shutdownSession == null)
                        {
                            shutdownSession = _activeConversationSessions[e.Conversation];
                        }

                        _activeConversationSessions.Remove(e.Conversation);
                    }
                } // lock
            }

            if (shutdownSession != null)
            {
                shutdownSession.TranscriptRecorderSessionChanged -= this.TranscriptRecorder_OnTranscriptRecorderSessionChanged;
                shutdownSession.TranscriptRecorderSessionShutdown -= this.TranscriptRecorder_OnTranscriptRecorderSessionShutdown;
                Console.WriteLine("TranscriptRecorder_OnTranscriptRecorderSessionShutdown: Saving Transcript of shutdown TranscriptRecorderSession. SessionId: {0}",
                    shutdownSession.SessionId);
                SaveTranscript(shutdownSession);
            }
            else
            {
                Console.WriteLine("TranscriptRecorder_OnTranscriptRecorderSessionShutdown: TranscriptRecorderSession doesn't exist or was already shutdown");
            }

            if (_activeConferenceSessions.Count == 0 && _activeConversationSessions.Count == 0)
            {
                this.Shutdown();
            }
        }

        void TranscriptRecorder_OnTranscriptRecorderSessionChanged(object sender, TranscriptRecorderSessionChangedEventArgs e)
        {
            Console.WriteLine("TranscriptRecorder_OnTranscriptRecorderSessionChanged event. SessionId: {0}. ConversationId: {1}. ConferenceId: {2}",
                e.SessionId.ToString(),
                (e.Conversation == null) ? "null" : e.Conversation.Id,
                (e.Conference == null) ? "null" : e.Conference.ConferenceUri);

            bool addedConferenceEntry = false;
            TranscriptRecorderSession session = null;
            if ((e.Conversation != null) && (e.Conference != null) 
                && _activeConversationSessions.TryGetValue(e.Conversation, out session))
            {
                // Add TranscriptRecorderSession to conference table (if no entry for this Conference already exists)
                lock (_activeConferenceSessions)
                {
                    if (!_activeConferenceSessions.ContainsKey(e.Conference))
                    {
                        Console.WriteLine("TranscriptRecorder_OnTranscriptRecorderSessionChanged: Adding TranscriptRecorderSession for Conference entry: {0}.",
                            e.Conference.ConferenceUri);

                        _activeConferenceSessions.Add(e.Conference, session);
                        addedConferenceEntry = true;
                    }
                } // lock

                // If successfully added TranscriptRecorderSession to conference table, remove from conversation table
                if (addedConferenceEntry)
                {
                    lock (_activeConversationSessions)
                    {
                        if (_activeConversationSessions.ContainsKey(e.Conversation))
                        {
                            Console.WriteLine("TranscriptRecorder_OnTranscriptRecorderSessionChanged: Removing TranscriptRecorderSession for Conversation entry: {0}.",
                            e.Conversation.Id);

                            _activeConversationSessions.Remove(e.Conversation);
                        }
                    } // lock
                }
            }
            else
            {
                Console.WriteLine("[Warn] TranscriptRecorder_OnTranscriptRecorderSessionChanged called on invalid Conversation or Conference. Ignoring event.");
            }
        }

        #endregion // Event Handlers
    }
}