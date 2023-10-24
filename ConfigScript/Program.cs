using System;
using System.Diagnostics;
using System.IO;
using System.Xml.Schema;
using System.Text.RegularExpressions;
using System.Collections;
using System.Security.Cryptography;
using System.Globalization;
using System.Runtime.CompilerServices;

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


        static string wall_barrier;
        static string time_slot_duration;
        static string number_of_time_slots;

        static List<string> all_servers_names = new List<string>();

        static List<string> rounds_of_failure = new List<string>();
        static List<List<int>> failures_per_round = new List<List<int>>();
        static List<int> num_failures_per_round = new List<int>();
        static int num_failures;

        static List<List<string>> suspects_per_round =  new List<List<string>>();
        static List<int> num_suspects_per_round = new List<int>();


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
                all_servers_names.Add(name);
                tm_hosts_run.Add(inp);
                num_tm++;
            }
            if (PDcp == "L")
            {
                lm_names += " " + name;
                lm_hosts += " " + inp;
                lm_names_run.Add(name);
                all_servers_names.Add(name);
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

        public static string getNameId(string name)
        {
            int ret = 0;

            foreach (string s in all_servers_names)
            {
                if (s == name)
                {
                    return "" + ret; 
                }
                ret++;
            }

            return "NOT POSSIBLE, THIS IS AN ERROR";
        }

        public static string parseSuspect(string suspect)
        {

            string s1 = "";
            string s2 = "";
            bool seen = false;
            foreach(char c in suspect)
            {
                if (c != ')' && c != '(' && !seen && c != ',') 
                {
                    s1 += c;
                }
                if (c == ',')
                {
                    seen = true;
                }
                if (c != ')' && c != '(' && seen && c != ',')
                {
                    s2 += c;
                }

            }

            return getNameId(s1) + " " + getNameId(s2);
        }

        public static void parseFailure(string line)
        {
            int count = 0;
            int all_servers = all_servers_names.Count();

            Tuple<string, string> split = splitStr(line);
            string failure_round = split.Item1;
            rounds_of_failure.Add(failure_round);
            line = split.Item2;
            Console.WriteLine("BITCH: " + line);

            // CHECK THE CRASHES
            List<int> failures = new List<int>();
            int num_failures_round = 0;
            while (count <=  all_servers)
            {
                split = splitStr(line);
                string n = split.Item1;
                line = split.Item2;
                count++;

                if (n == "C")
                {
                    failures.Add(count);
                    num_failures_round++;
                }
            }

            failures_per_round.Add(failures);
            num_failures_per_round.Add(num_failures_round);

            // CHECK THE SUSPECTS

            List<string> suspects = new List<string>();
            int num_suspects = 0;
            while (line != "")
            {
                split = splitStr(line);
                string n = split.Item1;
                line = split.Item2;
                suspects.Add(parseSuspect(n));
                num_suspects++;
            }
            suspects_per_round.Add(suspects);
            num_suspects_per_round.Add(num_suspects);

            Console.Write("CRASHOU :::> ");
            foreach (int e in failures)
            {
                Console.Write("[" + e + "]");
            }

            Console.WriteLine(num_failures_round.ToString());


            Console.Write("DESCONFIOU:::> ");
            foreach (string e in suspects)
            {
                Console.Write("[" + e + "]");
            }

            Console.WriteLine(num_suspects);

        }

        
        public static string buildFailureArguments()
        {

            // <num_falhas> <ronda> <num_faiture> <faiulures> <num_suspects> <suspects>

            string ret = "";

            ret += num_failures.ToString() + " ";

            int i = 0;

            while (i < num_failures)
            {
                ret += rounds_of_failure[i] + " ";
                ret += num_failures_per_round[i] + " ";

                int j = 0; 
                
                while (j < num_failures_per_round[i])
                {
                    ret += failures_per_round[i][j] + " ";
                    j += 1;
                }

                ret += num_suspects_per_round[i] + " ";

                j = 0;

                while (j < num_suspects_per_round[i])
                {
                    ret += suspects_per_round[i][j] + " ";
                    j += 1;
                }

                i += 1;

            }


            Console.WriteLine("COMPLETED: " + ret);

            return ret;
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

            // PHYSICAL WALL TIME
            if (func == "T")
            {
                wall_barrier = line;
                Console.WriteLine("[CONFIG] Physical wall time " + wall_barrier);
            }

            // TIME SLOT DURATION
            if (func == "D")
            {
                time_slot_duration = line;
                Console.WriteLine("[CONFIG] Time slot duration " + time_slot_duration);
            }

            if (func == "S")
            {
                number_of_time_slots = line;
                Console.WriteLine("[CONFIG] Number of time slots: " +  number_of_time_slots);
            }

            // FAILURES
            if (func == "F")
            {
                num_failures++;
                parseFailure(line);
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

            Console.WriteLine("NUM OF FAILURES: " + num_failures);


            foreach (string s in rounds_of_failure) {
                Console.WriteLine("RONDA: " + s);
            }

            // Create LM
            for (int i = 0; i < num_lm;i++)
            {
                Console.WriteLine("[CONFIG] LM started with sucess");
                Process.Start(LM_PATH, i + " " + lm_names_run[i] + " " + lm_hosts_run[i] + " " + num_lm.ToString() + " " + lm_hosts + " " + num_tm.ToString() + " " + tm_hosts + " " +  wall_barrier + " " + time_slot_duration + " " + number_of_time_slots  + " " + buildFailureArguments());
            }
            //Thread.Sleep(1000);
            // Create TM 
            for (int i = 0; i < num_tm;i++)
            {
                Console.WriteLine("[CONFIG] TM started with sucess");
                Process.Start(TM_PATH, tm_names_run[i] + " " + tm_hosts_run[i] + " " + num_lm.ToString() + " "+ lm_hosts + " " + num_tm.ToString() + " " + tm_hosts + " " + wall_barrier + " " + time_slot_duration + " " + number_of_time_slots + " " + buildFailureArguments());
                //Thread.Sleep(500);
            }
            // Create CLI
            for (int i = 0; i < num_cli; i++)
            {
                Console.WriteLine("[CONFIG] Cliented started with sucess");
                Process.Start(CLI_PATH, cli_names_run[i] + " " + cli_scripts_run[i] + " " + i%num_tm + " " + num_tm.ToString() + " " + tm_hosts + " " + wall_barrier);
                //Thread.Sleep(500);
            }
            while (true);
            // TODO : INCOMPLETO 
        }
    }
}