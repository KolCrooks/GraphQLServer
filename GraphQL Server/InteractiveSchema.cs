using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GraphQL_Server.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GraphQL_Server
{
    public class SerializedQuery
    {
        public HashSet<GQLTypeInstance> Params { get; } = new HashSet<GQLTypeInstance>();
        public string Name { get; set; }
        public GQLTypeInstance returnType { get; set; }

        public override string ToString()
        {
            String paramString = "";
            foreach (var p in Params)
            {
                if (paramString != "") paramString += ",";

                paramString += p.ToString();
            }

            return $"{Name}({paramString}): {returnType}";
        }
    }

    public class CallableController
    {
        public GraphQLController Controller { get; set; }
        public Dictionary<string, MethodInfo> Queries { get; set; } = new Dictionary<string, MethodInfo>();

        public Dictionary<string, MethodInfo> Mutations { get; set; } =
            new Dictionary<string, MethodInfo>();

        public Dictionary<string, MethodInfo> FieldResolvers { get; set; } =
            new Dictionary<string, MethodInfo>();

        public CallableController(GraphQLController controller)
        {
            Controller = controller;
        }


        public MethodInfo FindMethod(string query, QueryType requestType)
        {
            Dictionary<string, MethodInfo> loopThrough;
            if (requestType == QueryType.MUTATION)
                loopThrough = Mutations;
            else if (requestType == QueryType.QUERY)
                loopThrough = Queries;
            else if (requestType == QueryType.FIELD)
                loopThrough = FieldResolvers;
            else return null;

            foreach (var q in loopThrough)
                if (q.Key == query)
                    return q.Value;
            return null;
        }

        public MethodInfo FindMethod(GraphQLType gqlType, QueryType requestType)
        {
            Dictionary<string, MethodInfo> loopThrough;
            if (requestType == QueryType.MUTATION)
                loopThrough = Mutations;
            else if (requestType == QueryType.QUERY)
                loopThrough = Queries;
            else if (requestType == QueryType.FIELD)
                loopThrough = FieldResolvers;
            else return null;

            foreach (var q in loopThrough)
                if (GraphQLType.FindOrGenerate(q.Value.ReturnType).TypeName == gqlType.TypeName)
                    return q.Value;
            return null;
        }

        // public List<SerializedQuery> SerializeQueries()
        // {
        //     List<SerializedQuery> queries = new List<SerializedQuery>();
        //     foreach (var query in Queries)
        //     {
        //         var serialized = new SerializedQuery();
        //         serialized.Name = query.Key;
        //         foreach (var (arg, param) in query.Value.Args)
        //             serialized.Params.Add(new GQLTypeInstance
        //             {
        //                 Name = param.Name,
        //                 type = GraphQLType.FindOrGenerate(param.GetType()),
        //                 isArray = param.GetType().IsArray
        //             });
        //         var returnType = new GQLTypeInstance
        //         {
        //             Name = null,
        //             Nullable = false,
        //             type = GraphQLType.FindOrGenerate(query.Value.Source.ReturnType),
        //             isArray = query.Value.Source.ReturnType.IsArray
        //         };
        //
        //         serialized.returnType = returnType;
        //
        //         queries.Add(serialized);
        //     }
        //
        //     return queries;
        // }
    }

    public class InteractiveSchema
    {
        private HashSet<CallableController> _controllers { get; } = new();


        public JObject Call(RequestNode node, object parentObj = null)
        {
            foreach (var c in _controllers)
            {
                MethodInfo method = null;
                switch (node.QueryType)
                {
                    case QueryType.FIELD:
                        method = c.FindMethod(node.NodeType, QueryType.FIELD);
                        break;

                    case QueryType.QUERY:
                        // Implicit Call
                        if (node.implicitQuery)
                            method = c.FindMethod(node.NodeType, QueryType.QUERY);
                        else // Explicit Call
                            method = c.FindMethod(node.Name, QueryType.QUERY);
                        break;
                    case QueryType.MUTATION:
                        method = c.FindMethod(node.Name, QueryType.QUERY);
                        break;
                    case QueryType.QUERYGROUP:
                    case QueryType.MUTATIONGROUP:
                        JObject r = new JObject();

                        foreach (var f in node.Fields)
                            r.Merge(Call(f));

                        return r;
                }

                if (method == null)
                    continue;
                var args = MapArgs(node.Args, method, parentObj);
                var self = method.Invoke(c.Controller, args);
                JToken obj = JToken.FromObject(self);
                JObject ret = new JObject();

                if (self.GetType().IsArray)
                {
                    ret[node.Name] = new JArray();
                    for (int i = 0; i < ((object[]) self).Length; i++)
                    {
                        if (obj[i] is JObject)
                            foreach (var field in node.Fields)
                                if (obj[i][field.Name].Type == JTokenType.Null)
                                    ((JObject) obj[i]).Merge(Call(field, ((object[]) self)[i]));

                        obj[i] = PergeTree(obj[i], node);
                        ((JArray) ret[node.Name]).Add(obj[i]);
                    }
                }
                else
                {
                    if (obj is JObject)
                        foreach (var field in node.Fields)
                            if (obj[field.Name].Type == JTokenType.Null)
                                ((JObject) obj).Merge(Call(field, self));

                    obj = PergeTree(obj, node);
                    ret[node.Name] = obj;
                }

                return ret;
            }

            return null;
        }


        public InteractiveSchema(IEnumerable<GraphQLController> controllers)
        {
            foreach (var controller in controllers)
            {
                var controllerType = controller.GetType();
                var controllerMethods = controllerType.GetMethods();

                var callableController = new CallableController(controller);
                foreach (var controllerMethod in controllerMethods)
                {
                    if (controllerMethod.IsPublic)
                    {
                        if (controllerMethod.GetCustomAttribute(typeof(Query)) != null ||
                            controllerMethod.GetCustomAttribute(typeof(Mutation)) != null ||
                            controllerMethod.GetCustomAttribute(typeof(FieldResolver)) != null)
                            GraphQLType.FindOrGenerate(controllerMethod.ReturnType);

                        if (controllerMethod.GetCustomAttribute(typeof(Query)) != null)
                        {
                            callableController.Queries[controllerMethod.Name] = controllerMethod;
                        }
                        else if (controllerMethod.GetCustomAttribute(typeof(Mutation)) != null)
                        {
                            callableController.Mutations[controllerMethod.Name] = controllerMethod;
                        }
                        else if (controllerMethod.GetCustomAttribute(typeof(FieldResolver)) != null)
                        {
                            callableController.FieldResolvers[controllerMethod.Name] = controllerMethod;
                        }
                    }
                }

                _controllers.Add(callableController);
            }
        }

        private object[] MapArgs(Dictionary<string, JToken> providedArgs, MethodInfo method, object root = null)
        {
            List<object> args = new List<object>();
            foreach (var param in method.GetParameters())
            {
                foreach (var attr in param.GetCustomAttributes())
                {
                    if (attr is Arg)
                    {
                        GraphQLType type;
                        type = ((Arg) attr).TypeOverride ?? GraphQLType.FindOrGenerate(param.ParameterType);
                        var val = providedArgs[((Arg) attr).Name];

                        if (type.Validate(val))
                            args.Add(Convert.ChangeType(val, type.TypeRef));
                    }
                    else if (attr is Root)
                    {
                        args.Add(root);
                    }
                }
            }

            return args.ToArray();
        }

        private JToken PergeTree(JToken token, RequestNode compare)
        {
            if (compare.Fields.Count == 0)
                return token;
            if (token is JObject)
            {
                JObject output = new JObject();
                foreach (var f in (JObject) token)
                {
                    var field = compare.Fields.Find(i => i.Name == f.Key);
                    if (field != null)
                        output[f.Key] = PergeTree(f.Value, field);
                }

                return output;
            }
            else if (token is JArray)
            {
                JArray output = new JArray();
                foreach (var treeItem in (JArray) token)
                    output.Add(PergeTree(treeItem, compare));
                return output;
            }
            else
            {
                return token;
            }
        }
    }
}