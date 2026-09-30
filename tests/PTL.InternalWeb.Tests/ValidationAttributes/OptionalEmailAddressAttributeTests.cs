using PTL.InternalWeb.ValidationAttributes;

namespace PTL.InternalWeb.Tests.ValidationAttributes;

public class OptionalEmailAddressAttributeTests
{
    private static readonly OptionalEmailAddressAttribute Attribute = new();

    [Fact]
    public void IsValid_Null_ReturnsTrue() =>
        Assert.True(Attribute.IsValid(null));

    [Fact]
    public void IsValid_EmptyString_ReturnsTrue() =>
        Assert.True(Attribute.IsValid(string.Empty));

    [Fact]
    public void IsValid_Whitespace_ReturnsTrue() =>
        Assert.True(Attribute.IsValid("   "));

    [Fact]
    public void IsValid_ValidEmail_ReturnsTrue() =>
        Assert.True(Attribute.IsValid("alice@example.com"));

    [Fact]
    public void IsValid_InvalidEmail_ReturnsFalse() =>
        Assert.False(Attribute.IsValid("not-an-email"));

    [Fact]
    public void IsValid_NonStringValue_ReturnsTrue() =>
        Assert.True(Attribute.IsValid(42));
}
