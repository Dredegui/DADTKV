using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace LeaseManager
{
    internal class LeaseState
    {
        private int epoch = 0;
        private int accepted = 0;
        private Dictionary<string, List<string>> currentLeases = new Dictionary<string, List<string>>();
        private Dictionary<string, List<string>> proposedLeases = new Dictionary<string, List<string>>();
        private Dictionary<string, List<string>> todoLeases = new Dictionary<string, List<string>>();
        private List<string> todoTMNames = new List<string>(); // Save todo dictionary order

        public LeaseState() {
        }

        public void NextEpoch()
        {
            this.epoch++; 
        }
        public int GetEpoch()
        {
            return this.epoch;
        }

        public void accept()
        {
            this.accepted++;
        }

        public bool Updated() {
            return this.accepted == this.epoch;
        }

        public void AcceptLeases(Dictionary<string, List<string>> leases)
        {
            currentLeases = leases;
        }

        public void ClearState() {
            currentLeases.Clear();
        }

        public bool AddProposedLease(string tm, List<string> leases)
        {
            List<string> prLeases = new List<string>();
            foreach (var list in proposedLeases.Values)
            {
                prLeases.AddRange(list);
            }
            foreach (string lease in leases)
            {
                if (prLeases.Contains(lease) && !proposedLeases[tm].Contains(lease))
                {
                    todoLeases[tm] = leases;
                    todoTMNames.Add(tm);
                    return false;
                }
            }
            proposedLeases[tm] = proposedLeases[tm].Concat(leases).Distinct().ToList();
            return true;
        }

        public void ClearProposed() {
            proposedLeases.Clear();
            List<string> addedLeases = new List<String>();
            for (int i = 0; i< todoTMNames.Count; i++)
            {
                string tm = todoTMNames[i];
                if (!addedLeases.Intersect(todoLeases[tm]).Any()) {
                    addedLeases.AddRange(todoLeases[tm]);
                    proposedLeases[tm] = todoLeases[tm];
                    todoLeases.Remove(tm);
                    todoTMNames.RemoveAt(i);
                    i--;
                }
                
            }
        }

        public void CompareProposed()
        {
            foreach (string key in proposedLeases.Keys)
            {
                if (currentLeases.ContainsKey(key))
                {
                    int inter = currentLeases[key].Intersect(proposedLeases[key]).Count();
                    if (inter == proposedLeases[key].Count)
                    {
                        continue;
                    }
                }
                todoTMNames.Insert(0, key);
                todoLeases[key] = proposedLeases[key];
            }
        }
    }
}
