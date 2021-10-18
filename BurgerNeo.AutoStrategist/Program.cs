using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Numerics;
using System.Linq;
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using Neo.VM;
using Utility = Neo.Network.RPC.Utility;
using GAMS;

namespace BurgerNeo.AutoStrategist
{
    class AutoStrategist
    {
        static readonly OperatingSystem os = Environment.OSVersion;
        static readonly UInt160 burgerneo_contract = UInt160.Parse("0x48c40d4666f93408be1bef038b6722404d9a4c2a");
        static readonly UInt160 neo_contract = NativeContract.NEO.Hash;

        static readonly string wif;  // secret key of BurgerNeo Strategist in Wallet Import Format
        static readonly KeyPair keypair;
        static readonly UInt160 contract;
        static readonly Signer[] signers;

        static readonly string rpc;  // RPC URL for mainnet
        static readonly ProtocolSettings settings;  // config.json for mainnet
        static readonly RpcClient client;

        static readonly TransactionManagerFactory factory;

        static AutoStrategist()
        {
            if (os.Platform == PlatformID.Win32NT || os.Platform == PlatformID.Win32Windows)
            {
                wif = Environment.GetEnvironmentVariable("BURGERNEO_WIF", EnvironmentVariableTarget.User)!;
                rpc = Environment.GetEnvironmentVariable("BURGERNEO_RPC", EnvironmentVariableTarget.User)!;
            }
            else
            {
                wif = Environment.GetEnvironmentVariable("BURGERNEO_WIF")!;
                rpc = Environment.GetEnvironmentVariable("BURGERNEO_RPC")!;
            }

            keypair = Utility.GetKeyPair(wif);
            contract = Contract.CreateSignatureContract(keypair.PublicKey).ScriptHash;
            signers = new[] { new Signer { Scopes = WitnessScope.CalledByEntry, Account = contract } };

            settings = ProtocolSettings.Load("/dev/stdin");
            client = new RpcClient(new Uri(rpc), null, null, settings);
            factory = new TransactionManagerFactory(client);
        }

