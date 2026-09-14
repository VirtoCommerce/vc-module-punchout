using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Data.Models;

public class PunchoutIntegrationEntity : AuditableEntity, IDataEntity<PunchoutIntegrationEntity, PunchoutIntegration>
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public bool IsActive { get; set; }

    [StringLength(256)]
    public string Name { get; set; }

    [StringLength(128)]
    public string StoreId { get; set; }

    [StringLength(256)]
    public string CredentialDomain { get; set; }

    [StringLength(512)]
    public string SenderIdentity { get; set; }

    public string SharedSecretHash { get; set; }

    /// <summary>
    /// A JSON array of URLs. Exposed on the model as <see cref="PunchoutIntegration.AllowedReturnUrls"/>.
    /// </summary>
    public string AllowedReturnUrls { get; set; }

    public virtual ObservableCollection<PunchoutIntegrationOrganizationEntity> Organizations { get; set; } = [];

    public virtual PunchoutIntegration ToModel(PunchoutIntegration model)
    {
        model.Id = Id;
        model.CreatedBy = CreatedBy;
        model.CreatedDate = CreatedDate;
        model.ModifiedBy = ModifiedBy;
        model.ModifiedDate = ModifiedDate;

        model.IsActive = IsActive;
        model.Name = Name;
        model.StoreId = StoreId;
        model.CredentialDomain = CredentialDomain;
        model.SenderIdentity = SenderIdentity;
        model.SharedSecretHash = SharedSecretHash;
        model.AllowedReturnUrls = DeserializeUrls(AllowedReturnUrls);
        model.OrganizationIds = Organizations.Select(x => x.OrganizationId).ToList();

        return model;
    }

    public virtual PunchoutIntegrationEntity FromModel(PunchoutIntegration model, PrimaryKeyResolvingMap pkMap)
    {
        pkMap.AddPair(model, this);

        Id = model.Id;
        CreatedBy = model.CreatedBy;
        CreatedDate = model.CreatedDate;
        ModifiedBy = model.ModifiedBy;
        ModifiedDate = model.ModifiedDate;

        IsActive = model.IsActive;
        Name = model.Name;
        StoreId = model.StoreId;
        CredentialDomain = model.CredentialDomain;
        SenderIdentity = model.SenderIdentity;
        SharedSecretHash = model.SharedSecretHash;
        AllowedReturnUrls = SerializeUrls(model.AllowedReturnUrls);

        // A null collection tells Patch() to leave the stored links alone, so that a client which does not
        // manage organizations does not wipe them by omitting the property.
        Organizations = model.OrganizationIds is null
            ? new NullCollection<PunchoutIntegrationOrganizationEntity>()
            : new ObservableCollection<PunchoutIntegrationOrganizationEntity>(model.OrganizationIds
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct()
                .Select(x => new PunchoutIntegrationOrganizationEntity { OrganizationId = x, IntegrationId = Id }));

        return this;
    }

    public virtual void Patch(PunchoutIntegrationEntity target)
    {
        target.IsActive = IsActive;
        target.Name = Name;
        target.StoreId = StoreId;
        target.CredentialDomain = CredentialDomain;
        target.SenderIdentity = SenderIdentity;
        target.AllowedReturnUrls = AllowedReturnUrls;

        // The hash is only filled in when a new shared secret was supplied. Otherwise keep the stored one,
        // because it is never sent back to the client and therefore never comes back on update.
        if (!string.IsNullOrEmpty(SharedSecretHash))
        {
            target.SharedSecretHash = SharedSecretHash;
        }

        if (!Organizations.IsNullCollection())
        {
            var comparer = AnonymousComparer.Create((PunchoutIntegrationOrganizationEntity x) => x.OrganizationId);
            Organizations.Patch(target.Organizations, comparer, (_, _) => { });
        }
    }

    private static string SerializeUrls(IList<string> urls)
    {
        var values = urls?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

        return values.IsNullOrEmpty()
            ? null
            : JsonSerializer.Serialize(values, _jsonOptions);
    }

    private static IList<string> DeserializeUrls(string json)
    {
        return string.IsNullOrEmpty(json)
            ? []
            : JsonSerializer.Deserialize<List<string>>(json, _jsonOptions) ?? [];
    }
}
