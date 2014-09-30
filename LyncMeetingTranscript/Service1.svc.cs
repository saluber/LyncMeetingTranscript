using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

namespace LyncMeetingTranscript
{
    public class LyncMeetingTranscriptService : ILyncMeetingTranscriptService
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

            return string.Format("Starting LyncMeetingTranscriptService for UserUri {0} on {1}Uri {2}",
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

            return string.Format("Stopping LyncMeetingTranscriptService for UserUri {0} on {1}Uri {2}",
                meetingTranscriptSessionInfo.UserUri, (meetingTranscriptSessionInfo.TransferTypeValue == 2) ? "Conference" : "Conversation",
                meetingTranscriptSessionInfo.TransferTargetURI);
        }
    }
}
