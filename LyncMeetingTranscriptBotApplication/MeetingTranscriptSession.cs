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
    public class MeetingTranscriptSession
    {
        private UcmaHelper _helper;
        private UserEndpoint _userEndpoint;
        private Dictionary<Conversation, TranscriptRecorder> _activeTranscriptRecorders;

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

        public MeetingTranscriptSession()
        {         
            _activeTranscriptRecorders = new Dictionary<Conversation, TranscriptRecorder>();
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
            try
            {
                SaveTranscripts();
                SendTranscripts();

                if (_cancelToken.Token.CanBeCanceled)
                {
                    _cancelToken.Cancel();
                }

                foreach (TranscriptRecorder t in _activeTranscriptRecorders.Values)
                {
                    t.Shutdown();
                }

                this.UnregisterEndpointEvents();
            }
            finally
            {
                // Clean up by shutting down the platform.
                _helper.ShutdownPlatform();
            }
        }

        #region Helper Methods

        private void SaveTranscripts()
        {
            // TODO
            string filename = "LyncMeetingTranscript_" + DateTime.Now.ToShortDateString();
            using (FileStream fs = new FileStream(filename, FileMode.OpenOrCreate))
            {
                using (BinaryWriter w = new BinaryWriter(fs))
                {
                    foreach (TranscriptRecorder t in _activeTranscriptRecorders.Values)
                    {
                        w.Write(t.GetFullTranscript());
                    }
                }
            }
        }

        private void SendTranscripts()
        {
            // TODO
        }

        private void RegisterEndpointEvents()
        {
            if (_userEndpoint != null)
            {
                _userEndpoint.RegisterForIncomingCall<AudioVideoCall>(AudioVideoCall_Received);
                _userEndpoint.RegisterForIncomingCall<InstantMessagingCall>(InstantMessagingCall_Received);
                _userEndpoint.ConferenceInvitationReceived += new EventHandler<ConferenceInvitationReceivedEventArgs>(UserEndpoint_ConferenceInvitationReceived);
            }
        }

        private void UnregisterEndpointEvents()
        {
            if (_userEndpoint != null)
            {
                _userEndpoint.UnregisterForIncomingCall<AudioVideoCall>(AudioVideoCall_Received);
                _userEndpoint.UnregisterForIncomingCall<InstantMessagingCall>(InstantMessagingCall_Received);
                _userEndpoint.ConferenceInvitationReceived -= UserEndpoint_ConferenceInvitationReceived;
            }
        }

        #endregion // Helper Methods

        #region Event Handlers

        // Delegate that is called when an incoming AudioVideoCall arrives.
        void AudioVideoCall_Received(object sender, CallReceivedEventArgs<AudioVideoCall> e)
        {
            if (_activeTranscriptRecorders.ContainsKey(e.Call.Conversation))
            {
                _activeTranscriptRecorders[e.Call.Conversation].AddAVIncomingCall(e);
            }
            else if (e.IsNewConversation)
            {
                Conversation c = e.Call.Conversation;
                TranscriptRecorder t = new TranscriptRecorder(e);
                _activeTranscriptRecorders.Add(c, t);
            }
            else if (e.IsConferenceDialOut)
            {
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
            if (_activeTranscriptRecorders.ContainsKey(e.Call.Conversation))
            {
                _activeTranscriptRecorders[e.Call.Conversation].AddIMIncomingCall(e);
            }
            else if (e.IsNewConversation)
            {
                Conversation c = e.Call.Conversation;
                TranscriptRecorder t = new TranscriptRecorder(e);
                _activeTranscriptRecorders.Add(c, t);
            }
            else if (e.IsConferenceDialOut)
            {
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
            if (_activeTranscriptRecorders.ContainsKey(conversation))
            {
                _activeTranscriptRecorders[conversation].AddIncomingInvitedConferece(e);
            }

            else
            {
                TranscriptRecorder t = new TranscriptRecorder(e);

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
                    _activeTranscriptRecorders.Add(conversation, t);
                //}
            }

            _waitForCallorInviteRecv.Set();
        }

        // TODO: Need to raise an event on MeetingTranscriptSession when TranscriptRecorder is shutdown (or conversation/conference ends)

        #endregion // Event Handlers
    }
}