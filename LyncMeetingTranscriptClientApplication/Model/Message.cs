using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LyncMeetingTranscriptClientApplication.Model
{
    public enum MessageDirection { Incoming, Outgoing };

    public enum MessageModality { Audio, Video, InstantMessage, ConversationInfo, ConferenceInfo, Error }

    public class Message
    {
        /// <summary>
        /// Display name of the message sender.
        /// </summary>
        public string SenderDisplayName { get; set; }

        /// <summary>
        /// Alias of the message sender.
        /// </summary>
        public string SenderAlias { get; set; }

        // TODO: Remove this or SenderAlias field
        /// <summary>
        /// Uri of the message sender.
        /// </summary>
        public string SenderUri { get; set; }

        /// <summary>
        /// Time when the message was sent / received.
        /// </summary>
        public DateTime TimeStamp { get; set; }

        /// <summary>
        /// Gets or sets the conversation id.
        /// </summary>
        /// <value>
        /// The conversation id.
        /// </value>
        public string ConversationId { get; set; }

        /// <summary>
        /// Gets or sets the conference URI.
        /// </summary>
        /// <value>
        /// The conference URI
        /// </value>
        public string ConferenceUri { get; set; }

        /// <summary>
        /// Direction of the message.
        /// </summary>
        public MessageDirection Direction { get; set; }

        /// <summary>
        /// Modality of the message.
        /// </summary>
        public MessageModality Modality { get; set; }

        /// <summary>
        /// Content of the message.
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="content"></param>
        /// <param name="senderDisplayName"></param>
        /// <param name="senderAlias"></param>
        /// <param name="senderUri"></param>
        /// <param name="modality"></param>
        /// <param name="conversationId"></param>
        /// <param name="messageDirection"></param>
        public Message(string content, string senderDisplayName, string senderAlias, string senderUri, MessageModality modality,
            string conversationId, MessageDirection messageDirection = MessageDirection.Incoming)
            : this(content, senderDisplayName, senderAlias, senderUri, DateTime.Now, conversationId, String.Empty, modality, messageDirection)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="content"></param>
        /// <param name="senderDisplayName"></param>
        /// <param name="senderAlias"></param>
        /// <param name="senderUri"></param>
        /// <param name="modality"></param>
        /// <param name="conversationId"></param>
        /// <param name="conferenceUri"></param>
        /// <param name="messageDirection"></param>
        public Message(string content, string senderDisplayName, string senderAlias, string senderUri,
            MessageModality modality, string conversationId, string conferenceUri, MessageDirection messageDirection = MessageDirection.Incoming)
            : this(content, senderDisplayName, senderAlias, senderUri, DateTime.Now, conversationId, conferenceUri, modality, messageDirection)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="content"></param>
        /// <param name="modality"></param>
        /// <param name="conversationId"></param>
        public Message(string content, MessageModality modality, string conversationId)
            : this(content, String.Empty, String.Empty, String.Empty, DateTime.Now, conversationId, String.Empty, modality, MessageDirection.Incoming)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="content"></param>
        /// <param name="modality"></param>
        /// <param name="conversationId"></param>
        /// <param name="conferenceUri"></param>
        public Message(string content, MessageModality modality, string conversationId, string conferenceUri)
            : this(content, String.Empty, String.Empty, String.Empty, DateTime.Now, conversationId, conferenceUri, modality, MessageDirection.Incoming)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Message"/> class.
        /// </summary>
        /// <param name="content">The content.</param>
        /// <param name="senderDisplayName">Display name of the sender.</param>
        /// <param name="senderAlias">The sender alias.</param>
        /// <param name="timeStamp">The timestamp.</param>
        /// <param name="conversationId">The conversation id.</param>
        /// <param name="conferenceUri">The conference URI.</param>
        public Message(string content, string senderDisplayName, string senderAlias, string senderUri, DateTime timeStamp,
            string conversationId, string conferenceUri, MessageModality modality, MessageDirection direction = MessageDirection.Incoming)
        {
            this.Content = content;
            this.SenderDisplayName = SenderDisplayName;
            this.SenderAlias = senderAlias;
            this.SenderUri = senderUri;
            this.TimeStamp = timeStamp;
            this.ConversationId = conversationId;
            this.ConferenceUri = conferenceUri;
            this.Modality = modality;
            this.Direction = direction;
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            String s =
                "Timestamp: " + TimeStamp.ToShortTimeString() + "\n"
                + "Sender: " + SenderDisplayName + " (" + SenderAlias + ")\n"
                + "Conversation Id: " + ConversationId + "\n"
                + "Conference Uri: " + ConferenceUri + "\n"
                + "Direction: " + Direction.ToString() + "\n"
                + "Modality: " + Modality.ToString() + "\n"
                + "Message Content: " + Content + "\n"
                + "-----------------------------------------------\n";

            return s;
        }

        internal string ToTranscriptString()
        {
            String s =
                "[" + SenderDisplayName + " (" + SenderAlias + ")]"
                + "[" + Modality.ToString() + "]"
                + "[" + TimeStamp.ToShortTimeString() + "]"
                + ": " + Content + "\n";

            return s;
        }

        internal void Print()
        {
            Console.WriteLine(this.ToString());
        }
    }
}
