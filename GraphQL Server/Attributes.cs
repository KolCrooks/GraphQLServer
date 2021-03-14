using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GraphQL_Server
{
    namespace Attributes
    {
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
        public class GQLController : Attribute
        {
            public Type type { get; set; }
        }

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class Query : Attribute
        {
            public String Name { get; set; }
            public bool Default { get; set; } = false;
        }

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class Mutation : Attribute
        {
            public String Name { get; set; }
        }

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
        public class FieldResolver : Attribute
        {
            public String Name { get; set; }
        }

        [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
        public class Root : Attribute
        {
        }

        [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
        public class Arg : Attribute
        {
            public String Name { get; }

            public Arg(string name, Type typeOverride = null)
            {
                Name = name;
                if (typeOverride != null)
                    TypeOverride = (GraphQLType) Activator.CreateInstance(typeOverride);
            }

            public GraphQLType TypeOverride { get; }
        }

        [AttributeUsage(AttributeTargets.Enum | AttributeTargets.Class, AllowMultiple = false)]
        public class GQLTypeDefinition : Attribute
        {
        }

        [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
        public class IsType : Attribute
        {
            public GraphQLType Type { get; }

            public IsType(Type type)
            {
                Type = (GraphQLType) Activator.CreateInstance(type);
            }
        }
    }
}