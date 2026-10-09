using VirtoCommerce.Punchout.Core.Models;
using VirtoCommerce.Punchout.Data.Validators;
using Xunit;

namespace VirtoCommerce.Punchout.Tests;

[Trait("Category", "Unit")]
public class PunchoutOptionsValidatorTests
{
    private readonly PunchoutOptionsValidator _validator = new();

    [Fact]
    public void Validate_NoConfigurations_Succeeds()
    {
        var result = _validator.Validate(null, new PunchoutOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_ValidConfigurations_Succeeds()
    {
        var options = CreateOptions(
            CreateConfiguration("config-1", "secret-1"),
            CreateConfiguration("config-2", "secret-2"));

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(nameof(PunchoutConfiguration.Id))]
    [InlineData(nameof(PunchoutConfiguration.StoreId))]
    [InlineData(nameof(PunchoutConfiguration.SharedSecret))]
    public void Validate_MissingRequiredProperty_Fails(string propertyName)
    {
        var configuration = CreateConfiguration("config-1", "secret-1");
        typeof(PunchoutConfiguration).GetProperty(propertyName)!.SetValue(configuration, null);

        var result = _validator.Validate(null, CreateOptions(configuration));

        Assert.True(result.Failed);
        Assert.Contains($"Punchout:Configurations:0:{propertyName} is required.", result.Failures);
    }

    [Fact]
    public void Validate_DuplicateId_Fails()
    {
        var options = CreateOptions(
            CreateConfiguration("config-1", "secret-1"),
            CreateConfiguration("CONFIG-1", "secret-2"));

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Single(result.Failures);
    }

    [Fact]
    public void Validate_DuplicateSharedSecret_FailsWithoutSecretInMessage()
    {
        var options = CreateOptions(
            CreateConfiguration("config-1", "secret-1"),
            CreateConfiguration("config-2", "secret-1"));

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.DoesNotContain("secret-1", result.FailureMessage);
    }

    private static PunchoutOptions CreateOptions(params PunchoutConfiguration[] configurations)
    {
        return new PunchoutOptions { Configurations = configurations };
    }

    private static PunchoutConfiguration CreateConfiguration(string id, string sharedSecret)
    {
        return new PunchoutConfiguration
        {
            Id = id,
            StoreId = "store",
            SharedSecret = sharedSecret,
        };
    }
}
