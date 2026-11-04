using PTL.Core.Scheme;

namespace PTL.Api.Tests.Scheme;

// Exercises the compiler-generated record members (equality, hash, ToString, Deconstruct, with-copy) that
// the detector tests never call, so the audit rows can safely be compared and copied by value.
public class SchemeAuditRecordTests
{
    private static readonly Guid ItemId = Guid.Parse("00000001-0000-0000-0000-000000000000");
    private static readonly Guid TypeId = Guid.Parse("00000002-0000-0000-0000-000000000000");

    private static SchemeItemAudit Item() => new(ItemId, SchemeAuditOperation.Update, TypeId, true, 2, false);

    private static SchemeTabulationAudit Tabulation() => new(ItemId, SchemeAuditOperation.Update, "Published", true, false, true);

    [Fact]
    public void SchemeItemAudit_EqualityHashAndOperatorsAreByValue()
    {
        var audit = Item();
        var same = Item();

        Assert.True(audit.Equals(same));
        Assert.True(audit.Equals((object)same));
        Assert.True(audit == same);
        Assert.False(audit != same);
        Assert.Equal(audit.GetHashCode(), same.GetHashCode());
        Assert.False(audit.Equals(null));
        Assert.False(audit.Equals((object)"not an audit"));
        Assert.False(audit == null);
        Assert.True(audit != null);
    }

    [Fact]
    public void SchemeItemAudit_AnyDifferingComponentMakesItUnequal()
    {
        var audit = Item();

        Assert.NotEqual(audit, audit with { Id = Guid.NewGuid() });
        Assert.NotEqual(audit, audit with { Operation = SchemeAuditOperation.Delete });
        Assert.NotEqual(audit, audit with { TypeId = Guid.NewGuid() });
        Assert.NotEqual(audit, audit with { TypeChanged = false });
        Assert.NotEqual(audit, audit with { Order = 3 });
        Assert.NotEqual(audit, audit with { OrderChanged = true });
    }

    [Fact]
    public void SchemeItemAudit_WithCopyDeconstructAndToStringExposeEveryComponent()
    {
        var copy = Item() with { Order = 5, OrderChanged = true };

        var (id, operation, typeId, typeChanged, order, orderChanged) = copy;

        Assert.Equal((ItemId, SchemeAuditOperation.Update, TypeId, true, 5, true), (id, operation, typeId, typeChanged, order, orderChanged));
        Assert.Equal(2, Item().Order);
        var text = copy.ToString();
        Assert.Contains(nameof(SchemeItemAudit), text);
        Assert.Contains("Operation = Update", text);
        Assert.Contains("Order = 5", text);
    }

    [Fact]
    public void SchemeTabulationAudit_EqualityHashAndOperatorsAreByValue()
    {
        var audit = Tabulation();
        var same = Tabulation();

        Assert.True(audit.Equals(same));
        Assert.True(audit.Equals((object)same));
        Assert.True(audit == same);
        Assert.False(audit != same);
        Assert.Equal(audit.GetHashCode(), same.GetHashCode());
        Assert.False(audit.Equals(null));
        Assert.False(audit.Equals((object)"not an audit"));
        Assert.False(audit == null);
        Assert.True(audit != null);
    }

    [Fact]
    public void SchemeTabulationAudit_AnyDifferingComponentMakesItUnequal()
    {
        var audit = Tabulation();

        Assert.NotEqual(audit, audit with { Id = Guid.NewGuid() });
        Assert.NotEqual(audit, audit with { Operation = SchemeAuditOperation.Insert });
        Assert.NotEqual(audit, audit with { Name = "Other" });
        Assert.NotEqual(audit, audit with { NameChanged = false });
        Assert.NotEqual(audit, audit with { IntendedResultsOnly = true });
        Assert.NotEqual(audit, audit with { IntendedResultsOnlyChanged = false });
    }

    [Fact]
    public void SchemeTabulationAudit_WithCopyDeconstructAndToStringExposeEveryComponent()
    {
        var copy = Tabulation() with { Name = "Renamed", IntendedResultsOnly = true };

        var (id, operation, name, nameChanged, intendedResultsOnly, intendedResultsOnlyChanged) = copy;

        Assert.Equal((ItemId, SchemeAuditOperation.Update, "Renamed", true, true, true), (id, operation, name, nameChanged, intendedResultsOnly, intendedResultsOnlyChanged));
        Assert.Equal("Published", Tabulation().Name);
        var text = copy.ToString();
        Assert.Contains(nameof(SchemeTabulationAudit), text);
        Assert.Contains("Name = Renamed", text);
        Assert.Contains("IntendedResultsOnly = True", text);
    }
}
