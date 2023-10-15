using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LeaseManager
{
    internal class LearnServicesImpl : LearnServices.LearnServicesBase
    {
        LeaseState state;
        public LearnServicesImpl(LeaseState state) {
            this.state = state;
        }

        public override Task<LearnReply> Learn(LearnRequest request, ServerCallContext context)
        {
            return Task.FromResult(LearnImpl(request));
        }

        public LearnReply LearnImpl(LearnRequest request)
        {
            Console.WriteLine("[LM] Received a Learn Request from a TM");
            // Build proposed
            string tm = request.Tm;
            List<string> proposedLeases = request.Leases.ToList();
            state.AddProposedLeases(tm, proposedLeases);
            LearnReply reply = new LearnReply();
            return reply;
        }

    }
}
