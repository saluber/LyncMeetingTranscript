﻿using System;
using System.Threading;

using Microsoft.Rtc.Collaboration;
using Microsoft.Rtc.Signaling;

namespace LyncMeetingTranscriptBotApplication.TranscriptRecorders
{
    class IMTranscriptRecorder : MediaTranscriptRecorder
    {
        #region Fields
        private static TranscriptRecorderType _type = TranscriptRecorderType.InstantMessage;
        private TranscriptRecorderState _state = TranscriptRecorderState.Initialized;
        private TranscriptRecorderSession _transcriptRecorder;

        private EventHandler<CallStateChangedEventArgs> _imCallStateChangedEventHandler;
        private EventHandler<InstantMessagingFlowConfigurationRequestedEventArgs> _imFlowConfigurationRequestedEventHandler;
        private EventHandler<MediaFlowStateChangedEventArgs> _imFlowStateChangedEventHandler;
        private EventHandler<InstantMessageReceivedEventArgs> _imFlowMessageReceivedEventHandler;
        private EventHandler<ConversationChangedEventArgs> _imCallConversationChangedEventHandler;

        private AutoResetEvent _waitForIMCallAccepted = new AutoResetEvent(false);
        private AutoResetEvent _waitForIMCallEstablished = new AutoResetEvent(false);
        private AutoResetEvent _waitForIMCallTerminated = new AutoResetEvent(false);
        private AutoResetEvent _waitForIMFlowStateChangedToActiveCompleted = new AutoResetEvent(false);

        private InstantMessagingCall _instantMessagingCall;
        private InstantMessagingFlow _instantMessagingFlow;
        private Conversation _subConversation;
        #endregion // Fields

        #region Properties

        public override TranscriptRecorderType RecorderType
        {
            get { return _type; }
        }

        public override TranscriptRecorderState State
        {
            get { return _state; }
        }

        public TranscriptRecorderSession TranscriptRecorder
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

        public IMTranscriptRecorder(TranscriptRecorderSession transcriptRecorder,
            EventHandler<CallStateChangedEventArgs> imCallStateChangedEventHandler = null,
            EventHandler<InstantMessagingFlowConfigurationRequestedEventArgs> imFlowConfigurationRequestedEventHandler = null,
            EventHandler<MediaFlowStateChangedEventArgs> imFlowStateChangedEventHandler = null,
            EventHandler<InstantMessageReceivedEventArgs> imFlowMessageReceivedEventHandler = null,
            EventHandler<ConversationChangedEventArgs> imCallConversationChangedEventHandler = null)
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
            _imCallConversationChangedEventHandler = imCallConversationChangedEventHandler;
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
                _instantMessagingCall.ConversationChanged -= this.InstantMessagingCall_ConversationChanged;
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
            if (_subConversation != null)
            {
                _transcriptRecorder.OnSubConversationRemoved(_subConversation, this);
                _subConversation = null;
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

            _transcriptRecorder.OnMediaTranscriptRecorderTerminated(this);
            _transcriptRecorder = null;
        }

        #region Event Handlers

