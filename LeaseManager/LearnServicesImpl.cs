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
            // Save current epoch
            int curr_epoch = state.GetEpoch();
            // If false the case is epoch x and accepted x-1
            if (state.Updated(curr_epoch)) // If epoch x and accepted x, wait for epoch x+1 to completed
            {
                curr_epoch += 1;
            }
            // Build proposed
            string tm = request.Tm;
            List<string> proposedLeases = request.Leases.ToList();
            state.AddProposedLeases(tm, proposedLeases);
            // Wait for the next consensus | TODO Sleep 
            while (state.Updated(curr_epoch));
            List<LeaseTransaction> consensusOrder = state.GetCurrentLeases();
            LearnReply reply = new LearnReply();
            foreach (LeaseTransaction lt in consensusOrder)
            {
                LearnRequest interRequest = new LearnRequest();
                interRequest.Tm = lt.tm;
                interRequest.Leases.AddRange(lt.leases);
                reply.Values.Add(interRequest);
            }
            state.ClearProposed(); // TODO REVIEW
            return reply;
        }

    }
}
