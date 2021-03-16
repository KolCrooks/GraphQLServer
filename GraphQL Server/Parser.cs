using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GraphQL_Server.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GraphQL_Server
{
    public enum QueryType
    {
        QUERY,
        QUERYGROUP,
        MUTATION,
        MUTATIONGROUP,
        FIELD
    }

    public class ServerRequest
    {
        public string operationName { get; set; }
        public JObject variables { get; set; }
        public string query { get; set; }
    }

    public class RequestNode
    {
        /// <summary>
        /// The type of the query (QUERY | MUTATION | FIELD | ...)
        /// </summary>
        public QueryType QueryType { get; set; }

        /// <summary>
        /// GraphQL type of the node
        /// </summary>
        public GraphQLType NodeType { get; set; }

        /// <summary>
        /// Name of the node. Can be name of the query/mutation or the name of the field
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Sub Fields for this node
        /// </summary>
        public List<RequestNode> Fields { get; set; } = new();

        /// <summary>
        /// Node argument
        /// </summary>
        public Dictionary<string, JToken> Args = new();

        /// <summary>
        /// Query is implicitly defined (query by the type of the object)
        /// </summary>
        public bool implicitQuery = false;
    }

    public class Parser
    {
        public static List<RequestNode> ParseRequest(String code)
        {
            Console.WriteLine(code);
            var request = JsonConvert.DeserializeObject<ServerRequest>(code);
            List<RequestNode> output = new List<RequestNode>();

            var query = request.query.Trim(' ', '\n');
            var queryGroups = GetGroups(query);

            foreach (var g in queryGroups)
                output.Add(GetGroupInfo(g, request.variables));

            Console.WriteLine("PARSED!");
            return output;
        }

        private static List<string> GetGroups(string query)
        {
            List<string> groups = new List<string>();

            while (query.Length > 0)
            {
                query = query.Trim(' ', '\n');
                var (_, end) = NextBracketSection(query);
                if (end == -1)
                {
                    var remainder = query.Trim(' ', '\n');
                    if (remainder == "")
                        break;
                    groups.Add(remainder);
                    break;
                }

                groups.Add(query.Substring(0, end + 1));
                query = query.Substring(end + 1);
            }

            return groups;
        }

        private static List<RequestNode> GetFieldGroups(string query, GraphQLTypeIntrinsic.GQLInterface parentType,
            JObject variables)
        {
            List<RequestNode> groups = new List<RequestNode>();

            while (query.Length > 0)
            {
                query = query.Trim(' ', '\n');
                if (query == "") break;

                var (start, end) = NextBracketSection(query);

                // If non sub group fields are the only ones that remain
                if (end == -1)
                {
                    foreach (var fName in query.Split('\n'))
                    {
                        var (name, args) = ParseArgs(fName, variables);
                        GraphQLType t = null;
                        foreach (var f in parentType.Fields)
                            if (f.Name == name)
                            {
                                t = f.type;
                                break;
                            }

                        if (t != null)
                        {
                            RequestNode n = new RequestNode
                                {QueryType = QueryType.FIELD, NodeType = t, Name = name, Args = args};
                            groups.Add(n);
                        }
                    }

                    break;
                }

                var fullGroup = query.Substring(0, end);
                var names = fullGroup.Split('{')[0];

                // Parse possible fields that are above a sub group
                var fields2 = names.Split('\n');
                for (int i = 0; i < fields2.Length - 1; i++)
                {
                    var (name, args) = ParseArgs(fields2[i], variables);
                    GraphQLType t = null;
                    foreach (var f in parentType.Fields)
                        if (f.Name == name)
                        {
                            t = f.type;
                            break;
                        }

                    if (t != null)
                    {
                        RequestNode n = new RequestNode
                            {QueryType = QueryType.FIELD, NodeType = t, Name = name, Args = args};
                        groups.Add(n);
                    }
                }

                // Parse the sub group
                if (fullGroup.Length > 0)
                {
                    var (name, args) = ParseArgs(fields2[fields2.Length - 1], variables);
                    GraphQLType t = null;
                    foreach (var f in parentType.Fields)
                        if (f.Name == name)
                        {
                            t = f.type;
                            break;
                        }

                    if (t != null)
                    {
                        List<RequestNode> fields = new List<RequestNode>();
                        if (t is GraphQLTypeIntrinsic.GQLInterface)
                            fields = GetFieldGroups(query.Substring(start, end - start),
                                (GraphQLTypeIntrinsic.GQLInterface) t, variables);

                        RequestNode n = new RequestNode
                        {
                            QueryType = QueryType.FIELD, NodeType = t, Name = name, Fields = fields,
                            Args = args
                        };
                        groups.Add(n);
                    }
                }

                query = query.Substring(end);
            }

            return groups;
        }

        private static (string name, Dictionary<string, JToken> args) ParseArgs(string title, JObject variables)
        {
            Dictionary<string, JToken> output = new Dictionary<string, JToken>();
            title = title.Trim(' ', '\n');
            var argsMatch = Regex.Match(title, @"\(.*\)");
            string fieldName;
            if (argsMatch.Success)
            {
                fieldName = title.Substring(0, argsMatch.Index);
                string[] args = argsMatch.Groups[0].Value.Substring(1).Replace(")", "").Split(',');
                foreach (var arg in args)
                {
                    var name = arg.Split(':')[0].Trim().Replace("$", "");
                    var value = variables[name];
                    output[name] = value;
                }
            }
            else
                fieldName = title;


            return (fieldName, output);
        }

        private static RequestNode GetGroupInfo(string group, JObject variables)
        {
            group = group.Trim(' ', '\n');

            var groups = group.Split('{');
            var name = groups[0];
            if (groups.Length == 0) return null;
            // Implicit Query block
            if (name == "")
            {
                var sub = InsideFirstGroup(group);
                var subGroups = GetGroups(sub);

                var fields = new List<RequestNode>();
                foreach (var g in subGroups)
                    fields.Add(GetGroupInfo(g, variables));

                return new RequestNode {QueryType = QueryType.QUERYGROUP, Fields = fields, implicitQuery = true};
            }

            // anonymous Query or Mutation block with potential args 
            if (Regex.IsMatch(name, @"^(query|mutation)\s?[\({]"))
            {
                QueryType qt;
                string eon;

                if (Regex.IsMatch(name, @"^query\s?[\({]"))
                {
                    qt = QueryType.QUERYGROUP;
                    eon = name.Substring("query".Length).TrimStart();
                }
                else
                {
                    qt = QueryType.MUTATIONGROUP;
                    eon = name.Substring("mutation".Length).TrimStart();
                }

                var (_, args) = ParseArgs(eon, variables);

                var sub = InsideFirstGroup(group);
                var subGroups = GetGroups(sub);

                var fields = new List<RequestNode>();
                foreach (var g in subGroups)
                    fields.Add(GetGroupInfo(g, variables));

                return new RequestNode {QueryType = qt, Fields = fields, Args = args};
            }

            // Specific Query or mutation
            if (name.StartsWith("query ") || name.StartsWith("mutation "))
            {
                string afterQuery;
                QueryType qt;
                if (name.StartsWith("query "))
                {
                    afterQuery = name.Substring("query ".Length);
                    qt = QueryType.QUERY;
                }
                else
                {
                    afterQuery = name.Substring("mutation ".Length);
                    qt = QueryType.MUTATION;
                }

                var (queryName, args) = ParseArgs(afterQuery, variables);
                var sub = InsideFirstGroup(group);
                var subGroups = GetGroups(sub);

                var fields = new List<RequestNode>();
                foreach (var g in subGroups)
                    fields.Add(GetGroupInfo(g, variables));

                return new RequestNode {QueryType = qt, Name = queryName, Fields = fields, Args = args};
            }

            // implicit query request
            else
            {
                var (qName, args) = ParseArgs(name, variables);
                var sub = InsideFirstGroup(group);
                var type = GraphQLType.RegisteredTypes.Find(t => t.TypeName == qName);
                if (type == null)
                    return null;

                List<RequestNode> fields = new List<RequestNode>();
                if (type is GraphQLTypeIntrinsic.GQLInterface)
                    fields = GetFieldGroups(sub,
                        (GraphQLTypeIntrinsic.GQLInterface) type, variables);

                return new RequestNode
                {
                    QueryType = QueryType.QUERY, NodeType = type, Name = qName, Fields = fields, Args = args,
                    implicitQuery = true
                };
            }
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

        private static string InsideFirstGroup(string totalGroup)
        {
            var start = -1;
            var end = -1;
            for (var i = 0; i < totalGroup.Length; i++)
            {
                if (totalGroup[i] == '{')
                {
                    start = i;
                    break;
                }
            }

            for (var i = totalGroup.Length - 1; i >= 0; i--)
            {
                if (totalGroup[i] == '}')
                {
                    end = i;
                    break;
                }
            }

            if (start == -1 || end == -1) return "";

            return totalGroup.Substring(start + 1, end - start - 1);
        }
    }
}