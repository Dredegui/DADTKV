using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace LeaseManager
{
    internal class PaxosServicesImpl : PaxosConsensusServices.PaxosConsensusServicesBase
    {
        LeaseState state;
        private int promisedRound = -1;

        public PaxosServicesImpl(LeaseState state) {
            this.state = state;
        }

        public override Task<CommitReply> Commit(CommitRequest request, ServerCallContext context)
        {
            return Task.FromResult(CommitImpl(request));
        }

        public CommitReply CommitImpl(CommitRequest commitRequest)
        {
            Console.WriteLine("[LM] Received a commit request");
            List<LeaseTransaction> consensusOrder = state.GetCurrentLeases();
            state.ClearProposed();
            InformRequest request = new InformRequest();
            request.Epoch = commitRequest.Epoch;
            foreach (LeaseTransaction lt in consensusOrder)
            {
                Lease interRequest = new Lease();
                interRequest.Tm = lt.tm;
                interRequest.Leases.AddRange(lt.leases);
                request.Values.Add(interRequest);
            }
            // broadcast consensus to tm's
            try
            {
                foreach (LeaseInformServices.LeaseInformServicesClient stub in state.stubsTM.Values)
                {
                    stub.BroadcastInformAsync(request);
                }
            } catch (Exception ex)
            {
                Console.WriteLine("(ERROR)[LM LEARNER] Couldnt Broadcast using the stub");
            }
            CommitReply reply = new CommitReply();
            return reply;
        }

        public override Task<AcceptReply> Accept(AcceptRequest request, ServerCallContext context) {
            return Task.FromResult(AcceptImpl(request));
        }

        public AcceptReply AcceptImpl(AcceptRequest request)
        {
            Console.WriteLine("[LM LEARNER] Received a accept request => Check if the the round I promided is the new proposed");
            AcceptReply reply = new AcceptReply();
            Console.WriteLine("[ACCEPT ROUND] proposed: " + request.ProposedRound + " | promisedRound: " + promisedRound);
            lock (state)
            {
                if (request.ProposedRound == promisedRound) {
                    reply.Accepted = true;
                    state.ClearCurrentLeases();
                    List<LeaseTransaction> commitedOrder = new List<LeaseTransaction>();
                    List<Request> requests = request.Values.ToList();
                    foreach (Request req in  requests) {
                        LeaseTransaction lt = new LeaseTransaction();
                        lt.tm = req.Tm;
                        lt.leases = req.Leases.ToList();
                        commitedOrder.Add(lt);
                    }
                    state.AcceptLeases(commitedOrder);
                    Console.WriteLine("[LM LEANER] It is so I will accept it");
                } else
                {
                    Console.WriteLine("[LM LEANER] It is not => REJECT IT");
                    reply.Accepted = false;
                }
                Monitor.PulseAll(state);
            }
            return reply;
        }

        public override Task<PrepareReply> Prepare(PrepareRequest request, ServerCallContext context)
        {
            return Task.FromResult(PrepareImpl(request));
        }

        public PrepareReply PrepareImpl(PrepareRequest request)
        {
            Console.WriteLine("[LM LEARNER][ACCEPT ROUND] proposed: " + request.ProposedRound + " | promisedRound: " + promisedRound);
            Console.WriteLine("[LM LEARNER] Received a prepare request");
            PrepareReply reply = new PrepareReply();
            if (request.ProposedRound > promisedRound)
            {
                Console.WriteLine("[LM LEANER] The proposed round is newer than the round I promised before => New promise => Build response");
                reply.Promise = true;
                promisedRound = request.ProposedRound;
            } else
            {
                Console.WriteLine("[LM LEANER] I can accept your proposed becaused I promised to a higher round than yours => BUILD RESPONSE");
                reply.Promise = false;
            }
            reply.AcceptedRound = state.GetAcceptedRound();
            List<LeaseTransaction> acceptedOrder = state.GetCurrentLeases();
            foreach (LeaseTransaction lt in acceptedOrder)
            {
                Request interRequest = new Request();
                interRequest.Tm = lt.tm;
                interRequest.Leases.AddRange(lt.leases);
                reply.Values.Add(interRequest);
            }

            return reply;
        }
    }
}
