using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransactionManager
{
    internal class LeaseBroadcastImpl : LeaseInformServices.LeaseInformServicesBase
    {
        private ServerState state;
        static string SPACE = "                                    ";

        public LeaseBroadcastImpl(ServerState state)
        {
            this.state = state;
        }

        public void buildQueue(InformRequest reply)
        {
            foreach (Lease lr in reply.Values)
            {
                foreach (string lease in lr.Leases)
                {
                    if (!state.queue.ContainsKey(lease))
                    {
                        state.queue[lease] = new List<string> { lr.Tm };
                    }
                    else
                    {
                        state.queue[lease].Add(lr.Tm);
                    }
                }
            }
        }

        public override Task<InformReply> BroadcastInform(InformRequest request, ServerCallContext context) {
            return Task.FromResult(BroadcastInformImpl(request));
        }

        public InformReply BroadcastInformImpl(InformRequest request)
        {
            lock (state)
            {
                Console.WriteLine(SPACE + "[" + state.GetName() + "] Received a Inform request");
                // Build queue
                Console.WriteLine(SPACE + "[" + state.GetName() + "] RECEIVED EPOCH: " + request.Epoch + " | CURRENT SAVED EPOCH: " + state.epoch);
                if (request.Epoch > state.epoch) // TODO IMPROVE ORDER, WE COULD HAVE 3, RECEIVE 5, AND SKIP 4, WE DON'T WANT THIS
                {
                    Console.WriteLine(SPACE + "[" + state.GetName() + "] BUILD QUEUE");
                    buildQueue(request);
                    // Pulse all
                    Monitor.PulseAll(state);
                    state.epoch = request.Epoch;
                }
            }
            InformReply reply = new InformReply();
            return reply;
        }


    }
}