        public void InstantMessagingCall_Received(CallReceivedEventArgs<InstantMessagingCall> e)
        {
            if (_state == TranscriptRecorderState.Terminated)
            {
                NonBlockingConsole.WriteLine("Error: IMTranscriptRecorder is shutdown.");
                // TODO: Info message
                return;
            }

            if (_instantMessagingCall != null)
            {
                NonBlockingConsole.WriteLine("Warn: IMCall already exists for this Conversation. Shutting down previous call...");
                // TODO: Info message
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

            _instantMessagingCall.ConversationChanged += new EventHandler<ConversationChangedEventArgs>(InstantMessagingCall_ConversationChanged);

            // Remote Participant URI represents the far end (caller) in this 
            // conversation. Toast is the message set by the caller as the 
            // 'greet' message for this call. In Microsoft Lync, the 
            // toast will show up in the lower-right of the screen.
            // TODO: Change to protect privacy
            // NonBlockingConsole.WriteLine("IMCall Received! From: " + e.RemoteParticipant.Uri + " Toast is: " + e.ToastMessage.Message);
            NonBlockingConsole.WriteLine("IMCall Received! From: " + e.RemoteParticipant.Uri);
            // Console.Writelin("IMCall Received!");

            Message m = new Message("InstantMessagingCall Received. Inbound call state: " + _instantMessagingCall.State.ToString(),
                e.RemoteParticipant.DisplayName, e.RemoteParticipant.UserAtHost, e.RemoteParticipant.Uri,
                MessageType.InstantMessage, _transcriptRecorder.Conversation.Id, MessageDirection.Incoming);
            _transcriptRecorder.OnMessageReceived(m);

            // Now, accept the call. EndAcceptCall will be raised on the 
            // same thread.
            _instantMessagingCall.BeginAccept(InstantMessagingCallAcceptedCallBack, _instantMessagingCall);
        }

        public void EstablishInstantMessagingCall(Conversation conversation)
        {
            if (_state == TranscriptRecorderState.Terminated)
            {
                NonBlockingConsole.WriteLine("Error: IMTranscriptRecorder is shutdown.");
                // TODO: error message
                return;
            }

            if (_instantMessagingCall != null)
            {
                NonBlockingConsole.WriteLine("Warn: IMCall already exists for this Conversation. Shutting down previous call...");
                // TODO: Info message
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
                NonBlockingConsole.WriteLine("Error: imCall.BeginEstablish failed. Exception: {0}", ex.ToString());
                // TODO: Error Message
            }
        }

        void IMCall_EstablishCompleted(IAsyncResult result)
        {
            try
            {
                InstantMessagingCall imCall = result.AsyncState as InstantMessagingCall;
                imCall.EndEstablish(result);

                Message m = new Message("InstantMessagingCall Established. Call state: " + _instantMessagingCall.State.ToString() + ". CallId: " + _instantMessagingCall.CallId + ".",
                    _instantMessagingCall.RemoteEndpoint.Participant.DisplayName, _instantMessagingCall.RemoteEndpoint.Participant.UserAtHost,
                    _instantMessagingCall.RemoteEndpoint.Participant.Uri,
                    MessageType.InstantMessage, _transcriptRecorder.Conversation.Id, MessageDirection.Incoming);
                _transcriptRecorder.OnMessageReceived(m);

                _transcriptRecorder.OnRemoteParticipantAdded(null, imCall.RemoteEndpoint);
            }
            catch (RealTimeException ex)
            {
                NonBlockingConsole.WriteLine("Error: imCall.EndEstablish failed. Exception: {0}", ex.ToString());
                // TODO: error message
            }
            finally
            {
                _state = TranscriptRecorderState.Active;
                this._waitForIMCallEstablished.Set();
            }
        }

        void InstantMessagingCall_StateChanged(object sender, CallStateChangedEventArgs e)
        {
            Call call = sender as Call;

            //Call participants allow for disambiguation.
            NonBlockingConsole.WriteLine("The InstantMessaging call with Local Participant: " + call.Conversation.LocalParticipant +
                " and Remote Participant: " + call.RemoteEndpoint.Participant +
                " has changed state. The previous call state was: " + e.PreviousState +
                " and the current state is: " + e.State);

            Message m = new Message("The InstantMessaging call with Local Participant: " + call.Conversation.LocalParticipant +
                " and Remote Participant: " + call.RemoteEndpoint.Participant +
                " has changed state. The previous call state was: " + e.PreviousState +
                " and the current state is: " + e.State,
                MessageType.InstantMessage,
                _transcriptRecorder.Conversation.Id);
            _transcriptRecorder.OnMessageReceived(m);

            if (e.State == CallState.Terminating || e.State == CallState.Terminated)
            {
                NonBlockingConsole.WriteLine("Shutting down IMTranscriptRecorder");
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
            NonBlockingConsole.WriteLine("IM Flow Configuration Requested.");
            _instantMessagingFlow = e.Flow;

            Message m = new Message("IM Flow Configuration Requested.",
                MessageType.InstantMessage,
                _transcriptRecorder.Conversation.Id);
            _transcriptRecorder.OnMessageReceived(m);

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

        private void InstantMessagingCall_ConversationChanged(object sender, ConversationChangedEventArgs e)
        {
            NonBlockingConsole.WriteLine("IMCall conversation changed. Reason: " + e.Reason.ToString());
            if (_subConversation != null)
            {
                NonBlockingConsole.WriteLine("Warn: Subconversation already set. Clearing previous subconversation.");
                _transcriptRecorder.OnSubConversationRemoved(_subConversation, this);
            }

            _subConversation = e.NewConversation;

            Message m = new Message("IMCall conversation changed. Reason: " + e.Reason.ToString() + ". New Conversation: " + _subConversation.Id + ".",
                MessageType.InstantMessage,
                _transcriptRecorder.Conversation.Id);
            _transcriptRecorder.OnMessageReceived(m);

            _transcriptRecorder.OnSubConversationAdded(_subConversation, this);

            // call top level event handler
            if (_imCallConversationChangedEventHandler != null)
            {
                _imCallConversationChangedEventHandler(sender, e);
            }
        }

        private void InstantMessagingFlow_StateChanged(object sender, MediaFlowStateChangedEventArgs e)
        {
            NonBlockingConsole.WriteLine("IM flow state changed from " + e.PreviousState + " to " + e.State);

            Message m = new Message("InstantMessagingFlow changed from " + e.PreviousState + " to " + e.State + ".",
                MessageType.InstantMessage,
                _transcriptRecorder.Conversation.Id);
            _transcriptRecorder.OnMessageReceived(m);

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
            NonBlockingConsole.WriteLine(e.Sender.Uri + " said: " + e.TextBody);

            Message m = new Message(e.TextBody, e.Sender.DisplayName, e.Sender.UserAtHost, e.Sender.Uri, DateTime.Now,
                _instantMessagingCall.Conversation.Id, _instantMessagingCall.Conversation.ConferenceSession.ConferenceUri,
                MessageType.InstantMessage, MessageDirection.Incoming);
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

                Message m = new Message("InstantMessagingCall Accepted. Call state: " + instantMessagingCall.State.ToString() + ". CallId: " + instantMessagingCall.CallId + ".",
                    instantMessagingCall.RemoteEndpoint.Participant.DisplayName, instantMessagingCall.RemoteEndpoint.Participant.UserAtHost,
                    instantMessagingCall.RemoteEndpoint.Participant.Uri,
                    MessageType.InstantMessage, _transcriptRecorder.Conversation.Id, MessageDirection.Incoming);
                _transcriptRecorder.OnMessageReceived(m);

                _transcriptRecorder.OnRemoteParticipantAdded(null, instantMessagingCall.RemoteEndpoint);
            }
            catch (RealTimeException exception)
            {
                // RealTimeException may be thrown on media or link-layer failures. 
                // A production application should catch additional exceptions, such as OperationTimeoutException,
                // OperationTimeoutException, and CallOperationTimeoutException.

                NonBlockingConsole.WriteLine(exception.ToString());
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
                instantMessagingCall.StateChanged -= InstantMessagingCall_StateChanged;
                instantMessagingCall.InstantMessagingFlowConfigurationRequested -= this.InstantMessagingCall_FlowConfigurationRequested;

            }
            catch (Exception e)
            {
                NonBlockingConsole.WriteLine(e.ToString());
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
