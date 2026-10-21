using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PTL.InternalWeb.Features.Contract;

// Legacy ContractItems.aspx's "Audit Trail" dialog: the pending removals plus the reason for them.
public sealed class ContractItemRemovalAuditViewModel
{
    [BindRequired]
    public Guid ContractId { get; set; }

    public Guid[] Pending { get; set; } = [];

    // tblAuditParticipantScheme.fldAuditWhy is varchar(255).
    [StringLength(255, ErrorMessage = "The reason must be 255 characters or fewer")]
    public string? AuditWhy { get; set; }
}
