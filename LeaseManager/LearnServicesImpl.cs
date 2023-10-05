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
        LeaseState state = new LeaseState();
        public LearnServicesImpl() { }

        public override Task<LearnReply> Learn(LearnRequest request, ServerCallContext context)
        {
            return Task.FromResult(GetLearn(request));
        }

        public LearnReply GetLearn(LearnRequest request)
        {
            // recebe epoch atual
            int curr_epoch = state.GetEpoch();
            // loop a espera do proximo
            while (state.GetEpoch() != curr_epoch + 1 && state.Updated());
            // buscar current_leases e devolver
            LearnReply reply = null;
            return reply;
        }

    }
}
