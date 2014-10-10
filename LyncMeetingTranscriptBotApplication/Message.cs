using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace LyncMeetingTranscriptBotApplication
{
    public enum MessageDirection { Incoming, Outgoing };

    public enum MessageModality { Audio, Video, InstantMessage, ConversationInfo, ConferenceInfo, Error }

    public class Message
    {
        /// <summary>
        /// Gets or sets the display name of the sender.
        /// </summary>
        /// <value>The display name of the sender.</value>
        public string SenderDisplayName { get; set; }

        /// <summary>
        /// Gets or sets the sender alias.
        /// </summary>
        /// <value>The sender alias.</value>
        public string SenderAlias { get; set; }
        
        // TODO: Remove this or SenderAlias field
        /// <summary>
        /// Gets or sets the sender alias.
        /// </summary>
        /// <value>The sender alias.</value>
        public string SenderUri { get; set; }

        /// <summary>
        /// Gets or sets the sender timestamp.
        /// </summary>
        /// <value>When the message was sent.</value>
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
        /// The conference URI.
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
        /// Gets or sets the content.
        /// </summary>
        /// <value>The content.</value>
        public string Content { get; set; }

        // TODO: Default constructors for error messages and conversation/conference "messages"
        // (where there is no true "sender")

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
            : this(content, String.Empty, String.Empty, String.Empty, DateTime.Now, conversationId, String.Empty, modality, MessageDirection.Outgoing)
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
            : this(content, String.Empty, String.Empty, String.Empty, DateTime.Now, conversationId, conferenceUri, modality, MessageDirection.Outgoing)
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

        // TODO: Print messages should take MessageModality into account
        // Conversation/conversation/error messages should omit "sender" properties
        // and use conversation id or conference Uri instead

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
