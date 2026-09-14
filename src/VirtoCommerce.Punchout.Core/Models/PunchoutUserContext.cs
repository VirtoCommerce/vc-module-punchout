namespace VirtoCommerce.Punchout.Core.Models;

/// <summary>
/// The person on the buying side who started the punchout session,
/// taken from the endUser Contact of the setup request.
/// </summary>
public class PunchoutUserContext
{
    public string Name { get; set; }

    public string Email { get; set; }
}
