using System;

namespace GraphQL_Server.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class Query : Attribute
    {
        public String Name { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class Mutation : Attribute
    {
        public String Name { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class FieldResolver : Attribute
    {
        public String Name { get; set; }
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public class Root : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public class Arg : Attribute
    {
        public String Name { get; set; }
    }
}