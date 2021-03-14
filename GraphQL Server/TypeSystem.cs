using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GraphQL_Server.Attributes;

namespace GraphQL_Server
{
    public readonly struct GraphQLTypeIntrinsic
    {
        /// <summary>
        /// Represents the a GQL Enum
        /// </summary>
        public class GQLEnum : GraphQLType
        {
            public HashSet<string> Names { get; } = new();

            public GQLEnum(Type typeObject, string name) : base(typeObject, name)
            {
            }
        }

        /// <summary>
        /// Represents a GQL interface
        /// </summary>
        public class GQLInterface : GraphQLType
        {
            public HashSet<GQLTypeInstance> Fields { get; } = new();

            public List<FieldNode> GetFieldsTree()
            {
                List<FieldNode> output = new();
                foreach (var v in Fields)
                {
                    if (v.type is GQLInterface)
                    {
                        output.Add(new()
                            {fields = ((GQLInterface) v.type).GetFieldsTree(), name = v.Name, nodeType = v.type});
                    }
                    else
                    {
                        output.Add(new() {name = v.Name, nodeType = v.type});
                    }
                }

                return output;
            }

            public GQLInterface(Type typeObject, string name) : base(typeObject, name)
            {
            }
        }

        /// <summary>
        /// Represents the GQL Scalar Int
        /// </summary>
        public class Int : GraphQLType
        {
            public Int() : base(typeof(int), "Int")
            {
            }
        }

        /// <summary>
        /// Represents the GQL Scalar Float
        /// </summary>
        public class Float : GraphQLType
        {
            public Float() : base(typeof(float), "Float")
            {
            }
        }

        /// <summary>
        /// Represents the GQL Scalar String
        /// </summary>
        public class String : GraphQLType
        {
            public String() : base(typeof(String), "String")
            {
            }
        }

        /// <summary>
        /// Represents the GQL Scalar Boolean
        /// </summary>
        public class Boolean : GraphQLType
        {
            public Boolean() : base(typeof(bool), "Boolean")
            {
            }
        }

        /// <summary>
        /// Represents the GQL Scalar ID
        /// </summary>
        public class ID : GraphQLType
        {
            public ID() : base(typeof(string), "ID")
            {
                OverrideSameType = true;
            }
        }

        /// <summary>
        /// Represents the GQL Scalar ID
        /// </summary>
        public class Null : GraphQLType
        {
            public Null() : base(typeof(void), "null")
            {
            }
        }
    };

    public class GQLTypeInstance
    {
        public bool isArray { get; set; }
        public GraphQLType type { get; set; }
        public string Name { get; set; } = null;

        public bool Nullable { get; set; } = false;

        public override string ToString()
        {
            string output = "";

            // Start stuff
            if (Name != null)
                output += Name + ": ";

            if (isArray)
                output += "[";

            // center
            output += type.TypeName;


            // End Stuff
            if (isArray)
                output += "]";
            if (!Nullable)
                output += "!";

            return output;
        }
    }

    /// <summary>
    /// Base type for all GraphQL Types
    /// </summary>
    public abstract class GraphQLType
    {
        public static List<GraphQLType> RegisteredTypes { get; set; } = new List<GraphQLType>();
        public Type TypeRef { get; }
        public string TypeName { get; set; }

        private bool _overrideSameType = false;

        /// <summary>
        /// True if when searching for a type based on raw type, this will have priority in being chosen
        /// </summary>
        public bool OverrideSameType
        {
            get => _overrideSameType;
            set
            {
                if (value == true)
                {
                    var existingOverride = RegisteredTypes.Find(t =>
                        t.OverrideSameType && (t.TypeName != TypeName) && t.TypeRef == TypeRef);
                    if (existingOverride != null)
                        existingOverride.OverrideSameType = false;
                    _overrideSameType = true;
                }
                else
                {
                    _overrideSameType = false;
                }
            }
        }

        public GraphQLType(Type typeRef, string typeName)
        {
            TypeRef = typeRef;
            TypeName = typeName;
            if (!RegisteredTypes.Exists((obj) => obj.TypeName == TypeName))
                RegisteredTypes.Add(this);
        }

        public static GraphQLType GetTypeOf(PropertyInfo prop)
        {
            foreach (Attribute attr in prop.GetCustomAttributes(true))
            {
                if (attr is IsType typeAttr)
                {
                    return typeAttr.Type;
                }
            }

            var possible = RegisteredTypes.FindAll(rt => rt.TypeRef == prop.PropertyType);
            if (possible.Count == 0)
                return null;
            if (possible.Count == 1)
                return possible[0];
            return possible.Find(f => f.OverrideSameType);
        }

        public static GraphQLType FindOrGenerate(Type T)
        {
            if (T == typeof(void))
                return new GraphQLTypeIntrinsic.Null();

            if (T.IsArray)
                T = T.GetElementType();
            var typeAttrs = T.GetCustomAttributes();
            if (!(T.IsClass || T.IsInterface || T.IsEnum))
                throw new Exception($"{T.Name} is an unsupported GQL type!");

            if (T.GetCustomAttribute(typeof(GQLTypeDefinition)) == null)
                throw new Exception($"{T.Name} is missing the GQLType Tag!");


            if (T.IsClass || T.IsInterface)
                return GenerateObjectTypeDefinition(T);
            if (T.IsEnum)
                return GenerateEnumTypeDefinition(T);
            return RegisteredTypes.Find(t => t.TypeName == T.Name);
        }

        private static GraphQLTypeIntrinsic.GQLInterface GenerateObjectTypeDefinition(Type T)
        {
            var typeAttrs = T.GetCustomAttributes();
            if (!(T.IsClass || T.IsInterface))
                throw new Exception($"{T.Name} is not a class or interface!");

            if (T.GetCustomAttribute(typeof(GQLTypeDefinition)) == null)
                throw new Exception($"{T.Name} is missing the GQLType Tag!");

            var existingType = RegisteredTypes.Find(t => t.TypeName == T.Name);
            if (existingType != null)
                return (GraphQLTypeIntrinsic.GQLInterface) existingType;

            var props = T.GetProperties();
            var customObject = new GraphQLTypeIntrinsic.GQLInterface(T, T.Name);

            foreach (var prop in props)
            {
                var gqlType =
                    GraphQLType.GetTypeOf(prop) ?? FindOrGenerate(prop.PropertyType);
                customObject.Fields.Add(new GQLTypeInstance
                    {Name = prop.Name, isArray = prop.PropertyType.IsArray, type = gqlType});
            }

            return customObject;
        }

        private static GraphQLTypeIntrinsic.GQLEnum GenerateEnumTypeDefinition(Type T)
        {
            var typeAttrs = T.GetCustomAttributes();
            if (!T.IsEnum)
                throw new Exception($"{T.Name} is not an Enum!");


            if (T.GetCustomAttribute(typeof(GQLTypeDefinition)) == null)
                throw new Exception($"{T.Name} is missing the GQLType Tag!");

            var names = T.GetEnumNames();
            var customEnum = new GraphQLTypeIntrinsic.GQLEnum(T, T.Name);

            foreach (var name in names)
                customEnum.Names.Add(name);

            return customEnum;
        }

        public static void RegisterIntrinsic()
        {
            GraphQLType empty = new GraphQLTypeIntrinsic.Boolean();
            empty = new GraphQLTypeIntrinsic.Float();
            empty = new GraphQLTypeIntrinsic.Int();
            empty = new GraphQLTypeIntrinsic.String();
            empty = new GraphQLTypeIntrinsic.ID();
            empty = new GraphQLTypeIntrinsic.Null();
            empty = null;
        }
    }
}