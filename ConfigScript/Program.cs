using System;
using System.Diagnostics;
using System.IO;
using System.Xml.Schema;
using System.Text.RegularExpressions;

namespace ConfigScript
{
    public class Program
    {
        static string PATH = Process.GetCurrentProcess().MainModule.FileName;
        static string SOLUTION_PATH = getSolutionPath();
        static string TM_PATH = SOLUTION_PATH + "\\TransactionManager\\bin\\Debug\\net6.0\\TransactionManager.exe";
        static string CLI_PATH = SOLUTION_PATH + "\\Client\\bin\\Debug\\net6.0\\Client.exe";
        static string LM_PATH = SOLUTION_PATH + "\\LeaseManager\\bin\\Debug\\net6.0\\LeaseManager.exe";
        
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

            string path_exe = null;
            if (PDcp == "T")
            {
                path_exe = TM_PATH;
            }
            if (PDcp == "L")
            {
                path_exe = LM_PATH;
            }
            if (PDcp == "C")
            {
                path_exe = CLI_PATH;
            }
            if (path_exe != null)
            {
                Process.Start(path_exe, name + " " + inp);
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
            string relativePath = @"..\..\..\configFiles\configuration_sample.txt";
            string filePath = Path.Combine(baseDirectory, relativePath);

            using (StreamReader reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lineBehaviour(line);
                }
            }
        }
    }
}