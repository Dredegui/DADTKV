using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization.Formatters;
using System.Text;
using System.Threading.Tasks;

namespace TransactionManager
{
    public class ClientServicesImpl : TransactionServices.TransactionServicesBase
    {
        private ServerState state;
        private Dictionary<string, BroadcastServices.BroadcastServicesClient> stubsTM = new Dictionary<string, BroadcastServices.BroadcastServicesClient> ();
        private Dictionary<string, LearnServices.LearnServicesClient> stubsLM = new Dictionary<string, LearnServices.LearnServicesClient> ();
        private List<string> leases = new List<string> ();
        private int transcationId;
        private int counter = 0;
        private List<string> names;
        private List<string> urls;
        private List<int> types;
        static string SPACE = "                                    ";
        public ClientServicesImpl(ServerState state, List<string> names, List<string> urls, List<int> types)
        {
            this.state = state;
            transcationId = 0;
            this.names = names;
            this.urls = urls;
            this.types = types;
        }

        public override Task<SubmitReply> Submit(SubmitRequest request, ServerCallContext context)
        {
            return TxSubmit(request);
        }

        public void registerStubs()
        {
            for (int i = 0; i < urls.Count; i++)
            {
                GrpcChannel channel = GrpcChannel.ForAddress(urls[i]);
                if (types[i] == 0)
                {
                    // Transaction Managers stub
                    stubsTM[urls[i]] = new BroadcastServices.BroadcastServicesClient(channel);
                }
                else
                {
                    // Lease Managers stub
                    stubsLM[urls[i]] = new LearnServices.LearnServicesClient(channel);
                }

            }
        }


        public bool checkLeases(List<string> reads, List<string> keys) {
            return !reads.Except(leases).Any() && !keys.Except(leases).Any();
        }

        

        public bool checkQueue(List<string> reads, List<string> keys)
        {
            Console.WriteLine(SPACE + "[" + state.GetName() + transcationId + "] CHECK QUEUE");
            foreach (string read in reads)
            {
                if (state.queue[read][0] != state.GetName())
                {
                    return false;
                }
            }

            foreach (string key in keys)
            {
                if (state.queue[key][0] != state.GetName())
                {
                    return false;
                }
            }
            Console.WriteLine(SPACE + "[" + state.GetName() + transcationId + "] END CHECK QUEUE");
            return true;
        }


        public bool checkMyLeases(List<string> leases)
        {
            foreach (string lease in leases)
            {
                if (!state.queue[lease].Contains(state.GetName()))
                {
                    return false;
                }
            }
            return true;
        }

        public bool removeFromQueue(List<string> reads, List<string> keys)
        {
            Console.WriteLine(SPACE + "[" + state.GetName() + transcationId + "] REMOVING READS");
            foreach (string read in reads)
            {
                state.queue[read].RemoveAt(0);
                if (state.queue[read].Count == 0 && !leases.Contains(read))
                {
                    leases.Add(read);
                }
            }
            Console.WriteLine(SPACE + "[" + state.GetName() + transcationId + "] REMOVING WRITES");
            foreach (string key in keys)
            {
                if (state.queue[key][0] == state.GetName())
                {
                    state.queue[key].RemoveAt(0);
                    if (state.queue[key].Count == 0 && !leases.Contains(key))
                    {
                        leases.Add(key);
                    }
                }
            }
            Console.WriteLine(SPACE + "[" + state.GetName() + transcationId + "] END REMOVE");
            return true;
        }

