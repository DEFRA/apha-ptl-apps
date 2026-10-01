using PTL.InternalWeb.ValidationAttributes;

namespace PTL.InternalWeb.Tests.ValidationAttributes;

public class NotEmptyGuidAttributeTests
{
    private static readonly NotEmptyGuidAttribute Attribute = new();

    [Fact]
    public void IsValid_NonEmptyGuid_ReturnsTrue() =>
        Assert.True(Attribute.IsValid(Guid.NewGuid()));

    [Fact]
    public void IsValid_EmptyGuid_ReturnsFalse() =>
        Assert.False(Attribute.IsValid(Guid.Empty));

    [Fact]
    public void IsValid_Null_ReturnsFalse() =>
        Assert.False(Attribute.IsValid(null));

    [Fact]
    public void IsValid_NonGuidValue_ReturnsFalse() =>
        Assert.False(Attribute.IsValid("not-a-guid"));
}
