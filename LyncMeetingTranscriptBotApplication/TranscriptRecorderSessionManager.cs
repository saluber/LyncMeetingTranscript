using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Rtc.Collaboration;
using Microsoft.Rtc.Collaboration.AudioVideo;

namespace LyncMeetingTranscriptBotApplication
{
    enum TranscriptSessionManagerState 
    { 
        Created = 1, 
        Idle = 2, 
        Active = 3, 
        Terminating = 4, 
        Terminated = 5 
    }
    
    public class TranscriptRecorderSessionManager
    {
        private static object s_lock = new object();

        private Guid _identity;
        private TranscriptSessionManagerState _state = TranscriptSessionManagerState.Created;
        private Dictionary<Conversation, TranscriptRecorderSession> _activeConversationSessions;
        private Dictionary<ConferenceSession, TranscriptRecorderSession> _activeConferenceSessions;

        private UcmaHelper _helper;
        private UserEndpoint _userEndpoint;

        // Wait handles are used to synchronize the main thread and the worker thread that is
        // used for callbacks and event handlers.
        private AutoResetEvent _waitForTranscriptSessionStarted = new AutoResetEvent(false);
        private AutoResetEvent _waitForTranscriptSessionTerminated = new AutoResetEvent(false);
        private AutoResetEvent _waitForUserEndpointTerminated = new AutoResetEvent(false);
        private CancellationToken _cancelToken;
        private Task _runTask;
        private Task _shutdownTask;

        #region Properties

        public Guid Identity
        {
            get { return _identity; }
        }

        public AutoResetEvent TranscriptSessionStartedWaitHandle
        {
            get { return _waitForTranscriptSessionStarted; }
        }

        public AutoResetEvent TranscriptSessionTerminatedWaitHandle
        {
            get { return _waitForTranscriptSessionTerminated; }
        }

        #endregion // Properties

        public TranscriptRecorderSessionManager()
        {
            _identity = Constants.NextGuid();
            _activeConversationSessions = new Dictionary<Conversation, TranscriptRecorderSession>();
            _activeConferenceSessions = new Dictionary<ConferenceSession, TranscriptRecorderSession>();
            _helper = new UcmaHelper();
            _userEndpoint = _helper.CreateEstablishedUserEndpoint(Constants.ApplicationEndpointName);
        }

        #region Public Methods

        public async Task RunAsync(CancellationToken token)
        {
            NonBlockingConsole.WriteLine("RunAsync - Entry");
            bool startTask = true;
            List<Task> runTasks = new List<Task>();
            lock (s_lock)
            {
                if (_state != TranscriptSessionManagerState.Created)
                {
                    NonBlockingConsole.WriteLine("RunAsync - Warn: TranscriptSessionManager is already running.");
                    startTask = false;
                    if (_runTask != null)
                    {
                        runTasks.Add(_runTask);
                    }
                }
                else
                {
                    _state = TranscriptSessionManagerState.Idle;
                    _cancelToken = token;
                    RegisterEndpointEvents();
                    _runTask = new Task(() =>
                        {
                            #if (CONVERSATION_DIALIN_ENABLED)
                            string conversationUri = _helper.GetRemoteUserURI();
                            if (string.IsNullOrEmpty(conversationUri))
                            {
                                NonBlockingConsole.WriteLine("Error: Valid remote user Uri must be provided for CONVERSATION_DIALIN_ENABLED mode.\n Exiting...\n");
                                return;
                            }
                            StartConversationTranscriptRecorderSession(conversationUri);
                            #endif // (CONVERSATION_DIALIN_ENABLED)

                            #if (CONFERENCE_DIALIN_ENABLED)
                                string conferenceUri = _helper.GetConferenceURI();
                                if (string.IsNullOrEmpty(conferenceUri))
                                {
                                    NonBlockingConsole.WriteLine("Error: Valid conference Uri must be provided for CONFERENCE_DIALIN_ENABLED mode.\n Exiting...\n");
                                    return;
                                }
                                StartConferenceTranscriptRecorderSession(conferenceUri);
                            #endif // CONFERENCE_DIALIN_ENABLED

                            _waitForTranscriptSessionStarted.WaitOne();
                            _waitForTranscriptSessionTerminated.WaitOne();
                        }, token);
                    runTasks.Add(_runTask);
                }
            } // lock

            if (startTask)
            {
                _runTask.Start();
            }

            await Task.WhenAll(runTasks.ToArray());

            NonBlockingConsole.WriteLine("RunAsync - Exit");
        }

