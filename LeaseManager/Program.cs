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

            // INITIALIZE TM that he knows
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

            // WALL BARRIER
            string wall_barrier = args[5 + num_lm + num_tm];

            // TIME SLOT DURATION
            int time_slot_duration = Int32.Parse(args[5 + num_lm + num_tm + 1]);

            // NUMBER OF TIME SLOTS
            int number_time_slots = Int32.Parse(args[5 + num_lm + num_tm + 2]);

            // GET REAL IDS ORDER
            List<int> idOrder = new List<int>();

            int c = 0;

            while (c < num_tm + num_lm)
            {
                idOrder.Add(Int32.Parse(args[5 + num_lm + num_tm + 3 + c]));
                c += 1;
            }

            // NUM FAILURES
            int num_failures = Int32.Parse(args[5 + num_lm + num_tm + 3 + num_tm + num_lm]);

            // GET FAILURE INFORMATION
            int index = 5 + num_lm + num_tm + 3 + num_tm + num_lm + 1;
            int f = 0;
            int total_in_failures = 0;
            int total_in_suspescts = 0;

            List<int> rounds_of_failure = new List<int>();
            List<List<int>> failures_per_round = new List<List<int>>();
            List<List<int>> suspects_per_round = new List<List<int>>();
            while (f < num_failures)
            {
                rounds_of_failure.Add(Int32.Parse(args[index + f + total_in_failures + total_in_suspescts]));

                int n = Int32.Parse(args[index + f + total_in_failures + total_in_suspescts + 1]);
                total_in_failures++;
                int i = 0;
                List<int> fails_this_round = new List<int>();
                while (i < n)
                {
                    fails_this_round.Add(Int32.Parse(args[index + f + total_in_failures + total_in_suspescts + 1]));
                    total_in_failures++;
                    i++;
                }

                n = Int32.Parse(args[index + f + total_in_failures + total_in_suspescts + 1]);
                total_in_suspescts++;
                i = 0;
                List<int> suspects_this_round = new List<int>();
                while (i < n)
                {
                    suspects_this_round.Add(Int32.Parse(args[index + f + total_in_failures + total_in_suspescts + 1]));
                    total_in_suspescts++;
                    suspects_this_round.Add(Int32.Parse(args[index + f + total_in_failures + total_in_suspescts + 1]));
                    total_in_suspescts++;
                    i++;
                }

                suspects_per_round.Add(suspects_this_round);
                failures_per_round.Add(fails_this_round);

                f++;
            }

            // DEBUG
            int r = 0;
            foreach (List<int> l in failures_per_round)
            {
                foreach (int i in l)
                {
                    Console.WriteLine("[LM MOTHER FUCKER] FALHAS DA RONDA " + r + " : " + i);
                }
                r++;
            }

            // DEBUG
            r = 0;
            foreach (List<int> l in suspects_per_round)
            {
                foreach (int i in l)
                {
                    Console.WriteLine("[LM MOTHER FUCKER] DESCONFIANÇA DA RONDA " + r + " : " + i);
                }
                r++;
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

