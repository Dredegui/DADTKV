using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransactionManager
{
    internal class BroadcastServicesImpl : BroadcastServices.BroadcastServicesBase
    {
        private ServerState state;


        public BroadcastServicesImpl(ServerState state)
        {
            this.state = state;
        }

        public override Task<BroadcastAck> Broadcast(BroadcastMessage message, ServerCallContext context) {
            return Task.FromResult(BroadcastImpl(message));
        }

        public BroadcastAck BroadcastImpl(BroadcastMessage message)
        {
            // Write operation
            List<string> keys = message.Keys.ToList();
            List<int> values = message.Values.ToList();
            lock (state)
            {
                for (int i = 0; i < keys.Count; i++)
                {
                    state.SetValue(keys[i], values[i]);
                    state.queue[keys[i]].RemoveAt(0);
                }
                foreach (string read in message.Reads)
                {
                    if (state.queue[read][0] == message.Name)
                    {
                        state.queue[read].RemoveAt(0);
                    }
                }
                Monitor.PulseAll(state);
            }
            Console.WriteLine("[TM] My service is completed: Pinging other TM that might be waiting for the queue");
            BroadcastAck ack = new BroadcastAck();
            ack.Value = true;
            return ack;
        }

        public override Task<LeaseUpdateReply> LeaseUpdate(LeaseUpdateRequest request, ServerCallContext context)
        {
            return Task.FromResult(LeaseUpdateImpl(request));
        }
        public LeaseUpdateReply LeaseUpdateImpl(LeaseUpdateRequest request)
        {
            LeaseUpdateReply reply = new LeaseUpdateReply();
            reply.Ack = false;
            List<string> leases = request.Leases.ToList();
            List<int> lenghts = request.Lenghts.ToList();
            for (int i = 0; i < leases.Count; i++)
            {
                string lease = leases[i];
                int length = lenghts[i];
                if (state.queue[lease].Count < length)
                {
                    Queue temp = new Queue();
                    temp.Lease = lease;
                    temp.Tms.AddRange(state.queue[lease]);
                    reply.Update.Add(temp);
                    reply.Ack = true;
                }
            }
            return reply;
        }
    }
}
