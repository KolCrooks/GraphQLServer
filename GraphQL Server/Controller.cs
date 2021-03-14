using System;
using System.Collections.Generic;
using System.Reflection;
using GraphQL_Server.Attributes;

namespace GraphQL_Server
{
    public abstract class GraphQLController
    {
        public GraphQLType gqlType { get; }

        public GraphQLController()
        {
            var controllerAttr = (GQLController) GetType().GetCustomAttribute(typeof(GQLController));
            gqlType = GraphQLType.FindOrGenerate(controllerAttr.type);
        }
    }
}