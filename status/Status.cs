using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Neo.Plugins
{
    public class Status : Plugin, IPersistencePlugin
    {
        private static readonly UInt160 BNEO = UInt160.Parse("0x48c40d4666f93408be1bef038b6722404d9a4c2a");
        private static readonly uint UNTIL = uint.Parse(Environment.GetEnvironmentVariable("UNTIL") ?? "999999999");
        void IPersistencePlugin.OnPersist(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            if (block.Index > UNTIL)
            {
                Environment.Exit(0);
            }
            Console.OutputEncoding = Encoding.ASCII;
            Console.WriteLine();
            Console.Error.WriteLine($"SYNCING BLOCK: {block.Index}");
            ApplicationEngine ts = ApplicationEngine.Run(BNEO.MakeScript("totalSupply"), snapshot, settings: system.Settings);
            ApplicationEngine rps = ApplicationEngine.Run(BNEO.MakeScript("rPS"), snapshot, settings: system.Settings);
            if (ts.State != VMState.HALT || rps.State != VMState.HALT)
            {
                Console.Error.WriteLine($"NOT FOUND: {block.Index}");
                return;
            }
            Console.WriteLine($"{JsonSerializer.Serialize(new { timestamp = block.Timestamp, blocknum = block.Index, rps = rps.ResultStack.Select(v => v.GetInteger().ToString()).First(), total_supply = ts.ResultStack.Select(v => v.GetInteger().ToString()).First() })}");
            Console.Out.Flush();
            Console.Error.Flush();
        }
    }
}
