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
            Console.WriteLine("[TM] Start a submit request");
            if (counter == 0)
            {
                Console.WriteLine("");
                this.registerStubs();
                Console.WriteLine("[TM] Registed stubs for another TM and LM");
                counter++;
            }
            
            // Initialization
            List<string> reads = request.Reads.ToList();
            List<string> keys = request.Keys.ToList();
            List<int> values = request.Values.ToList();
            List<string> results = new List<string>();
            if (!checkLeases(reads, keys))
            { 
                Console.WriteLine("[TM] Dont have the Lease for the request of the client => Build new request for LM");
                LearnRequest learnRequest = new LearnRequest();
                learnRequest.Tm = state.GetName();
                learnRequest.Leases.AddRange(reads.Concat(keys).Distinct());
                List<Task<LearnReply>> learnAwaitList = new List<Task<LearnReply>>();
                foreach (LearnServices.LearnServicesClient stub in stubsLM.Values)
                {
                    Console.WriteLine("[TM]         >>>> Sendind request for a LM");
                    learnAwaitList.Add(stub.LearnAsync(learnRequest).ResponseAsync);
                }
                Console.WriteLine("[TM] Waiting for every LM to respond");
                Task<LearnReply[]> waitTask = Task.WhenAll(learnAwaitList);
                await waitTask;
                Console.WriteLine("[TM] Waited for every LM with sucess");
                LearnReply[] learnResults = waitTask.Result;
                LearnReply learnResult = learnResults[0];


                // save our leases TODO Ver com stor (libertar sempre)
                Console.WriteLine("[TM] Reset previous Leases //TODO: Dont do this");
                leases = new List<string>();

                Console.WriteLine("[TM] Building the queue to respect LMs");
                buildQueue(learnResult);

                while (checkQueue(reads, keys))
                {
                    Console.WriteLine("[TM] Its not my turn yet so I will sleep until it is");
                    lock (state)
                    {
                        Monitor.Wait(state);
                    }
                }
                Console.WriteLine("[TM] It's my turn on the queue so I will do the read and write operations");

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
                Console.WriteLine("[TM] Read operations done with sucess: Build response for the client");
                // Write operation

                for (int i = 0; i < keys.Count; i++)
                {
                    state.SetValue(keys[i], values[i]);
                    state.queue[keys[i]].RemoveAt(0);
                }
                Console.WriteLine("[TM] Write operation done with success: Changed values");
            }

            
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
            Console.WriteLine("[TM] Broadcasted request for another TMs in order to replicate the state");
            SubmitReply reply = new SubmitReply();
            reply.Keys.AddRange(reads);
            reply.Values.AddRange(results);
            return reply;
        }

    }
}