        /// <summary>
        /// Call remote user and start TranscriptRecorderSession on Conversation
        /// </summary>
        /// <param name="remoteUserUri"></param>
        /// <param name="options"></param>
        public async Task StartConversationTranscriptRecorderSession(string remoteUserUri, CallEstablishOptions options = null)
        {
            throw new NotImplementedException("StartConversationTranscriptRecorderSession is not yet implemented");
        }

        /// <summary>
        /// Join Conference and start TranscriptRecorderSession on Conference
        /// </summary>
        /// <param name="conferenceUri"></param>
        /// <param name="options"></param>
        public async Task StartConferenceTranscriptRecorderSession(string conferenceUri, ConferenceJoinOptions options = null)
        {
            throw new NotImplementedException("StartConferenceTranscriptRecorderSession is not yet implemented");
        }

        public async Task ShutdownAsync(bool runSync = false)
        {
            NonBlockingConsole.WriteLine("ShutdownAsync - Entry");
            bool startTask = true;
            List<Task> preShutdownTasks = new List<Task>();
            List<Task> shutdownTasks = new List<Task>();
            lock (s_lock)
            {
                if (_state == TranscriptSessionManagerState.Terminating || _state == TranscriptSessionManagerState.Terminated)
                {
                    NonBlockingConsole.WriteLine("Warn: Already shutdown or shutting down.");
                    startTask = false;
                    if (_shutdownTask != null)
                    {
                        shutdownTasks.Add(_shutdownTask);
                    }
                }
                else
                {
                    _state = TranscriptSessionManagerState.Terminating;
                    this.UnregisterEndpointEvents();

                    List<TranscriptRecorderSession> sessionsToShutdown = new List<TranscriptRecorderSession>();
                    // Add all active conversation transcript sessions to shutdown list
                    foreach (TranscriptRecorderSession t in _activeConversationSessions.Values)
                    {
                        sessionsToShutdown.Add(t);
                    }
                    // Add all active conference transcript sessions to shutdown list
                    foreach (TranscriptRecorderSession t in _activeConferenceSessions.Values)
                    {
                        sessionsToShutdown.Add(t);
                    }
                    _shutdownTask = new Task(()=>
                        {
                            SaveTranscripts();
                            SendTranscripts();

                            _activeConversationSessions.Clear();
                            _activeConferenceSessions.Clear();
                            foreach (TranscriptRecorderSession t in sessionsToShutdown)
                            {
                                Task task = new Task(()=>
                                    {
                                        t.TranscriptRecorderSessionChanged -= this.TranscriptRecorder_OnTranscriptRecorderSessionChanged;
                                        t.TranscriptRecorderSessionShutdown -= this.TranscriptRecorder_OnTranscriptRecorderSessionShutdown;
                                        t.Shutdown();
                                    });
                                task.Wait();
                            }

                            // Terminate user endpoint
                            if (_userEndpoint != null)
                                {
                                    
                                    _userEndpoint.BeginTerminate(EndTerminateUserEndpoint, _userEndpoint);
                                    _waitForUserEndpointTerminated.WaitOne();
                                }

                            // Clean up by shutting down the platform.
                            if (_helper != null)
                            {
                                _helper.ShutdownPlatform();
                            }

                            _state = TranscriptSessionManagerState.Terminated;
                        });
                    shutdownTasks.Add(_shutdownTask);
                }
            } // lock

            if (startTask)
            {
                _shutdownTask.Start();
            }

            if (runSync)
            {
                Task.WhenAll(shutdownTasks.ToArray()).Wait();
            }
            else
            {
                await Task.WhenAll(shutdownTasks.ToArray());
            }

            NonBlockingConsole.WriteLine("ShutdownAsync - Exit");
        }

