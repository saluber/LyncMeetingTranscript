using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace LyncMeetingTranscriptBotApplication
{
    class Program
    {
        private static TranscriptRecorderSessionManager _appSession = null;

        static void Main(string[] args)
        {
            // Init Transcript Recorder Session Manager
            _appSession = new TranscriptRecorderSessionManager();

            // Cancel token to terminate program early on cntrl + c
            CancellationTokenSource cts = new CancellationTokenSource();
            System.Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            try
            {
                _appSession.RunAsync(cts.Token).Wait();
            }
            catch (Exception e)
            {
                NonBlockingConsole.WriteLine("TranscriptRecorderSessionManager exited with exception: " + e.ToString());
            }
            finally
            {
                Task shutdownTask = _appSession.ShutdownAsync();
                shutdownTask.Wait();
            }
        }
    }
}
