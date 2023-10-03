using Grpc.Core;

namespace TransactionManager
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
            string hostname = args[1];
            string LOCALHOST = "localhost";
            int port = getPort(hostname);

            // Initialize LM he knows about
            int num_lm = Int32.Parse(args[2]);
            List<string> names_lm = new List<string>();
            List<string> urls_lm = new List<string>();
            for (int i = 0; i < num_lm; i++)
            {
                names_lm.Add("lm" + i.ToString());
                urls_lm.Add(args[3 + i]);
            }

            // Initialize TM he knows about
            int num_tm = Int32.Parse(args[2 + num_lm + 1]);
            List<string> names_tm = new List<string>();
            List<string> urls_tm = new List<string>();
            List<int> types = new List<int> { 0, 0 };
            for (int i = 0; i < num_tm; i++)
            {
                
                int port_tm = getPort(args[3 + num_lm + 1 + i]);
                if (port_tm != port)
                {
                    names_tm.Add("tm" + i.ToString());
                    urls_tm.Add("http://localhost:" + port_tm.ToString());
                    Console.WriteLine("[TM] connected to: " + "http://localhost:" + port_tm.ToString());
                }
            }

            string startupMessage;
            ServerPort serverPort;


            ServerState serverState = new ServerState(name);
            serverPort = new ServerPort(LOCALHOST, port, ServerCredentials.Insecure);
            startupMessage = "Insecure ChatServer server listening on port " + port;

            BroadcastServicesImpl brdImpl = new BroadcastServicesImpl(serverState);
            ClientServicesImpl cltImpl = new ClientServicesImpl(serverState, names_tm, urls_tm, types);
            
            Server server = new Server
            {
                Services = { BroadcastServices.BindService(brdImpl), TransactionServices.BindService(cltImpl) },
                Ports = { serverPort }
            };

            server.Start();

            Console.WriteLine(startupMessage);
            //Configuring HTTP for client connections in Register method
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            int c = 0;
            while (true) ;

        }
    }
}