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
        private int counter = 0;
        private List<string> names;
        private List<string> urls;
        private List<int> types;

        public ClientServicesImpl(ServerState state, List<string> names, List<string> urls, List<int> types)
        {
            this.state = state;
            transcationId = 0;
            this.names = names;
            this.urls = urls;
            this.types = types;
        }

        public override Task<SubmitReply> Submit(SubmitRequest request, ServerCallContext context)
        {
            return Task.FromResult(TxSubmit(request));
        }

        public void registerStubs()
        {
            for (int i = 0; i < names.Count; i++)
            {
                Console.WriteLine(names.Count + " < OLHA AQUI >  URL: " + urls[i] + " NOME: " + names[i]);
                GrpcChannel channel = GrpcChannel.ForAddress(urls[i]);

                if (types[i] == 0)
                {
                    stubs[names[i]] = new BroadcastServices.BroadcastServicesClient(channel);
                    Console.WriteLine("========");
                }
                else
                {
                    // Lease stub
                }

            }
            Console.WriteLine("Helloooo????");
        }

        public SubmitReply TxSubmit(SubmitRequest request)
        {
            Console.WriteLine("Start submit");
            if (counter == 0)
            {
                this.registerStubs();
                counter++;
            }
            Console.WriteLine("Registed stubs");
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
            for (int i = 0;i < keys.Count;i++)
            {
                state.SetValue(keys[i], values[i]);
            }
            Console.WriteLine("Changed values");
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
            Console.WriteLine("Broadcasted");
            SubmitReply reply = new SubmitReply();
            reply.Keys.AddRange(reads);
            reply.Values.AddRange(results);
            return reply;
        }

    }
}
