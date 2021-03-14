using GraphQL_Server.Attributes;

namespace GraphQL_Server
{
    [GQLTypeDefinition]
    public class Country
    {
        public int number { get; set; }
        public string Id { get; set; }
    }

    [GQLController(type = typeof(Country))]
    public class CountryController : GraphQLController
    {
        public Country getCountry([Arg("id", typeof(GraphQLTypeIntrinsic.ID))]
            string id)
        {
            return new Country {number = 1000, Id = id};
        }
    }
}