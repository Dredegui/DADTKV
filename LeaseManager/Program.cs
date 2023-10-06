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
            for (int i = 0; i < num_lm; i++)
            {

                int port_lm = getPort(args[4 + i]);
                if (port_lm != port)
                {
                    names_lm.Add("lm" + i.ToString());
                    urls_lm.Add("http://localhost:" + port_lm.ToString());
                    Console.WriteLine("[LM] connected to: " + "http://localhost:" + port_lm.ToString());
                }
            }
            string startupMessage;
            ServerPort serverPort;


            LeaseState leaseState = new LeaseState();
            serverPort = new ServerPort(LOCALHOST, port, ServerCredentials.Insecure);
            startupMessage = "Insecure ChatServer server listening on port " + port;

            LearnServicesImpl lrnImpl = new LearnServicesImpl(leaseState);
            PaxosServicesImpl pxsImpl = new PaxosServicesImpl(leaseState);
            LeaseLogic leaseLogic = new LeaseLogic(leaseState, urls_lm, names_lm, num_lm, id, 10000);

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

