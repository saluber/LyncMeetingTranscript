using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Rtc.Collaboration;
using Microsoft.Rtc.Collaboration.AudioVideo;
using Microsoft.Rtc.Signaling;
using LyncMeetingTranscriptBotApplication.UcmaCommon;
using LyncMeetingTranscriptBotApplication.TranscriptRecorders;

namespace LyncMeetingTranscript.BotApplication
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
        private AutoResetEvent _waitForCallAccepted = new AutoResetEvent(false);
        private AutoResetEvent _waitForB2BCallToEstablish = new AutoResetEvent(false);
        private AutoResetEvent _waitUntilOneUserHangsUp = new AutoResetEvent(false);
        private AutoResetEvent _waitForB2BCallToTerminate = new AutoResetEvent(false);

        public MeetingTranscriptSession()
        {
            _helper = new UcmaHelper();
            _userEndpoint = _helper.CreateEstablishedUserEndpoint("Lync Meeting Transcript App User");
            _activeTranscriptRecorders = new Dictionary<Conversation, TranscriptRecorder>();
        }

        public void Shutdown()
        {
            try
            {
                SaveTranscripts();
                SendTranscripts();

                foreach (TranscriptRecorder t in _activeTranscriptRecorders.Values)
                {
                    t.Shutdown();
                }

                // Clean up by shutting down the platform.
                _helper.ShutdownPlatform();
            }
            finally
            {

            }
        }

        #region Helper Methods

        private void SaveTranscripts()
        {
            // TODO
            string filename = "LyncMeetingTranscript_" + DateTime.Now.ToShortDateString();
            using (FileStream fs = new FileStream(filename, FileMode.Create))
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

            // _waitForCallAccepted.Set();

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

            //_waitForCallAccepted.Set();

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
            if (_activeTranscriptRecorders.ContainsKey(conversation))
            {
                // TODO: join conference with existing conversation
            }
            else
            {
                TranscriptRecorder t = new TranscriptRecorder(e);
                _activeTranscriptRecorders.Add(conversation, t);
            }

            //_waitForCallAccepted.Set();
        }

        #endregion // Event Handlers
    }
}