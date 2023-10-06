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
            PrepareRequest replyRequest = new PrepareRequest();
            replyRequest.ProposedRound = state.GetEpoch();
            int counter = 0;
            List<Task<PrepareReply>> replyAwaitList = new List<Task<PrepareReply>>();
            foreach (string name in names)
            {
                replyAwaitList.Add(stubs[name].PrepareAsync(replyRequest).ResponseAsync);
                // Use prepare reply info 
            }
            await Task.WhenAll(replyAwaitList);
            List<PrepareReply> replyResults = replyAwaitList.Select(reply => reply.Result).ToList();
            foreach (PrepareReply reply in replyResults)
            {
                if (reply.Promise)
                {
                    counter++;
                }
            }
            Console.WriteLine("Id: " + id + "Counter: " + counter + " | Number of lms: " + numLM);
            if (counter / numLM > 0.5)
            {
                Console.WriteLine("Id: " + id + " | HAS MAJORITY");
                // Start accept requests
                // Build request
                AcceptRequest acceptRequest = new AcceptRequest();
                acceptRequest.ProposedRound = state.GetEpoch();
                List<LeaseTransaction> commitedOrder = state.GetProposedLeases();
                foreach (LeaseTransaction lt in commitedOrder)
                {
                    Request request = new Request();
                    request.Tm = lt.tm;
                    request.Leases.AddRange(lt.leases);
                    acceptRequest.Values.Add(request);
                }
                // Build response await list
                List<Task<AcceptReply>> acceptAwaitList = new List<Task<AcceptReply>>();
                foreach (string name in names)
                {
                    acceptAwaitList.Add(stubs[name].AcceptAsync(acceptRequest).ResponseAsync);
                    // Use prepare reply info 
                }
                await Task.WhenAll(acceptAwaitList);
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
                    state.ClearCurrentLeases();
                    state.AcceptLeases(commitedOrder);
                }
                // TODO Else where the majority didn't accept and it need to retry
            }
        }

        public void Loop()
        {
            int i = 0;
            while (i < numSlots)
            {
                Thread.Sleep(10000);
                if (i == 0)
                {
                    registerStubs();
                }
                if (id == 0) {
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
