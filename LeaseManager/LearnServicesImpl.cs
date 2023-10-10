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
            Console.WriteLine("LEARN SERVER SIDE");
            // Build proposed
            string tm = request.Tm;
            List<string> proposedLeases = request.Leases.ToList();
            state.AddProposedLeases(tm, proposedLeases);
            // Wait for the next consensus | TODO Sleep 
            List<LeaseTransaction> consensusOrder;
            lock (state)
            {
                Monitor.Wait(state);
                consensusOrder = state.GetCurrentLeases();
            }
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
