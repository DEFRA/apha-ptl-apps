using System.ComponentModel.DataAnnotations;

namespace PTL.InternalWeb.Features.Account
{
    public class AccessDeniedViewModel
    {
        [Display(Name = "Return URL")]
        public string? ReturnUrl { get; set; }
    }
}
