using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransactionManager
{
    public class TransactionManagerImpl : TransactionServices.TransactionServicesBase
    {
        private Dictionary<string, int> DadInts = new Dictionary<string, int>();

        public TransactionManagerImpl() {
        }

        public override Task<SubmitReply> Submit(SubmitRequest request, ServerCallContext context)
        {
            return Task.FromResult(TxSubmit(request));
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
                results.Add(DadInts[read]);
            }
            // Write operation
            List<string> keys= request.Keys.ToList();
            List<int> values = request.Values.ToList();
            int i = 0;
            while (i < keys.Count)
            {
                DadInts[keys[i]] = values[i];
            }
            // TODO propagate asynchronously to other tms
            SubmitReply reply = new SubmitReply();
            reply.Keys.AddRange(reads);
            reply.Values.AddRange(results);
            return reply;
        }

    }
}
