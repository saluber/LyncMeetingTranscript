using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace LyncMeetingTranscriptBotApplication
{
    class Program
    {
        public TranscriptRecorderSessionManager _appSession = null;

        static void Main(string[] args)
        {
            Program program = new Program();
            program.Run();
        }

        void Run()
        {
            _appSession = new TranscriptRecorderSessionManager();
            _appSession.Run();

            Console.WriteLine("Program: Press any key to exit.");
            Console.ReadLine();
            _appSession.Shutdown();
        }

        // TODO: Use async correctly at Program/Main-level
        void RunAsync()
        {
            _appSession = new TranscriptRecorderSessionManager();
            Task t = Task.Factory.StartNew(() => _appSession.RunAsync(), TaskCreationOptions.LongRunning);
            /*
            Task t = Task.Factory.StartNew(()=>
                {
                    _appSession.RunAsync().Wait();
                }, Task.Factory.CancellationToken, TaskCreationOptions.LongRunning, Task.Factory.Scheduler);
            */
            while (true)
            {
                if (t.IsCompleted || t.IsCanceled || t.IsFaulted)
                {
                    break;
                }

                Thread.Sleep(5000);
            }

            //System.Console.WriteLine("MeetingTranscriptSession is now running. Press any key to stop, or session will automatically stop when first active conversation is terminated");
            /*while (!Console.ReadLine().Equals("quit", StringComparison.InvariantCultureIgnoreCase) && !t.IsCompleted)
            {
                Task.Delay(5000).Wait();
            }

            if (!t.IsCompleted)
            {
                t.Dispose();
            }
            */
            Console.WriteLine("Program: Press any key to exit.");
            Console.ReadLine();
            _appSession.Shutdown();
        }
    }
}
