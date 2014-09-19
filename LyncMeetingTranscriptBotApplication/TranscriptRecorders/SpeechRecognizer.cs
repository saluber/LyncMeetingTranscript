using System;
using System.IO;
using System.Xml;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Speech.AudioFormat;
using Microsoft.Speech.Synthesis;
using Microsoft.Speech.Recognition;
using Microsoft.Rtc.Collaboration;
using Microsoft.Rtc.Collaboration.AudioVideo;
using Microsoft.Rtc.Signaling;

namespace LyncMeetingTranscriptBotApplication.TranscriptRecorders
{
    class SpeechRecognizer
    {
        private AutoResetEvent _waitForAudioVideoFlowStateChangedToActiveCompleted = new AutoResetEvent(false);
        private AutoResetEvent _waitForLoadGrammarCompleted = new AutoResetEvent(false);

        private TranscriptRecorder _transcriptRecorder;
        private bool _isActive;

        private SpeechRecognitionConnector _speechRecognitionConnector;
        private SpeechRecognitionEngine _speechRecognitionEngine;
        private SpeechRecognitionStream _speechRecognitionStream;
        private AudioVideoFlow _audioVideoFlow;

        private List<RecognitionResult> _speechTranscript;

        private SpeechAudioFormatInfo speechAudioFormatInfo = new SpeechAudioFormatInfo(8000, AudioBitsPerSample.Sixteen, Microsoft.Speech.AudioFormat.AudioChannel.Mono);
        //interval for which speech reconizer should wait for additional input before finalizing a recognition operation.
        private TimeSpan _completeRecognitionTimeOut = new TimeSpan(0, 0, 10);
        //Array of strings of expected speech inputs.
        private string[] _expectedSpeechInputs { get; set; }
        //List of speech grammars that will be active during recognition.
        private List<Grammar> _grammars { get; set; }
        private int _pendingLoadSpeechGrammarCounter = 0;

        public List<RecognitionResult> SpeechTranscript
        {
            get { return _speechTranscript; }
        }

        public Boolean IsActive
        {
            get { return _isActive; }
        }

        public SpeechRecognizer(TranscriptRecorder transcriptRecorder)
        {
            _transcriptRecorder = transcriptRecorder;
            _speechTranscript = new List<RecognitionResult>();
            _isActive = false;

            // Create a speech recognition connector
            _speechRecognitionConnector = new SpeechRecognitionConnector();

            // Create speech recognition engine and start recognizing by attaching connector to engine
            _speechRecognitionEngine = new SpeechRecognitionEngine();
            _speechRecognitionEngine.SpeechDetected += new EventHandler<SpeechDetectedEventArgs>(SpeechRecognitionEngine_SpeechDetected);
            _speechRecognitionEngine.RecognizeCompleted += new EventHandler<RecognizeCompletedEventArgs>(SpeechRecognitionEngine_RecognizeCompleted);
            _speechRecognitionEngine.LoadGrammarCompleted += new EventHandler<LoadGrammarCompletedEventArgs>(SpeechRecognitionEngine_LoadGrammarCompleted);
            _pendingLoadSpeechGrammarCounter = 0;

            // TODO: Replace with language grammar sets
            _grammars = new List<Grammar>();
            String currDirPath = Environment.CurrentDirectory;
            Grammar gr = new Grammar(currDirPath + @"\Resources\en-US.grxml", "Expression");
            _grammars.Add(gr);

            string[] recognizedString = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "exit" };
            Choices numberChoices = new Choices(recognizedString);
            _grammars.Add(new Grammar(new GrammarBuilder(numberChoices)));

            LoadSpeechGrammarAsync();
        }

        public void AttachAndStartSpeechRecognition(AudioVideoFlow avFlow)
        {
            if (avFlow == null)
            {
                throw new InvalidOperationException("Cannot recognize speech of inactive AudioVideoFlow");
            }

            if (_isActive == false)
            {
                _waitForAudioVideoFlowStateChangedToActiveCompleted.Reset();
                _speechTranscript.Clear();
                StopSpeechRecognition();
            }

            _isActive = true;
            _audioVideoFlow = avFlow;
            _audioVideoFlow.StateChanged += AudioVideoFlow_StateChanged;

            _waitForAudioVideoFlowStateChangedToActiveCompleted.WaitOne();
            _waitForLoadGrammarCompleted.WaitOne();

            _speechRecognitionConnector.AttachFlow(_audioVideoFlow);
            _speechRecognitionStream = _speechRecognitionConnector.Start();
            _speechRecognitionEngine.SetInputToAudioStream(_speechRecognitionStream, speechAudioFormatInfo);
            _speechRecognitionEngine.RecognizeAsync(RecognizeMode.Multiple);
        }

        public void StopSpeechRecognition()
        {
            _isActive = false;

            if (_speechRecognitionEngine != null)
            {
                _speechRecognitionEngine.RecognizeAsyncCancel();
            }

            if (_speechRecognitionConnector != null)
            {
                //Stop the connector
                _speechRecognitionConnector.Stop();

                //speech recognition connector must be detached from the flow, otherwise if the connector is rooted, it will keep the flow in memory.
                _speechRecognitionConnector.DetachFlow();
            }

            if (_speechRecognitionStream != null)
            {
                _speechRecognitionStream.Dispose();
                _speechRecognitionStream = null;
            }

            if ((_audioVideoFlow != null) && (_audioVideoFlow.SpeechRecognitionConnector != null))
            {
                _audioVideoFlow.SpeechRecognitionConnector.Stop();
                _audioVideoFlow.SpeechRecognitionConnector.DetachFlow();
                _audioVideoFlow.StateChanged -= AudioVideoFlow_StateChanged;
            }
        }

