using Grpc.Core;

namespace TransactionManager
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string hostname = "localhost";
            Console.WriteLine("Input name:");
            string name = Console.ReadLine();
            Console.WriteLine("Input port:");
            int port = int.Parse(Console.ReadLine());
            List<string> names;
            List<string> urls;
            List<int> types;
            string startupMessage;
            ServerPort serverPort;

            if (name == "tm1") {
                names = new List<string> { "tm2" };
                urls = new List<string> { "http://localhost:5002" };
                types = new List<int> { 0 };
            }
            else
            {
                names = new List<string> { "tm1" };
                urls = new List<string> { "http://localhost:5001" };
                types = new List<int> { 0 };
            }
            ServerState serverState = new ServerState(name);
            serverPort = new ServerPort(hostname, port, ServerCredentials.Insecure);
            startupMessage = "Insecure ChatServer server listening on port " + port;

            BroadcastServicesImpl brdImpl = new BroadcastServicesImpl(serverState);
            ClientServicesImpl cltImpl = new ClientServicesImpl(serverState, names, urls, types);
            
            Server server = new Server
            {
                Services = { BroadcastServices.BindService(brdImpl), TransactionServices.BindService(cltImpl) },
                Ports = { serverPort }
            };

            server.Start();

            Console.WriteLine(startupMessage);
            //Configuring HTTP for client connections in Register method
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            while (true) ;

        }
    }
}