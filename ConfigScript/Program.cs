using System;
using System.Diagnostics;
using System.IO;
using System.Xml.Schema;
using System.Text.RegularExpressions;
using System.Collections;

namespace ConfigScript
{
    public class Program
    {
        static readonly string PATH = Process.GetCurrentProcess().MainModule.FileName;
        static readonly string SOLUTION_PATH = getSolutionPath();
        static readonly string INTER_PATH = "\\bin\\Debug\\net6.0";
        static readonly string TM_PATH = SOLUTION_PATH + "\\TransactionManager" + INTER_PATH + "\\TransactionManager.exe";
        static readonly string CLI_PATH = SOLUTION_PATH + "\\Client" + INTER_PATH + "\\Client.exe";
        static readonly string LM_PATH = SOLUTION_PATH + "\\LeaseManager" + INTER_PATH +  "\\LeaseManager.exe";

        static string tm_names;
        static string lm_names;
        static List<string> tm_names_run = new List<string>();
        static List<string> lm_names_run = new List<string>();

        static string tm_hosts;
        static string lm_hosts;
        static List<string> tm_hosts_run = new List<string>();
        static List<string> lm_hosts_run = new List<string>();

        static List<string> cli_names_run = new List<string>();
        static List<string> cli_scripts_run = new List<string>();

        static int num_lm;
        static int num_tm;
        static int num_cli;

        
        public static Tuple<string,string> splitStr(string str)
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
        
        public static void startNewProcess(Tuple<string,string> split, string line)
        {
            // GET NAME
            string name = split.Item1;
            line = split.Item2;

            // WHICH PROCESS
            split = splitStr(line);
            string PDcp = split.Item1;
            string inp = split.Item2;

            if (PDcp == "T")
            {
                tm_names += " " + name;
                tm_hosts += " " + inp;
                tm_names_run.Add(name);
                tm_hosts_run.Add(inp);
                num_tm++;
            }
            if (PDcp == "L")
            {
                lm_names += " " + name;
                lm_hosts += " " + inp;
                lm_names_run.Add(name);
                lm_hosts_run.Add(inp);
                num_lm++;
            }
            if (PDcp == "C")
            {
                cli_names_run.Add(name);
                cli_scripts_run.Add(inp);
                num_cli++;
            }
        }

        public static void lineBehaviour(string line)
        {

            Tuple<string,string> split = splitStr(line);
            string func = split.Item1;
            line = split.Item2;
            split = splitStr(line);

            // Creating a new process
            if (func == "P")
            {
                startNewProcess(split, line);
            }
            if (func != "#" && func != "P")
            {
                Console.Write("// TODO: ");
                Console.WriteLine(line);
            }
        }
        public static string getSolutionPath()
        {
            string ret = "";
            int contador = 0;
            int i = PATH.Length - 1;
            while (contador < 5) 
            {
                if (PATH[i] == '\\')
                {
                    contador += 1;
                }
                i--;
            }
            int j = 0;

            while (j <= i) 
            {
                ret += PATH[j];
                j++;
            }
            return ret;
        }

        public static void Main(string[] args)
        {
            // OPEN CONFIG SCRIPT
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string relativePath = @"..\..\..\configFiles\config_script_1.txt";
            string filePath = Path.Combine(baseDirectory, relativePath);

            using (StreamReader reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lineBehaviour(line);
                }
            }

            // Create LM
            for (int i = 0; i < num_lm;i++)
            {
                Console.WriteLine("[CONFIG] LM started with sucess");
                Process.Start(LM_PATH, i + " " + lm_names_run[i] + " " + lm_hosts_run[i] + " " + num_lm.ToString() + " " + lm_hosts + " " + num_tm.ToString() + " " + tm_hosts);
            }
            //Thread.Sleep(1000);
            // Create TM 
            for (int i = 0; i < num_tm;i++)
            {
                Console.WriteLine("[CONFIG] TM started with sucess");
                Process.Start(TM_PATH, tm_names_run[i] + " " + tm_hosts_run[i] + " " + num_lm.ToString() + " "+ lm_hosts + " " + num_tm.ToString() + " " + tm_hosts);
                //Thread.Sleep(500);
            }
            // Create CLI
            for (int i = 0; i < num_cli; i++)
            {
                Console.WriteLine("[CONFIG] Cliented started with sucess");
                Process.Start(CLI_PATH, cli_names_run[i] + " " + cli_scripts_run[i] + " " + i%num_tm + " " + num_tm.ToString() + "  "+ tm_hosts);
                //Thread.Sleep(500);
            }
            while (true);
            // TODO : INCOMPLETO 
        }
    }
}