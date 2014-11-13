using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LyncMeetingTranscriptBotApplication
{
    enum TraceLevel
    {
        Error = 1,
        Warn = 2,
        Info = 3,
        Verbose = 4
    }

    public static class Constants
    {
        #region Conversation Context Constants

        public const string ApplicationName = @"Lync Meeting Transcript";

        public const string ApplicationEndpointName = @"LyncBotApplication";

        public const string ApplicationId = @"5c25bcb7-4df6-4746-8b71-740ed37ab47f";

        public const string Toast = @"Lync Meeting Transcript session started!";

        public const string ApplicationInstallerPath = @"Lync Meeting Transcript";

        public const string SimpleLink = @"Lync Meeting Transcript";

        #endregion // Conversation Context Constants

        private static Random s_randomNumberGenerator = new Random();

        #region Utility Helper Methods
        public static byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length * sizeof(char)];
            System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public static Guid NextGuid()
        {
            byte[] bytes = new byte[16 /* GUID is 16-element byte array */];
            s_randomNumberGenerator.NextBytes(bytes);
            return new Guid(bytes);
        }

        #endregion // Utility Helper Methods
    }

    public static class NonBlockingConsole
    {
        private static BlockingCollection<string> m_Queue = new BlockingCollection<string>();

        static NonBlockingConsole()
        {
            var thread = new Thread(
              () =>
              {
                  while (true) Console.WriteLine(m_Queue.Take());
              });
            thread.IsBackground = true;
            thread.Start();
        }

        public static void WriteLine(string value)
        {
            m_Queue.Add(value);
        }

        public static void WriteLine(string value, object[] args)
        {
            m_Queue.Add(String.Format(value, args));
        }

        public static void WriteLine(string value, string args1)
        {
            m_Queue.Add(String.Format(value, args1));
        }

        public static void WriteLine(string value, string args1, string args2)
        {
            m_Queue.Add(String.Format(value, args1, args2));
        }

        public static void WriteLine(string value, string args1, string args2, string args3)
        {
            m_Queue.Add(String.Format(value, args1, args2, args3));
        }

        public static void WriteLine(string value, string args1, string args2, string args3, string args4, string args5)
        {
            m_Queue.Add(String.Format(value, args1, args2, args3, args4, args5));
        }
    }
}