        private async void TimerThread(Object queue)
        {
            Dictionary<string, List<string>> shallowCopy = (Dictionary<string, List<string>>)queue;
            
            // Simulate some work in the function thread.
            Thread.Sleep(100);
            bool diff = false;
            LeaseUpdateRequest request = new LeaseUpdateRequest();
            request.Host = state.hostport;
            foreach (string key in shallowCopy.Keys)
            {
                if (state.queue[key].Count < shallowCopy[key].Count)
                {
                    diff = true;
                }
            }
            foreach (string lease in shallowCopy.Keys)
            {
                request.Leases.Add(lease);
                request.Lenghts.Add(shallowCopy[lease].Count);
            }
            if (!diff)
            {
                Console.WriteLine(SPACE + "[" + state.GetName() + transcationId + " TIMER] Timer handling started");
                // Broadcast leases to see if there is an invalid tm
                List<Task<LeaseUpdateReply>> updateAwaitList = new List<Task<LeaseUpdateReply>>();
                try
                {
                    foreach (string stubName in stubsTM.Keys)
                    {
                        if (!state.suspectList.Contains(stubName))
                        {
                            updateAwaitList.Add(stubsTM[stubName].LeaseUpdateAsync(request).ResponseAsync);
                        } else
                        {
                            Console.WriteLine(SPACE + "[" + state.GetName() + "] Ignores because it suspects " + stubName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(SPACE + "(ERROR)[" + state.GetName() + " id: " + transcationId + "] Trying to use a stub that is unavailable");
                    Console.WriteLine(ex.ToString());
                }
                Console.WriteLine(SPACE + "[" + state.GetName() + transcationId + "] Waiting for every TM to respond");
                List<LeaseUpdateReply> updateResults = new List<LeaseUpdateReply>();
                foreach (Task<LeaseUpdateReply> update in updateAwaitList)
                {
                    try
                    {
                        await update;
                        updateResults.Add(update.Result);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(SPACE + "(ERROR)[TM] Waiting for a result that will never come");
                    }
                }
                bool removeLease = true;
                foreach (LeaseUpdateReply update in updateResults)
                {
                    if (update.Ack)
                    {
                        removeLease = false;
                        foreach (Queue temp in update.Update)
                        {
                            if (state.queue[temp.Lease].Count > temp.Tms.Count)
                            {
                                state.queue[temp.Lease] = temp.Tms.ToList();
                            }
                        }
                    }
                }
                if (removeLease)
                {
                    foreach (string lease in request.Leases)
                    {
                        if (state.queue[lease][0] != state.GetName())
                        {
                            state.queue[lease].RemoveAt(0);
                        }
                    }
                }
                // The function thread can pulse and wake up the main thread.
                lock (state)
                {
                    Monitor.PulseAll(state);
                }               
            }
            else
            {
                Console.WriteLine(SPACE + "[" + state.GetName() + transcationId + " TIMER] Timer was not activited - Everything is OK");
            }
            
        }


        public void printQueue(string pos)
        {
            foreach (string key in state.queue.Keys)
            {
                string vals = "";
                foreach (string val in state.queue[key])
                {
                    vals += val + ", ";
                }
                Console.WriteLine(SPACE + "[" + state.GetName() + transcationId + "]" + pos + "key: " + key + "| list: " + vals);
            }
        }

        public async Task<SubmitReply> TxSubmit(SubmitRequest request)
        {
            transcationId = state.transId;
            state.transId = transcationId + 1;
            Console.WriteLine(SPACE + "[" + state.GetName() + " id: " + transcationId + "] Start a submit request");
            if (counter == 0)
            {
                this.registerStubs();
                Console.WriteLine(SPACE + "[" + state.GetName() + " id: " + transcationId + "] Registed stubs for another TM and LM");
                counter++;
            }
            
            // Initialization
            List<string> reads = request.Reads.ToList();
            List<string> keys = request.Keys.ToList();
            List<int> values = request.Values.ToList();
            List<string> results = new List<string>();
            List<string> transactionLeases = reads.Concat(keys).Distinct().ToList();
            if (!checkLeases(reads, keys))
            { 
                Console.WriteLine(SPACE + "[" + state.GetName() + " id: " + transcationId + "] Dont have the Lease for the request of the client => Build new request for LM");
                LearnRequest learnRequest = new LearnRequest();
                learnRequest.Tm = state.GetName();
                learnRequest.Leases.AddRange(transactionLeases);
                List<Task<LearnReply>> learnAwaitList = new List<Task<LearnReply>>();
                try
                {
                    foreach (LearnServices.LearnServicesClient stub in stubsLM.Values)
                    {
                        Console.WriteLine(SPACE + "[" + state.GetName() + " id: " + transcationId + "] Sending lease requests for LM");
                        learnAwaitList.Add(stub.LearnAsync(learnRequest).ResponseAsync);
                    }
                } catch (Exception ex)
                {
                    Console.WriteLine(SPACE + "(ERROR)[" + state.GetName() + " id: " + transcationId + "] Trying to request a Lease to an LM that is CRASHED");
                }
                
                Console.WriteLine(SPACE + "[" + state.GetName() + " id: " + transcationId + "] Waiting for every LM to respond");
                List<LearnReply> learnResults = new List<LearnReply>();
                foreach (Task<LearnReply> learn in learnAwaitList)
                {
                    try
                    {
                        await learn;
                        if (learn.Result != null)
                        {
                            learnResults.Add(learn.Result);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("(ERROR)[TM] Trying to wait for an LM that is CRASHED");
                    }
                }
                Console.WriteLine(SPACE + "[" + state.GetName() + " id: " + transcationId + "] Waited for learn ACKS");
                
                Console.WriteLine(SPACE + "[" + state.GetName() + " id: " + transcationId + "] NUMBER OF ACKS: " + learnResults.Count);
                // Wait for lease inform broadcast
                lock (state)
                {
                    do
                    {
                        Monitor.Wait(state);
                    } while (!checkMyLeases(transactionLeases));
                }
                // save our leases TODO Ver com stor (libertar sempre)
                Console.WriteLine(SPACE + "[" + state.GetName() + " id: " + transcationId + "] Reset previous Leases //TODO: Dont do this");
                leases = new List<string>();
                Dictionary<string, List<string>> improvQueue = new Dictionary<string, List<string>>();
                foreach (string key in transactionLeases)
                {
                    improvQueue[key] = new List<string>();
                    foreach (string value in state.queue[key])
                    {
                        improvQueue[key].Add(value);
                    }
                }
                while (!checkQueue(reads, keys))
                {
                    Thread functionThread = new Thread(TimerThread);
                    functionThread.Start(improvQueue);
                    Console.WriteLine(SPACE + "[" + state.GetName() + " id: " + transcationId + "] Its not my turn yet so I will sleep until it is");
                    lock (state)
                    {
                        Monitor.Wait(state);
                        Console.WriteLine(SPACE + "[" + state.GetName() + transcationId + "] WOKE UPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP");
                    }
                }
                removeFromQueue(reads, keys); // TODO SAME LEASES ON 2 TRANSACTIONS ON SAME TM
                Console.WriteLine(SPACE + "[" + state.GetName() + " id: " + transcationId + "] It's my turn on the queue so I will do the read and write operations");

            }
            PermissionRequest permissionRequest = new PermissionRequest();
            permissionRequest.Host = state.hostport;
            List<Task<PermissionReply>> permissionAwaitList = new List<Task<PermissionReply>>();
            try
            {
                foreach(string stubName in stubsTM.Keys)
                {
                    if (!state.suspectList.Contains(stubName))
                    {
                        permissionAwaitList.Add(stubsTM[stubName].ReceivePermissionAsync(permissionRequest).ResponseAsync);
                    }
                    else
                    {
                        Console.WriteLine("[" + state.GetName() + "] Ignores because it suspects " + stubName);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(SPACE + "(ERROR)[" + state.GetName() + " id: " + transcationId + "] Trying to request permissions to a CRASHED server");
            }
            int permissionNum = 0;
            foreach (Task<PermissionReply> permission in permissionAwaitList)
            {
                try
                {
                    await permission;
                    if (permission.Result != null && permission.Result.Value)
                    {
                        permissionNum++;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(SPACE + "(ERROR)[TM] Waiting for a response that will never come");
                }
            }
            SubmitReply reply = new SubmitReply();
            if (permissionNum / (permissionAwaitList.Count + 0.0F) <= 0.5)
            {
                reply.Keys.Add("Abort");
                Console.WriteLine(SPACE + "[" + state.GetName() + " id: " + transcationId + "] Aborted");
                return reply;
            } else
            {
                Console.WriteLine(SPACE + "[" + state.GetName() + " id: " + transcationId + "] Has the majority doesn't need to abort");

            }
            List<int> lengthsReads = new List<int>();
            List<int> lengthsWrites = new List<int>();
            lock (state)
            {
                foreach (string read in reads)
                {
                    lengthsReads.Add(state.queue[read].Count);
                    if (state.ValidKey(read))
                    {
                        results.Add(state.GetValue(read));
                    }
                    else
                    {
                        results.Add("unknown DadInt");
                    }
                }
                Console.WriteLine(SPACE + "[" + state.GetName() + " id: " + transcationId + "] Read operations done with sucess: Build response for the client");
                // Write operation

                for (int i = 0; i < keys.Count; i++)
                {
                    lengthsWrites.Add(state.queue[keys[i]].Count);
                    state.SetValue(keys[i], values[i]);
                }
                Console.WriteLine(SPACE + "[" + state.GetName() + " id: " + transcationId + "] Write operation done with success: Changed values");
            }

            BroadcastMessage message = new BroadcastMessage();
            message.Id = transcationId;
            message.Name = state.GetName();
            message.Host = state.hostport;
            message.Keys.AddRange(keys);
            message.Values.AddRange(values);
            message.Reads.AddRange(reads);
            message.Lenghts.AddRange(lengthsWrites.Concat(lengthsReads));
            Console.WriteLine(SPACE + "[" + state.GetName() + " id: " + transcationId + "] Broadcast to other tms");
            
            List<Task<BroadcastAck>> broadWaitList = new List<Task<BroadcastAck>>();
            try
            {
                foreach (string stubName in stubsTM.Keys)
                {
                    if (!state.suspectList.Contains(stubName))
                    {
                        broadWaitList.Add(stubsTM[stubName].BroadcastAsync(message).ResponseAsync);
                    }
                    else
                    {
                        Console.WriteLine("[" + state.GetName() + "] Ignores because it suspects " + stubName);
                    }
                }
            } catch (Exception ex)
            {
                Console.WriteLine(SPACE + "(ERROR)[" + state.GetName() + " id: " + transcationId + "] Trying to broadcast to a CRASHED TM");
            }
            
            Console.WriteLine(SPACE + "[" + state.GetName() + " id: " + transcationId + "] Broadcasted request for another TMs in order to replicate the state");
            reply.Keys.AddRange(reads);
            reply.Values.AddRange(results);
            return reply;
        }

    }
}
