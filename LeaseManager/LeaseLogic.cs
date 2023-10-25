using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace LeaseManager
{
    internal class LeaseLogic
    {
        private LeaseState state;
        private Dictionary<string, PaxosConsensusServices.PaxosConsensusServicesClient> stubsLM = new Dictionary<string, PaxosConsensusServices.PaxosConsensusServicesClient>();
        
        private List<string> urls;
        private List<string> names;
        private List<int> types;
        private int numLM;
        private int id;
        private int numSlots;
        private int slotDuration;


        public LeaseLogic(LeaseState state, List<string> urls, List<string> names, List<int> types, int numLM, int id, int numSlots,int slotDuration)
        {
            this.state = state;
            this.urls = urls;
            this.types = types;
            this.names = names;
            this.numLM = numLM;
            this.id = id;
            this.numSlots = numSlots;
            this.slotDuration = slotDuration;
        }

        public void registerStubs()
        {
            Console.WriteLine("[LM] REGISTERING NEW STUBS => Num lm:" + numLM + " || Names count: " + names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                GrpcChannel channel = GrpcChannel.ForAddress(urls[i]);
                if (types[i] == 0)
                {
                    stubsLM[names[i]] = new PaxosConsensusServices.PaxosConsensusServicesClient(channel);
                }
                else
                {
                    state.stubsTM[names[i]] = new LeaseInformServices.LeaseInformServicesClient(channel);
                }
            }
            Console.WriteLine("End register");
        }

        public async void StartPaxos()
        {
            Console.WriteLine("[LM LEADER] Building prepare request");
            PrepareRequest replyRequest = new PrepareRequest();
            int currentEpoch = state.GetEpoch();
            replyRequest.ProposedRound = currentEpoch;
            int counter = 0;
            List<Task<PrepareReply>> replyAwaitList = new List<Task<PrepareReply>>();
            foreach (string name in stubsLM.Keys)
            {
                Console.WriteLine("[LM LEADER] Sending async PREPARE REQUESTS for every LM " + name);
                replyAwaitList.Add(stubsLM[name].PrepareAsync(replyRequest).ResponseAsync);
                // Use prepare reply info 
            }
            Console.WriteLine("[LM LEADER] Waiting for every prepare reply => Waiting for every LM");

            Task<PrepareReply[]> waitTask = Task.WhenAll(replyAwaitList);
            await waitTask;
            Console.WriteLine("[LM LEADER] Waited for every process with sucess");
            PrepareReply[] prepareResults = waitTask.Result;
            List<PrepareReply> replyResults = prepareResults.ToList();
            foreach (PrepareReply reply in replyResults)
            {
                Console.WriteLine("[LM LEADER] Check if we have the majoraty");
                if (reply.Promise)
                {
                    counter++;
                }
            }
            Console.WriteLine("[LM LEADER] Id: " + id + "Counter: " + counter + " | Number of lms: " + numLM);
            float floatCheck= counter / (numLM + 0.0F);
            if (floatCheck > 0.5)
            {
                Console.WriteLine("[LM LEADER] Id: " + id + " | HAS MAJORITY => Start accept request");
                // Start accept requests
                // Build request
                AcceptRequest acceptRequest = new AcceptRequest();
                acceptRequest.ProposedRound = currentEpoch;
                List<LeaseTransaction> commitedOrder = state.GetProposedLeases();
                Console.WriteLine("[LM LEADER] Building the accepted list for other LMs");
                foreach (LeaseTransaction lt in commitedOrder)
                {
                    Request request = new Request();
                    request.Tm = lt.tm;
                    request.Leases.AddRange(lt.leases);
                    acceptRequest.Values.Add(request);
                }
                Console.WriteLine("[LM LEADER] Sending the accepted list for other LM");
                // Build response await list
                List<Task<AcceptReply>> acceptAwaitList = new List<Task<AcceptReply>>();
                foreach (string name in stubsLM.Keys)
                {
                    acceptAwaitList.Add(stubsLM[name].AcceptAsync(acceptRequest).ResponseAsync);
                    // Use prepare reply info 
                }

                Console.WriteLine("[LM LEADER] Waiting for every LM to accept my accepted list");
                await Task.WhenAll(acceptAwaitList);
                Console.WriteLine("[LM LEADER] Every one responded => Let's check every one accepted");
                List<AcceptReply> acceptResults = acceptAwaitList.Select(reply => reply.Result).ToList();
                // Count all the acks
                counter = 0;
                foreach (AcceptReply reply in acceptResults)
                {
                    if (reply.Accepted)
                    {
                        counter++;
                    }
                }
                if (counter == numLM - 1) // TODO Can be different
                {
                    Console.WriteLine("[LM LEADER] They accepted my accept request, everything is OK");
                    lock (state)
                    {
                        state.ClearCurrentLeases();
                        state.Accept();
                        state.AcceptLeases(commitedOrder);
                        CommitRequest request = new CommitRequest();
                        request.Epoch = currentEpoch;
                        foreach (var stub in stubsLM.Values)
                        {
                            stub.CommitAsync(request);
                        }
                        state.ClearProposed();
                    }
                }
                else
                {
                    Console.WriteLine("[LM LEADER] Not everyone accepted my accept request //TODO: Ainda não resolvemos isto");
                }
                // TODO Else where the majority didn't accept and it need to retry
            }
            else
            {
                Console.WriteLine("[LM LEADER] Don't have the majoraty //TODO: Ainda não resolvemos este caso");
            }
        }

        public async void Loop(List<int> rounds_of_failure, List<List<int>> failures_per_round,List<int> idOrder, List<string> all_servers) 
        {
            int i = 1;
            int crash_count = 0;

            int num_servers = 0;

            foreach(int s in idOrder)
            {
                num_servers++;
            }

            while (i < numSlots)
            {
                Thread.Sleep(slotDuration);
                if (i == 1)
                {
                    registerStubs();
                    Console.WriteLine("[LM] Register other LM stubs - Its the first time we are doing this so...");
                }

                // CHECK CRASHES:
                if (rounds_of_failure.Contains(i))
                {
                    foreach (int el in failures_per_round[crash_count])
                    {
                        // TODO : MANDAR ABAIXO O SERVIDOR ---> all_servers[idOrder[el-1]]
                        if (el < num_servers)
                        {
                            Console.WriteLine("[LM TODO CRASH HERE] " + all_servers[idOrder[el]]);
                        }
                        else
                        {
                            Console.WriteLine("[LM TODO CRASH HERE] " + all_servers[idOrder[0]]);
                        }

                    }
                    crash_count++;
                }


                if (id == 0) {
                    Console.WriteLine("[LM] I am the leader of this: Let's start PAXOS");
                    StartPaxos();
                }
                lock (state)
                {
                    state.NextEpoch();
                }
                i++;
                
            }
        }
    }
}
