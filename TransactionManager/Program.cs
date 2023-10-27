using System.Net.Sockets;
using System.Runtime.ConstrainedExecution;
using Grpc.Core;

namespace TransactionManager
{
    internal class Program
    {
        static string SPACE = "                                    ";
        private static int getPort(string hostname)
        {
            return Int32.Parse(hostname.Split(':')[2]);
        }

        public static Task WaitForTimeAsync(DateTime wantedTimeToStart)
        {
            TimeSpan delay = wantedTimeToStart - DateTime.Now;

            if (delay.TotalMilliseconds > 0)
            {
                return Task.Delay(delay).ContinueWith((t) =>
                {
                    // Continue with your code after the desired time has been reached
                });
            }
            else
            {
                // The desired time has already passed; you can handle this case accordingly
                return Task.CompletedTask;
            }
        }

        static void Main(string[] args)
        {

            string name = args[0];
            string hostname = args[1];
            string LOCALHOST = "localhost";
            int port = getPort(hostname);

            int s = DateTime.Now.Second;
            int until_next_minute = 60 - s;
            int m = 1000 - DateTime.Now.Millisecond;

            DateTime wantedTimeToStart = DateTime.Now.AddSeconds(until_next_minute).AddMilliseconds(m); // Replace this with your desired start time

            var task = WaitForTimeAsync(wantedTimeToStart);

            // You can wait for the task to complete using Wait
            task.Wait();
            // Initialize LM he knows about
            int num_lm = Int32.Parse(args[2]);

            List<string> all_servers = new List<string>();
            List<string> all_names = new List<string>();
            List<string> names_lm = new List<string>();
            List<string> urls_lm = new List<string>();
            List<int> types = new List<int>();
            int YOUR_ID = -1;
            for (int i = 0; i < num_lm; i++)
            {

                int port_lm = getPort(args[3 + i]);
                names_lm.Add("lm" + i.ToString());
                urls_lm.Add("http://localhost:" + port_lm.ToString());
                types.Add(1);
                all_servers.Add("http://localhost:" + port_lm.ToString());
                all_names.Add("lm" + i.ToString());
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
                    types.Add(0);
                    all_names.Add("tm" + i.ToString());
                    
                }
                else
                {
                    YOUR_ID = all_servers.Count;
                    all_names.Add("me");
                }
                all_servers.Add("http://localhost:" + port_tm.ToString());
            }

            Console.WriteLine(SPACE + "[TM]Created connections with every tm and lm with sucess");

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

            string startupMessage;
            ServerPort serverPort;


            ServerState serverState = new ServerState(name);
            serverPort = new ServerPort(LOCALHOST, port, ServerCredentials.Insecure);
            startupMessage = "[TM]Insecure ChatServer server listening on port " + port;

            BroadcastServicesImpl brdImpl = new BroadcastServicesImpl(serverState);
            LeaseBroadcastImpl lsImpl = new LeaseBroadcastImpl(serverState);
            ClientServicesImpl cltImpl = new ClientServicesImpl(serverState, names_lm.Concat(names_tm).ToList(), urls_lm.Concat(urls_tm).ToList(), types);
            
            Server server = new Server
            {
                Services = { BroadcastServices.BindService(brdImpl), TransactionServices.BindService(cltImpl), LeaseInformServices.BindService(lsImpl) },
                Ports = { serverPort }
            };

            
            server.Start();
            Console.WriteLine(SPACE + startupMessage);
            //Configuring HTTP for client connections in Register method
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            int round_count = 1;

            // TODO: Wait until wall barrier
            int crash_count = 0;
            while (round_count < number_time_slots)
            {
                Thread.Sleep(time_slot_duration);
                if (rounds_of_failure.Contains(round_count))
                {

                    serverState.newFailureRound();

                    foreach (int el in failures_per_round[crash_count])
                    {
                        // TODO : CODE FOR THE CRASH
                        if (el < num_lm + num_tm)
                        {
                            if ("http://localhost:" + port == all_servers[idOrder[el]])
                            {
                                Console.WriteLine(SPACE + "XXXXX NEW CRASH -> " + all_servers[idOrder[el]] + "XXXXX");
                                server.ShutdownAsync().Wait();
                                return;
                            }
                        }
                        else
                        {
                            if ("http://localhost:" + port == all_servers[idOrder[el]])
                            {
                                Console.WriteLine(SPACE + "XXXXX NEW CRASH -> " + all_servers[idOrder[0]] + "XXXXX");
                                server.ShutdownAsync().Wait();
                                return;
                            }
                        }
                        
                    }
                    // CHECK SUSPECTS
                    for (int k = 0; k < suspects_per_round[crash_count].Count; k += 2)
                    {
                        int oq_suspeita = suspects_per_round[crash_count][k] + 1;
                        int o_suspeito = suspects_per_round[crash_count][k + 1] + 1;
                        if (oq_suspeita >= num_lm + num_tm)
                        {
                            oq_suspeita = 0;
                        }
                        if (o_suspeito >= num_lm + num_tm)
                        {
                            o_suspeito = 0;
                        }
                        // A -> B
                        //Console.WriteLine("[OQ SUSPEITA TAM TAM TAM DO LADO DA FUCKING TM]: " + all_servers[idOrder[oq_suspeita]]);
                        //Console.WriteLine("[O SUSPEITO TUM TUM TUM DO LADO DA FUCKING TM]: " + all_servers[idOrder[o_suspeito]]);
                        if (idOrder[oq_suspeita] == YOUR_ID)
                        {
                            serverState.addSuspect(all_names[idOrder[o_suspeito]]);
                        }

                    }

                    crash_count++;

                }
                
                round_count++;
            }

            while (true) { }

        }
    }
}