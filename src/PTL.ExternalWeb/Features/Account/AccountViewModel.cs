using System.ComponentModel.DataAnnotations;
using PTL.ApiClient;

namespace PTL.ExternalWeb.Features.Account
{
    public class AccountViewModel : IAccountCredentials
    {
        [Required]
        public string? Username { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string? Password { get; set; }

        public string? ReturnUrl { get; set; }
    }
}
