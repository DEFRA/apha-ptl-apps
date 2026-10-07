using PTL.InternalWeb.Pagination;

namespace PTL.InternalWeb.Tests.Pagination;

public class PaginationModelTests
{
    [Fact]
    public void TotalPages_PageSizeZeroOrLess_ReturnsOne()
    {
        var model = new PaginationModel { PageSize = 0, TotalRecords = 100 };

        Assert.Equal(1, model.TotalPages);
    }

    [Fact]
    public void TotalPages_NoRecords_ReturnsOneRatherThanZero()
    {
        var model = new PaginationModel { PageSize = 25, TotalRecords = 0 };

        Assert.Equal(1, model.TotalPages);
    }

    [Fact]
    public void TotalPages_RecordsSpanMultiplePages_RoundsUp()
    {
        var model = new PaginationModel { PageSize = 25, TotalRecords = 51 };

        Assert.Equal(3, model.TotalPages);
    }

    [Fact]
    public void HasPreviousPage_FirstPage_IsFalse()
    {
        var model = new PaginationModel { CurrentPage = 1, PageSize = 25, TotalRecords = 100 };

        Assert.False(model.HasPreviousPage);
    }

    [Fact]
    public void HasPreviousPage_LaterPage_IsTrue()
    {
        var model = new PaginationModel { CurrentPage = 2, PageSize = 25, TotalRecords = 100 };

        Assert.True(model.HasPreviousPage);
    }

    [Fact]
    public void HasNextPage_LastPage_IsFalse()
    {
        var model = new PaginationModel { CurrentPage = 4, PageSize = 25, TotalRecords = 100 };

        Assert.False(model.HasNextPage);
    }

    [Fact]
    public void HasNextPage_EarlierPage_IsTrue()
    {
        var model = new PaginationModel { CurrentPage = 1, PageSize = 25, TotalRecords = 100 };

        Assert.True(model.HasNextPage);
    }
}
