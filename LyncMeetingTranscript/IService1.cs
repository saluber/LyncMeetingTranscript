using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

namespace LyncMeetingTranscript
{
    [ServiceContract]
    public interface IService1
    {

        [OperationContract]
        string GetUsageOptions();

        [OperationContract]
        string StartMeetingTranscript(LyncMeetingTranscriptSessionInfo composite);

        [OperationContract]
        string StopMeetingTranscript(LyncMeetingTranscriptSessionInfo composite);
    }


    // Data contract for LyncMeetingTranscript service operations.
    [DataContract]
    public class LyncMeetingTranscriptSessionInfo
    {
        internal enum TransferType
        {
            Conversation = 1,
            Conference = 2
        }

        private uint m_transferType = 1;
        private string m_userUri;
        private string m_transferTargetUri;

        [DataMember]
        public string UserUri
        {
            get { return m_userUri; }
            set { m_userUri = value; }
        }

        /// <summary>
        /// User URI of the remote user to start a MeetingTranscriptSession with, in the format user@host or tel:+XXXYYYZZZZ
        /// </summary>
        [DataMember]
        public string TransferTargetURI
        {
            get { return m_transferTargetUri; }
            set { m_transferTargetUri = value; }
        }

        [DataMember]
        public uint TransferTypeValue
        {
            get { return m_transferType; }
            set { m_transferType = value; }
        }
    }
}
