﻿namespace Client
{
    internal class Program
    {
        static void Main(string[] args)
        {

            Console.WriteLine("[CLI] Client start");

            for (int i = 0; i < args.Length; i++)
            {
                Console.WriteLine(args[i]);
            }
            return;

            Console.WriteLine("Client Start");
            List<string> tms = new List<string> {"tm1", "tm2"};
            List<string> urls = new List<string> {"http://localhost:5001", "http://localhost:5002"};
            ClientLogic client = new ClientLogic("joao", 0, tms, urls);
            List<string> reads = new List<string>();
            List<string> keys = new List<string> {"balance1", "balance2"};
            List<int> values = new List<int> {40, 10};
            client.TxSubmit(reads, keys, values);
            reads.Add("balance1");
            keys.Clear();
            values.Clear();
            client.TxSubmit(reads, keys, values);

            ClientLoop CLI = new ClientLoop(@"..\..\..\scriptsCLI\DADTKV_client_script_sample.txt");
            CLI.Loop();
        }
    }
}