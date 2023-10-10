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
        private Dictionary<string, PaxosConsensusServices.PaxosConsensusServicesClient> stubs = new Dictionary<string, PaxosConsensusServices.PaxosConsensusServicesClient>();
        List<string> urls;
        List<string> names;
        private int numLM;
        private int id;
        private int numSlots;


        public LeaseLogic(LeaseState state, List<string> urls, List<string> names, int numLM, int id, int numSlots)
        {
            this.state = state;
            this.urls = urls;
            this.names = names;
            this.numLM = numLM;
            this.id = id;
            this.numSlots = numSlots;
        }

        public void registerStubs()
        {
            for (int i = 0; i < names.Count; i++)
            {
                GrpcChannel channel = GrpcChannel.ForAddress(urls[i]);
                stubs[names[i]] = new PaxosConsensusServices.PaxosConsensusServicesClient(channel);
            }
        }

        public async void StartPaxos()
        {
            Console.WriteLine("[LM LEADER] Building prepare request");
            PrepareRequest replyRequest = new PrepareRequest();
            replyRequest.ProposedRound = state.GetEpoch();
            int counter = 0;
            List<Task<PrepareReply>> replyAwaitList = new List<Task<PrepareReply>>();
            foreach (string name in names)
            {
                Console.WriteLine("[LM LEADER] Sending async PREPARE REQUESTS for every LM");
                replyAwaitList.Add(stubs[name].PrepareAsync(replyRequest).ResponseAsync);
                // Use prepare reply info 
            }
            Console.WriteLine("[LM LEADER] Waiting for every prepare reply //TODO: Await?");
            await Task.WhenAll(replyAwaitList);
            List<PrepareReply> replyResults = replyAwaitList.Select(reply => reply.Result).ToList(); // TODO AWAIT?
            foreach (PrepareReply reply in replyResults)
            {
                Console.WriteLine("[LM LEADER] Check if we have the majoraty");
                if (reply.Promise)
                {
                    counter++;
                }
            }
            Console.WriteLine("[LM LEADER] Id: " + id + "Counter: " + counter + " | Number of lms: " + numLM);
            if (counter / numLM > 0.5)
            {
                Console.WriteLine("[LM LEADER] Id: " + id + " | HAS MAJORITY => Start accept request");
                // Start accept requests
                // Build request
                AcceptRequest acceptRequest = new AcceptRequest();
                acceptRequest.ProposedRound = state.GetEpoch();
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
                foreach (string name in names)
                {
                    acceptAwaitList.Add(stubs[name].AcceptAsync(acceptRequest).ResponseAsync);
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
                if (counter == numLM) // TODO Can be different
                {
                    Console.WriteLine("[LM LEADER] They accepted my accept request, everything is OK");
                    state.ClearCurrentLeases();
                    state.AcceptLeases(commitedOrder);
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

        public async void Loop()
        {
            int i = 0;
            while (i < numSlots)
            {
                Thread.Sleep(10000);
                if (i == 0)
                {
                    registerStubs();
                    Console.WriteLine("[LM] Register other LM stubs - Its the first time we are doing this so...");
                }
                if (id == 0) {
                    Console.WriteLine("[LM] I am the leader of this: Let's start PAXOS");
                    StartPaxos();
                    Monitor.PulseAll(state);
                    state.Accept();
                }
                state.NextEpoch();
                i++;
            }
        }
    }
}
