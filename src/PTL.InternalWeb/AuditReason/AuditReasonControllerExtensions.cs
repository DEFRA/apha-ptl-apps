using Microsoft.AspNetCore.Mvc;
using PTL.InternalWeb.Confirmation;

namespace PTL.InternalWeb.AuditReason;

public static class AuditReasonControllerExtensions
{
    // Returns the shared audit reason page for the POST being handled (legacy Audit.aspx).
    public static ViewResult AuditReasonPage(this Controller controller, AuditReasonPageViewModel model)
    {
        var request = controller.HttpContext?.Request;

        return controller.View(AuditReasonFlow.ViewName, model with
        {
            PostUrl = ConfirmationControllerExtensions.PostedUrl(request),
            Fields = ConfirmationControllerExtensions.PostedFields(
                request, AuditReasonFlow.IsAuditStepField, AuditReasonFlow.AuditObjectNameField, AuditReasonFlow.AuditWhyField),
        });
    }
}
