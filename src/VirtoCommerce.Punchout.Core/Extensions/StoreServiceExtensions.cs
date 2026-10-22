using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.Punchout.Core.Extensions;

public static class StoreServiceExtensions
{
    public static async Task<Store> GetPunchoutStoreAsync(this IStoreService storeService, string storeId)
    {
        if (string.IsNullOrEmpty(storeId))
        {
            return null;
        }

        var store = await storeService.GetNoCloneAsync(storeId);

        return store != null && store.Settings.GetValue<bool>(ModuleConstants.Settings.General.PunchoutEnabled)
            ? store
            : null;
    }
}
