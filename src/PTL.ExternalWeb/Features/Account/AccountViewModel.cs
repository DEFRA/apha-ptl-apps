using System.ComponentModel.DataAnnotations;

namespace PTL.ExternalWeb.Features.Account
{
    public class AccountViewModel
    {
        [Required]
        public string Username { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public string ReturnUrl { get; set; }
    }
}
