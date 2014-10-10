using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LyncMeetingTranscriptBotApplication.TranscriptRecorders
{
    public enum TranscriptRecorderType { AudioVideo, InstantMessage, Conversation, Conference }

    public enum TranscriptRecorderState { Initialized, Active, Terminated }

    public abstract class MediaTranscriptRecorder
    {
        public abstract void Shutdown();

        public abstract TranscriptRecorderType RecorderType { get; }

        public abstract TranscriptRecorderState State { get; }
    }
}
