using System.Reflection;
using Dapper;
using PTL.Core.Distribution;

namespace PTL.Data.Infrastructure;

public static partial class DapperColumnMappings
{
    // The Tabulation email rows follow the convention column "fld{Property}" -> Property.
    private static void RegisterTabulationEmailMappings()
    {
        foreach (var type in new[] { typeof(TabulationEmailCandidateEntity), typeof(TabulationConsultantEntity), typeof(NotificationEmailEntity) })
        {
            var map = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .ToDictionary(p => "fld" + p.Name, p => p.Name, StringComparer.OrdinalIgnoreCase);

            SqlMapper.SetTypeMap(type, new CustomPropertyTypeMap(type, (t, columnName) => ResolveProperty(t, map, columnName)!));
        }
    }
}
