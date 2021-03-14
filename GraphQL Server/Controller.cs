using System;
using System.Collections.Generic;

namespace GraphQL_Server
{
    public class GraphQLTypeMap
    {
        public static IGraghQLType String = new IGraghQLType {TypeRef = typeof(string), TypeName = "String"};
        public static IGraghQLType String = new IGraghQLType {TypeRef = typeof(string), TypeName = "String"};
        public static IGraghQLType String = new IGraghQLType {TypeRef = typeof(string), TypeName = "String"};
        public static IGraghQLType String = new IGraghQLType {TypeRef = typeof(string), TypeName = "String"};
    }

    public struct IGraghQLType
    {
        public Type TypeRef { get; set; }
        public String TypeName { get; set; }
    }

    public struct IGraphQLField
    {
        public String Name { get; set; }
        public IGraghQLType Type { get; set; }
    }

    public interface IGraphQLInterface
    {
    }

    public abstract class GraphQLController
    {
        public GraphQLServer server { get; set; }

        GraphQLController()
        {
        }
    }
}