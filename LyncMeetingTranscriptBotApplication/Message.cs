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

    public enum MessageType { Audio, Video, InstantMessage, ConversationInfo, ConferenceInfo, Error, Info }

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
        public MessageType MessageType { get; set; }

        /// <summary>
        /// Gets or sets the content.
        /// </summary>
        /// <value>The content.</value>
        public string Content { get; set; }

        /// <summary>
        /// Default constructors for error messages and TranscriptRecordingSessions "messages" (where there is no true "sender")
        /// </summary>
        /// <param name="content"></param>
        /// <param name="modality"></param>
        /// <param name="sessionId"></param>
        public Message(string content, string sessionId)
            : this(content, String.Empty, String.Empty, String.Empty, DateTime.Now, sessionId, String.Empty, MessageType.Info, MessageDirection.Outgoing)
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
        /// <param name="messageDirection"></param>
        public Message(string content, string senderDisplayName, string senderAlias, string senderUri, MessageType modality,
            string conversationId, MessageDirection messageDirection = MessageDirection.Incoming)
            : this(content, senderDisplayName, senderAlias, senderUri, DateTime.Now, conversationId, String.Empty, modality, messageDirection)
        {
        }

        /// <param name="content"></param>
        /// <param name="senderDisplayName"></param>
        /// <param name="senderAlias"></param>
        /// <param name="senderUri"></param>
        /// <param name="modality"></param>
        /// <param name="conversationId"></param>
        /// <param name="conferenceUri"></param>
        /// <param name="messageDirection"></param>
        public Message(string content, string senderDisplayName, string senderAlias, string senderUri,
            MessageType modality, string conversationId, string conferenceUri, MessageDirection messageDirection = MessageDirection.Incoming)
            : this(content, senderDisplayName, senderAlias, senderUri, DateTime.Now, conversationId, conferenceUri, modality, messageDirection)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="content"></param>
        /// <param name="modality"></param>
        /// <param name="conversationId"></param>
        public Message(string content, MessageType modality, string conversationId)
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
        public Message(string content, MessageType modality, string conversationId, string conferenceUri)
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
            string conversationId, string conferenceUri, MessageType modality, MessageDirection direction = MessageDirection.Incoming)
        {
            this.Content = content;
            this.SenderDisplayName = SenderDisplayName;
            this.SenderAlias = senderAlias;
            this.SenderUri = senderUri;
            this.TimeStamp = timeStamp;
            this.ConversationId = conversationId;
            this.ConferenceUri = conferenceUri;
            this.MessageType = modality;
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
            String s = "Timestamp: " + TimeStamp.ToString() + "\n";
            
            if (!String.IsNullOrEmpty(SenderDisplayName))
            { 
                s += "Sender: " + SenderDisplayName + " (" + SenderAlias + ")(" + SenderUri + ")\n"; 
            }
            if (!String.IsNullOrEmpty(ConversationId))
            {
                s += "Conversation Id: " + ConversationId + "\n";
            }
            if (!String.IsNullOrEmpty(ConferenceUri))
            {
                s += "Conference Uri: " + ConferenceUri + "\n";
            }
                
             s += "Direction: " + Direction.ToString() + "\n"
                + "Message Type: " + MessageType.ToString() + "\n"
                + "Message Content: " + Content + "\n"
                + "-----------------------------------------------\n";

            return s;
        }

        /// <summary>
        /// Formats Message into ContextualData format:
        /// [SenderDisplayName (senderAlias@senderHost.com)(sip:senderAlias@senderHost.com)][ConversationId:12345678][ConferenceUri:senderAlias@senderHost.com;conf][TImeStamp][MessageType][MessageDirection][MessageContent]
        /// </summary>
        /// <returns></returns>
        internal string ToTranscriptString()
        {
            String s = "[" + TimeStamp.ToString() + "]";

            if (!String.IsNullOrEmpty(ConferenceUri))
            {
                s = "[ConferenceUri:" + ConferenceUri + "]" + s;
            }
            else
            {
                s = "[ConferenceUri:null]" + s;
            }

            if (!String.IsNullOrEmpty(ConversationId))
            {
                s = "[ConversationId:" + ConversationId + "]" + s;
            }
            else
            {
                s = "[ConversationId:null]" + s;
            }

            if (!String.IsNullOrEmpty(SenderDisplayName))
            {
                s = "[" + SenderDisplayName + " (" + SenderAlias + ")(" + SenderUri + ")]" + s;
            }
            else
            {
                s = "[null]" + s;
            }

            s = "[" + MessageType.ToString() + "][" + Direction.ToString() + "]" + s;

            s += "[MessageContent:" + Content + "]\n";

            return s;
        }

        internal void Print()
        {
            Console.WriteLine(this.ToString());
        }
    }
}
