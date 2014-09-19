using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LyncMeetingTranscriptClientApplication.Model
{
    public enum MessageDirection { Incoming, Outgoing };

    public enum MessageModality { Audio, InstantMessage, ParticipantInfo, ConferenceInfo }

    public class MessageContext
    {
        /// <summary>
        /// Direction of the message.
        /// </summary>
        public MessageDirection Direction { get; set; }

        /// <summary>
        /// Time when the message was sent / received.
        /// </summary>
        public DateTime MessageTime { get; set; }

        /// <summary>
        /// Sender of the message.
        /// </summary>
        public string ParticipantName { get; set; }

        /// <summary>
        /// Sender of the message.
        /// </summary>
        public string ParticipantUri { get; set; }

        /// <summary>
        /// Modality of the message.
        /// </summary>
        public MessageModality Modality { get; set; }

        /// <summary>
        /// Content of the message.
        /// </summary>
        public string Message { get; set; }
    }
}