        public void Shutdown()
        {
            _isActive = false;

            if (_speechRecognitionEngine != null)
            {
                _speechRecognitionEngine.RecognizeAsyncCancel();
                _speechRecognitionEngine.UnloadAllGrammars();
                _pendingLoadSpeechGrammarCounter = 0;
                _speechRecognitionEngine.SpeechDetected -= new EventHandler<SpeechDetectedEventArgs>(SpeechRecognitionEngine_SpeechDetected);
                _speechRecognitionEngine.RecognizeCompleted -= new EventHandler<RecognizeCompletedEventArgs>(SpeechRecognitionEngine_RecognizeCompleted);
                _speechRecognitionEngine.LoadGrammarCompleted -= new EventHandler<LoadGrammarCompletedEventArgs>(SpeechRecognitionEngine_LoadGrammarCompleted);
            }

            if (_speechRecognitionConnector != null)
            {
                //Stop the connector
                _speechRecognitionConnector.Stop();

                //speech recognition connector must be detached from the flow, otherwise if the connector is rooted, it will keep the flow in memory.
                _speechRecognitionConnector.DetachFlow();
            }

            if (_speechRecognitionStream != null)
            {
                _speechRecognitionStream.Dispose();
                _speechRecognitionStream = null;
            }

            if (_audioVideoFlow != null && _audioVideoFlow.SpeechRecognitionConnector != null)
            {
                _audioVideoFlow.SpeechRecognitionConnector.Stop();
                _audioVideoFlow.SpeechRecognitionConnector.DetachFlow();
                _audioVideoFlow.StateChanged -= AudioVideoFlow_StateChanged;
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
            }
            if (e.State == MediaFlowState.Terminated)
            {
                // Detach SpeechSynthesisConnector since AVFlow will not work anymore
                _audioVideoFlow.SpeechRecognitionConnector.DetachFlow();
            }
        }

        #region Speech Event Handlers

        void SpeechRecognitionEngine_LoadGrammarCompleted(object sender, LoadGrammarCompletedEventArgs e)
        {
            _pendingLoadSpeechGrammarCounter--;
            Console.WriteLine("SpeechRecognitionEngine load grammar completed. Pending grammar loads remaining: " + _pendingLoadSpeechGrammarCounter);

            if (_pendingLoadSpeechGrammarCounter == 0)
            {
                _waitForLoadGrammarCompleted.Set();
            }
        }

        void SpeechRecognitionEngine_SpeechDetected(object sender, SpeechDetectedEventArgs e)
        {
            Console.WriteLine("SpeechRecognitionEngine has detected speech.");
        }

        void SpeechRecognitionEngine_RecognizeCompleted(object sender, RecognizeCompletedEventArgs e)
        {
            string messageText = "";
            MessageModality messageModality = MessageModality.Audio;

            RecognitionResult result = e.Result;
            if (result != null)
            {
                Console.WriteLine("Speech recognized: " + result.Text);
                _speechTranscript.Add(result);
                messageText = result.Text;

            }
            else
            {
                if (e.Error != null)
                {
                    messageText = e.Error.ToString();
                    messageModality = MessageModality.Error;
                    Console.WriteLine("Error occured during speech detection: " + e.Error.ToString());
                }

                Console.WriteLine("Failed to recognize speech.");
            }

            Conversation conv = _audioVideoFlow.Call.Conversation;
            ConversationParticipant speaker = _audioVideoFlow.Call.RemoteEndpoint.Participant;
            Message m = new Message(messageText, speaker.Uri, speaker.DisplayName, DateTime.Now, conv.Id,
                conv.ConferenceSession.ConferenceUri, messageModality, MessageDirection.Outgoing);

            this._transcriptRecorder.AddMessage(m);
        }

        #endregion // Speech Event Handlers

        #region Private Helper Methods

        /// <summary>
        /// Method to generate grammar from the array of expected inputs.
        /// </summary>
        private Grammar GenerateGrammar(string[] phrases)
        {
            GrammarBuilder grammarBuilder = new GrammarBuilder();
            grammarBuilder.Append(new Choices(phrases));

            return new Grammar(grammarBuilder);
        }

        /// <summary>
        /// Loads speech grammar async.
        /// </summary>
        private void LoadSpeechGrammarAsync()
        {
            //Set the end silence time out.
            _speechRecognitionEngine.EndSilenceTimeout = this._completeRecognitionTimeOut;

            if (_grammars.Count != 0)
            {
                //Register handler and load each grammar
                foreach (Grammar grammar in _grammars)
                {
                    _pendingLoadSpeechGrammarCounter++;
                    _speechRecognitionEngine.LoadGrammarAsync(grammar);
                }
            }
            else
            {
                _waitForLoadGrammarCompleted.Set();
            }
        }

        #endregion // Private Helper Methods
    }

}
