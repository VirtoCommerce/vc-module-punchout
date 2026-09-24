using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.Punchout.ExperienceApi.Schemas;

public class PunchoutSessonActivationResultType : ExtendableGraphType<PunchoutSessionActivationResult>
{
    public PunchoutSessonActivationResultType()
    {
        Field(x => x.Error, nullable: true)
            .Description("Error code of a failed activation. Empty when the session was activated.");

        Field(x => x.PunchoutCartId, nullable: true)
            .Description("The cart the punchout session works with. Empty when the session uses the default cart.");

        Field(x => x.PunchoutCartName, nullable: true)
            .Description("The name of the cart the punchout session works with.");

        Field(x => x.ExpiresIn, nullable: true)
            .Description("Number of seconds until the punchout session expires, counted from the moment of activation. Empty when the activation failed.");
    }
}
