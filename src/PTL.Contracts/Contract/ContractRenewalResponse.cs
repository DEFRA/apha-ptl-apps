namespace PTL.Contracts.Contract;

// Public API contract for GET /api/contracts/{contractId}/renewal, matching legacy
// PtaBusinessObjects.BusinessObjects.Exports.ContractRenewal (spgaExportContractRenewal).
// ContractStartDate/ContractEndDate are derived by the stored procedure from
// tblSystemSettings.fldContractStartDate and the current year; RenewalInformation is the
// concatenation produced by fnConcatRenewalInformation, not tblContract.fldRenewalInformation.
public sealed record ContractRenewalResponse(
    Guid ContractId,
    Guid CustomerId,
    string QalNumber,
    string OrganisationName,
    string ContactName,
    string Address1,
    string Address2,
    string Address3,
    string Address4,
    string Address5,
    string Country,
    DateTime ContractStartDate,
    DateTime ContractEndDate,
    string RenewalInformation);
