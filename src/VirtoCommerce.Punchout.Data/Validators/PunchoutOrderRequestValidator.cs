using FluentValidation;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Punchout.Core;
using VirtoCommerce.Punchout.Core.Cxml;
using VirtoCommerce.Punchout.Core.Models;

namespace VirtoCommerce.Punchout.Data.Validators;

public class PunchoutOrderRequestValidator : AbstractValidator<PunchoutOrderRequestData>
{
    public PunchoutOrderRequestValidator()
    {
        RuleFor(x => x.PayloadId)
            .NotEmpty()
            .WithMessage("The cXML document has no payloadID.")
            .MaximumLength(ModuleConstants.FieldLength.PayloadId)
            .WithMessage($"The payloadID is longer than {ModuleConstants.FieldLength.PayloadId} characters.");

        RuleFor(x => x.PreviousPayloadId)
            .MaximumLength(ModuleConstants.FieldLength.PayloadId)
            .WithMessage($"The PreviousPayloadId is longer than {ModuleConstants.FieldLength.PayloadId} characters.");

        RuleFor(x => x.Sender)
            .MaximumLength(ModuleConstants.FieldLength.Identity)
            .WithMessage($"Sender is longer than {ModuleConstants.FieldLength.Identity} characters.");

        RuleFor(x => x.From)
            .MaximumLength(ModuleConstants.FieldLength.Identity)
            .WithMessage($"From is longer than {ModuleConstants.FieldLength.Identity} characters.");

        RuleFor(x => x.OrderVersion)
            .MaximumLength(ModuleConstants.FieldLength.OrderVersion)
            .WithMessage($"The OrderVersion is longer than {ModuleConstants.FieldLength.OrderVersion} characters.");

        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("The order has no orderID.")
            .MaximumLength(ModuleConstants.FieldLength.PurchaseOrderNumber)
            .WithMessage($"The orderID is longer than {ModuleConstants.FieldLength.PurchaseOrderNumber} characters.");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("The order has no Total currency.")
            .MaximumLength(ModuleConstants.FieldLength.Currency)
            .WithMessage($"The order Currency is longer than {ModuleConstants.FieldLength.Currency} characters.");

        RuleFor(x => x.OrderType)
            .MaximumLength(ModuleConstants.FieldLength.OrderType)
            .WithMessage($"The order type is longer than {ModuleConstants.FieldLength.OrderType} characters.");

        // 'delete' type may come without items
        RuleFor(x => x.Items)
            .NotEmpty()
            .When(x => ItemsRequired(x.OrderType))
            .WithMessage("The order has no items.");

        RuleForEach(x => x.Items)
            .Must(item => !item.SupplierPartId.IsNullOrEmpty())
            .WithMessage((_, item) => $"Line {item.LineNumber}: SupplierPartID is missing.")

            .Must(item => item.Quantity > 0)
            .WithMessage((_, item) => $"Line {item.LineNumber}: quantity must be positive.")

            .Must(item => item.UnitPrice is >= 0)
            .WithMessage((_, item) => $"Line {item.LineNumber}: unit price is missing or invalid.")

            // The item currency is optional, the order currency applies then
            .Must((request, item) => item.Currency.IsNullOrEmpty() || item.Currency.EqualsIgnoreCase(request.Currency))
            .WithMessage((request, item) => $"Line {item.LineNumber}: currency '{item.Currency}' does not match the order currency '{request.Currency}'.");
    }

    protected virtual bool ItemsRequired(string orderType)
    {
        // Empty type attribute defaults to 'new'
        return orderType.IsNullOrEmpty() ||
               orderType.EqualsIgnoreCase(CxmlConstants.OrderRequestType.New) ||
               orderType.EqualsIgnoreCase(CxmlConstants.OrderRequestType.Update);
    }
}
