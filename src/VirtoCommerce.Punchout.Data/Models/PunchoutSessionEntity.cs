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
    public string SessionTokenHash { get; set; }

    public bool IsSessionTokenRedeemed { get; set; }

    [StringLength(512)]
    public string BuyerCookie { get; set; }

    [StringLength(512)]
    public string BuyerIdentity { get; set; }

    [StringLength(256)]
    public string BuyerDomain { get; set; }

    [StringLength(512)]
    public string SenderIdentity { get; set; }

    [StringLength(256)]
    public string SenderDomain { get; set; }

    [StringLength(2048)]
    public string ReturnUrl { get; set; }

    public DateTime? ExpirationDate { get; set; }

    public DateTime? TokenExpirationDate { get; set; }

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
        model.SessionTokenHash = SessionTokenHash;
        model.IsSessionTokenRedeemed = IsSessionTokenRedeemed;
        model.BuyerCookie = BuyerCookie;
        model.BuyerIdentity = BuyerIdentity;
        model.BuyerDomain = BuyerDomain;
        model.SenderIdentity = SenderIdentity;
        model.SenderDomain = SenderDomain;
        model.ReturnUrl = ReturnUrl;
        model.ExpirationDate = ExpirationDate;
        model.TokenExpirationDate = TokenExpirationDate;
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
        SessionTokenHash = model.SessionTokenHash;
        IsSessionTokenRedeemed = model.IsSessionTokenRedeemed;
        BuyerCookie = model.BuyerCookie;
        BuyerIdentity = model.BuyerIdentity;
        BuyerDomain = model.BuyerDomain;
        SenderIdentity = model.SenderIdentity;
        SenderDomain = model.SenderDomain;
        ReturnUrl = model.ReturnUrl;
        ExpirationDate = model.ExpirationDate;
        TokenExpirationDate = model.TokenExpirationDate;
        Status = model.Status;
        StartPage = model.StartPage;
        UserId = model.UserId;

        return this;
    }

    public virtual void Patch(PunchoutSessionEntity target)
    {
        target.StoreId = StoreId;
        target.SessionTokenHash = SessionTokenHash;
        target.IsSessionTokenRedeemed = IsSessionTokenRedeemed;
        target.BuyerCookie = BuyerCookie;
        target.BuyerIdentity = BuyerIdentity;
        target.BuyerDomain = BuyerDomain;
        target.SenderIdentity = SenderIdentity;
        target.SenderDomain = SenderDomain;
        target.ReturnUrl = ReturnUrl;
        target.ExpirationDate = ExpirationDate;
        target.TokenExpirationDate = TokenExpirationDate;
        target.Status = Status;
        target.StartPage = StartPage;
        target.UserId = UserId;
    }
}
