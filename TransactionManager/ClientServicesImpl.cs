using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransactionManager
{
    public class ClientServicesImpl : TransactionServices.TransactionServicesBase
    {
        private ServerState state;
        private Dictionary<string, BroadcastServices.BroadcastServicesClient> stubs = new Dictionary<string, BroadcastServices.BroadcastServicesClient> ();
        private int transcationId;

        public ClientServicesImpl(ServerState state)
        {
            this.state = state;
            transcationId = 0;
        }

        public override Task<SubmitReply> Submit(SubmitRequest request, ServerCallContext context)
        {
            return Task.FromResult(TxSubmit(request));
        }

        public void registerStubs(List<string> names, List<string> urls, List<int> types)
        {
            for (int i = 0; i < names.Count; i++)
            {
                GrpcChannel channel = GrpcChannel.ForAddress(urls[i]);
                if (types[i] == 0)
                {
                    stubs[names[i]] = new BroadcastServices.BroadcastServicesClient(channel);
                }
                else
                {
                    // Lease stub
                }

            }
        }

        public SubmitReply TxSubmit(SubmitRequest request)
        {
            // TODO Check leases, run paxos if there is no lease
            // Execution of the transaction
            // Read operation
            List<string> reads = request.Reads.ToList();
            List<int> results = new List<int>();
            foreach (string read in reads)
            {
                results.Add(state.GetValue(read));
            }
            // Write operation
            List<string> keys= request.Keys.ToList();
            List<int> values = request.Values.ToList();
            int i = 0;
            while (i < keys.Count)
            {
                state.SetValue(keys[i], values[i]);
            }
            BroadcastMessage message = new BroadcastMessage();
            message.Id = transcationId;
            message.Name = state.GetName();
            message.Keys.AddRange(keys);
            message.Values.AddRange(values);
            transcationId++;
            foreach (var stub in stubs.Values)
            {

                stub.Broadcast(message); // TODO async?
            }
            SubmitReply reply = new SubmitReply();
            reply.Keys.AddRange(reads);
            reply.Values.AddRange(results);
            return reply;
        }

    }
}
