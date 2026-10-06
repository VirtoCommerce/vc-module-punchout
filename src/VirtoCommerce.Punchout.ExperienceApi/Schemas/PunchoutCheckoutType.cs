//using System.Linq;
//using GraphQL.Types;
//using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.Punchout.ExperienceApi.Schemas;

public class PunchoutCheckoutType : ExtendableGraphType<PunchoutCheckoutResult>
{
    public PunchoutCheckoutType()
    {
        Field(x => x.Url, nullable: true);
        Field(x => x.FormField, nullable: true);
        Field(x => x.Cxml, nullable: true);
        Field(x => x.ErrorCode, nullable: true);
    }
}
