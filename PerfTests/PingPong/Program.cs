﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Management;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using System.Threading.Tasks;
using Stacks;
using Stacks.Actors;

// Code has been ported from Akka.Net library https://github.com/akkadotnet/akka.net

namespace PingPong
{
    public class Program
    {
        public static uint CpuSpeed()
        {
#if !mono
            var mo = new ManagementObject("Win32_Processor.DeviceID='CPU0'");
            var sp = (uint)(mo["CurrentClockSpeed"]);
            mo.Dispose();
            return sp;
#else
            return 0;
#endif
        }

        private static void Main(params string[] args)
        {
            uint timesToRun = args.Length == 1 ? uint.Parse(args[0]) : 1u;
            Start(timesToRun);
            Console.ReadKey();
        }

        private static async void Start(uint timesToRun)
        {
            const int repeatFactor = 5;
            const long repeat = 30000L * repeatFactor;

            var processorCount = Environment.ProcessorCount;
            if (processorCount == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to read processor count..");
                return;
            }

            int workerThreads;
            int completionPortThreads;
            ThreadPool.GetAvailableThreads(out workerThreads, out completionPortThreads);

            Console.WriteLine("Worker threads:         {0}", workerThreads);
            Console.WriteLine("OSVersion:              {0}", Environment.OSVersion);
            Console.WriteLine("ProcessorCount:         {0}", processorCount);
            Console.WriteLine("ClockSpeed:             {0} MHZ", CpuSpeed());
            Console.WriteLine("Actor Count:            {0}", processorCount * 2);
            Console.WriteLine("Messages sent/received: {0}  ({0:0e0})", GetTotalMessagesReceived(repeat));
            Console.WriteLine();

            //Warm up
            //Console.Write("Local     first start time: ");
            //await Benchmark(1, 1, 1, PrintStats.StartTimeOnly, -1, -1,
            //    (idx, wfs) => Tuple.Create((IActorServerProxy)null, new Destination(wfs)),
            //    (idx, d) => d,
            //    (idx, wfs, dest, r, latch) => new PingPongActor(wfs, dest, r, latch));
            //Console.WriteLine(" ms");
            //Console.Write("Remote    first start time: ");
            //await Benchmark(1, 1, 1, PrintStats.StartTimeOnly, -1, -1,
            //    (idx, wfs) =>
            //    {
            //        var dest = new Destination(wfs);
            //        return Tuple.Create(ActorServerProxy.Create("tcp://localhost:" + (54000 + idx), dest), dest);
            //    },
            //    (idx, d) => ActorClientProxy.CreateActor<IDestination>("tcp://localhost:" + (54000 + idx)).Result,
            //    (idx, wfs, dest, r, latch) => new PingPongActor(wfs, dest, r, latch));
            //Console.WriteLine(" ms");
            //Console.WriteLine();

            Console.WriteLine("         Local actor                        Remote actor");
            Console.WriteLine("Thruput, Msgs/sec, Start [ms], Total [ms],  Msgs/sec, Start [ms], Total [ms]");
            for (var i = 0; i < timesToRun; i++)
            {
                var redCountLocalActor = 0;
                var redCountRemoteActor = 0;
                var bestThroughputLocalActor = 0L;
                var bestThroughputRemoteActor = 0L;
                foreach (var throughput in GetThroughputSettings())
                {
                    var result1 = await Benchmark(throughput, processorCount, repeat, PrintStats.LineStart | PrintStats.Stats, bestThroughputLocalActor, redCountLocalActor,
                       (idx, wfs) => Tuple.Create((IActorServerProxy)null, new Destination(wfs)),
                       (idx, d) => d,
                       (idx, wfs, dest, r, latch) => new PingPongActor(wfs, dest, r, latch));

                    bestThroughputLocalActor = result1.Item2;
                    redCountLocalActor = result1.Item3;
                    Console.Write(",  ");

                    var result2 = await Benchmark(throughput, processorCount, repeat, PrintStats.Stats, bestThroughputRemoteActor, redCountRemoteActor,
                        (idx, wfs) =>
                        {
                            var dest = new Destination(wfs);
                            return Tuple.Create(ActorServerProxy.Create("tcp://localhost:" + (54000 + idx), dest), dest);
                        },
                        (idx, d) => ActorClientProxy.CreateActor<IDestination>("tcp://localhost:" + (54000 + idx)).Result,
                        (idx, wfs, dest, r, latch) => new PingPongActor(wfs, dest, r, latch));
                    bestThroughputRemoteActor = result2.Item2;
                    redCountRemoteActor = result2.Item3;
                    Console.WriteLine();
                }
            }

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Done..");
        }

        public static IEnumerable<int> GetThroughputSettings()
        {
            yield return 1;
            yield return 5;
            yield return 10;
            yield return 15;
            for (int i = 20; i < 100; i += 10)
            {
                yield return i;
            }
            for (int i = 100; i < 1000; i += 100)
            {
                yield return i;
            }
        }

