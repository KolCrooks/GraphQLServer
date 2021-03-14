using System;
using System.Net;
using System.IO;
using System.Text;

namespace GraphQL_Server
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            GraphQLServer server = new GraphQLServer();
            server.AddController(new CountryController());
            server.Start();
        }
    }
}