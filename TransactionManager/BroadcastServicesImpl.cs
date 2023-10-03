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
            return Task.FromResult(URBroadcast(message));
        }

        public BroadcastAck URBroadcast(BroadcastMessage message)
        {
            // Write operation
            List<string> keys = message.Keys.ToList();
            List<int> values = message.Values.ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                state.SetValue(keys[i], values[i]);
            }
            BroadcastAck ack = new BroadcastAck();
            ack.Value = true;
            return ack;
        }
    }
}
