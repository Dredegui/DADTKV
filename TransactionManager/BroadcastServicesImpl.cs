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
        static string SPACE = "                                    ";

        public BroadcastServicesImpl(ServerState state)
        {
            this.state = state;
        }

        public override Task<PermissionReply> ReceivePermission(PermissionRequest request, ServerCallContext context)
        {
            return Task.FromResult(ReceivePermissionImpl(request));
        }

        public PermissionReply ReceivePermissionImpl(PermissionRequest request)
        {
            if (state.suspectList.Contains(request.Host))
            {
                Console.WriteLine("[" + state.GetName() + "] IGNORE " + request.Host);
                return null;
            }
            PermissionReply reply = new PermissionReply();
            reply.Value = true;
            return reply;
        }

        public override Task<BroadcastAck> Broadcast(BroadcastMessage message, ServerCallContext context) {
            return Task.FromResult(BroadcastImpl(message));
        }

        public BroadcastAck BroadcastImpl(BroadcastMessage message)
        {
            if (state.suspectList.Contains(message.Name))
            {
                Console.WriteLine("[" + state.GetName() + "] IGNORE " + message.Name);
                return null;
            }
            // Write operation
            List<string> keys = message.Keys.ToList();
            List<int> values = message.Values.ToList();
            List<int> lengths = message.Lenghts.ToList();
            lock (state)
            {
                for (int i = 0; i < keys.Count; i++)
                {
                    string key = keys[i];
                    Console.WriteLine(SPACE + "[TM] Received length: " + lengths[i] + " | Current Length: " + state.queue[key].Count);
                    if (lengths[i] + 1 == state.queue[key].Count)
                    {
                        state.SetValue(key, values[i]);
                        state.queue[key].RemoveAt(0);
                    }
                }
                for (int i = 0; i < message.Reads.Count; i++)
                {
                    string read = message.Reads[i];
                    if (lengths[i + keys.Count] + 1 == state.queue[read].Count)
                    {
                        if (state.queue[read][0] == message.Name)
                        {
                            state.queue[read].RemoveAt(0);
                        }
                    }
                }
                Monitor.PulseAll(state);
            }
            Console.WriteLine(SPACE + "[TM] My service is completed: Pinging other TM that might be waiting for the queue");
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
            if (state.suspectList.Contains(request.Host))
            {
                Console.WriteLine("[" + state.GetName() + "] IGNORE " + request.Host);
                return null;
            }
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
