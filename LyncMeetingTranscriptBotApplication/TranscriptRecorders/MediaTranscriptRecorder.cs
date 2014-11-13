namespace LyncMeetingTranscriptBotApplication.TranscriptRecorders
{
    public enum TranscriptRecorderType { AudioVideo = 1, InstantMessage = 2, Conversation = 3, Conference = 4 }

    public enum TranscriptRecorderState { Initialized = 1, Active = 2, Terminated = 3 }

    public abstract class MediaTranscriptRecorder
    {
        public abstract void Shutdown();

        public abstract TranscriptRecorderType RecorderType { get; }

        public abstract TranscriptRecorderState State { get; }
    }
}
