using System;
using System.Collections.Generic;
using GraphQL_Server.Attributes;
using Newtonsoft.Json.Linq;

namespace GraphQL_Server
{
    [GQLTypeDefinition]
    public class Town
    {
        public string Name { get; set; }
        public string Id { get; set; }
    }

    [GQLTypeDefinition]
    public class Country
    {
        public int Number { get; set; }
        public string Id { get; set; }

        public Town[] Towns { get; set; }
    }

    [GQLController(type = typeof(Country))]
    public class CountryController : GraphQLController
    {
        private List<Country> _countries = new() {new() {Number = 1000, Id = "01234"}};

        private Dictionary<string, List<Town>> _towns = new();

        public CountryController()
        {
            _towns["01234"] = new List<Town>
                {new() {Name = "Town Name", Id = "SOMEID"}, new() {Name = "Town Name 2", Id = "SOMEID2"}};
        }

        [Query(Name = "getCountry", Default = true)]
        public Country getCountry(
            [Arg("Id", typeof(GraphQLTypeIntrinsic.ID))]
            string id)
        {
            Console.WriteLine("HI");
            return _countries.Find(c => c.Id == id);
        }

        [FieldResolver(Name = "Towns")]
        public Town[] Towns([Root] JObject root)
        {
            if (_towns.ContainsKey(root["Id"].Value<string>()))
                return _towns[root["Id"].Value<string>()].ToArray();

            return new Town[] { };
        }

        [Mutation(Name = "addCountry")]
        public Country addCountry([Arg("Id2", typeof(GraphQLTypeIntrinsic.ID))]
            string Id,
            [Arg("number", typeof(GraphQLTypeIntrinsic.Int))]
            int number)
        {
            var created = new Country() {Id = Id, Number = number};
            _countries.Add(created);
            return created;
        }
    }
}