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
        public MeetingTranscriptSession _appSession = null;

        static void Main(string[] args)
        {
            Program program = new Program();
            program.Run();
        }

        void Run()
        {
            _appSession = new MeetingTranscriptSession();
            _appSession.Run();
        }

        void RunAsync()
        {
            _appSession = new MeetingTranscriptSession();
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
            _appSession.Shutdown();
        }
    }
}
