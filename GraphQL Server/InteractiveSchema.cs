using System;
using System.Collections.Generic;

namespace GraphQL_Server
{
    public struct callableController
    {
        public GraphQLController Controller { get; set; }
        public string ObjectName { get; set; }
        public List<> 
    }
    public class SchemaInterface
    {
        public SchemaInterface(IEnumerable<GraphQLController> controllers)
        {
            foreach (var controller in controllers)
            {
                var controllerType = controller.GetType();
                var controllerMethods = controllerType.GetMethods();
                foreach (var controllerMethod in controllerMethods)
                {
                    if(controllerMethod.IsPublic && controllerMethod.)
                    
                }
            }
        }
    }
}