        #endregion // Public Methods

        #region Helper Methods

        private void SaveTranscript(TranscriptRecorderSession trs)
        {
            NonBlockingConsole.WriteLine("SaveTranscript - Entry");

            string filename = "LyncMeetingTranscript.txt";
            using (FileStream fs = new FileStream(filename, FileMode.OpenOrCreate))
            {
                using (BinaryWriter w = new BinaryWriter(fs))
                {
                    w.Write(trs.GetFullTranscript());
                }
            }

            NonBlockingConsole.WriteLine("SaveTranscript - Exit");
        }

        private void SaveTranscripts()
        {
            NonBlockingConsole.WriteLine("SaveTranscripts - Entry");

            // TODO: Save transcripts to network share folder or upload to DB

            // Save transcripts in local file
            string filename = "LyncMeetingTranscript.txt";
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

            NonBlockingConsole.WriteLine("SaveTranscripts - Exit");
        }

        private void SendTranscript(TranscriptRecorderSession trs)
        {
            // TODO
            NonBlockingConsole.WriteLine("SendTranscript - Entry");
            NonBlockingConsole.WriteLine("SendTranscript - Exit");
        }

        private void SendTranscripts()
        {
            // TODO
            NonBlockingConsole.WriteLine("SendTranscripts - Entry");
            NonBlockingConsole.WriteLine("SendTranscripts - Exit");
        }

        private void RegisterEndpointEvents()
        {
            NonBlockingConsole.WriteLine("RegisterEndpointEvents - Entry");
            if (_userEndpoint != null)
            {
                #if (CONVERSATION_DIALOUT_ENABLED || CONFERENCE_DIALOUT_ENABLED)
                _userEndpoint.RegisterForIncomingCall<AudioVideoCall>(AudioVideoCall_Received);
                _userEndpoint.RegisterForIncomingCall<InstantMessagingCall>(InstantMessagingCall_Received);
                #endif // (CONVERSATION_DIALOUT_ENABLED || CONFERENCE_DIALOUT_ENABLED)

                #if (CONFERENCE_DIALOUT_ENABLED)
                _userEndpoint.ConferenceInvitationReceived += new EventHandler<ConferenceInvitationReceivedEventArgs>(UserEndpoint_ConferenceInvitationReceived);
                #endif // (CONFERENCE_DIALOUT_ENABLED)
            }

            NonBlockingConsole.WriteLine("RegisterEndpointEvents - Exit");
        }

        private void UnregisterEndpointEvents()
        {
            NonBlockingConsole.WriteLine("UnregisterEndpointEvents - Entry");

            if (_userEndpoint != null)
            {
                #if (CONVERSATION_DIALOUT_ENABLED || CONFERENCE_DIALOUT_ENABLED)
                _userEndpoint.UnregisterForIncomingCall<AudioVideoCall>(AudioVideoCall_Received);
                _userEndpoint.UnregisterForIncomingCall<InstantMessagingCall>(InstantMessagingCall_Received);
                #endif // (CONVERSATION_DIALOUT_ENABLED || CONFERENCE_DIALOUT_ENABLED)

                #if (CONFERENCE_DIALOUT_ENABLED)
                _userEndpoint.ConferenceInvitationReceived -= UserEndpoint_ConferenceInvitationReceived;
                #endif // (CONFERENCE_DIALOUT_ENABLED)
            }

            NonBlockingConsole.WriteLine("UnregisterEndpointEvents - Exit");
        }

