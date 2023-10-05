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
            AcceptReply reply = new AcceptReply();
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
            } else
            {
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
            PrepareReply reply = new PrepareReply();
            if (request.ProposedRound > promisedRound)
            {
                reply.Promise = true;
                promisedRound = request.ProposedRound;
            } else
            {
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
