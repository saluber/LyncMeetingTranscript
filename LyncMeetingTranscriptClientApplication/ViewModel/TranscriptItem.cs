using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace LyncMeetingTranscriptClientApplication.ViewModel
{
    public class TranscriptItem
    {
        public TranscriptItem(DateTime messageTime, string name, string uri, string modality, string message)
        {
            this.MessageTime = messageTime;
            this.ParticipantName = name;
            this.ParticipantUri = uri;
            this.Modality = modality;
            this.Message = message;
        }

        /// <summary>
        /// Original message time.
        /// </summary>
        public DateTime MessageTime { get; set; }

        /// <summary>
        /// Name of the participant who sent the message
        /// </summary>
        public string ParticipantName { get; set; }

        /// <summary>
        /// Uri of the participant who sent the message
        /// </summary>
        public string ParticipantUri { get; set; }

        /// <summary>
        /// Modality of the message
        /// </summary>
        public string Modality { get; set; }

        /// <summary>
        /// Message content.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Returns the appropriate background for this instance.
        /// </summary>
        public string Background
        {
            get
            {
                return "#FFFFFFFF";
            }
        }
    }
}
