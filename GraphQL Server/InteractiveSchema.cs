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

    public class ControllerMethod
    {
        public MethodInfo Source { get; set; }
        private List<(Arg arg, ParameterInfo param)> _cachedArgs;

        public List<(Arg arg, ParameterInfo param)> Args
        {
            get
            {
                if (_cachedArgs != null)
                    return _cachedArgs;
                _cachedArgs = new List<(Arg, ParameterInfo)>();

                var parameter = Source.GetParameters();
                foreach (var param in parameter)
                {
                    var argAttr = param.GetCustomAttribute(typeof(Arg));
                    if (argAttr != null)
                        _cachedArgs.Add(((Arg) argAttr, param));
                }

                return _cachedArgs;
            }
        }
    }

    public class CallableController
    {
        public GraphQLController Controller { get; set; }
        public Dictionary<string, ControllerMethod> Queries { get; set; } = new Dictionary<string, ControllerMethod>();

        public Dictionary<string, ControllerMethod> Mutations { get; set; } =
            new Dictionary<string, ControllerMethod>();

        public Dictionary<string, ControllerMethod> FieldResolvers { get; set; } =
            new Dictionary<string, ControllerMethod>();

        public CallableController(GraphQLController controller)
        {
            Controller = controller;
        }

        public JObject Call(RequestNodes node)
        {
            ControllerMethod method;
            if (node.Query == null)
                method = FindMethod(node.Nodes[0].nodeType, node.type);
            else
                method = FindMethod(node.Query, node.type);


            var methodParams = new List<object>();

            foreach (var a in method.Args)
            {
                switch (node.args[a.arg.Name].Type)
                {
                    case JTokenType.Boolean:
                        methodParams.Add(node.args[a.arg.Name].Value<bool>());
                        break;
                    case JTokenType.Float:
                        methodParams.Add(node.args[a.arg.Name].Value<float>());
                        break;
                    case JTokenType.String:
                        methodParams.Add(node.args[a.arg.Name].Value<string>());
                        break;
                    case JTokenType.Integer:
                        methodParams.Add(node.args[a.arg.Name].Value<int>());
                        break;
                    case JTokenType.Object:
                        methodParams.Add(node.args[a.arg.Name].Value<object>());
                        break;
                }
            }

            var found = new JObject();
            var methodResponse = method.Source.Invoke(Controller, methodParams.ToArray());
            if (methodResponse == null)
                return null;
            found[methodResponse.GetType().Name] = JToken.FromObject(methodResponse);
            if (node.type == QueryType.MUTATION)
            {
                var temp = new JObject();
                temp[node.Query] = CorrectData(node, found);
                return temp;
            }

            return (JObject) CorrectData(node, found);
        }

        private JToken CorrectData(RequestNodes node, object foundAlready) =>
            CorrectData(node.Nodes, JToken.FromObject(foundAlready));

        private JToken CorrectData(List<FieldNode> nodes, JToken found)
        {
            if (found == null)
                return null;
            var output = JObject.FromObject(new { });
            if (nodes != null)
                foreach (var n in nodes)
                {
                    if (found[n.name] == null)
                        continue;
                    if (found[n.name].Type == JTokenType.Null)
                    {
                        if (FieldResolvers[n.name] != null)
                        {
                            var o = JToken.FromObject(FieldResolvers[n.name].Source
                                .Invoke(
                                    Controller,
                                    new object[]
                                    {
                                        found
                                    }));
                            if (o is JArray)
                                output[n.name] = CorrectData(n.fields, (JArray) o);
                            else
                            {
                                output[n.name] = CorrectData(n.fields, o);
                            }
                        }
                    }
                    else
                    {
                        if (found[n.name] is JValue)
                            output[n.name] = found[n.name];
                        else
                            output[n.name] = CorrectData(n.fields, (JToken) found[n.name]);
                    }
                }
            else
                return found;

            return output;
        }

        private JArray CorrectData(List<FieldNode> nodes, JArray found)
        {
            if (found == null)
                return null;
            var output = JArray.FromObject(new object[] { });
            for (var i = 0; i < found.Count; i++)
                output.Add(JObject.FromObject(new()));

            if (nodes != null)
                for (var i = 0; i < found.Count; i++)
                    foreach (var n in nodes)
                    {
                        if (found[i][n.name] == null)
                            continue;
                        if (found[i][n.name].Type == JTokenType.Null)
                        {
                            if (FieldResolvers[n.name] != null)
                            {
                                var o = JToken.FromObject(FieldResolvers[n.name].Source
                                    .Invoke(
                                        Controller,
                                        new object[]
                                        {
                                            found
                                        }));
                                if (o is JArray)
                                    output[i][n.name] = CorrectData(n.fields, (JArray) o);
                                else
                                {
                                    output[i][n.name] = CorrectData(n.fields, o);
                                }
                            }
                        }
                        else
                        {
                            if (found[i][n.name] is JValue)
                                output[i][n.name] = found[i][n.name];
                            else
                                output[i][n.name] = CorrectData(n.fields, (JToken) found[i][n.name]);
                        }
                    }
            else
                return found;

            return output;
        }

        public ControllerMethod FindMethod(string query, QueryType type)
        {
            var loopThrough = Queries;
            if (type == QueryType.MUTATION)
                loopThrough = Mutations;
            foreach (var q in loopThrough)
                if (q.Key == query)
                    return q.Value;
            return null;
        }

        public ControllerMethod FindMethod(GraphQLType gqlType, QueryType requestType)
        {
            var loopThrough = Queries;
            if (requestType == QueryType.MUTATION)
                loopThrough = Mutations;
            foreach (var q in loopThrough)
                if (GraphQLType.FindOrGenerate(q.Value.Source.ReturnType).TypeName == gqlType.TypeName)
                    return q.Value;
            return null;
        }

        public List<SerializedQuery> SerializeQueries()
        {
            List<SerializedQuery> queries = new List<SerializedQuery>();
            foreach (var query in Queries)
            {
                var serialized = new SerializedQuery();
                serialized.Name = query.Key;
                foreach (var (arg, param) in query.Value.Args)
                    serialized.Params.Add(new GQLTypeInstance
                    {
                        Name = param.Name,
                        type = GraphQLType.FindOrGenerate(param.GetType()),
                        isArray = param.GetType().IsArray
                    });
                var returnType = new GQLTypeInstance
                {
                    Name = null,
                    Nullable = false,
                    type = GraphQLType.FindOrGenerate(query.Value.Source.ReturnType),
                    isArray = query.Value.Source.ReturnType.IsArray
                };

                serialized.returnType = returnType;

                queries.Add(serialized);
            }

            return queries;
        }
    }

    public class InteractiveSchema
    {
        private HashSet<CallableController> _controllers { get; } = new();


        public JObject Call(RequestNodes node)
        {
            foreach (var c in _controllers)
            {
                if (node.Query == null)
                {
                    if (c.FindMethod(node.Nodes[0].nodeType, node.type) != null)
                        return c.Call(node);
                }
                else
                {
                    if (c.FindMethod(node.Query, node.type) != null)
                        return c.Call(node);
                }
            }

            throw new Exception("Unable to find request!");
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
                            var cMethod = new ControllerMethod {Source = controllerMethod};

                            callableController.Queries[controllerMethod.Name] = cMethod;
                        }
                        else if (controllerMethod.GetCustomAttribute(typeof(Mutation)) != null)
                        {
                            var cMethod = new ControllerMethod {Source = controllerMethod};
                            callableController.Mutations[controllerMethod.Name] = cMethod;
                        }
                        else if (controllerMethod.GetCustomAttribute(typeof(FieldResolver)) != null)
                        {
                            var cMethod = new ControllerMethod {Source = controllerMethod};
                            callableController.FieldResolvers[controllerMethod.Name] = cMethod;
                        }
                    }
                }

                _controllers.Add(callableController);
            }
        }
    }
}