        static void Main()
        // Read the current NEO balance of each agent
        // Generate the plan of changing NEO balance
        // Compute the GAS reward of the new plan
        // If the plan yields more than 110% that of the current status, re-balance NEO among agents to execute the new plan
        {
            // List of candidates' publicKey
            //List<string> voting_target_plans = ReadEnvironmentVariableAsStringArray("BURGERNEO_VOTING_TARGET_PLANS")!.ToList();
            //LogList("voting_target_plans", voting_target_plans);

            // List of the plan of change of NEOs of each agent.
            // Positive value means this agent needs extra NEO.
            // Negative value means this agent should send NEO to other agents.
            //List<BigInteger> transfer_plans = Array.ConvertAll(ReadEnvironmentVariableAsStringArray("BURGERNEO_TRANSFER_PLANS"), BigInteger.Parse)!.ToList();
            //LogList("transfer_plans", transfer_plans);

            //if (voting_target_plans.Count != transfer_plans.Count)
            //    throw new ArgumentException($"voting_target_plans.Count != transfer_plans.Count; got {voting_target_plans.Count} and {transfer_plans.Count}");
            //BigInteger sum_transfer_plans = transfer_plans.Sum();
            //if (sum_transfer_plans != 0)
            //    throw new ArithmeticException($"Sum of BURGERNEO_TRANSFER_PLANS must be 0; got {sum_transfer_plans}");

            // agent scripthashes
            var tmp_agents = client.InvokeScriptAsync(
                Convert.FromBase64String("AAARwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAAERwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAAIRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAAMRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAAQRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAAURwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAAYRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAAcRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAAgRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAAkRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAAoRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAAsRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAAwRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAA0RwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAA4RwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSAA8RwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSABARwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSABERwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSABIRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSABMRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtSABQRwB8MBWFnZW50DBQqTJpNQCJniwPvG74INPlmRg3ESEFifVtS")
                ).GetAwaiter().GetResult().Stack.ToList();
            // Find indices where Agent(index)!=null;
            List<int> not_null_agent_indices = tmp_agents.Where(item => item.IsNull == false).Select((item, index) => index).ToList();
            LogList("not_null_agent_indices", not_null_agent_indices);
            // List agent scripthashes
            List<string> agents = tmp_agents.FindAll(i => !(i.IsNull)).Select(i => "0x" + BytesToString((byte[])i.ToParameter().Value, true)).ToList();
            LogList("agents", agents);
            //if (agents.Count != voting_target_plans.Count)
            //    throw new ArgumentException($"agents.Count != voting_target_plans.Count; got {agents.Count} and {voting_target_plans.Count}");

            // current NEO balances and vote targets of each agent
            var account_state_awaiters = agents.Select(i => client.InvokeScriptAsync(neo_contract.MakeScript("getAccountState", UInt160.Parse(i))).GetAwaiter());
            List<ContractParameter> account_states = account_state_awaiters.Select(i => i.GetResult().Stack[0].ToParameter()).ToList();
            List<BigInteger> neo_balances = account_states.Select(i => (BigInteger)((List<ContractParameter>)i.Value)[0].Value).ToList();
            List<string> current_vote_targets = account_states.Select(i => BytesToString((byte[])((List<ContractParameter>)i.Value)[2].Value)).ToList();

            // vote statistics of each candidate (except BurgerNeo's vote)
            List<ContractParameter> candidates_with_votes = (List<ContractParameter>)client.InvokeScriptAsync(neo_contract.MakeScript("getCandidates"))
                .GetAwaiter().GetResult().Stack[0].ToParameter().Value;
            Dictionary<string, BigInteger> candidates_with_votes_dict = candidates_with_votes.ToDictionary(i => BytesToString((byte[])((List<ContractParameter>)i.Value)[0].Value), i => (BigInteger)((List<ContractParameter>)i.Value)[1].Value);
            foreach(var can_w_v in current_vote_targets.Zip(neo_balances, (t, v) => new { target = t, vote = v }))
                candidates_with_votes_dict[can_w_v.target] -= can_w_v.vote;

            string[] candidates_arr = candidates_with_votes_dict.Keys.ToArray();
            BigInteger[] others_votes_arr = candidates_arr.Select(i => candidates_with_votes_dict[i]).ToArray();
            Array.Sort(others_votes_arr, candidates_arr);  Array.Reverse(others_votes_arr);  Array.Reverse(candidates_arr);
            List<BigInteger> others_votes = others_votes_arr.ToList(); List<string> candidates = candidates_arr.ToList();

            // Current voting vector
            Dictionary<string, int> candidate_at_index = candidates.Select((v, i) => new { v, i }).ToDictionary(x => x.v, x => x.i);
            List<BigInteger> my_votes = new(new BigInteger[candidate_at_index.Count]);
            foreach(var can_w_v in current_vote_targets.Zip(neo_balances, (t, v) => new { target = t, vote = v }))
                my_votes[candidate_at_index[can_w_v.target]] += can_w_v.vote;

            // Generate planned voting vector
            List<BigInteger> planned_my_votes = FindVotingVector(others_votes, neo_balances.Sum(), neo_balances.Count);
            List<BigInteger> planned_my_votes_of_agent = new();
            List<string> voting_target_plans = new();
            foreach ((BigInteger vote, int index) in planned_my_votes.Select((value, i) => (value, i)))
                if(vote > 0)
                {
                    voting_target_plans.Add(candidates[index]);
                    planned_my_votes_of_agent.Add(vote);
                }
            LogList("planned voting vector", planned_my_votes);
            if(planned_my_votes_of_agent.Count != agents.Count)
                throw new ArgumentException($"planned_my_votes_of_agent.Count != agents.Count; got {planned_my_votes_of_agent.Count} and {agents.Count}");
            List<BigInteger> transfer_plans = planned_my_votes_of_agent.Zip(neo_balances, (x, y) => x - y).ToList();
            LogList("transfer_plans", transfer_plans);
            BigInteger sum_transfer_plans = transfer_plans.Sum();
            if (sum_transfer_plans != 0)
                throw new ArithmeticException($"Sum of transfer_plans must be 0; got {sum_transfer_plans}");

            // Planned voting vector
            //List<BigInteger> planned_neo_balances = neo_balances.Zip(transfer_plans, (x, y) => x + y).ToList();
            //List<BigInteger> planned_my_votes = new(new BigInteger[candidate_at_index.Count]);
            //foreach (var can_w_v in voting_target_plans.Zip(planned_neo_balances, (t, v) => new { target = t, vote = v }))
            //    planned_my_votes[candidate_at_index[can_w_v.target]] += can_w_v.vote;

            // Compare rewards of current and planned voting vector
            decimal current_reward = CalcReward(others_votes, my_votes);
            decimal planned_reward = CalcReward(others_votes, planned_my_votes);
            Console.WriteLine($"planned reward / current reward == {planned_reward / current_reward}");
            if (planned_reward < (decimal)1.1 * current_reward)
            {
                Console.WriteLine($"planned_reward {planned_reward} < 1.1 * current reward {current_reward}\nNo need to execute our plan.");
                return;
            }
            Console.WriteLine($"planned_reward {planned_reward} > 1.1 * current reward {current_reward}\nExecute our plan!");



            // current state (an example):
            // not_null_agent_indices: [0, 2, 5]
            //     Only Agent(0,2,5) in the BurgerNeo contract has effective values
            // transfer_plans: [10, 0, -10]
            //     Agent(5) should give 10 NEOs to Agent(0). No other transfer needed.
            // voting_target_plans: ["a", "b", "c"]
            //     Agent(0) should vote for candidate "a"; Agent(2) should vote for candidate "b"

            // Execute the plan

            // A copy of transfer_plans to plan for intermediate transfer steps
            List<BigInteger> working_transfer_plans = transfer_plans.ToList();

            // How many NEOs should the current agent receive when the plan is executed
            BigInteger tmp_transfer_amount;

            // shared index for not_null_agent_indices and working_transfer_plans,
            // recording which next agent should have a transfer with the current agent
            int next_agent_id_index;

            LogList("working_transfer_plans", working_transfer_plans);
            for (int i = 0; i < agents.Count; ++i)
            {
                // First change the voting targets
                if (current_vote_targets[i] != voting_target_plans[i])
                    TrigVote(not_null_agent_indices[i], voting_target_plans[i]);

                // Then re-balance NEO between agents
                if (working_transfer_plans[i] == 0) continue;

                // Find the next agent that needs transfer (working_transfer_plans[some index larger than i] != 0)
                next_agent_id_index = i + 1;
                while (working_transfer_plans[next_agent_id_index] == 0)
                    next_agent_id_index += 1;
                Console.WriteLine($"next_agent_id_index: {next_agent_id_index}; Agent id in contract: {not_null_agent_indices[next_agent_id_index]}");

                // Plan to transfer between Agent(not_null_agent_indices[next_agent_id_index]) and Agent(not_null_agent_indices[i]).
                // Record the state after the planned transfer
                tmp_transfer_amount = working_transfer_plans[i];
                working_transfer_plans[next_agent_id_index] += working_transfer_plans[i];
                working_transfer_plans[i] = 0;  // Unnecessary for correct execution, but makes working_transfer_plans.Sum() == 0
                LogList("working_transfer_plans after next planned transfer", working_transfer_plans);

                // Actually execute the transfer
                if (tmp_transfer_amount < 0)  // Balance of current agent should be reduced. Current agent should give NEO to a following agent
                    TrigTransfer(not_null_agent_indices[i], not_null_agent_indices[next_agent_id_index], -tmp_transfer_amount);
                else if (tmp_transfer_amount > 0)  // Balance of current agent should be increased. A following agent should give NEO to current agent
                    TrigTransfer(not_null_agent_indices[next_agent_id_index], not_null_agent_indices[i], tmp_transfer_amount);
            }
            Console.WriteLine("End execution");
        }
        static UInt256 TrigVote(int agent_id, string candidate_publickey)
        {
            Console.WriteLine($"start trigVote(agent_id={agent_id}, target={candidate_publickey})");
            return ExecuteAndRelayTransaction(burgerneo_contract.MakeScript("trigVote", agent_id, candidate_publickey), signers);
        }
        static UInt256 TrigTransfer(int agent_id_from, int agent_id_to, BigInteger amount)
        {
            Console.WriteLine($"start trigTransfer(agent_id_from={agent_id_from}, agent_id_to={agent_id_to}, amount={amount})");
            return ExecuteAndRelayTransaction(burgerneo_contract.MakeScript("trigTransfer", agent_id_from, agent_id_to, amount), signers);
        }
        static UInt256 ExecuteAndRelayTransaction(byte[] script, Signer[] _signers)
        {
            // Needs extra test
            var result = client.InvokeScriptAsync(script!, _signers).GetAwaiter().GetResult();
            if(result.State == VMState.HALT)
            {
                TransactionManager manager = factory.MakeTransactionAsync(script!, _signers).GetAwaiter().GetResult();
                Transaction tx = manager.AddSignature(keypair).SignAsync().GetAwaiter().GetResult();
                UInt256 txid = client.SendRawTransactionAsync(tx).GetAwaiter().GetResult();
                Console.WriteLine($"relayed txid: {txid}");
                return txid;
            }
            else
                throw new ArgumentException($"Exception from Neo: {result.Exception}");
        }

