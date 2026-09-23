using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.Punchout.Core;

public static class ModuleConstants
{
    public static class Security
    {
        public static class Permissions
        {
            public const string Access = "punchout:access";
            public const string Create = "punchout:create";
            public const string Read = "punchout:read";
            public const string Update = "punchout:update";
            public const string Delete = "punchout:delete";

            public static string[] AllPermissions { get; } =
            [
                Access,
                Create,
                Read,
                Update,
                Delete,
            ];
        }
    }

    public static class ConfigurationSections
    {
        public const string CoupaConfiguration = "Punchout:CoupaConfiguration";
    }

    public static class SessionStatus
    {
        public const string Created = "Created";
        public const string Active = "Active";
        public const string Returned = "Returned";
        public const string Expired = "Expired";
    }

    /// <summary>
    /// Errors returned by the session activation mutation.
    /// </summary>
    public static class ActivationErrors
    {
        public const string SessionNotFound = "SESSION_NOT_FOUND";
        public const string StoreNotFound = "STORE_NOT_FOUND";
    }

    public static class Settings
    {
        public static class General
        {
            public static SettingDescriptor PunchoutEnabled { get; } = new()
            {
                Name = "Punchout.Enabled",
                GroupName = "Punchout|General",
                ValueType = SettingValueType.Boolean,
                DefaultValue = false,
                IsPublic = true,
            };

            public static IEnumerable<SettingDescriptor> AllGeneralSettings
            {
                get
                {
                    yield return PunchoutEnabled;
                }
            }
        }

        public static class Jobs
        {
            public static SettingDescriptor ExpireSessionsJobEnabled { get; } = new()
            {
                Name = "Punchout.ExpireSessionsJob.Enable",
                GroupName = "Punchout|Jobs",
                ValueType = SettingValueType.Boolean,
                DefaultValue = true,
            };

            public static SettingDescriptor ExpireSessionsJobCronExpression { get; } = new()
            {
                Name = "Punchout.ExpireSessionsJob.CronExpression",
                GroupName = "Punchout|Jobs",
                ValueType = SettingValueType.ShortText,
                DefaultValue = "*/15 * * * *",
            };

            public static IEnumerable<SettingDescriptor> AllJobsSettings
            {
                get
                {
                    yield return ExpireSessionsJobEnabled;
                    yield return ExpireSessionsJobCronExpression;
                }
            }
        }

        public static IEnumerable<SettingDescriptor> AllSettings
        {
            get
            {
                return General.AllGeneralSettings.Concat(Jobs.AllJobsSettings);
            }
        }
    }
}
