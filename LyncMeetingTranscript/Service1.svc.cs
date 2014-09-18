using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

namespace LyncMeetingTranscript
{
    // NOTE: In order to launch WCF Test Client for testing this service, please select Service1.svc or Service1.svc.cs at the Solution Explorer and start debugging.
    public class Service1 : IService1
    {
        public string GetUsageOptions()
        {
            return string.Format("Sample usage:\nStartMeetingTranscript(new LyncMeetingTranscriptSessionInfo(){UserUri = samil@microsoft.com, TransferTargetURI = personToCall@microsoft.com, TransferType = 1};)");
        }

        public string StartMeetingTranscript(LyncMeetingTranscriptSessionInfo meetingTranscriptSessionInfo)
        {
            if (meetingTranscriptSessionInfo == null)
            {
                throw new ArgumentNullException("meetingTranscriptSessionInfo");
            }
            if (String.IsNullOrEmpty(meetingTranscriptSessionInfo.UserUri))
            {
                throw new ArgumentNullException("UserUri");
            }
            if (String.IsNullOrEmpty(meetingTranscriptSessionInfo.TransferTargetURI))
            {
                throw new ArgumentNullException("TransferTargetURI");
            }

            // TODO: Start LyncMeetingTranscript session

            return string.Format("Starting LyncMeetingTranscript for UserUri {0} on {1}Uri {2}",
                meetingTranscriptSessionInfo.UserUri, (meetingTranscriptSessionInfo.TransferTypeValue == 2) ? "Conference" : "Conversation",
                meetingTranscriptSessionInfo.TransferTargetURI);
        }

        public string StopMeetingTranscript(LyncMeetingTranscriptSessionInfo meetingTranscriptSessionInfo)
        {
            if (meetingTranscriptSessionInfo == null)
            {
                throw new ArgumentNullException("meetingTranscriptSessionInfo");
            }
            if (String.IsNullOrEmpty(meetingTranscriptSessionInfo.UserUri))
            {
                throw new ArgumentNullException("UserUri");
            }
            if (String.IsNullOrEmpty(meetingTranscriptSessionInfo.TransferTargetURI))
            {
                throw new ArgumentNullException("TransferTargetURI");
            }

            // TODO: Stop LyncMeetingTranscript session

            return string.Format("Stopping LyncMeetingTranscript for UserUri {0} on {1}Uri {2}",
                meetingTranscriptSessionInfo.UserUri, (meetingTranscriptSessionInfo.TransferTypeValue == 2) ? "Conference" : "Conversation",
                meetingTranscriptSessionInfo.TransferTargetURI);
        }
    }
}
