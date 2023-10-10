using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grpc.Net.Client;

namespace Client
{
    public class ClientLogic
    {
        private readonly string name;
        private int chosen;
        private readonly GrpcChannel channel;
        private TransactionServices.TransactionServicesClient stub;
        List<string> tms;
        List<string> urls;

        public ClientLogic(string name, int chosen, List<string> tms, List<string> urls)
        {
            this.name = name;
            this.chosen = chosen;
            this.tms = tms;
            this.urls = urls;
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            channel = GrpcChannel.ForAddress(urls[chosen]);
            stub = new TransactionServices.TransactionServicesClient(channel);
        }
        public void TxSubmit(List<string> reads, List<string> keys, List<int> values)
        {
            SubmitRequest request = new SubmitRequest();
            request.Name = name;
            request.Reads.AddRange(reads);
            request.Keys.AddRange(keys);
            request.Values.AddRange(values);
            SubmitReply reply = stub.Submit(request);
            Console.WriteLine("[CLI] Received a transaction response with sucess: " + reply.ToString);
        }
    }
}
