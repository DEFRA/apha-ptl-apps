using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace PTL.Data.Tests.Fakes;

// Minimal ADO.NET parameter/collection pair so FakeDbCommand can accept whatever Dapper (or
// DynamicParameters) adds to it - values are only ever read back for test assertions, never
// interpreted, so most members are simple auto-backed implementations.
internal sealed class FakeDbParameter : DbParameter
{
    public override DbType DbType { get; set; }
    public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
    public override bool IsNullable { get; set; }

    [AllowNull]
    public override string ParameterName { get; set; } = string.Empty;
    public override int Size { get; set; }

    [AllowNull]
    public override string SourceColumn { get; set; } = string.Empty;
    public override bool SourceColumnNullMapping { get; set; }
    public override object? Value { get; set; }
    public override void ResetDbType() => DbType = DbType.Object;
}

internal sealed class FakeDbParameterCollection : DbParameterCollection
{
    private readonly List<DbParameter> _parameters = [];

    public override int Count => _parameters.Count;
    public override object SyncRoot => _parameters;

    public override int Add(object value)
    {
        _parameters.Add((DbParameter)value);
        return _parameters.Count - 1;
    }

    public override void AddRange(Array values)
    {
        foreach (var value in values)
        {
            Add(value);
        }
    }

    public override void Clear() => _parameters.Clear();

    public override bool Contains(object value) => _parameters.Contains((DbParameter)value);

    public override bool Contains(string value) => _parameters.Any(p => p.ParameterName == value);

    public override void CopyTo(Array array, int index) => ((ICollection)_parameters).CopyTo(array, index);

    public override IEnumerator GetEnumerator() => _parameters.GetEnumerator();

    protected override DbParameter GetParameter(int index) => _parameters[index];

    protected override DbParameter GetParameter(string parameterName) => _parameters.Single(p => p.ParameterName == parameterName);

    public override int IndexOf(object value) => _parameters.IndexOf((DbParameter)value);

    public override int IndexOf(string parameterName) => _parameters.FindIndex(p => p.ParameterName == parameterName);

    public override void Insert(int index, object value) => _parameters.Insert(index, (DbParameter)value);

    public override void Remove(object value) => _parameters.Remove((DbParameter)value);

    public override void RemoveAt(int index) => _parameters.RemoveAt(index);

    public override void RemoveAt(string parameterName) => _parameters.RemoveAt(IndexOf(parameterName));

    protected override void SetParameter(int index, DbParameter value) => _parameters[index] = value;

    protected override void SetParameter(string parameterName, DbParameter value) => _parameters[IndexOf(parameterName)] = value;

    // Dapper's DynamicParameters may or may not preserve the leading "@" on the actual
    // IDbDataParameter.ParameterName it creates - compare with it stripped, case-insensitively, so
    // tests can just ask for "@Foo" regardless of that internal detail.
    public object? ValueOf(string parameterName)
    {
        var normalised = parameterName.TrimStart('@');
        var parameter = _parameters.Single(p => p.ParameterName.TrimStart('@').Equals(normalised, StringComparison.OrdinalIgnoreCase));
        return parameter.Value;
    }
}
