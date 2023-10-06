using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization.Formatters;
using System.Text;
using System.Threading.Tasks;

namespace TransactionManager
{
    public class ClientServicesImpl : TransactionServices.TransactionServicesBase
    {
        private ServerState state;
        private Dictionary<string, BroadcastServices.BroadcastServicesClient> stubsTM = new Dictionary<string, BroadcastServices.BroadcastServicesClient> ();
        private Dictionary<string, LearnServices.LearnServicesClient> stubsLM = new Dictionary<string, LearnServices.LearnServicesClient> ();
        private List<string> leases = new List<string> ();
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
            return TxSubmit(request);
        }

        public void registerStubs()
        {
            for (int i = 0; i < names.Count; i++)
            {
                GrpcChannel channel = GrpcChannel.ForAddress(urls[i]);

                if (types[i] == 0)
                {
                    // Transaction Managers stub
                    stubsTM[names[i]] = new BroadcastServices.BroadcastServicesClient(channel);
                }
                else
                {
                    // Lease Managers stub
                    stubsLM[names[i]] = new LearnServices.LearnServicesClient(channel);
                }

            }
        }


        public bool checkLeases(List<string> reads, List<string> keys) {
            return !reads.Except(leases).Any() && !keys.Except(leases).Any();
        }

        public bool checkCollision(List<string> myLeases, List<string> otherLeases)
        {
            return myLeases.Intersect(otherLeases).Any();
        }

        public List<int> getWaitingQueue(LearnReply result)
        {
            List<int> ret = new List<int>();

            foreach(LearnRequest req in result.Values) 
            {
                if (req.Tm != state.GetName())
                {
                    if (checkCollision(leases,req.Leases.ToList()))
                    {
                        ret.Add(0);
                    }
                    else
                    {
                        ret.Add(1);
                    }
                }
                else
                {
                    ret.Add(1);
                }
            }

            return ret;
        }

        public async Task<SubmitReply> TxSubmit(SubmitRequest request)
        {
            Console.WriteLine("Start submit");
            if (counter == 0)
            {
                this.registerStubs();
                counter++;
            }
            Console.WriteLine("Registed stubs");
            // Initialization
            List<string> reads = request.Reads.ToList();
            List<string> keys = request.Keys.ToList();
            List<int> values = request.Values.ToList();
            List<string> results = new List<string>();

            if (!checkLeases(reads, keys))
            {
                LearnRequest learnRequest = new LearnRequest();
                learnRequest.Tm = state.GetName();
                learnRequest.Leases.AddRange(reads.Concat(keys).Distinct());
                List<Task<LearnReply>> learnAwaitList = new List<Task<LearnReply>>();
                foreach (LearnServices.LearnServicesClient stub in stubsLM.Values)
                {
                    learnAwaitList.Add(stub.LearnAsync(learnRequest).ResponseAsync);
                }
                await Task.WhenAll(learnAwaitList);
                LearnReply learnResult = learnAwaitList.Select(reply => reply.Result).ToList()[0];

                // save our leases TODO Ver com stor (libertar sempre)
                leases = new List<string>();
                foreach (LearnRequest tmL in learnResult.Values)
                {
                    if (tmL.Tm == state.GetName())
                    {
                        leases.AddRange(tmL.Leases.ToList());
                    }
                }

                List<int> queue = getWaitingQueue(learnResult);


                while (queue.Count > 0)
                {
                    // Esperar mensagens?
                    // TODO : meter um sleep? 
                }

            }

            

            


            foreach (string read in reads)
            {
                if (state.ValidKey(read))
                {
                    results.Add(state.GetValue(read));
                } else
                {
                    results.Add("unknown DadInt");
                }
            }
            // Write operation
            
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
            foreach (var stub in stubsTM.Values)
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
