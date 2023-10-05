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
        private int acceptedRound = -1;
        private List<LeaseTransaction> currentLeases = new List<LeaseTransaction>();
        private List<LeaseTransaction> proposedLeases = new List<LeaseTransaction>();
        public LeaseState()
        {
        }

        public void NextEpoch()
        {
            this.epoch++; 
        }
        public int GetEpoch()
        {
            return this.epoch;
        }

        public void Accept()
        {
            this.acceptedRound++;
        }

        public int GetAcceptedRound()
        {
            return this.acceptedRound;
        }

        public bool Updated(int leaseEpoch) {
            return leaseEpoch == acceptedRound;
        }

        public List<LeaseTransaction> GetCurrentLeases()
        {
            return this.currentLeases;
        }

        public void AcceptLeases(List<LeaseTransaction> leases)
        {
            currentLeases = leases;
        }

        public void ClearCurrentLeases() {
            currentLeases.Clear();
        }

        public List<LeaseTransaction> GetProposedLeases()
        {
            return this.proposedLeases;
        }

        public void AddProposedLeases(string tm, List<string> leases)
        {
            LeaseTransaction lt = new LeaseTransaction();
            lt.tm = tm;
            lt.leases = leases;
            this.proposedLeases.Add(lt);
        }

        public void ClearProposed()
        {
            proposedLeases.Clear();
        }
    }
}
