using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public override Task<AcceptReply> Accept(AcceptRequest request, ServerCallContext context) {
            return Task.FromResult(AcceptImpl(request));
        }

        public AcceptReply AcceptImpl(AcceptRequest request)
        {
            Console.WriteLine("[LM LEARNER] Received a accept request => Check if the the round I promided is the new proposed");
            AcceptReply reply = new AcceptReply();
            Console.WriteLine("[ACCEPT ROUND] proposed: " + request.ProposedRound + " | promisedRound: " + promisedRound);
            if (request.ProposedRound == promisedRound) {
                reply.Accepted = true;
                state.ClearCurrentLeases();
                List<LeaseTransaction> commitedOrder = new List<LeaseTransaction>();
                List<Request> requests = request.Values.ToList();
                foreach (Request req in  requests) {
                    LeaseTransaction lt = new LeaseTransaction();
                    lt.tm = req.Tm;
                    lt.leases = req.Leases.ToList();
                }
                state.AcceptLeases(commitedOrder);
                Console.WriteLine("[LM LEANER] It is so I will accept it");
            } else
            {
                Console.WriteLine("[LM LEANER] It is not => REJECT IT");
                reply.Accepted = false;
            }
            return reply;
        }

        public override Task<PrepareReply> Prepare(PrepareRequest request, ServerCallContext context)
        {
            return Task.FromResult(PrepareImpl(request));
        }

        public PrepareReply PrepareImpl(PrepareRequest request)
        {
            Console.WriteLine("[ACCEPT ROUND] proposed: " + request.ProposedRound + " | promisedRound: " + promisedRound);
            Console.WriteLine("[LM LEANER] Received a prepare request");
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
