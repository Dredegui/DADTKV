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

            List<string> all_servers = new List<string>();
            List<string> names_lm = new List<string>();
            List<string> urls_lm = new List<string>();
            List<int> types = new List<int>();
            for (int i = 0; i < num_lm; i++)
            {

                int port_lm = getPort(args[3 + i]);
                names_lm.Add("lm" + i.ToString());
                urls_lm.Add("http://localhost:" + port_lm.ToString());
                Console.WriteLine("[TM] connected to another LM: " + "http://localhost:" + port_lm.ToString());
                types.Add(1);
                all_servers.Add("http://localhost:" + port_lm.ToString());
            }

            // Initialize TM he knows about
            int num_tm = Int32.Parse(args[2 + num_lm + 1]);
            List<string> names_tm = new List<string>();
            List<string> urls_tm = new List<string>();
            for (int i = 0; i < num_tm; i++)
            {
                
                int port_tm = getPort(args[3 + num_lm + 1 + i]);
                if (port_tm != port)
                {
                    names_tm.Add("tm" + i.ToString());
                    urls_tm.Add("http://localhost:" + port_tm.ToString());
                    Console.WriteLine("[TM] connected to another TM: " + "http://localhost:" + port_tm.ToString());
                    types.Add(0);
                }
                all_servers.Add("http://localhost:" + port_tm.ToString());
            }

            // WALL BARRIER
            string wall_barrier = args[3 + num_lm + 1 + num_tm];
            
            // TIME SLOT DURATION
            int time_slot_duration = Int32.Parse(args[3 + num_lm + 1 + num_tm + 1]);

            // NUMBER OF TIME SLOTS
            int number_time_slots = Int32.Parse(args[3 + num_lm + 1 + num_tm + 2]);

            // GET REAL IDS ORDER
            List<int> idOrder = new List<int>();

            int c = 0;

            while (c < num_tm + num_lm)
            {
                idOrder.Add(Int32.Parse(args[3 + num_lm + 1 + num_tm + 3 + c]));
                c += 1;
            }

            // NUM FAILURES
            int num_failures = Int32.Parse(args[3 + num_lm + 1 + num_tm + 3 + num_tm + num_lm]);

            // GET FAILURE INFORMATION
            int index = 3 + num_lm + 1 + num_tm + 3 + num_tm + num_lm + 1;
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

            /*
            // DEBUG
            int r = 0;
            foreach (List<int> l in failures_per_round)
            {
                foreach (int i in l)
                {
                    Console.WriteLine("[TM MOTHER FUCKER] FALHAS DA RONDA " + r + " : " +  i);
                }
                r++;
            }

            // DEBUG
            r = 0;
            foreach (List<int> l in suspects_per_round)
            {
                foreach (int i in l)
                {
                    Console.WriteLine("[TM MOTHER FUCKER] DESCONFIANÇA DA RONDA " + r + " : " + i);
                }
                r++;
            }
            */ 

            string startupMessage;
            ServerPort serverPort;


            ServerState serverState = new ServerState(name);
            serverPort = new ServerPort(LOCALHOST, port, ServerCredentials.Insecure);
            startupMessage = "Insecure ChatServer server listening on port " + port;

            BroadcastServicesImpl brdImpl = new BroadcastServicesImpl(serverState);
            LeaseBroadcastImpl lsImpl = new LeaseBroadcastImpl(serverState);
            ClientServicesImpl cltImpl = new ClientServicesImpl(serverState, names_lm.Concat(names_tm).ToList(), urls_lm.Concat(urls_tm).ToList(), types);
            
            Server server = new Server
            {
                Services = { BroadcastServices.BindService(brdImpl), TransactionServices.BindService(cltImpl), LeaseInformServices.BindService(lsImpl) },
                Ports = { serverPort }
            };

            
            server.Start();
            Console.WriteLine("[TM] Started tm services server || " + startupMessage);
            //Configuring HTTP for client connections in Register method
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            int round_count = 1;

            // TODO: Wait until wall barrier
            int crash_count = 0;
            while (round_count < number_time_slots)
            {
                if (rounds_of_failure.Contains(round_count))
                {
                    foreach (int el in failures_per_round[crash_count])
                    {
                        // TODO : MANDAR ABAIXO O SERVIDOR ---> all_servers[idOrder[el-1]]
                        if (el < num_lm + num_tm)
                        {
                            Console.WriteLine("[TM XXXXXXXXXXXXXXXXXXXXXXXXXX] " + all_servers[idOrder[el]]);
                        }
                        else
                        {
                            Console.WriteLine("[TM XXXXXXXXXXXXXXXXXXXXXXXXXX] " + all_servers[idOrder[0]]);
                        }
                        
                    }
                    crash_count++;

                }
                Thread.Sleep(time_slot_duration);
                round_count++;
            }

            while (true) { }

        }
    }
}