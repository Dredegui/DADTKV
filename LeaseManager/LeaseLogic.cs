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
            for (int i = 0; i < urls.Count; i++)
            {
                try
                {
                    GrpcChannel channel = GrpcChannel.ForAddress(urls[i]);
                    if (types[i] == 0)
                    {
                        stubsLM[urls[i]] = new PaxosConsensusServices.PaxosConsensusServicesClient(channel);
                    }
                    else
                    {
                        state.stubsTM[urls[i]] = new LeaseInformServices.LeaseInformServicesClient(channel);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("(ERROR)[LM " + id + "] Trying to register a LM that is unavailable");
                    urls.RemoveAt(i);
                    if (stubsLM.ContainsKey(urls[i]))
                    {
                        stubsLM.Remove(urls[i]);
                    } else if (state.stubsTM.ContainsKey(urls[i]))
                    {
                        state.stubsTM.Remove(urls[i]);
                    }
                    i--;
                }
            }
        }

        public async void StartPaxos()
        {
            Console.WriteLine("[LM " + id + "LEADER] Building prepare request");
            PrepareRequest replyRequest = new PrepareRequest();
            replyRequest.Host = state.hostport;
            int currentEpoch = state.GetEpoch();
            replyRequest.ProposedRound = currentEpoch;
            int counter = 0;
            List<Task<PrepareReply>> replyAwaitList = new List<Task<PrepareReply>>();
            List<LeaseTransaction> commitedOrder = new List<LeaseTransaction>(state.GetProposedLeases());
            state.ClearProposed();
            try
            {
                foreach (string stubName in stubsLM.Keys)
                {
                    Console.WriteLine("[LM LEADER " + id + "] Sending async PREPARE REQUESTS for every LM " + stubName);
                    if (!state.suspectList.Contains(stubName))
                    {
                        replyAwaitList.Add(stubsLM[stubName].PrepareAsync(replyRequest).ResponseAsync);
                    } else
                    {
                        Console.WriteLine("[LM LEADER " + id + "] Ignores because it suspects " + stubName);
                    }
                    // Use prepare reply info 
                }
            } catch (Exception e)
            {
                Console.WriteLine("(ERROR)[LM LEADER " + id + "] Couldnt send the prepare request for at least one LM");
            }
            Console.WriteLine("[LM LEADER] Waiting for every prepare reply => Waiting for every LM");
            List<PrepareReply> prepareResults = new List<PrepareReply>();
            foreach (Task<PrepareReply> reply in replyAwaitList)
            {
                try
                {
                    await reply;
                    if (reply.Result != null)
                    {
                        prepareResults.Add(reply.Result);
                    }
                } catch (Exception e)
                {
                    Console.WriteLine("(ERROR)[LM LEADER " + id + "] Waiting for a response that will never exist");
                }
            }
            Console.WriteLine("[LM LEADER " + id + "] Waited for every process with sucess");
            foreach (PrepareReply reply in prepareResults)
            {
                Console.WriteLine("[LM LEADER " + id + "] Check if we have the majoraty");
                if (reply.Promise)
                {
                    counter++;
                }
            }
            Console.WriteLine("[LM LEADER " + id + "] Id: " + id + "Counter: " + counter + " | Number of lms: " + numLM);
            float prepareCheck = counter / (prepareResults.Count + 0.0F);
            if (prepareCheck > 0.5)
            {
                Console.WriteLine("[LM LEADER " + id + "] Id: " + id + " | HAS MAJORITY => Start accept request");
                // Start accept requests
                // Build request
                AcceptRequest acceptRequest = new AcceptRequest();
                acceptRequest.Host = state.hostport;
                acceptRequest.ProposedRound = currentEpoch;
                Console.WriteLine("[LM LEADER " + id + "] Building the accepted list for other LMs");
                foreach (LeaseTransaction lt in commitedOrder)
                {
                    Request request = new Request();
                    request.Tm = lt.tm;
                    request.Leases.AddRange(lt.leases);
                    acceptRequest.Values.Add(request);
                }
                Console.WriteLine("[LM LEADER " + id + "] Sending the accepted list for other LM");
                // Build response await list
                List<Task<AcceptReply>> acceptAwaitList = new List<Task<AcceptReply>>();
                try
                {
                    foreach (string stubName in stubsLM.Keys)
                    {
                        if (!state.suspectList.Contains(stubName))
                        {
                            acceptAwaitList.Add(stubsLM[stubName].AcceptAsync(acceptRequest).ResponseAsync);
                        }
                        // Use prepare reply info 
                    }
                } catch (Exception e) {
                    Console.WriteLine("(ERROR)[LM LEADER " + id + "] Tried to reach a LM that closed the connection");
                }
                List<AcceptReply> acceptResults = new List<AcceptReply>();
                Console.WriteLine("[LM LEADER " + id + "] Waiting for every LM to accept my accepted list");
                Console.WriteLine("[LM LEADER " + id + "] Every one responded => Let's check every one accepted");
                foreach (Task<AcceptReply> reply in acceptAwaitList)
                {
                    try
                    {
                        await reply;
                        if (reply.Result != null)
                        {
                            acceptResults.Add(reply.Result);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("[LM LEADER " + id + "] One of the LMs is compromised");
                    }
                }
                // Count all the acks
                counter = 0;
                foreach (AcceptReply reply in acceptResults)
                {
                    if (reply.Accepted)
                    {
                        counter++;
                    }
                }
                float acceptCheck = counter / (acceptResults.Count + 0.0F);
                if (acceptCheck > 0.5) // TODO Can be different
                {
                    Console.WriteLine("[LM LEADER " + id + "] They accepted my accept request, everything is OK");
                    lock (state)
                    {
                        state.ClearCurrentLeases();
                        state.Accept();
                        state.AcceptLeases(commitedOrder);
                        CommitRequest request = new CommitRequest();
                        request.Host = state.hostport;
                        request.Epoch = currentEpoch;
                        try
                        {
                            foreach (string stubName in stubsLM.Keys)
                            {
                                if (!state.suspectList.Contains(stubName))
                                {
                                    stubsLM[stubName].CommitAsync(request);
                                }
                            }
                        } catch (Exception ex)
                        {
                            Console.WriteLine("(ERROR)[LM LEADER " + id + "] Tried to reach a LM that closed the connection");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("[LM LEADER " + id + "] Not everyone accepted my accept request (try again)");
                }
                // TODO Else where the majority didn't accept and it need to retry
            }
            else
            {
                Console.WriteLine("[LM LEADER " + id + "] Don't have the majoraty (try again)");
            }
        }

        public async void Loop(List<int> rounds_of_failure, List<List<int>> failures_per_round,List<int> idOrder, List<string> all_servers,List<List<int>> suspects_per_round,int YOUR_ID,Server server,List<string> all_names,int port) 
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
                    Console.WriteLine("[LM " + id + "] Register other LM stubs - Its the first time we are doing this so...");
                }

                // CHECK CRASHES:
                if (rounds_of_failure.Contains(i))
                {
                    foreach (int el in failures_per_round[crash_count])
                    {
                        // TODO : MANDAR ABAIXO O SERVIDOR COM O SEU ID
                        if (el < num_servers)
                        {
                            
                            if ("http://localhost:" + port == all_servers[idOrder[el]])
                            {
                                Console.WriteLine("[LM] **NEW CRASH** hostname: " + all_servers[idOrder[el]]);
                                server.ShutdownAsync().Wait();
                                return;
                            }

                        }
                        else
                        {

                            if ("http://localhost:" + port == all_servers[idOrder[el]])
                            {
                                Console.WriteLine("[LM] **NEW CRASH** hostname: " + all_servers[idOrder[0]] + " XXXXX");
                                server.ShutdownAsync().Wait();
                                return;
                            }

                        }

                    }

                    // CHECK SUSPECTS
                    for (int k = 0; k < suspects_per_round[crash_count].Count; k+=2) 
                    {
                        int oq_suspeita = suspects_per_round[crash_count][k] + 1;
                        int o_suspeito = suspects_per_round[crash_count][k + 1] + 1;
                        if (oq_suspeita >= num_servers)
                        {
                            oq_suspeita = 0;
                        }
                        if (o_suspeito >= num_servers)
                        {
                            o_suspeito = 0;
                        }
                    }

                    // CHECK SUSPECTS
                    for (int k = 0; k < suspects_per_round[crash_count].Count; k += 2)
                    {
                        int oq_suspeita = suspects_per_round[crash_count][k] + 1;
                        int o_suspeito = suspects_per_round[crash_count][k + 1] + 1;
                        if (oq_suspeita >= num_servers)
                        {
                            oq_suspeita = 0;
                        }
                        if (o_suspeito >= num_servers)
                        {
                            o_suspeito = 0;
                        }
                        // A -> B
                        //Console.WriteLine("[OQ SUSPEITA TAM TAM TAM DO LADO DA FUCKING TM]: " + all_servers[idOrder[oq_suspeita]]);
                        //Console.WriteLine("[O SUSPEITO TUM TUM TUM DO LADO DA FUCKING TM]: " + all_servers[idOrder[o_suspeito]]);
                        if (idOrder[oq_suspeita] == YOUR_ID)
                        {
                            Console.WriteLine("[LM " + id + "][" + all_servers[idOrder[oq_suspeita]] + "] ** NEW SUSPECT** -> " + all_servers[idOrder[o_suspeito]]);
                            state.addSuspect(all_servers[idOrder[o_suspeito]]);
                        }

                    }


                    crash_count++;
                }
                if (id == i%numLM) {
                    Console.WriteLine("[LM " + id + "] I am the leader of this: Let's start PAXOS");
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