        static void LogList<T>(string name, List<T> list, bool writeline = false)
        {
            if (writeline)
                Console.WriteLine($"{name}: ");
            else
                Console.Write($"{name}: ");
            Console.Write("[");
            list.ForEach(i => Console.Write($"{i}, "));
            Console.Write("]");
            Console.WriteLine();// Console.WriteLine();
        }

        static string BytesToString(byte[] input_bytes, bool reverse=false)
        {
            if (reverse)
                Array.Reverse(input_bytes);
            return BitConverter.ToString(input_bytes).Replace("-", string.Empty).ToLower();
        }
        static string[] ReadEnvironmentVariableAsStringArray(string key_string)
        {
            // allowed environment varible value format:
            // [1,2,"a",'a',a]
            // quotation marks, spaces and sqaure brackets not necessary
            // numbers are always considered string
            if (os.Platform == PlatformID.Win32NT || os.Platform == PlatformID.Win32Windows) {
                return Regex.Replace(
                    Environment.GetEnvironmentVariable(key_string, EnvironmentVariableTarget.User)!,
                    "[\\[\\]\"\'\\s]|,\\s*\\]$", "", RegexOptions.Compiled).Split(',');
            } else {
                return Regex.Replace(
                    Environment.GetEnvironmentVariable(key_string)!,
                    "[\\[\\]\"\'\\s]|,\\s*\\]$", "", RegexOptions.Compiled).Split(',');
            }
        }

