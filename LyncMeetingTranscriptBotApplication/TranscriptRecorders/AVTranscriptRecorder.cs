using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Rtc.Collaboration;
using Microsoft.Rtc.Collaboration.AudioVideo;
using Microsoft.Rtc.Signaling;

namespace LyncMeetingTranscriptBotApplication.TranscriptRecorders
{
    class AVTranscriptRecorder : MediaTranscriptRecorder
    {
        private TranscriptRecorder _transcriptRecorder;
        private SpeechRecognizer _speechRecognizer;

        private AutoResetEvent _waitForAudioVideoCallAccepted = new AutoResetEvent(false);
        private AutoResetEvent _waitForAudioVideoCallTerminated = new AutoResetEvent(false);
        private AutoResetEvent _waitForAudioVideoFlowStateChangedToActiveCompleted = new AutoResetEvent(false);

        private EventHandler<CallStateChangedEventArgs> _audioVideoCallStateChangedEventHandler;
        private EventHandler<AudioVideoFlowConfigurationRequestedEventArgs> _audioVideoFlowConfigurationRequestedEventHandler;
        private EventHandler<MediaFlowStateChangedEventArgs> _audioVideoFlowStateChangedEventHandler;
        private EventHandler<ConversationChangedEventArgs> _audioVideoCallConversationChangedEventHandler;

        private AudioVideoCall _audioVideoCall;
        private AudioVideoFlow _audioVideoFlow;
        private Conversation _subConversation;

        #region Properties
        public TranscriptRecorder TranscriptRecorder
        {
            get { return _transcriptRecorder; }
        }

        public SpeechRecognizer SpeechRecognizer
        {
            get { return _speechRecognizer; }
        }

        public AudioVideoCall AudioVideoCall
        {
            get { return _audioVideoCall; }
        }

        public AudioVideoFlow AudioVideoFlow
        {
            get { return _audioVideoFlow; }
        }
        #endregion // Properties

        public AVTranscriptRecorder(TranscriptRecorder transcriptRecorder,
            EventHandler<ConversationChangedEventArgs> audioVideoCallConversationChangedEventHandler = null,
            EventHandler<CallStateChangedEventArgs> audioVideoCallStateChangedEventHandler = null,
            EventHandler<AudioVideoFlowConfigurationRequestedEventArgs> audioVideoFlowConfigurationRequestedEventHandler = null,
            EventHandler<MediaFlowStateChangedEventArgs> audioVideoFlowStateChangedEventHandler = null)
        {
            _transcriptRecorder = transcriptRecorder;
            _speechRecognizer = new SpeechRecognizer(transcriptRecorder);

            _audioVideoCallConversationChangedEventHandler = audioVideoCallConversationChangedEventHandler;
            _audioVideoCallStateChangedEventHandler = audioVideoCallStateChangedEventHandler;
            _audioVideoFlowConfigurationRequestedEventHandler = audioVideoFlowConfigurationRequestedEventHandler;
            _audioVideoFlowStateChangedEventHandler = audioVideoFlowStateChangedEventHandler;
        }

        #region Public Methods

        public void TerminateCall()
        {
            if (_speechRecognizer != null)
            {
                _speechRecognizer.StopSpeechRecognition();
            }

            if (_audioVideoFlow != null)
            {
                _audioVideoFlow.StateChanged -= AudioVideoFlow_StateChanged;
                _audioVideoFlow = null;
            }

            if (_audioVideoCall != null)
            {
                _audioVideoCall.BeginTerminate(AudioVideoCallTerminated, _audioVideoCall);
                _audioVideoCall = null;

                _audioVideoCall.StateChanged -= AudioVideoCall_StateChanged;
                _audioVideoCall.AudioVideoFlowConfigurationRequested -= AudioVideoCall_FlowConfigurationRequested;
                _audioVideoCall.ConversationChanged -= AudioVideoCall_ConversationChanged;
            }

            _waitForAudioVideoCallAccepted.Reset();
            _waitForAudioVideoCallTerminated.Reset();
            _waitForAudioVideoFlowStateChangedToActiveCompleted.Reset();
        }

        public override void Shutdown()
        {
            this.TerminateCall();

            if (_speechRecognizer != null)
            {
                _speechRecognizer.Shutdown();
                _speechRecognizer = null;
            }

            if (_subConversation != null)
            {
                _subConversation.BeginTerminate(ConversationTerminated, _subConversation);
                _subConversation = null;
            }

            _transcriptRecorder = null;
        }

        #endregion // Public Methods

        #region Event Handlers

        //call received event handler
        public void AudioVideoCall_Received(CallReceivedEventArgs<AudioVideoCall> e)
        {
            if (_audioVideoCall != null)
            {
                Console.WriteLine("Warn: AVCall already exists for this Conversation. Shutting down previous call...");
                TerminateCall();
            }

            //Type checking was done by the platform; no risk of this being any 
            // type other than the type expected.
            _audioVideoCall = e.Call;

            // Call: StateChanged: Only hooked up for logging, to show the call 
            // state transitions.
            _audioVideoCall.StateChanged += new EventHandler<CallStateChangedEventArgs>(AudioVideoCall_StateChanged);

            // Subscribe for the flow configuration requested event; the flow will be used to send the media.
            // Ultimately, as a part of the callback, the media will be sent/recieved.
            _audioVideoCall.AudioVideoFlowConfigurationRequested += new EventHandler<AudioVideoFlowConfigurationRequestedEventArgs>(AudioVideoCall_FlowConfigurationRequested);

            _audioVideoCall.ConversationChanged += new EventHandler<ConversationChangedEventArgs>(AudioVideoCall_ConversationChanged);

            // Remote Participant URI represents the far end (caller) in this 
            // conversation. Toast is the message set by the caller as the 'greet'
            // message for this call. In Microsoft Lync, the toast will 
            // show up in the lower-right of the screen.
            //Console.WriteLine("Call Received! From: " + e.RemoteParticipant.Uri + " Toast is: " + e.ToastMessage.Message);

            //change this to preserve confidentiality in the video demo
            Console.WriteLine("Call Received! From: " + e.RemoteParticipant.Uri);
            //Console.WriteLine("Call Received!");

            // Accept the call. Before transferring the call, it must be in the Established state.
            // Note that the docs are wrong in the state machine for the AVCall. BeginEstablish 
            // should be called on outgoing calls, not incoming calls.
            _audioVideoCall.BeginAccept(AudioVideoCallAccepted, _audioVideoCall);

            // Wait for a few seconds to give time for the call to get to the Established state.
            //_waitForAudioVideoCallAccepted.WaitOne(2000);
            Console.WriteLine("Inbound call state is {0}\n", _audioVideoCall.State);
        }

