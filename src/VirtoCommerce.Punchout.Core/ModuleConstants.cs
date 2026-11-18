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

    /// <summary>
    /// Maximum lengths of the stored order request fields, shared by the validation, the storage and the database columns.
    /// </summary>
    public static class FieldLength
    {
        public const int Id = 128;
        public const int PayloadId = 512;
        public const int Identity = 512;
        public const int PurchaseOrderNumber = 256;
        public const int OrderType = 32;
        public const int OrderVersion = 128;
        public const int LineNumber = 128;
        public const int Currency = 64;
        public const int Status = 64;
        public const int ErrorCode = 64;
        public const int ErrorMessage = 2048;
    }

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
