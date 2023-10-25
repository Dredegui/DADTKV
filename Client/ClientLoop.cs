using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Client
{
    public class ClientLoop
    {
        private static string SCRIPT_PATH;
        private ClientLogic client;
        public ClientLoop(string script_path, ClientLogic client) 
        {
            SCRIPT_PATH = script_path;
            this.client = client;
        }

        public static Tuple<string, string> splitStr(string str)
        {
            string ret_1 = "";
            string ret_2 = "";
            bool s = false;
            int size = str.Length;

            for (int i = 0; i < size; i++)
            {
                if (str[i] == ' ' && !s)
                {
                    s = true;
                }
                else
                {
                    if (!s)
                    {
                        ret_1 += str[i];
                    }
                    else
                    {
                        ret_2 += str[i];
                    }
                }
            }

            return Tuple.Create(ret_1, ret_2);
        }

        private List<List<string>> split_reads_and_write(string line)
        {
            int size = line.Length;
            List<string> reads = new List<string>();
            List<string> keys = new List<string>();
            List<string> values = new List<string>();

            string[] parts = line.Split(' ');

            string reads_str = parts[0];
            string writes_str = parts[1];

            // READS
            reads_str = reads_str.TrimStart('(').TrimEnd(')');
            if (reads_str != "")
            {
                string[] reads_keys = reads_str.Split(',');
                for (int i = 0; i < reads_keys.Length; i++)
                {
                    string key = reads_keys[i].TrimStart('\"').TrimEnd('\"');
                    reads.Add(key);
                }
            }


            // WRITES
            writes_str = writes_str.TrimStart('(').TrimEnd(')');
            if (writes_str != "")
            {
                string pattern = @"<""([^""]+)"",(\d+)>";
                MatchCollection matches = Regex.Matches(writes_str, pattern);

                foreach (Match match in matches)
                {
                    string key = match.Groups[1].Value;
                    string value = match.Groups[2].Value;
                    keys.Add(key);
                    values.Add(value);
                }
            }
 

            List<List<string>> ret = new List<List<string>>{reads, keys, values};
 
            return ret;
        }

        private void NewTransaction(string line)
        {
            Console.WriteLine("[CLI] Starting a new transaction");
            List<List<string>> req = split_reads_and_write(line);
            List<int> values = new List<int>();
            int size_values = req[2].Count;


            for (int i = 0; i < size_values; i++)
            {
                values.Add(Int32.Parse(req[2][i]));
            }
            Console.WriteLine("[CLI] Requesting new transaction for TM server");
            client.TxSubmit(req[0], req[1], values);
        }
        private void CheckStatus()
        {
            Console.WriteLine("[CLI] Checking status is on our TODO list");
        }
        public void Loop()
        {
            var lines = File.ReadLines(SCRIPT_PATH);
            
            while (true)
            {
                foreach (var line in lines)
                {
                    Tuple<string, string> split = splitStr(line);
                    string com = split.Item1;

                    if (com == "T")
                    {
                        NewTransaction(split.Item2);
                    }
                    if (com == "W")
                    {
                        Thread.Sleep(Int32.Parse(split.Item2));
                    }
                    if (com == "S")
                    {
                        CheckStatus();
                    }
                }
            }
        }

    }
}
