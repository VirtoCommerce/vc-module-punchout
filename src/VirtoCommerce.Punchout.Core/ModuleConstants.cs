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

        public const string PunchoutGrantType = "punchout";

        public static class Claims
        {
            /// <summary>
            /// Set to <see cref="PunchoutGrantType"/> if the token is issued by the punchout grant.
            /// </summary>
            public const string ChannelId = "channelId";

            /// <summary>
            /// The punchout session id.
            /// </summary>
            public const string ChannelSessionId = "channelSessionId";
        }
    }

    public const string DefaultPunchoutHandlerName = "DefaultPunchoutHandler";

    public static class ConfigurationSections
    {
        public const string ConfigurationKey = "Punchout";
    }

    public static class SessionStatus
    {
        public const string Active = "Active";
        public const string Returned = "Returned";
        public const string Expired = "Expired";
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

        public static IEnumerable<SettingDescriptor> StoreSettings
        {
            get
            {
                yield return General.PunchoutEnabled;
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
