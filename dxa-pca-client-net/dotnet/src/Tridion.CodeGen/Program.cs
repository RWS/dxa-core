using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Tridion.Dxa.Api.Client.CodeGen;
using Tridion.Dxa.Api.Client.GraphQL.Client;

namespace Tridion.CodeGen
{
    /// <summary>
    /// code generator for graphql
    /// </summary>
    class Program
    {
        static void Write(string msg) => Console.WriteLine(msg);

        static string FullyQualifiedPath(string path) => Path.GetFullPath(path.Trim());

        static int Main(string[] args)
        {
            if (args.Length == 0) return ShowUsage();

            try
            {
                var argMap = new Dictionary<string, string> {{"url", args[0]}};
                for (int i = 1; i < args.Length - 1; i++)
                {
                    var arg = args[i];
                    var argValue = args[i + 1];

                    switch (arg.ToLower())
                    {
                        case "-h":
                        case "-help":
                            return ShowUsage();
                        case "-namespace":
                            if (argValue == null) return ShowUsage("-namespace incorrectly specified");
                            argMap.Add("namespace", argValue);
                            i++;
                            break;
                        case "-types":
                            argMap.Add("types", null);
                            break;
                        case "-builders":
                            argMap.Add("builders", null);
                            break;
                        case "-outdir":
                            if (argValue == null) return ShowUsage("-outdir incorrectly specified");
                            argMap.Add("outdir", FullyQualifiedPath(argValue));
                            i++;
                            break;
                        case "-singlefile":
                            if (argValue == null) return ShowUsage("-singlefile incorrectly specified");
                            argMap.Add("singlefile", argValue);
                            i++;
                            break;
                        default:
                            return ShowUsage($"unknown argument {arg}");
                    }
                }
                Write("Tridion graphql c# code generator");
                Write("---------------------------------");
                GraphQLClient client = new GraphQLClient(argMap["url"]) { ThrowOnAnyError = false };
                if (!client.HttpClient.Ping())
                {
                    Write($"failed to get response from url {argMap["url"]}");
                    return -1;
                }

                if (!Directory.Exists(argMap["outdir"]))
                {
                    Write($"> creating output directory {argMap["outdir"]}");
                    Directory.CreateDirectory(argMap["outdir"]);
                }
                else
                {
                    Write($"> using output directory {argMap["outdir"]}");
                }
                
                Write("> getting graphQL schema...");
                var codegen = new Tridion.Dxa.Api.Client.CodeGen.CodeGen(client, argMap["namespace"]);

                List<CodeGenInfo> generated = new List<CodeGenInfo>();
               
                if (argMap.ContainsKey("types"))
                {
                    Write("> generating types...");
                    generated.AddRange(codegen.GenerateTypes());
                }

                if (argMap.ContainsKey("builders"))
                {
                    Write("> generating query builders...");
                    generated.AddRange(codegen.GenerateQueryBuilders());
                }

                if (generated.Count == 0)
                {
                    Write("> nothing generated");
                    return 0;
                }

                if (argMap.ContainsKey("singlefile"))
                {
                    string outputfile = Path.Combine(argMap["outdir"], argMap["singlefile"]);
                    Write($"> generating output file {outputfile}");
                    var sb = new StringBuilder();
                    sb.AppendLine(generated[0].Header);
                    sb.AppendLine("{");
                    foreach (var info in generated)
                    {
                        sb.AppendLine(info.Content);
                    }

                    sb.AppendLine("}");
                    File.WriteAllText(outputfile, sb.ToString());
                }
                else
                {
                    var processed = new HashSet<string>();
                    foreach (var info in generated)
                    {
                        string outputfile = Path.Combine(argMap["outdir"], $"{info.Typename}.cs");
                        Write($"> generating output file {outputfile}");
                        if (processed.Contains(outputfile))
                        {                            
                            File.AppendAllText(outputfile, info.Content);
                        }
                        else
                        {
                            var sb = new StringBuilder();
                            sb.AppendLine(info.Header);
                            sb.AppendLine("{");
                            sb.AppendLine(info.Content);
                            sb.AppendLine("}");
                            processed.Add(outputfile);
                            File.WriteAllText(outputfile, sb.ToString());
                        }
                    }
                }

                Write("> finished");
            }
            catch (Exception e)
            {
                Write("error generating code");
                Write(e.Message);
                return -1;
            }

            return 0;
        }

        static int ShowUsage(string errorMsg = null)
        {
            int returnValue = string.IsNullOrEmpty(errorMsg) ? 0 : -1;
            Write("graphql code generator");
            Write("----------------------");
            if(errorMsg != null) Write(errorMsg);
            Write("usage: <url> -namespace <namespace> [-types|-builders] -outdir <path> [-singlefile <filename.ext>]");
            return returnValue;
        }
    }
}
