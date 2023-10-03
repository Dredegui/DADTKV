namespace Client
{
    internal class Program
    {
        private static int getPort(string hostname)
        {
            return Int32.Parse(hostname.Split(':')[2]);
        }
        static void Main(string[] args)
        {
            
            string name = args[0];
            string script_name = args[1];

            // GET Tms
            int num_tm = Int32.Parse(args[2]);
            List<string> tms = new List<string>();
            List<string> urls = new List<string>();
            for (int i = 0; i < num_tm; i++)
            {
                tms.Add("NO_NAME_YET");
                int port = getPort(args[3 + i]);
                Console.WriteLine("[CLI] http://localhost:" + port.ToString());
                urls.Add("http://localhost:" + port.ToString());
            }

            Console.WriteLine("Client Start");

            ClientLogic client = new ClientLogic(name, 0, tms, urls);
            List<string> reads = new List<string>();
            List<string> keys = new List<string> {"balance1", "balance2"};
            List<int> values = new List<int> {40, 10};
            client.TxSubmit(reads, keys, values);
            reads.Add("balance1");
            keys.Clear();
            values.Clear();
            client.TxSubmit(reads, keys, values);

            ClientLoop CLI = new ClientLoop(@"..\..\..\..\Client\scriptsCLI\DADTKV_client_script_sample.txt");
            CLI.Loop();
        }
    }
}