        static string GenGAMSCodes(List<BigInteger> others_votes, BigInteger our_NEOs, int num_agents)
        {
            List<int> candidate_index_list = Enumerable.Range(0, others_votes.Count).ToList();
            string candidate_index_str = String.Join(",", candidate_index_list);
            string Vi = String.Join("\n", others_votes.Select((item, index) => $"{index} {item}").ToList());
            List<BigInteger> reward_factor = new List<BigInteger> { 200, 200, 200, 200, 200, 200, 200 }
                .Concat(new List<BigInteger> { 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100 })
                .Concat(new List<BigInteger>(new BigInteger[others_votes.Count - 21])).ToList();
            string reward_factor_string = String.Join("\n", reward_factor.Select((item, index) => $"{index} {item}").ToList());
            return @"
Set i ""candidate index"" /
" + candidate_index_str + @"
    /;
Parameters
    V(i) ""other people's votes"" /
    "+ Vi + @"
    /
    NEO  ""our NEOs"" /" + our_NEOs + @"/
    num_agents  ""our num of agents"" /" + num_agents + @"/
    reward_factor(i) ""how much gas is given by the candidate at each rank"" /
    " + reward_factor_string +
    @"
    /;
        
Integer Variable vote(i) ""voting vector that needs to be optimized"";
vote.up(i) = NEO;
Free Variable GAS_reward ""total GAS reward"";

Binary Variable is_nonzero(i) ""whether an element in the voting vector is zero"";
Integer Variable count_nonzero ""count of nonzero values in vote(i)"";

Equations
    GAS_reward_eqn ""the reward according to our voting vector v""
    sum_v_eqn ""sum of voting vector equals num of our NEOs""
    is_nonzero_eqn(i) ""whether the element vote(i) is not zero""
    num_agents_eqn ""sum of nonzero element in voting vector does not exceed num of our agents"";

GAS_reward_eqn.. GAS_reward =e= sum(i, reward_factor(i) * vote(i) / (V(i) + vote(i) + 0.000001));
sum_v_eqn.. NEO =e= sum(i, vote(i));
is_nonzero_eqn(i).. vote(i) =l= is_nonzero(i) * vote.up(i);
num_agents_eqn.. sum(i, is_nonzero(i)) =l= num_agents;

Model NEOBurger / all /;
Option MINLP = lindoglobal, optcr = 0, threads = 0;
Solve NEOBurger using MINLP maximizing GAS_reward;
";
        }

        static List<BigInteger> FindVotingVector(List<BigInteger> others_votes, BigInteger our_NEOs, int num_agents)
        {
            GAMSWorkspace ws = new GAMSWorkspace();
            string codes = GenGAMSCodes(others_votes, our_NEOs, num_agents);
            GAMSJob t = ws.AddJobFromString(codes);
            t.Run();
            List<BigInteger> voting_vector = new();
            foreach (GAMSVariableRecord vote in t.OutDB.GetVariable("vote"))
                voting_vector.Add((BigInteger)vote.Level);
            return voting_vector;
        }

        static decimal CalcReward(List<BigInteger> others_votes, List<BigInteger> voting_vector)
        {
            List<BigInteger> reward_factor = new List<BigInteger> { 200, 200, 200, 200, 200, 200, 200 }
                .Concat(new List<BigInteger> { 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100 })
                .Concat(new List<BigInteger>(new BigInteger[others_votes.Count - 21])).Reverse().ToList();
            List<BigInteger> total_votes = others_votes.Zip(voting_vector, (x, y) => x + y).ToList();
            List<int> sortIndex = Enumerable.Range(0, total_votes.Count).OrderBy(i => total_votes[i]).ToList();
            List<BigInteger> sorted_total_votes = sortIndex.Select(i => total_votes[i]).ToList();
            List<BigInteger> sorted_voting_vector = sortIndex.Select(i => voting_vector[i]).ToList();

            decimal sum = (decimal)0.0;
            for(int i = 0; i < sorted_total_votes.Count; ++i)
                if(sorted_total_votes[i] > 0)
                    sum += (decimal)(reward_factor[i] * sorted_voting_vector[i]) / (decimal)(sorted_total_votes[i]);
            return sum;
        }
    }
}
