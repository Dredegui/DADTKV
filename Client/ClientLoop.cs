using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    public class ClientLoop
    {
        private static string SCRIPT_PATH;
        public ClientLoop(string script_path) 
        {
            SCRIPT_PATH = script_path;
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

        private void NewTransaction(string line)
        {
            Console.WriteLine("[CLI] Asking for new transaction is on our TODO list");
        }
        private void CheckStatus()
        {
            Console.WriteLine("[CLI] Checking status is on our TODO list");
        }
        public void Loop()
        {
            var lines = File.ReadLines(SCRIPT_PATH);
            

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

            while (true);
        }

    }
}
