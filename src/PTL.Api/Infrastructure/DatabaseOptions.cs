using Microsoft.Data.SqlClient;

namespace PTL.Api.Infrastructure;

public sealed record DatabaseOptions(string Host, string Name, string User, string Password, bool TrustServerCertificate, bool IntegratedSecurity = false)
{
    public string ToConnectionString()
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = Host,
            InitialCatalog = Name,
            TrustServerCertificate = TrustServerCertificate
        };

        if (IntegratedSecurity)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = User;
            builder.Password = Password;
        }

        return builder.ConnectionString;
    }
}
