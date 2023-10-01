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
        private string name;

        public ServerState(string name)
        {
            this.name = name;
        }

        public int GetValue(string key)
        {
            return DadInts[key];
        }

        public void SetValue(string key, int value) {
            DadInts[key] = value;
        }

        public string GetName() {
            return name;
        }
    }
}
