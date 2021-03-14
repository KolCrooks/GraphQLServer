using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GraphQL_Server
{
    public enum QueryType
    {
        QUERY,
        MUTATION
    }

    public class ServerRequest
    {
        public string operationName { get; set; }
        public JObject variables { get; set; }
        public string query { get; set; }
    }

    public class RequestNodes
    {
        public string Query;
        public QueryType type { get; set; }
        public Dictionary<string, JToken> args = new();
        public List<FieldNode> Nodes = new();
    }

    public class FieldNode
    {
        public GraphQLType nodeType { get; set; }
        public string name { get; set; }
        public List<FieldNode> fields { get; set; } = null;
    }

    public class Parser
    {
        public static List<RequestNodes> ParseRequest(String code)
        {
            Console.WriteLine(code);
            var request = JsonConvert.DeserializeObject<ServerRequest>(code);
            var requests = ParseRequests(request);
            Console.WriteLine("PARSED!");
            return requests;
        }

        private static List<RequestNodes> ParseRequests(ServerRequest request)
        {
            List<RequestNodes> output = new List<RequestNodes>();
            var query = request.query.Trim();
            while (query.Length > 0)
            {
                query = query.Trim();
                if (query.StartsWith("query ") || query.StartsWith("mutation "))
                {
                    RequestNodes node = new()
                        {type = query.StartsWith("query ") ? QueryType.QUERY : QueryType.MUTATION};

                    if (query.StartsWith("query "))
                        query = query.Substring("query ".Length);
                    else
                        query = query.Substring("mutation ".Length);


                    var queryName = Regex.Split(query, @"\(")[0].Trim();
                    node.Query = queryName;
                    var argsMatch = Regex.Match(query.Split('\n')[0], @"\(.*\)");
                    if (argsMatch.Length > 0)
                    {
                        string[] args = argsMatch.Groups[0].Value.Substring(1).Split(',');
                        foreach (var arg in args)
                        {
                            var name = arg.Split(':')[0].Trim().Replace("$", "");
                            var value = request.variables[name];
                            node.args[name] = value;
                        }
                    }


                    var (start, end) = NextBracketSection(query);
                    node.Nodes.AddRange(ParseFields(query.Substring(start, end - start)));
                    output.Add(node);
                    query = query.Substring(end + 1);
                }
                else if (query.StartsWith("\n"))
                    query = query.Substring(1);
                else
                    break;
            }


            return output;
        }

        private static (int, int) NextBracketSection(string total)
        {
            var start = -1;
            var end = -1;
            var count = 0;
            var started = false;
            for (var i = 0; i < total.Length; i++)
            {
                if (total[i] == '{')
                {
                    started = true;
                    if (count == 0)
                        start = i;
                    count++;
                }
                else if (total[i] == '}')
                {
                    count--;
                    end = i;
                }

                if (started && count == 0)
                    return (start, end);
            }

            return (-1, -1);
        }


        private static List<FieldNode> ParseFields(String groups, GraphQLTypeIntrinsic.GQLInterface topType = null)
        {
            groups = groups.Trim();
            var output = new List<FieldNode>();

            while (groups.Length > 0)
            {
                var fields = groups.Split('\n');
                if (fields.Length == 0) break;

                var name = fields[0].Split('{')[0].Trim();
                if (name == "")
                {
                    var newGroups = "";
                    for (var i = 1; i < fields.Length; i++)
                        newGroups += fields[i] + "\n";
                    groups = newGroups.Trim();
                    continue;
                }

                GraphQLType type = null;
                if (topType == null)
                    type = GraphQLType.RegisteredTypes.Find(t => t.TypeName == name);
                else
                {
                    foreach (var field in topType.Fields)
                    {
                        if (field.Name == name)
                            type = field.type;
                    }

                    if (type == null)
                        throw new Exception("Type not found");
                }

                FieldNode node = new() {nodeType = type, name = name};

                // Has sub-fields
                if (fields[0].Trim().EndsWith("{") || fields.Length > 1 && fields[1].Trim().StartsWith("{"))
                {
                    var (start, end) = NextBracketSection(groups);
                    if (type is GraphQLTypeIntrinsic.GQLInterface)
                    {
                        node.fields = ParseFields(groups.Substring(start, end - start),
                            (GraphQLTypeIntrinsic.GQLInterface) type);
                    }

                    groups = groups.Remove(start, end - start);
                }
                else if (type is GraphQLTypeIntrinsic.GQLInterface)
                    node.fields = ((GraphQLTypeIntrinsic.GQLInterface) type).GetFieldsTree();

                var toCombine = groups.Split('\n');
                var newGroups2 = "";
                for (var i = 1; i < toCombine.Length; i++)
                    newGroups2 += toCombine[i] + "\n";
                groups = newGroups2.Trim();

                output.Add(node);
            }

            return output;
        }
    }
}