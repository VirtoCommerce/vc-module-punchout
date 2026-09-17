using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.Punchout.ExperienceApi.Queries;

// Mock query to init punchout schema
public class PunchoutMockQuery : Query<PunchoutMockResult>
{
    public override IEnumerable<QueryArgument> GetArguments()
    {
        return [];
    }

    public override void Map(IResolveFieldContext context)
    {
        return;
    }
}

public class PunchoutMockResult
{
    public bool Success { get; set; } = true;
}

public class PunchoutResultType : ExtendableGraphType<PunchoutMockResult>
{
    public PunchoutResultType()
    {
        Field(x => x.Success);
    }
}
