using System;
using System.ComponentModel.DataAnnotations;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Data.Models;

public class PunchoutSessionEntity : AuditableEntity, IDataEntity<PunchoutSessionEntity, PunchoutSession>
{
    [StringLength(128)]
    public string StoreId { get; set; }

    [Required]
    [StringLength(128)]
    public string SessionToken { get; set; }

    [StringLength(512)]
    public string BuyerCookie { get; set; }

    [StringLength(512)]
    public string BuyerIdentity { get; set; }

    [StringLength(256)]
    public string BuyerDomain { get; set; }

    [StringLength(2048)]
    public string ReturnUrl { get; set; }

    public DateTime? ExpirationDate { get; set; }

    [Required]
    [StringLength(64)]
    public string Status { get; set; }

    [StringLength(2048)]
    public string StartPage { get; set; }

    [StringLength(128)]
    public string UserId { get; set; }

    public virtual PunchoutSession ToModel(PunchoutSession model)
    {
        model.Id = Id;
        model.CreatedBy = CreatedBy;
        model.CreatedDate = CreatedDate;
        model.ModifiedBy = ModifiedBy;
        model.ModifiedDate = ModifiedDate;

        model.StoreId = StoreId;
        model.SessionToken = SessionToken;
        model.BuyerCookie = BuyerCookie;
        model.BuyerIdentity = BuyerIdentity;
        model.BuyerDomain = BuyerDomain;
        model.ReturnUrl = ReturnUrl;
        model.ExpirationDate = ExpirationDate;
        model.Status = Status;
        model.StartPage = StartPage;
        model.UserId = UserId;

        return model;
    }

    public virtual PunchoutSessionEntity FromModel(PunchoutSession model, PrimaryKeyResolvingMap pkMap)
    {
        pkMap.AddPair(model, this);

        Id = model.Id;
        CreatedBy = model.CreatedBy;
        CreatedDate = model.CreatedDate;
        ModifiedBy = model.ModifiedBy;
        ModifiedDate = model.ModifiedDate;

        StoreId = model.StoreId;
        SessionToken = model.SessionToken;
        BuyerCookie = model.BuyerCookie;
        BuyerIdentity = model.BuyerIdentity;
        BuyerDomain = model.BuyerDomain;
        ReturnUrl = model.ReturnUrl;
        ExpirationDate = model.ExpirationDate;
        Status = model.Status;
        StartPage = model.StartPage;
        UserId = model.UserId;

        return this;
    }

    public virtual void Patch(PunchoutSessionEntity target)
    {
        target.StoreId = StoreId;
        target.SessionToken = SessionToken;
        target.BuyerCookie = BuyerCookie;
        target.BuyerIdentity = BuyerIdentity;
        target.BuyerDomain = BuyerDomain;
        target.ReturnUrl = ReturnUrl;
        target.ExpirationDate = ExpirationDate;
        target.Status = Status;
        target.StartPage = StartPage;
        target.UserId = UserId;
    }
}
