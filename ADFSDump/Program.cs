using System;
using ADFSDump.ReadDB;
using System.Collections.Generic;
using ADFSDump.RelyingPartyTrust;
using ADFSDump.About;
using ADFSDump.ActiveDirectory;

namespace ADFSDump
{

    class Program
    {
        private static Dictionary<string, string> ParseArgs(string[] args)
        {
            Dictionary<string, string> arguments = new Dictionary<string, string>();
            try
            {
                foreach (string argument in args)
                {
                    var index = argument.IndexOf(":", StringComparison.Ordinal);
                    if (index > 0)
                    {
                        arguments[argument.Substring(0, index)] = argument.Substring(index + 1);
                    }
                    else
                    {
                        arguments[argument] = "";
                    }
                }
            }
            catch (Exception)
            {
                Info.ShowHelp();
                Environment.Exit(1);
            }
            return arguments;
        }

        private static bool HasFlag(Dictionary<string, string> arguments, params string[] names)
        {
            foreach (string name in names)
            {
                if (arguments.ContainsKey(name)) return true;
            }
            return false;
        }

        static void Main(string[] args)
        {
            Dictionary<string, string> arguments = new Dictionary<string, string>();
            if (args.Length > 0) arguments = ParseArgs(args);

            if (HasFlag(arguments, "/help", "-h", "--help", "/?"))
            {
                Info.ShowHelp();
                return;
            }

            Log.Json = HasFlag(arguments, "/json");

            if (!Log.Json) Info.ShowInfo();

            var result = new DumpResult();

            if (!arguments.ContainsKey("/nokey"))
            {
                result.DkmKeys = ADSearcher.GetPrivKey(arguments);
            }

            DumpResult dbResult = DatabaseReader.ReadConfigurationDb(arguments);
            if (dbResult == null)
            {
                Environment.Exit(1);
            }

            result.SigningKey = dbResult.SigningKey;
            result.IssuerIdentifier = dbResult.IssuerIdentifier;
            result.RelyingParties = dbResult.RelyingParties;

            if (Log.Json)
            {
                Console.WriteLine(Json.Serialize(result));
            }
            else
            {
                foreach (var relyingparty in result.RelyingParties)
                {
                    Console.WriteLine($"[-] {relyingparty}");
                }
            }
        }
    }
}
