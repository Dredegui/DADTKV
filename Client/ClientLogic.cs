using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Grpc.Net.Client;

namespace Client
{
    public class ClientLogic
    {
        static string SPACE = "                                                                        ";
        public readonly string name;
        private int chosen;
        private GrpcChannel channel;
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
            SubmitReply reply = null;
            int count = 0;
            do
            {
                try
                {
                    reply = stub.Submit(request);
                } catch (Exception ex)
                {
                    Console.WriteLine("[CLI] Chosen TM is down, so we chose another");
                    chosen = (chosen + 1) % urls.Count;
                    channel = GrpcChannel.ForAddress(urls[chosen]);
                    stub = new TransactionServices.TransactionServicesClient(channel);
                    reply = null;
                    count++;
                    if (count >= urls.Count)
                    {
                        Console.WriteLine("[CLI] All TM's unavailable transaction is void");
                        return;
                    }
                }
            } while (reply == null);
            string replyToString = "";
            for (int i = 0; i < reply.Keys.Count; i++)
            {
                if (reply.Keys[i] == "Abort")
                {
                    replyToString += SPACE + "[CLI RESULTS] " + reply.Keys[i] + " |\n";
                    break;
                } 
                replyToString += SPACE + "[CLI RESULTS] " + reply.Keys[i] + ": " + reply.Values[i] + " |\n";
            }
            Console.WriteLine(SPACE + "[CLI] Received a transaction response with sucess:\n" + replyToString);
        }

        public void Status()
        {
            StatusRequest request = new StatusRequest();
            try
            {
                stub.Status(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[CLI] Chosen TM is down");
            }
        }
    }
}