        private async Task StopTranscriptRecorderSessionAsync(Guid sessionId, bool shutdownSession = true)
        {
            NonBlockingConsole.WriteLine("StopTranscriptRecorderSession - Entry. SessionId: {0}.", sessionId.ToString());
            TranscriptRecorderSession sessionToStop = null;
            bool shutdownManager = false;
            lock (s_lock)
            {
                if (_state == TranscriptSessionManagerState.Active)
                {
                    foreach (TranscriptRecorderSession trs in _activeConversationSessions.Values)
                    {
                        if (trs.SessionId.Equals(sessionId))
                        {
                            sessionToStop = trs;
                            break;
                        }
                    }

                    if (sessionToStop != null)
                    {
                        _activeConversationSessions.Remove(sessionToStop.Conversation);

                        if ((sessionToStop.Conference != null) && _activeConferenceSessions.ContainsKey(sessionToStop.Conference))
                        {
                            _activeConferenceSessions.Remove(sessionToStop.Conference);
                        }
                    }
                    else
                    {
                        foreach (TranscriptRecorderSession trs in _activeConferenceSessions.Values)
                        {
                            if (trs.SessionId.Equals(sessionId))
                            {
                                sessionToStop = trs;
                                break;
                            }
                        }

                        if (sessionToStop != null)
                        {
                            _activeConferenceSessions.Remove(sessionToStop.Conference);
                        }
                    }

                    if ((_activeConferenceSessions.Count + _activeConversationSessions.Count) == 0)
                    {
                        shutdownManager = true;
                    }
                } // (_state == TranscriptSessionManagerState.Active)
            } // lock

            // Only need to shutdown TranscriptRecorderSession once (if found)
            if (sessionToStop != null)
            {
                Task task = new Task(() =>
                    {
                        SaveTranscript(sessionToStop);
                        SendTranscript(sessionToStop);

                        if (shutdownSession)
                        {
                            sessionToStop.Shutdown();
                        }
                    });

                List<Task> tasks = new List<Task>()
                {
                    task
                };
                task.Start();
                await Task.WhenAll(tasks);
                if (shutdownManager)
                {
                    await this.ShutdownAsync();
                }
            }
            else
            {
                NonBlockingConsole.WriteLine("StopTranscriptRecorderSession: TranscriptRecorderSession {0} doesn't exist or was already shutdown",
                    sessionId.ToString());
            }

            NonBlockingConsole.WriteLine("StopTranscriptRecorderSession - Exit. SessionId: {0}.", sessionId.ToString());
        }

        #endregion // Helper Methods

        #region Event Handlers

