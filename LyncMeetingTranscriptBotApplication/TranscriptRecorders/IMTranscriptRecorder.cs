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
    class IMTranscriptRecorder : MediaTranscriptRecorder
    {
        private static TranscriptRecorderType _type = TranscriptRecorderType.InstantMessage;
        private TranscriptRecorderState _state = TranscriptRecorderState.Initialized;
        private TranscriptRecorder _transcriptRecorder;

        private EventHandler<CallStateChangedEventArgs> _imCallStateChangedEventHandler;
        private EventHandler<InstantMessagingFlowConfigurationRequestedEventArgs> _imFlowConfigurationRequestedEventHandler;
        private EventHandler<MediaFlowStateChangedEventArgs> _imFlowStateChangedEventHandler;
        private EventHandler<InstantMessageReceivedEventArgs> _imFlowMessageReceivedEventHandler;

        private AutoResetEvent _waitForIMCallAccepted = new AutoResetEvent(false);
        private AutoResetEvent _waitForIMCallEstablished = new AutoResetEvent(false);
        private AutoResetEvent _waitForIMCallTerminated = new AutoResetEvent(false);
        private AutoResetEvent _waitForIMFlowStateChangedToActiveCompleted = new AutoResetEvent(false);

        private InstantMessagingCall _instantMessagingCall;
        private InstantMessagingFlow _instantMessagingFlow;
        private Conversation _subConversation;

        #region Properties

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

        public InstantMessagingCall InstantMessagingCall
        {
            get { return _instantMessagingCall; }
        }

        public InstantMessagingFlow InstantMessagingFlow
        {
            get { return _instantMessagingFlow; }
        }

        public Conversation SubConversation
        {
            get { return _subConversation; }
        }

        #endregion // Properties

        public IMTranscriptRecorder(TranscriptRecorder transcriptRecorder,
            EventHandler<CallStateChangedEventArgs> imCallStateChangedEventHandler = null,
            EventHandler<InstantMessagingFlowConfigurationRequestedEventArgs> imFlowConfigurationRequestedEventHandler = null,
            EventHandler<MediaFlowStateChangedEventArgs> imFlowStateChangedEventHandler = null,
            EventHandler<InstantMessageReceivedEventArgs> imFlowMessageReceivedEventHandler = null)
        {
            if (transcriptRecorder == null)
            {
                throw new ArgumentNullException("transcriptRecorder");
            }

            _transcriptRecorder = transcriptRecorder;

            _imCallStateChangedEventHandler = imCallStateChangedEventHandler;
            _imFlowConfigurationRequestedEventHandler = imFlowConfigurationRequestedEventHandler;
            _imFlowStateChangedEventHandler = imFlowStateChangedEventHandler;
            _imFlowMessageReceivedEventHandler = imFlowMessageReceivedEventHandler;
        }

        public void TerminateCall()
        {
            if (_instantMessagingFlow != null)
            {
                _instantMessagingFlow.StateChanged -= this.InstantMessagingFlow_StateChanged;
                _instantMessagingFlow.MessageReceived -= this.InstantMessagingFlow_MessageReceived;
                _instantMessagingFlow = null;
            }

            if (_instantMessagingCall != null)
            {
                _instantMessagingCall.BeginTerminate(InstantMessagingCallTerminated, _instantMessagingCall);
                _instantMessagingCall.StateChanged -= this.InstantMessagingCall_StateChanged;
                _instantMessagingCall.InstantMessagingFlowConfigurationRequested -= this.InstantMessagingCall_FlowConfigurationRequested;
                _instantMessagingCall = null;
            }
            else
            {
                _waitForIMCallTerminated.Set();
            }

            if (_subConversation != null)
            {
                _transcriptRecorder.OnSubConversationRemoved(_subConversation, this);
                _transcriptRecorder = null;
            }

            _waitForIMCallAccepted.Reset();
            _waitForIMFlowStateChangedToActiveCompleted.Reset();
        }

        public override void Shutdown()
        {
            if (_state == TranscriptRecorderState.Terminated)
            {
                return;
            }
            _state = TranscriptRecorderState.Terminated;

            TerminateCall();

            // TODO: Shutdown message

            _transcriptRecorder.OnMediaTranscriptRecorderTerminated(this);
            _transcriptRecorder = null;
        }

        #region Event Handlers

        public void InstantMessagingCall_Received(CallReceivedEventArgs<InstantMessagingCall> e)
        {
            if (_state == TranscriptRecorderState.Terminated)
            {
                // TODO: error message
                return;
            }

            if (_instantMessagingCall != null)
            {
                Console.WriteLine("Warn: IMCall already exists for this Conversation. Shutting down previous call...");
                TerminateCall();
            }

            _state = TranscriptRecorderState.Initialized;
            _waitForIMCallTerminated.Reset();

            // Type checking was done by the platform; no risk of this being any 
            // type other than the type expected.
            _instantMessagingCall = e.Call;

            // Call: StateChanged: Only hooked up for logging, to show the call
            // state transitions.
            _instantMessagingCall.StateChanged +=
                new EventHandler<CallStateChangedEventArgs>(InstantMessagingCall_StateChanged);

            _instantMessagingCall.InstantMessagingFlowConfigurationRequested +=
                new EventHandler<InstantMessagingFlowConfigurationRequestedEventArgs>(InstantMessagingCall_FlowConfigurationRequested);

            // Remote Participant URI represents the far end (caller) in this 
            // conversation. Toast is the message set by the caller as the 
            // 'greet' message for this call. In Microsoft Lync, the 
            // toast will show up in the lower-right of the screen.
            Console.WriteLine("IMCall Received! From: " + e.RemoteParticipant.Uri + " Toast is: " +
                                                e.ToastMessage.Message);
            // Console.Writelin("IMCall Received!");

            // Now, accept the call. EndAcceptCall will be raised on the 
            // same thread.
            _instantMessagingCall.BeginAccept(InstantMessagingCallAcceptedCallBack, _instantMessagingCall);
        }

        public void EstablishInstantMessagingCall(Conversation conversation)
        {
            if (_state == TranscriptRecorderState.Terminated)
            {
                // TODO: error message
                return;
            }

            if (_instantMessagingCall != null)
            {
                Console.WriteLine("Warn: IMCall already exists for this Conversation. Shutting down previous call...");
                TerminateCall();
            }

            _state = TranscriptRecorderState.Initialized;
            this._waitForIMCallTerminated.Reset();

            try
            {
                InstantMessagingCall imCall = new InstantMessagingCall(conversation);

                // Register for Call events
                _instantMessagingCall = imCall;

                // Call: StateChanged: Only hooked up for logging, to show the call
                // state transitions.
                _instantMessagingCall.StateChanged +=
                    new EventHandler<CallStateChangedEventArgs>(InstantMessagingCall_StateChanged);

                _instantMessagingCall.InstantMessagingFlowConfigurationRequested +=
                    new EventHandler<InstantMessagingFlowConfigurationRequestedEventArgs>(InstantMessagingCall_FlowConfigurationRequested);

                // Establish AudioVideoCall
                imCall.BeginEstablish(IMCall_EstablishCompleted, imCall);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine("imCall.BeginEstablish failed. Exception: {0}", ex.ToString());
            }
        }

        void IMCall_EstablishCompleted(IAsyncResult result)
        {
            try
            {
                InstantMessagingCall imCall = result.AsyncState as InstantMessagingCall;
                imCall.EndEstablish(result);
            }
            catch (RealTimeException ex)
            {
                Console.WriteLine("imCall.EndEstablish failed. Exception: {0}", ex.ToString());
            }
            finally
            {
                this._waitForIMCallEstablished.Set();
                _state = TranscriptRecorderState.Active;
            }
        }

        void InstantMessagingCall_StateChanged(object sender, CallStateChangedEventArgs e)
        {
            Call call = sender as Call;

            //Call participants allow for disambiguation.
            Console.WriteLine("The InstantMessaging call with Local Participant: " + call.Conversation.LocalParticipant +
                " and Remote Participant: " + call.RemoteEndpoint.Participant +
                " has changed state. The previous call state was: " + e.PreviousState +
                " and the current state is: " + e.State);

            if (e.State == CallState.Terminating || e.State == CallState.Terminated)
            {
                _waitForIMCallTerminated.Set();
                this.Shutdown();
            }

            // call top level event handler
            if (_imCallStateChangedEventHandler != null)
            {
                _imCallStateChangedEventHandler(sender, e);
            }
        }

        // Flow created indicates that there is a flow present to begin media 
        // operations with, and that it is no longer null.
        public void InstantMessagingCall_FlowConfigurationRequested(object sender,
            InstantMessagingFlowConfigurationRequestedEventArgs e)
        {
            Console.WriteLine("IM Flow Configuration Requested.");
            _instantMessagingFlow = e.Flow;

            // Now that the flow is non-null, bind the event handlers for State 
            // Changed and Message Received. When the flow goes active, 
            // (as indicated by the state changed event) the program will send 
            // the IM in the event handler.
            _instantMessagingFlow.StateChanged += new EventHandler<MediaFlowStateChangedEventArgs>(this.InstantMessagingFlow_StateChanged);

            // Message Received is the event used to indicate that a message has
            // been received from the far end.
            _instantMessagingFlow.MessageReceived += new EventHandler<InstantMessageReceivedEventArgs>(this.InstantMessagingFlow_MessageReceived);

            // call top level event handler
            if (_imFlowConfigurationRequestedEventHandler != null)
            {
                _imFlowConfigurationRequestedEventHandler(sender, e);
            }
        }

        private void InstantMessagingFlow_StateChanged(object sender, MediaFlowStateChangedEventArgs e)
        {
            Console.WriteLine("IM flow state changed from " + e.PreviousState + " to " + e.State);

            // When flow is active, media operations (here, sending an IM) 
            // may begin.
            if (e.State == MediaFlowState.Active)
            {
                // When flow is active, media operations can begin
                _waitForIMFlowStateChangedToActiveCompleted.Set();
            }

            // call top level event handler
            if (_imFlowStateChangedEventHandler != null)
            {
                _imFlowStateChangedEventHandler(sender, e);
            }
        }

        private void InstantMessagingFlow_MessageReceived(object sender, InstantMessageReceivedEventArgs e)
        {
            // On an incoming Instant Message, print the contents to the console.
            Console.WriteLine(e.Sender.Uri + " said: " + e.TextBody);

            Message m = new Message(e.TextBody, e.Sender.DisplayName, e.Sender.UserAtHost, e.Sender.Uri, DateTime.Now,
                _instantMessagingCall.Conversation.Id, _instantMessagingCall.Conversation.ConferenceSession.ConferenceUri,
                MessageModality.InstantMessage, MessageDirection.Outgoing);

            this._transcriptRecorder.OnMessageReceived(m);

            // call top level event handler
            if (_imFlowMessageReceivedEventHandler != null)
            {
                _imFlowMessageReceivedEventHandler(sender, e);
            }
        }

        #endregion // Event Handlers

        #region Callbacks

        private void InstantMessagingCallAcceptedCallBack(IAsyncResult ar)
        {
            InstantMessagingCall instantMessagingCall = ar.AsyncState as InstantMessagingCall;
            try
            {
                // Determine whether the call was accepted successfully.
                instantMessagingCall.EndAccept(ar);
            }
            catch (RealTimeException exception)
            {
                // RealTimeException may be thrown on media or link-layer failures. 
                // A production application should catch additional exceptions, such as OperationTimeoutException,
                // OperationTimeoutException, and CallOperationTimeoutException.

                Console.WriteLine(exception.ToString());
            }
            finally
            {
                // Synchronize with main thread.
                _waitForIMCallAccepted.Set();
                _state = TranscriptRecorderState.Active;
            }
        }

        private void InstantMessagingCallTerminated(IAsyncResult ar)
        {
            InstantMessagingCall instantMessagingCall = ar.AsyncState as InstantMessagingCall;

            try
            {
                // End terminating the incoming call.
                instantMessagingCall.EndTerminate(ar);

                // Remove this event handler now that the call has been terminated.
                _instantMessagingCall.StateChanged -= InstantMessagingCall_StateChanged;
                _instantMessagingCall.InstantMessagingFlowConfigurationRequested -= this.InstantMessagingCall_FlowConfigurationRequested;

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            finally
            {
                //Again, just to sync the completion of the code.
                _waitForIMCallTerminated.Set();
            }
        }

        #endregion // Callbacks

        #region Private Methods

        #endregion // Private Methods
    }
}
