using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Rtc.Collaboration;
using Microsoft.Rtc.Collaboration.AudioVideo;
using Microsoft.Rtc.Signaling;

namespace LyncMeetingTranscriptBotApplication.TranscriptRecorders
{
    class ConferenceTranscriptRecorder : MediaTranscriptRecorder
    {
        private TranscriptRecorder _transcriptRecorder;
        private Conference _conference;

        public ConferenceTranscriptRecorder(Conference conference)
        {
            _conference = conference;
        }
    }
}
