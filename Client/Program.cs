﻿namespace Client
{
    internal class Program
    {

        private static string RELATIVE_PATH = @"..\..\..\..\Client\scriptsCLI\";

        private static int getPort(string hostname)
        {
            return Int32.Parse(hostname.Split(':')[2]);
        }
        static void Main(string[] args)
        {
            
            string name = args[0];
            string script_name = args[1];
            int chosen_tm = Int32.Parse(args[2]);

            // GET Tms
            int num_tm = Int32.Parse(args[3]);


            List<string> tms = new List<string>();
            List<string> urls = new List<string>();
            for (int i = 0; i < num_tm; i++)
            {
                tms.Add("NO_NAME_YET");
                int port = getPort(args[4 + i]);
                Console.WriteLine("[CLI] New connection with TM http://localhost:" + port.ToString());
                urls.Add("http://localhost:" + port.ToString());
            }


            // WALL BARRIER
            string wall_barrier = args[4 + num_tm];

            Console.WriteLine("[CLI] Started to run a client");

            ClientLogic client = new ClientLogic(name, chosen_tm, tms, urls);
            ClientLoop CLI = new ClientLoop(RELATIVE_PATH + script_name + ".txt",client);
            CLI.Loop();
        }
    }
}