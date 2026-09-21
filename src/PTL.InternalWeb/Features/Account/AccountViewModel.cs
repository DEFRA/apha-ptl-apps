using System.ComponentModel.DataAnnotations;
using PTL.ApiClient;

namespace PTL.InternalWeb.Features.Account
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
