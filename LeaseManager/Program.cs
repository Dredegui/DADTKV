using Grpc.Core;

namespace LeaseManager
{
    internal class Program
    {
        private static int getPort(string hostname)
        {
            return Int32.Parse(hostname.Split(':')[2]);
        }
        static void Main(string[] args)
        {
            int id = Int32.Parse(args[0]);
            string name = args[1];
            string host = args[2];
            string LOCALHOST = "localhost";
            int port = getPort(host);

            int num_lm = Int32.Parse(args[3]);

            // INITIALIZE LM that he knows
            List<string> names_lm = new List<string>();
            List<string> urls_lm = new List<string>();
            List<int> types = new List<int>();
            for (int i = 0; i < num_lm; i++)
            {

                int port_lm = getPort(args[4 + i]);
                if (port_lm != port)
                {
                    names_lm.Add("lm" + i.ToString());
                    urls_lm.Add("http://localhost:" + port_lm.ToString());
                    Console.WriteLine("[LM] connected to: " + "http://localhost:" + port_lm.ToString());
                    types.Add(0);
                }
            }
            int num_tm = Int32.Parse(args[4 + num_lm]);
            List<string> names_tm = new List<string>();
            List<string> urls_tm = new List<string>();
            for (int i = 0; i < num_tm; i++)
            {

                int port_tm = getPort(args[5 + num_lm + i]);
                names_tm.Add("tm" + i.ToString());
                urls_tm.Add("http://localhost:" + port_tm.ToString());
                Console.WriteLine("[TM] connected to another TM: " + "http://localhost:" + port_tm.ToString());
                types.Add(1);
            }
            string startupMessage;
            ServerPort serverPort;


            LeaseState leaseState = new LeaseState();
            serverPort = new ServerPort(LOCALHOST, port, ServerCredentials.Insecure);
            startupMessage = "Insecure ChatServer server listening on port " + port;

            LearnServicesImpl lrnImpl = new LearnServicesImpl(leaseState);
            PaxosServicesImpl pxsImpl = new PaxosServicesImpl(leaseState);
            LeaseLogic leaseLogic = new LeaseLogic(leaseState, urls_lm.Concat(urls_tm).ToList(), names_lm.Concat(names_tm).ToList(), types, num_lm, id, 10000);

            Server server = new Server
            {
                Services = { LearnServices.BindService(lrnImpl), PaxosConsensusServices.BindService(pxsImpl) },
                Ports = { serverPort }
            };

            server.Start();

            Console.WriteLine(startupMessage);
            //Configuring HTTP for client connections in Register method
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            leaseLogic.Loop();
        }
    }
}

