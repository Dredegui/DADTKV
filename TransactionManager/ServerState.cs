using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransactionManager
{
    public class ServerState
    {
        private Dictionary<string, int> DadInts = new Dictionary<string, int>();
        public int transId = 0;
        public Dictionary<string, List<string>> queue = new Dictionary<string, List<string>>();

        public List<string> suspectList = new List<string>();

        public int epoch = -1;

        private string name;

        public ServerState(string name)
        {
            this.name = name;
        }

        public bool ValidKey(string key)
        {
            return DadInts.ContainsKey(key);
        }

        public string GetValue(string key)
        {
            return "" + DadInts[key];
        }

        public void SetValue(string key, int value) {
            DadInts[key] = value;
        }

        public string GetName() {
            return name;
        }

        public void addSuspect(string suspect)
        { 
            suspectList.Add(suspect);
        }

        public void newFailureRound()
        {
            suspectList.Clear();
        }

        public void printState()
        {
            foreach (var kvp in DadInts) {
                Console.WriteLine("[TM] INTERNAL STATE >> Key: " + kvp.Key + "| Val: " + kvp.Value);
            }

        }
    }
}
