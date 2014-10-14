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

        public const string ApplicationId = "97AD7B8A-3220-4855-8D1E-E70BB0973C4D";

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
