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
        private TranscriptRecorder _transcriptRecorder;

        private EventHandler<CallStateChangedEventArgs> _imCallStateChangedEventHandler;
        private EventHandler<InstantMessagingFlowConfigurationRequestedEventArgs> _imFlowConfigurationRequestedEventHandler;
        private EventHandler<MediaFlowStateChangedEventArgs> _imFlowStateChangedEventHandler;
        private EventHandler<InstantMessageReceivedEventArgs> _imFlowMessageReceivedEventHandler;

        private AutoResetEvent _waitForIMCallAccepted = new AutoResetEvent(false);
        private AutoResetEvent _waitForIMCallTerminated = new AutoResetEvent(false);
        private AutoResetEvent _waitForIMFlowStateChangedToActiveCompleted = new AutoResetEvent(false);

        private InstantMessagingCall _instantMessagingCall;
        private InstantMessagingFlow _instantMessagingFlow;
        private Conversation _subConversation;

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

        public IMTranscriptRecorder(TranscriptRecorder transcriptRecorder,
            EventHandler<CallStateChangedEventArgs> imCallStateChangedEventHandler = null,
            EventHandler<InstantMessagingFlowConfigurationRequestedEventArgs> imFlowConfigurationRequestedEventHandler = null,
            EventHandler<MediaFlowStateChangedEventArgs> imFlowStateChangedEventHandler = null,
            EventHandler<InstantMessageReceivedEventArgs> imFlowMessageReceivedEventHandler = null)
        {
            _transcriptRecorder = transcriptRecorder;

            _imCallStateChangedEventHandler = imCallStateChangedEventHandler;
            _imFlowConfigurationRequestedEventHandler = imFlowConfigurationRequestedEventHandler;
            _imFlowStateChangedEventHandler = imFlowStateChangedEventHandler;
            _imFlowMessageReceivedEventHandler = imFlowMessageReceivedEventHandler;
        }

        public void TerminateCall()
        {
            if (_instantMessagingCall != null)
            {
                _instantMessagingCall.BeginTerminate(CallTerminated, _instantMessagingCall);
                _waitForIMCallTerminated.WaitOne();
            }
        }

        public override void Shutdown()
        {
            TerminateCall();
        }

        #region Event Handlers

        public void On_InstantMessagingCall_Received(CallReceivedEventArgs<InstantMessagingCall> e)
        {
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
            Console.WriteLine("Call Received! From: " + e.RemoteParticipant.Uri + " Toast is: " +
                                                e.ToastMessage.Message);

            // Now, accept the call. EndAcceptCall will be raised on the 
            // same thread.
            _instantMessagingCall.BeginAccept(InstantMessagingCallAcceptedCallBack, _instantMessagingCall);
        }

        void InstantMessagingCall_StateChanged(object sender, CallStateChangedEventArgs e)
        {
            Call call = sender as Call;

            //Call participants allow for disambiguation.
            Console.WriteLine("The InstantMessaging call with Local Participant: " + call.Conversation.LocalParticipant +
                " and Remote Participant: " + call.RemoteEndpoint.Participant +
                " has changed state. The previous call state was: " + e.PreviousState +
                " and the current state is: " + e.State);

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

                // Send the message on the InstantMessagingFlow.
                //_instantMessagingFlow.BeginSendInstantMessage(_messageToSend, SendMessageCompleted,
                //    _instantMessagingFlow);
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

            Message m = new Message(e.TextBody, e.Sender.DisplayName, e.Sender.Uri, DateTime.Now,
                _instantMessagingCall.Conversation.Id, _instantMessagingCall.Conversation.ConferenceSession.ConferenceUri,
                MessageModality.InstantMessage, MessageDirection.Outgoing);

            this._transcriptRecorder.AddMessage(m);

            /*
            // Shutdown if the far end tells us to.
            if (e.TextBody.Equals("bye", StringComparison.OrdinalIgnoreCase))
            {
                // Shutting down the platform will terminate all attached objects.
                // If this was a production application, it would tear down the 
                // Call/Conversation, rather than terminating the entire platform.
                _instantMessagingFlow.BeginSendInstantMessage("Shutting Down...", SendMessageCompleted,
                    _instantMessagingFlow);
                _helper.ShutdownPlatform();
                _waitForSendMessageCompleted.Set();
            }
            else
            {
                // Echo the instant message back to the far end (the sender of 
                // the instant message).
                // Change the composing state of the local end user while sending messages to the far end.
                // A delay is introduced purposely to demonstrate the typing notification displayed by the 
                // far end client; otherwise the notification will not last long enough to notice.
                _instantMessagingFlow.LocalComposingState = ComposingState.Composing;
                Thread.Sleep(2000);

                //Echo the message with an "Echo" prefix.
                _instantMessagingFlow.BeginSendInstantMessage("Echo: " + e.TextBody, SendMessageCompleted,
                    _instantMessagingFlow);
            }
            */

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
            }
        }

        private void CallTerminated(IAsyncResult ar)
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
    }
}