        /// <summary>
        /// Delegate that is called when an incoming AudioVideoCall arrives.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void AudioVideoCall_Received(object sender, CallReceivedEventArgs<AudioVideoCall> e)
        {
            Task.Factory.StartNew(() =>
                {
                    try
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
                            // TODO: Join Conference then accept AV call
                            throw new NotImplementedException("AudioVideoCall_Received with ConferenceDialOut AudioVideoCall is not yet supported.");
                        }
                    }
                    catch (Exception ex)
                    {
                        NonBlockingConsole.WriteLine("Error: Exception thrown in AudioVideoCall_Received: " + ex.ToString());
                    }
                    finally
                    {
                        _waitForTranscriptSessionStarted.Set();
                    }
                });
        }

        /// <summary>
        /// Delegate that is called when an incoming InstantMessagingCall arrives.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void InstantMessagingCall_Received(object sender, CallReceivedEventArgs<InstantMessagingCall> e)
        {
            Task.Factory.StartNew(()=>
                {
                try
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
                        // TODO: Join Conference then accept IM call
                        throw new NotImplementedException("InstantMessagingCall_Received with ConferenceDialOut AudioVideoCall is not yet supported.");
                    }
                }
                catch (Exception ex)
                {
                    NonBlockingConsole.WriteLine("Error: Exception thrown in InstantMessagingCall_Received: " + ex.ToString());
                }
                finally
                {
                    _waitForTranscriptSessionStarted.Set();
                }
            });
        }

        /// <summary>
        /// Delegate that is called when an incoming ConferenceInvite is received.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void UserEndpoint_ConferenceInvitationReceived(object sender, ConferenceInvitationReceivedEventArgs e)
        {
            Task.Factory.StartNew(() =>
            {
                try
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
                    }
                }
                catch (Exception ex)
                {
                    NonBlockingConsole.WriteLine("Error: Exception thrown in UserEndpoint_ConferenceInvitationReceived: " + ex.ToString());
                }
                finally
                {
                    _waitForTranscriptSessionStarted.Set();
                }
            });
        }

        #region TranscriptRecorderSession Event Handlers

        void TranscriptRecorder_OnTranscriptRecorderSessionShutdown(object sender, TranscriptRecorderSessionShutdownEventArgs e)
        {
            NonBlockingConsole.WriteLine("TranscriptRecorder_OnTranscriptRecorderSessionShutdown event. SessionId: {0}. ConversationId: {1}. ConferenceId: {2}",
                e.SessionId.ToString(),
                (e.Conversation == null) ? "null" : e.Conversation.Id,
                (e.Conference == null) ? "null" : e.Conference.ConferenceUri);

            Task task = StopTranscriptRecorderSessionAsync(e.SessionId, false);
            if (task != null)
            {
                task.Wait();
            }
        }

        void TranscriptRecorder_OnTranscriptRecorderSessionChanged(object sender, TranscriptRecorderSessionChangedEventArgs e)
        {
            NonBlockingConsole.WriteLine("TranscriptRecorder_OnTranscriptRecorderSessionChanged event. SessionId: {0}. ConversationId: {1}. ConferenceId: {2}",
                e.SessionId.ToString(),
                (e.Conversation == null) ? "null" : e.Conversation.Id,
                (e.Conference == null) ? "null" : e.Conference.ConferenceUri);

            TranscriptRecorderSession session = null;
            if ((e.Conversation != null) && (e.Conference != null) 
                && _activeConversationSessions.TryGetValue(e.Conversation, out session))
            {
                // Add TranscriptRecorderSession to conference table (if no entry for this Conference already exists)
                lock (s_lock)
                {
                    if (!_activeConferenceSessions.ContainsKey(e.Conference))
                    {
                        NonBlockingConsole.WriteLine("TranscriptRecorder_OnTranscriptRecorderSessionChanged: Adding TranscriptRecorderSession for Conference entry: {0}.",
                            e.Conference.ConferenceUri);
                        _activeConferenceSessions.Add(e.Conference, session);

                        // If successfully added TranscriptRecorderSession to conference table, remove from conversation table
                        if (_activeConversationSessions.ContainsKey(e.Conversation))
                        {
                            NonBlockingConsole.WriteLine("TranscriptRecorder_OnTranscriptRecorderSessionChanged: Removing TranscriptRecorderSession for Conversation entry: {0}.",
                            e.Conversation.Id);

                            _activeConversationSessions.Remove(e.Conversation);
                        }
                    }
                } // lock
            }
            else
            {
                NonBlockingConsole.WriteLine("[Warn] TranscriptRecorder_OnTranscriptRecorderSessionChanged called on invalid Conversation or Conference. Ignoring event.");
            }
        }

        #endregion // TranscriptRecorderSession Event Handlers

        public void EndTerminateUserEndpoint(IAsyncResult result)
        {
            UserEndpoint endpoint = (UserEndpoint)result.AsyncState;
            try
            {
                endpoint.EndTerminate(result);
            }
            finally
            {
                _userEndpoint = null;
                this._waitForUserEndpointTerminated.Set();
            }
        }

        #endregion // Event Handlers
    }
}