using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LyncMeetingTranscriptBotApplication
{
    public static class Constants
    {
        #region Conversation Context Constants

        public const string ApplicationName = "Lync Meeting Transcript";

        public const string ApplicationId = "5c25bcb7-4df6-4746-8b71-740ed37ab47f";

        public const string Toast = "Lync Meeting Transcript";

        public const string ApplicationInstallerPath = "Lync Meeting Transcript";

        public const string SimpleLink = "Lync Meeting Transcript";

        #endregion // Conversation Context Constants

        #region Utility Helper Methods
        public static byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length * sizeof(char)];
            System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }
        #endregion // Utility Helper Methods
    }
}
