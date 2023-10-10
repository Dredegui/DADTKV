using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization.Formatters;
using System.Text;
using System.Threading.Tasks;

namespace TransactionManager
{
    public class ClientServicesImpl : TransactionServices.TransactionServicesBase
    {
        private ServerState state;
        private Dictionary<string, BroadcastServices.BroadcastServicesClient> stubsTM = new Dictionary<string, BroadcastServices.BroadcastServicesClient> ();
        private Dictionary<string, LearnServices.LearnServicesClient> stubsLM = new Dictionary<string, LearnServices.LearnServicesClient> ();
        private List<string> leases = new List<string> ();
        private int transcationId;
        private int counter = 0;
        private List<string> names;
        private List<string> urls;
        private List<int> types;

        public ClientServicesImpl(ServerState state, List<string> names, List<string> urls, List<int> types)
        {
            this.state = state;
            transcationId = 0;
            this.names = names;
            this.urls = urls;
            this.types = types;
        }

        public override Task<SubmitReply> Submit(SubmitRequest request, ServerCallContext context)
        {
            return TxSubmit(request);
        }

        public void registerStubs()
        {
            for (int i = 0; i < names.Count; i++)
            {
                GrpcChannel channel = GrpcChannel.ForAddress(urls[i]);

                if (types[i] == 0)
                {
                    // Transaction Managers stub
                    stubsTM[names[i]] = new BroadcastServices.BroadcastServicesClient(channel);
                }
                else
                {
                    // Lease Managers stub
                    stubsLM[names[i]] = new LearnServices.LearnServicesClient(channel);
                }

            }
        }


        public bool checkLeases(List<string> reads, List<string> keys) {
            return !reads.Except(leases).Any() && !keys.Except(leases).Any();
        }

        public void buildQueue(LearnReply reply)
        {
            foreach (LearnRequest lr in reply.Values)
            {
                foreach (string lease in lr.Leases)
                {
                    state.queue[lease].Add(lr.Tm);
                }
            }
        }

        public bool checkQueue(List<string> reads, List<string> keys)
        {
            foreach (string read in reads)
            {
                if (state.queue[read][0] != state.GetName())
                {
                    return false;
                }
            }

            foreach (string key in keys)
            {
                if (state.queue[key][0] != state.GetName())
                {
                    return false;
                }
            }

            return true;
        }

        public async Task<SubmitReply> TxSubmit(SubmitRequest request)
        {
            Console.WriteLine("Start submit");
            if (counter == 0)
            {
                Console.WriteLine("");
                this.registerStubs();
                counter++;
            }
            Console.WriteLine("Registed stubs");
            // Initialization
            List<string> reads = request.Reads.ToList();
            List<string> keys = request.Keys.ToList();
            List<int> values = request.Values.ToList();
            List<string> results = new List<string>();
            if (!checkLeases(reads, keys))
            {
                LearnRequest learnRequest = new LearnRequest();
                learnRequest.Tm = state.GetName();
                learnRequest.Leases.AddRange(reads.Concat(keys).Distinct());
                LearnServices.LearnServicesClient specStub = stubsLM["lm0"];
                TestRequest testRequest = new TestRequest();
                testRequest.Sent = true;
                TestReply rep = specStub.Test(testRequest);
                Console.WriteLine(rep.Ack);
                List<Task<LearnReply>> learnAwaitList = new List<Task<LearnReply>>();
                foreach (LearnServices.LearnServicesClient stub in stubsLM.Values)
                {
                    learnAwaitList.Add(stub.LearnAsync(learnRequest).ResponseAsync);
                }
                Console.WriteLine("MID LEARN BEFORE AWAIT");
                Task<LearnReply[]> waitTask = Task.WhenAll(learnAwaitList);
                await waitTask;
                Console.WriteLine("MID LEARN AFTER AWAIT");
                LearnReply[] learnResults = waitTask.Result;
                Console.WriteLine("MID LEARN AFTER RESULT");
                LearnReply learnResult = learnResults[0];
                Console.WriteLine("MID LEARN AFTER EXTRACT");

                // save our leases TODO Ver com stor (libertar sempre)
                leases = new List<string>();

                buildQueue(learnResult);

                while (checkQueue(reads, keys))
                {
                    lock (state)
                    {
                        Monitor.Wait(state);
                    }
                }

            }
            lock (state)
            {
                foreach (string read in reads)
                {
                    if (state.ValidKey(read))
                    {
                        results.Add(state.GetValue(read));
                        state.queue[read].RemoveAt(0);
                    }
                    else
                    {
                        results.Add("unknown DadInt");
                    }
                }
                // Write operation

                for (int i = 0; i < keys.Count; i++)
                {
                    state.SetValue(keys[i], values[i]);
                    state.queue[keys[i]].RemoveAt(0);
                }
            }

            Console.WriteLine("Changed values");
            BroadcastMessage message = new BroadcastMessage();
            message.Id = transcationId;
            message.Name = state.GetName();
            message.Keys.AddRange(keys);
            message.Values.AddRange(values);
            transcationId++;
            foreach (var stub in stubsTM.Values)
            {

                stub.Broadcast(message); // TODO async?
            }
            Console.WriteLine("Broadcasted");
            SubmitReply reply = new SubmitReply();
            reply.Keys.AddRange(reads);
            reply.Values.AddRange(results);
            return reply;
        }

    }
}
