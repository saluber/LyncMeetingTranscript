using System;
using System.IO;
using System.Xml;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using Microsoft.Speech;
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
        #region Fields
        private static readonly string SpeechRecogLocaleKey = "SpeechRecognitionLocale";
        private static readonly string DefaultLocale = @"en-US";

        private AutoResetEvent _waitForAudioVideoFlowStateChangedToActiveCompleted = new AutoResetEvent(false);
        private AutoResetEvent _waitForLoadGrammarCompleted = new AutoResetEvent(false);

        private TranscriptRecorderSession _transcriptRecorder;
        private bool _isActive = false;
        private bool _isRecognizing = false;
        private string _currentSRLocale = DefaultLocale;

        private SpeechRecognitionConnector _speechRecognitionConnector;
        private Microsoft.Speech.Recognition.SpeechRecognitionEngine _speechRecognitionEngine;
        private SpeechRecognitionStream _speechRecognitionStream;
        private AudioVideoFlow _audioVideoFlow;

        private List<Microsoft.Speech.Recognition.RecognitionResult> _speechTranscript;

        private SpeechAudioFormatInfo speechAudioFormatInfo = new SpeechAudioFormatInfo(8000, AudioBitsPerSample.Sixteen, Microsoft.Speech.AudioFormat.AudioChannel.Mono);
        //interval for which speech reconizer should wait for additional input before finalizing a recognition operation.
        private TimeSpan _completeRecognitionTimeOut = new TimeSpan(0, 0, 10);
        //Array of strings of expected speech inputs.
        private string[] _expectedSpeechInputs { get; set; }
        //List of speech grammars that will be active during recognition.
        private List<Microsoft.Speech.Recognition.Grammar> _grammars { get; set; }
        private int _pendingLoadSpeechGrammarCounter = 0;
        #endregion // Fields

        public List<Microsoft.Speech.Recognition.RecognitionResult> SpeechTranscript
        {
            get { return _speechTranscript; }
        }

        public bool IsActive
        {
            get { return _isActive; }
        }

        public bool IsRecognizing
        {
            get { return _isRecognizing; }
        }

        public SpeechRecognizer(TranscriptRecorderSession transcriptRecorder)
        {
            _transcriptRecorder = transcriptRecorder;
            _speechTranscript = new List<Microsoft.Speech.Recognition.RecognitionResult>();
            _isActive = false;
            _isRecognizing = false;

            // Create a speech recognition connector
            _speechRecognitionConnector = new SpeechRecognitionConnector();

            _currentSRLocale = ConfigurationManager.AppSettings[SpeechRecogLocaleKey];
            if (String.IsNullOrEmpty(_currentSRLocale))
            {
                System.Console.WriteLine("No locale specified, using default locale for speech recognition: " + DefaultLocale);
                _currentSRLocale = DefaultLocale;
            }

            // Create speech recognition engine and start recognizing by attaching connector to engine
            try
            {
                //_speechRecognitionEngine = new Microsoft.Speech.Recognition.SpeechRecognitionEngine();
                System.Globalization.CultureInfo localeCultureInfo = new System.Globalization.CultureInfo(_currentSRLocale);
                foreach (RecognizerInfo r in Microsoft.Speech.Recognition.SpeechRecognitionEngine.InstalledRecognizers())
                {
                    if (r.Culture.Equals(localeCultureInfo))
                    {
                        _speechRecognitionEngine = new Microsoft.Speech.Recognition.SpeechRecognitionEngine(r);
                        break;
                    }
                }
                if (_speechRecognitionEngine == null)
                {
                    _speechRecognitionEngine = new SpeechRecognitionEngine();
                }

                //_speechRecognitionEngine = new Microsoft.Speech.Recognition.SpeechRecognitionEngine(new System.Globalization.CultureInfo(_currentSRLocale));
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: Unable to load SpeechRecognition locale: " + _currentSRLocale + ". Exception: " + e.ToString());
                // Use default locale
                Console.WriteLine("Falling back to default locale for SpeechRecognitionEngine: " + DefaultLocale);
                _currentSRLocale = DefaultLocale;
                _speechRecognitionEngine = new SpeechRecognitionEngine();
                //_speechRecognitionEngine = new Microsoft.Speech.Recognition.SpeechRecognitionEngine(new System.Globalization.CultureInfo(_currentSRLocale));
            }

            _speechRecognitionEngine.SpeechDetected += new EventHandler<Microsoft.Speech.Recognition.SpeechDetectedEventArgs>(SpeechRecognitionEngine_SpeechDetected);
            _speechRecognitionEngine.RecognizeCompleted += new EventHandler<Microsoft.Speech.Recognition.RecognizeCompletedEventArgs>(SpeechRecognitionEngine_RecognizeCompleted);
            _speechRecognitionEngine.LoadGrammarCompleted += new EventHandler<Microsoft.Speech.Recognition.LoadGrammarCompletedEventArgs>(SpeechRecognitionEngine_LoadGrammarCompleted);

            _grammars = new List<Microsoft.Speech.Recognition.Grammar>();

            // Add default installed speech recognizer grammar
            // Might already be done via compiling with Recognition Settings File?

            // Add default locale language grammar file (if it exists)
            String localLanguageGrammarFilePath = Path.Combine(Environment.CurrentDirectory, @"en-US.cfgpp");
            if (File.Exists(localLanguageGrammarFilePath))
            {
                System.Console.WriteLine("SpeechRecognizer(). Adding locale language file at path: " + localLanguageGrammarFilePath);
                GrammarBuilder builder = new GrammarBuilder();
                builder.AppendRuleReference(localLanguageGrammarFilePath);
                Grammar localeLanguageGrammar = new Grammar(builder);
                localeLanguageGrammar.Name = "Local language grammar";
                //localeLanguageGrammar.Priority = 1;
                _grammars.Add(localeLanguageGrammar);
            }
            
            string[] recognizedString = { "hello", "bye", "yes", "no", "help", "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "exit" };
            Choices numberChoices = new Choices(recognizedString);
            Grammar basicGrammar = new Grammar(new GrammarBuilder(numberChoices));
            basicGrammar.Name = "Basic Grammar";
            //basicGrammar.Priority = 2;
            _grammars.Add(basicGrammar);

            LoadSpeechGrammarAsync();
        }

        #region Public Methods
        public void AttachAndStartSpeechRecognition(AudioVideoFlow avFlow)
        {
            if (avFlow == null)
            {
                throw new InvalidOperationException("Cannot recognize speech of inactive AudioVideoFlow");
            }
            if (_isActive)
            {
                Console.WriteLine("Warn: SpeechRecognizer already active on an AudioFlow. Stopping current recognition session.");
                StopSpeechRecognition();
            }

            _waitForAudioVideoFlowStateChangedToActiveCompleted.Reset();
            _speechTranscript.Clear();
            //StopSpeechRecognition();

            _isActive = true;
            _audioVideoFlow = avFlow;
            _audioVideoFlow.StateChanged += new EventHandler<MediaFlowStateChangedEventArgs>(AudioVideoFlow_StateChanged);

            if (_audioVideoFlow.State == MediaFlowState.Active)
            {
                StartSpeechRecognition();
            }

            // Else, Speech Recognition will start when AudioVideoFlow state becomes active
        }

        public void StopSpeechRecognition()
        {
            if (!_isActive)
            {
                Console.WriteLine("Warn: StopSpeechRecognition() called on an inactive SpeechRecognizer.");
                return;
            }

            _isActive = false;

            if (_isRecognizing)
            {
                _isRecognizing = false;
                if (_speechRecognitionEngine != null)
                {
                    _speechRecognitionEngine.RecognizeAsyncCancel();
                }

                if (_speechRecognitionConnector != null)
                {
                    // Stop the connector
                    _speechRecognitionConnector.Stop();

                    // speech recognition connector must be detached from the flow, otherwise if the connector is rooted, it will keep the flow in memory.
                    _speechRecognitionConnector.DetachFlow();
                }

                if (_speechRecognitionStream != null)
                {
                    _speechRecognitionStream.Dispose();
                    _speechRecognitionStream = null;
                }
            }

            if ((_audioVideoFlow != null) && (_audioVideoFlow.SpeechRecognitionConnector != null))
            {
                _audioVideoFlow.SpeechRecognitionConnector.Stop();
                _audioVideoFlow.SpeechRecognitionConnector.DetachFlow();
                _audioVideoFlow.StateChanged -= AudioVideoFlow_StateChanged;
                _audioVideoFlow = null;
            }

            _waitForAudioVideoFlowStateChangedToActiveCompleted.Reset();
        }

        public void Shutdown()
        {
            if (_isActive)
            {
                StopSpeechRecognition();
            }

            if (_speechRecognitionEngine != null)
            {
                _speechRecognitionEngine.UnloadAllGrammars();
                _grammars.Clear();
                _pendingLoadSpeechGrammarCounter = 0;
                
                _speechRecognitionEngine.SpeechDetected -= (SpeechRecognitionEngine_SpeechDetected);
                _speechRecognitionEngine.RecognizeCompleted -= (SpeechRecognitionEngine_RecognizeCompleted);
                _speechRecognitionEngine.LoadGrammarCompleted -= (SpeechRecognitionEngine_LoadGrammarCompleted);
            }

            if (_speechRecognitionConnector != null)
            {
                _speechRecognitionConnector.Dispose();
                _speechRecognitionConnector = null;
            }

            _speechTranscript.Clear();
            _transcriptRecorder = null;
        }

        #endregion // Public Methods

        #region Event Handlers

        // Callback that handles when the state of an AudioVideoFlow changes
        private void AudioVideoFlow_StateChanged(object sender, MediaFlowStateChangedEventArgs e)
        {
            Console.WriteLine("AV flow state changed from " + e.PreviousState + " to " + e.State);
            string messageText = "";

            //When flow is active, media operations can begin
            if (e.State == MediaFlowState.Active)
            {
                Console.WriteLine("Starting speech recognition");
                messageText = "Starting speech recognition";

                // Flow-related media operations normally begin here.
                _waitForAudioVideoFlowStateChangedToActiveCompleted.Set();
                StartSpeechRecognition();
            }
            else if (e.State == MediaFlowState.Terminated)
            {
                Console.WriteLine("Stopping speech recognition");
                messageText = "Stopping speech recognition";

                // Detach SpeechSynthesisConnector since AVFlow will not work anymore
                this.StopSpeechRecognition();
            }

            if (!String.IsNullOrEmpty(messageText) && (this._transcriptRecorder != null))
            {
                Conversation conv = _audioVideoFlow.Call.Conversation;
                ConversationParticipant speaker = _audioVideoFlow.Call.RemoteEndpoint.Participant;
                Message m = new Message(messageText, speaker.DisplayName, speaker.UserAtHost,
                    speaker.Uri, DateTime.Now, conv.Id,
                    conv.ConferenceSession.ConferenceUri, MessageType.Info, MessageDirection.Outgoing);
                this._transcriptRecorder.OnMessageReceived(m);
            }
        }

        #region Speech Event Handlers

        void SpeechRecognitionEngine_LoadGrammarCompleted(object sender, LoadGrammarCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                Console.WriteLine("Error: SpeechRecognizer receieved error from LoadGrammar(): " + e.ToString());

                string errorMessageText = "Error: SpeechRecognizer receieved error from LoadGrammar(): " + e.ToString();
                Conversation conv = _audioVideoFlow.Call.Conversation;
                ConversationParticipant speaker = _audioVideoFlow.Call.RemoteEndpoint.Participant;
                Message m = new Message(errorMessageText, speaker.DisplayName, speaker.UserAtHost,
                    speaker.Uri, DateTime.Now, conv.Id,
                    conv.ConferenceSession.ConferenceUri, MessageType.Error, MessageDirection.Outgoing);
                this._transcriptRecorder.OnMessageReceived(m);

                _transcriptRecorder.OnMediaTranscriptRecorderError(m);
            }

            _pendingLoadSpeechGrammarCounter--;
            Console.WriteLine("SpeechRecognitionEngine load grammar completed. Pending grammar loads remaining: " + _pendingLoadSpeechGrammarCounter.ToString());

            if (_pendingLoadSpeechGrammarCounter == 0)
            {
                _waitForLoadGrammarCompleted.Set();
            }
        }

        void SpeechRecognitionEngine_SpeechDetected(object sender, Microsoft.Speech.Recognition.SpeechDetectedEventArgs e)
        {
            Console.WriteLine("SpeechRecognitionEngine has detected speech.");

            Conversation conv = _audioVideoFlow.Call.Conversation;
            ConversationParticipant speaker = _audioVideoFlow.Call.RemoteEndpoint.Participant;
            Message m = new Message("SpeechRecognitionEngine has detected speech.", speaker.DisplayName, speaker.UserAtHost,
                speaker.Uri, DateTime.Now, conv.Id,
                conv.ConferenceSession.ConferenceUri, MessageType.Info, MessageDirection.Outgoing);
            this._transcriptRecorder.OnMessageReceived(m);
        }

        void SpeechRecognitionEngine_RecognizeCompleted(object sender, Microsoft.Speech.Recognition.RecognizeCompletedEventArgs e)
        {
            Console.WriteLine("SpeechRecognitionEngine_RecognizeCompleted.");

            string messageText = "";
            MessageType messageModality = MessageType.Audio;

            Microsoft.Speech.Recognition.RecognitionResult result = e.Result;
            if (result != null)
            {
                Console.WriteLine("Speech recognized: " + result.Text);
                _speechTranscript.Add(result);
                messageText = result.Text;
            }
            else if (e.Error != null)
            {
                    messageText = e.Error.ToString();
                    messageModality = MessageType.Error;
                    Console.WriteLine("Error occured during speech detection: " + e.Error.ToString());
            }
            else if (e.InputStreamEnded || e.Cancelled)
            {
                Console.WriteLine("Speech recognization completed due to user disconnect or conference ending.");
                messageText = "Speech recognization completed due to user disconnect or conference ending.";
                messageModality = MessageType.Info;
            }
            else
            {
                messageText = "Failed to recognize speech.";
                messageModality = MessageType.Error;
                Console.WriteLine("Failed to recognize speech.");
            }

            if (!String.IsNullOrEmpty(messageText))
            {
                Conversation conv = _audioVideoFlow.Call.Conversation;
                ConversationParticipant speaker = _audioVideoFlow.Call.RemoteEndpoint.Participant;
                Message m = new Message(messageText, speaker.DisplayName, speaker.UserAtHost, speaker.Uri, DateTime.Now, conv.Id,
                    conv.ConferenceSession.ConferenceUri, messageModality, MessageDirection.Outgoing);
                this._transcriptRecorder.OnMessageReceived(m);
            }
        }

        #endregion // Speech Event Handlers

        #endregion // Event Handlers

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

            if (_grammars.Count > 0)
            {
                //Register handler and load each grammar
                foreach (Microsoft.Speech.Recognition.Grammar grammar in _grammars)
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

        private void StartSpeechRecognition()
        {
            if (_isActive && !_isRecognizing)
            {
                _isRecognizing = true;
                _waitForLoadGrammarCompleted.WaitOne();
                _speechRecognitionConnector.AttachFlow(_audioVideoFlow);
                _speechRecognitionStream = _speechRecognitionConnector.Start();
                _speechRecognitionEngine.SetInputToAudioStream(_speechRecognitionStream, speechAudioFormatInfo);
                _speechRecognitionEngine.RecognizeAsync(RecognizeMode.Multiple);

                #if EMULATE_SPEECH
                _speechRecognitionEngine.EmulateRecognizeAsync("one");
                _speechRecognitionEngine.EmulateRecognizeAsync("two");
                _speechRecognitionEngine.EmulateRecognizeAsync("three");
                _speechRecognitionEngine.EmulateRecognizeAsync("four");
                #endif // EMULATE_SPEECH
            }
        }

        #endregion // Private Helper Methods
    }
}
