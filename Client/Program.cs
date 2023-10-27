namespace Client
{
    internal class Program
    {

        private static string RELATIVE_PATH = @"..\..\..\..\Client\scriptsCLI\";
        static string SPACE = "                                                                        ";
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
            string script_name = args[1];
            int chosen_tm = Int32.Parse(args[2]);

            int s = DateTime.Now.Second;
            int until_next_minute = 60 - s;
            int m = 1000 - DateTime.Now.Millisecond;

            DateTime wantedTimeToStart = DateTime.Now.AddSeconds(until_next_minute).AddMilliseconds(m); // Replace this with your desired start time

            var task = WaitForTimeAsync(wantedTimeToStart);

            // You can wait for the task to complete using Wait
            task.Wait();
            // GET Tms
            int num_tm = Int32.Parse(args[3]);

            // WALL BARRIER
            string wall_barrier = args[4 + num_tm];

            List<string> tms = new List<string>();
            List<string> urls = new List<string>();
            for (int i = 0; i < num_tm; i++)
            {
                tms.Add("NO_NAME_YET");
                int port = getPort(args[4 + i]);
                urls.Add("http://localhost:" + port.ToString());
            }

            Console.WriteLine(SPACE + "[CLI] Created connections with every TM with sucess");

            Console.WriteLine(SPACE + "[CLI] Running client script");

            ClientLogic client = new ClientLogic(name, chosen_tm, tms, urls);
            ClientLoop CLI = new ClientLoop(RELATIVE_PATH + script_name + ".txt",client);
            CLI.Loop();
        }
    }
}