        void AudioVideoCall_StateChanged(object sender, CallStateChangedEventArgs e)
        {
            Call call = sender as Call;

            //Call participants allow for disambiguation.
            Console.WriteLine("The AudioVideo call with Local Participant: " + call.Conversation.LocalParticipant +
                " and Remote Participant: " + call.RemoteEndpoint.Participant +
                " has changed state. The previous call state was: " + e.PreviousState +
                " and the current state is: " + e.State);

            if ((e.State == CallState.Terminating)
                || (e.State == CallState.Terminated))
            {
                this.TerminateCall();
            }

            // call top level event handler
            if (_audioVideoCallStateChangedEventHandler != null)
            {
                _audioVideoCallStateChangedEventHandler(sender, e);
            }
        }

        //Flow configuration requested indicates that there is a flow present to begin media operations with that it is no longer null, and is ready to be configured.
        public void AudioVideoCall_FlowConfigurationRequested(object sender, AudioVideoFlowConfigurationRequestedEventArgs e)
        {
            Console.WriteLine("AV Flow Configuration Requested.");
            _audioVideoFlow = e.Flow;

            //Now that the flow is non-null, bind the event handler for State Changed.
            // When the flow goes active, (as indicated by the state changed event) the program will perform media related actions..
            _audioVideoFlow.StateChanged += new EventHandler<MediaFlowStateChangedEventArgs>(AudioVideoFlow_StateChanged);

            // call top level event handler
            if (_audioVideoFlowConfigurationRequestedEventHandler != null)
            {
                _audioVideoFlowConfigurationRequestedEventHandler(sender, e);
            }
        }

        private void AudioVideoCall_ConversationChanged(object sender, ConversationChangedEventArgs e)
        {
            Console.WriteLine("AVCall conversation changed. Reason: " + e.Reason.ToString());

            // TODO: Subscribe to events on subconversation. Delete conversation as part of shutdown
            // throw new NotImplementedException();
            _subConversation = e.NewConversation;

            // call top level event handler
            if (_audioVideoCallConversationChangedEventHandler != null)
            {
                _audioVideoCallConversationChangedEventHandler(sender, e);
            }
        }

        // Callback that handles when the state of an AudioVideoFlow changes
        private void AudioVideoFlow_StateChanged(object sender, MediaFlowStateChangedEventArgs e)
        {
            Console.WriteLine("AV flow state changed from " + e.PreviousState + " to " + e.State);

            //When flow is active, media operations can begin
            if (e.State == MediaFlowState.Active)
            {
                // Flow-related media operations normally begin here.
                _waitForAudioVideoFlowStateChangedToActiveCompleted.Set();

                if (!_speechRecognizer.IsActive)
                {
                    _speechRecognizer.AttachAndStartSpeechRecognition(_audioVideoFlow);
                }
            }
            else if (e.State == MediaFlowState.Terminated)
            {
                _speechRecognizer.StopSpeechRecognition();
            }

            // call top level event handler
            if (_audioVideoFlowStateChangedEventHandler != null)
            {
                _audioVideoFlowStateChangedEventHandler(sender, e);
            }
        }

        #endregion // Event Handlers

        #region Callbacks

        private void AudioVideoCallAccepted(IAsyncResult ar)
        {
            Call call = ar.AsyncState as Call;
            try
            {
                // Determine whether the Call was accepted successfully.
                call.EndAccept(ar);

                Console.WriteLine("Inbound AudioVideo call with Local Participant: " + call.Conversation.LocalParticipant + " and Remote Participant: " + call.RemoteEndpoint.Participant + " has been accepted.");
            }
            catch (RealTimeException exception)
            {
                // RealTimeException may be thrown on media or link-layer 
                // failures. 
                // TODO: Add actual error handling code here.
                Console.WriteLine(exception.ToString());
            }
            finally
            {
                //Again, just to sync the completion of the code.
                _waitForAudioVideoCallAccepted.Set();
            }
        }

        private void AudioVideoCallTerminated(IAsyncResult ar)
        {
            AudioVideoCall audioVideoCall = ar.AsyncState as AudioVideoCall;

            try
            {
                // End terminating the incoming call.
                audioVideoCall.EndTerminate(ar);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            finally
            {
                //Again, just to sync the completion of the code.
                _waitForAudioVideoCallTerminated.Set();
            }
        }

        private void ConversationTerminated(IAsyncResult ar)
        {
            Conversation conv = ar.AsyncState as Conversation;

            // End terminating the conversation.
            conv.EndTerminate(ar);
        }

        #endregion // Callbacks
    }
}
