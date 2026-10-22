using VirtoCommerce.Punchout.Core.Cxml.Models;
using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.XCart.Core;

namespace VirtoCommerce.Punchout.ExperienceApi.Services;

/// <summary>
/// Converts the buyer cart into a cXML PunchOutOrderMessage.
/// </summary>
public interface IPunchoutOrderMessageBuilder
{
    CxmlDocument Build(PunchoutSession session, CartAggregate cartAggregate);
}
