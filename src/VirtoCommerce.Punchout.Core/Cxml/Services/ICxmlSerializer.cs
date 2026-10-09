namespace VirtoCommerce.Punchout.Core.Cxml.Services;

public interface ICxmlSerializer
{
    T Deserialize<T>(string xml);

    string Serialize<T>(T value);
}
