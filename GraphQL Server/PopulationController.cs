using GraphQL_Server.Attributes;

namespace GraphQL_Server
{
    [GQLTypeDefinition]
    public class Population
    {
        public int populationAbove50 { get; set; }
        public int populationBelow50 { get; set; }
    }

    [GQLController(type = typeof(Population))]
    public class PopulationController : GraphQLController
    {
        [Query(Name = "getPopulation", Default = true)]
        public Population getPopulation()
        {
            return new() {populationAbove50 = 10000, populationBelow50 = 478293289};
        }
    }
}