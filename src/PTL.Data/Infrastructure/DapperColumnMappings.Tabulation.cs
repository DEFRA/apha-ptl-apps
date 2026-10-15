using System.Reflection;
using Dapper;
using PTL.Data.Distribution;

namespace PTL.Data.Infrastructure;

public static partial class DapperColumnMappings
{
    // Tabulation report rows follow the convention column "fld{Property}" -> Property.
    private static void RegisterTabulationReportMappings()
    {
        foreach (var type in typeof(TabulationReportRows).GetNestedTypes(BindingFlags.Public))
        {
            var map = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .ToDictionary(p => "fld" + p.Name, p => p.Name, StringComparer.OrdinalIgnoreCase);

            SqlMapper.SetTypeMap(type, new CustomPropertyTypeMap(type, (t, columnName) => ResolveProperty(t, map, columnName)!));
        }
    }
}