        private static async Task<Tuple<bool, long, int>> Benchmark(int factor, int numberOfClients, long numberOfRepeats, PrintStats printStats, long bestThroughput, int redCount,
            Func<int, WaitForStarts, Tuple<IActorServerProxy, Destination>> createDestination,
            Func<int, Destination, IDestination> createDestClient,
            Func<int, WaitForStarts, IDestination, long, TaskCompletionSource<bool>, PingPongActor> createPingPong)
        {
            var totalMessagesReceived = GetTotalMessagesReceived(numberOfRepeats);
            //times 2 since the client and the destination both send messages
            long repeatsPerClient = numberOfRepeats / numberOfClients;
            var totalWatch = Stopwatch.StartNew();

            var countdown = new CountdownEvent(numberOfClients * 2);
            var waitForStartsActor = new WaitForStarts(countdown);
            var clients = new List<PingPongActor>();
            var dests = new List<Tuple<IActorServerProxy, Destination>>();
            var tasks = new List<Task>();
            for (int i = 0; i < numberOfClients; i++)
            {
                var destination = createDestination(i, waitForStartsActor);
                dests.Add(destination);

                var ts = new TaskCompletionSource<bool>();
                tasks.Add(ts.Task);
                var client = createPingPong(i, waitForStartsActor, createDestClient(i, destination.Item2), repeatsPerClient, ts);
                clients.Add(client);

                client.Start();
                destination.Item2.Start();
            }
            if (!countdown.Wait(TimeSpan.FromSeconds(10)))
            {
                Console.WriteLine("The system did not start in 10 seconds. Aborting.");
                return Tuple.Create(false, bestThroughput, redCount);
            }
            var setupTime = totalWatch.Elapsed;
            var sw = Stopwatch.StartNew();
            clients.ForEach(c => c.Ping());

            await Task.WhenAll(tasks.ToArray());
            sw.Stop();

            totalWatch.Stop();

            dests.ForEach(d => { if (d.Item1 != null) d.Item1.Stop(); });
            clients.ForEach(c => c.Stop());

            var elapsedMilliseconds = sw.ElapsedMilliseconds;
            long throughput = elapsedMilliseconds == 0 ? -1 : totalMessagesReceived / elapsedMilliseconds * 1000;
            var foregroundColor = Console.ForegroundColor;
            if (throughput >= bestThroughput)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                bestThroughput = throughput;
                redCount = 0;
            }
            else
            {
                redCount++;
                Console.ForegroundColor = ConsoleColor.Red;
            }
            if (printStats.HasFlag(PrintStats.StartTimeOnly))
            {
                Console.Write("{0,5}", setupTime.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture));
            }
            else
            {
                if (printStats.HasFlag(PrintStats.LineStart))
                    Console.Write("{0,7}, ", factor);
                if (printStats.HasFlag(PrintStats.Stats))
                    Console.Write("{0,8}, {1,10}, {2,10}", throughput, setupTime.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture), totalWatch.Elapsed.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture));
            }
            Console.ForegroundColor = foregroundColor;

            return Tuple.Create(redCount <= 3, bestThroughput, redCount);
        }

        private static long GetTotalMessagesReceived(long numberOfRepeats)
        {
            return numberOfRepeats * 2;
        }

        public interface IDestination
        {
            Task Pong();
        }

        public class Destination : IDestination
        {
            private ActorContext context = new ActorContext();
            private WaitForStarts waitForStartsActor;

            public Destination(WaitForStarts waitForStartsActor)
            {
                this.waitForStartsActor = waitForStartsActor;
            }

            public async Task Pong()
            {
                await context;
                return;
            }

            public void Start()
            {
                waitForStartsActor.OnStart();
            }
        }

        public class WaitForStarts
        {
            private ActorContext context;
            private readonly CountdownEvent _countdown;

            public WaitForStarts(CountdownEvent countdown)
            {
                _countdown = countdown;
                context = new ActorContext();
            }

            public async void OnStart()
            {
                await context;
                _countdown.Signal();
            }
        }

        [Flags]
        public enum PrintStats
        {
            No = 0,
            LineStart = 1,
            Stats = 2,
            StartTimeOnly = 32768,
        }



        public class PingPongActor
        {
            private ActorContext context = new ActorContext();

            private IDestination destination;
            private WaitForStarts waitForStartsActor;
            private long repeat;
            private long sent;
            private long received;
            private TaskCompletionSource<bool> latch;

            public PingPongActor(WaitForStarts waitForStartsActor, IDestination destination, long repeat, TaskCompletionSource<bool> latch)
            {
                this.waitForStartsActor = waitForStartsActor;
                this.destination = destination;
                this.repeat = repeat;
                this.latch = latch;
            }

            public void Start()
            {
                waitForStartsActor.OnStart();
            }

            public async void Ping()
            {
                await context;

                for (int i = 0; i < Math.Min(1000, repeat); ++i)
                {
                    PingImpl();
                }

            }

            private async void PingImpl()
            {
                await context;
                await destination.Pong();
                ++sent;
                Recv();
            }

            public async void Recv()
            {
                await context;
                ++received;
                if (sent < repeat)
                {
                    await destination.Pong();
                    ++sent;
                    Recv();
                }
                else if (received == repeat)
                {
                    latch.SetResult(true);
                }
            }

            public void Stop()
            {
                var d = destination as IActorClientProxy<Destination>;

                if (d != null)
                    d.Close();
            }
        }

    }

}