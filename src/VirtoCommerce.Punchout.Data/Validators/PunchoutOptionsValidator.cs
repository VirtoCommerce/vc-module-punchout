using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Data.Validators;

/// <summary>
/// Checks the punchout configurations on the platform start.
/// </summary>
public class PunchoutOptionsValidator : IValidateOptions<PunchoutOptions>
{
    public ValidateOptionsResult Validate(string name, PunchoutOptions options)
    {
        if (options.Configurations.IsNullOrEmpty())
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        for (var i = 0; i < options.Configurations.Count; i++)
        {
            var configuration = options.Configurations[i];
            var path = $"{ModuleConstants.ConfigurationSections.ConfigurationKey}:{nameof(options.Configurations)}:{i}";

            if (configuration is null)
            {
                failures.Add($"{path} is empty.");
                continue;
            }

            if (configuration.Id.IsNullOrEmpty())
            {
                failures.Add($"{path}:{nameof(configuration.Id)} is required.");
            }

            if (configuration.StoreId.IsNullOrEmpty())
            {
                failures.Add($"{path}:{nameof(configuration.StoreId)} is required.");
            }

            if (configuration.SharedSecret.IsNullOrEmpty())
            {
                failures.Add($"{path}:{nameof(configuration.SharedSecret)} is required.");
            }
        }

        var configurations = options.Configurations.Where(x => x is not null).ToList();

        failures.AddRange(configurations
            .Where(x => !x.Id.IsNullOrEmpty())
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => $"{nameof(PunchoutConfiguration.Id)} '{x.Key}' is used by {x.Count()} configurations, it must be unique."));

        // Do not expose secret in logs
        failures.AddRange(configurations
            .Where(x => !x.SharedSecret.IsNullOrEmpty())
            .GroupBy(x => x.SharedSecret, StringComparer.Ordinal)
            .Where(x => x.Count() > 1)
            .Select(x => $"Configurations {string.Join(", ", x.Select(c => $"'{c.Id}'"))} have the same {nameof(PunchoutConfiguration.SharedSecret)}, it must be unique."));

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
