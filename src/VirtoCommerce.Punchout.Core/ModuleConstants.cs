using System.Collections.Generic;
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
            };

            public static IEnumerable<SettingDescriptor> AllGeneralSettings
            {
                get
                {
                    yield return PunchoutEnabled;
                }
            }
        }

        public static IEnumerable<SettingDescriptor> AllSettings
        {
            get
            {
                return General.AllGeneralSettings;
            }
        }
    }
}
