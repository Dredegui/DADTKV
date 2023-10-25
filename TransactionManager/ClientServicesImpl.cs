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
        
        public bool removeFromQueue(List<string> reads, List<string> keys)
        {
            foreach (string read in reads)
            {
                state.queue[read].RemoveAt(0);
                if (state.queue[read].Count == 0 && !leases.Contains(read))
                {
                    leases.Add(read);
                }
            }

            foreach (string key in keys)
            {
                if (state.queue[key][0] == state.GetName())
                {
                    state.queue[key].RemoveAt(0);
                    if (state.queue[key].Count == 0 && !leases.Contains(key))
                    {
                        leases.Add(key);
                    }
                }
            }

            return true;
        }

        private async void TimerThread(Object transLeases)
        {
            List<string> transactionLeases = (List<string>)transLeases;
            Dictionary<string, List<string>> shallowCopy = new Dictionary<string, List<string>>(state.queue);
            // Simulate some work in the function thread.
            Thread.Sleep(500);
            bool diff = false;
            LeaseUpdateRequest request = new LeaseUpdateRequest();
            foreach (string key in shallowCopy.Keys)
            {
                if (state.queue[key].Count < shallowCopy[key].Count)
                {
                    diff = true;
                }
            }
            foreach (string lease in transactionLeases)
            {
                request.Leases.Add(lease);
                request.Lenghts.Add(shallowCopy[lease].Count);
            }
            if (!diff)
            {
                Console.WriteLine("[" + state.GetName() + "] Timer handling started");
                // Broadcast leases to see if there is an invalid tm
                List<Task<LeaseUpdateReply>> updateAwaitList = new List<Task<LeaseUpdateReply>>();
                foreach (BroadcastServices.BroadcastServicesClient stub in stubsTM.Values)
                {
                    Console.WriteLine("[" + state.GetName() + "] Sending lease request for a TM");
                    updateAwaitList.Add(stub.LeaseUpdateAsync(request).ResponseAsync);
                }
                Console.WriteLine("[" + state.GetName() + "] Waiting for every TM to respond");
                Task<LeaseUpdateReply[]> waitUpdate = Task.WhenAll(updateAwaitList);
                await waitUpdate;
                LeaseUpdateReply[] updateResults = waitUpdate.Result;
                bool removeLease = true;
                foreach (LeaseUpdateReply update in updateResults)
                {
                    if (update.Ack)
                    {
                        removeLease = false;
                        foreach (Queue temp in update.Update)
                        {
                            if (state.queue[temp.Lease].Count > temp.Tms.Count)
                            {
                                state.queue[temp.Lease] = temp.Tms.ToList();
                            }
                        }
                    }
                }
                if (removeLease)
                {
                    foreach (string lease in request.Leases)
                    {
                        state.queue[lease].RemoveAt(0);
                    }
                }
                // The function thread can pulse and wake up the main thread.
                lock (state)
                {
                    Monitor.PulseAll(state);
                }
            }
            else
            {
                Console.WriteLine("[" + state.GetName() + "] Already received transaction broadcast");
            }
            
        }


        public void printQueue(string pos)
        {
            foreach (string key in state.queue.Keys)
            {
                string vals = "";
                foreach (string val in state.queue[key])
                {
                    vals += val + ", ";
                }
                Console.WriteLine("[" + state.GetName() + "]" + pos + "key: " + key + "| list: " + vals);
            }
        }

        public async Task<SubmitReply> TxSubmit(SubmitRequest request)
        {
            Console.WriteLine("[" + state.GetName() + "] Start a submit request");
            if (counter == 0)
            {
                Console.WriteLine("");
                this.registerStubs();
                Console.WriteLine("[" + state.GetName() + "] Registed stubs for another TM and LM");
                counter++;
            }
            
            // Initialization
            List<string> reads = request.Reads.ToList();
            List<string> keys = request.Keys.ToList();
            List<int> values = request.Values.ToList();
            List<string> results = new List<string>();
            List<string> transactionLeases = reads.Concat(keys).Distinct().ToList();
            if (!checkLeases(reads, keys))
            { 
                Console.WriteLine("[" + state.GetName() + "] Dont have the Lease for the request of the client => Build new request for LM");
                LearnRequest learnRequest = new LearnRequest();
                learnRequest.Tm = state.GetName();
                learnRequest.Leases.AddRange(transactionLeases);
                List<Task<LearnReply>> learnAwaitList = new List<Task<LearnReply>>();
                foreach (LearnServices.LearnServicesClient stub in stubsLM.Values)
                {
                    Console.WriteLine("[" + state.GetName() + "]         >>>> Sendind request for a LM");
                    learnAwaitList.Add(stub.LearnAsync(learnRequest).ResponseAsync);
                }
                Console.WriteLine("[" + state.GetName() + "] Waiting for every LM to respond");
                Task<LearnReply[]> waitTask = Task.WhenAll(learnAwaitList);
                await waitTask;
                Console.WriteLine("[" + state.GetName() + "] Waited for learn ACKS");
                LearnReply[] learnResults = waitTask.Result;
                // Wait for lease inform broadcast
                lock (state)
                {
                    Monitor.Wait(state);
                }
                // save our leases TODO Ver com stor (libertar sempre)
                Console.WriteLine("[" + state.GetName() + "] Reset previous Leases //TODO: Dont do this");
                leases = new List<string>();

                Console.WriteLine("[" + state.GetName() + "] Building the queue to respect LMs");
                
                while (!checkQueue(reads, keys))
                {
                    Thread functionThread = new Thread(TimerThread);
                    functionThread.Start(transactionLeases);
                    Console.WriteLine("[" + state.GetName() + "] Its not my turn yet so I will sleep until it is");
                    lock (state)
                    {
                        Monitor.Wait(state);
                    }
                }
                printQueue(" BEFORE REMOVE KEYS QUEUE ");
                removeFromQueue(reads, keys); // TODO SAME LEASES ON 2 TRANSACTIONS ON SAME TM
                printQueue(" AFTER REMOVE KEYS QUEUE ");
                Console.WriteLine("[" + state.GetName() + "] It's my turn on the queue so I will do the read and write operations");

            }
            lock (state)
            {
                foreach (string read in reads)
                {
                    if (state.ValidKey(read))
                    {
                        results.Add(state.GetValue(read));
                    }
                    else
                    {
                        results.Add("unknown DadInt");
                    }
                }
                Console.WriteLine("[" + state.GetName() + "] Read operations done with sucess: Build response for the client");
                // Write operation

                for (int i = 0; i < keys.Count; i++)
                {
                    state.SetValue(keys[i], values[i]);
                }
                Console.WriteLine("[" + state.GetName() + "] Write operation done with success: Changed values");
            }

            
            BroadcastMessage message = new BroadcastMessage();
            message.Id = transcationId;
            message.Name = state.GetName();
            message.Keys.AddRange(keys);
            message.Values.AddRange(values);
            message.Reads.AddRange(reads);
            transcationId++;
            Console.WriteLine("[" + state.GetName() + "] Broadcast to other tms");
            foreach (var stub in stubsTM.Values)
            {

                stub.BroadcastAsync(message); // TODO save async calls and wait for them
            }
            Console.WriteLine("[TM] Broadcasted request for another TMs in order to replicate the state");
            SubmitReply reply = new SubmitReply();
            reply.Keys.AddRange(reads);
            reply.Values.AddRange(results);
            return reply;
        }

    }
}
