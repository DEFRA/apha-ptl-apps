namespace PTL.Core.Contract.CreateOrder;

public sealed class CreateOrderOptions
{
    public const string SectionName = "CreateOrder";

    // Legacy web.config AphaCustomerTypeId: APHA's own customers see every scheme, not just the commercial ones.
    public Guid AphaCustomerTypeId { get; set; } = Guid.Parse("ED89AE94-AFC7-4C92-82EB-C20BD35D060B");
}
