using System.Text;
using FluentAssertions;
using SeatSync.Api.Utilities;

namespace SeatSync.Tests.Api;

public class SimplePdfBuilderTests
{
    [Fact]
    public void BuildSinglePageReceipt_Should_Create_A_Valid_Pdf_Structure()
    {
        var bytes = SimplePdfBuilder.BuildSinglePageReceipt(
        [
            "SeatSync Receipt",
            "Order Id: 123"
        ]);

        var content = Encoding.ASCII.GetString(bytes);

        content.Should().Contain("%PDF-1.4");
        content.Should().Contain("xref");
        content.Should().Contain("trailer");
        content.Should().Contain("%%EOF");
    }

    [Fact]
    public void BuildSinglePageReceipt_Should_Escape_Pdf_Text_Control_Characters()
    {
        var bytes = SimplePdfBuilder.BuildSinglePageReceipt(
        [
            @"Line with \ slash and (parens)"
        ]);

        var content = Encoding.ASCII.GetString(bytes);

        content.Should().Contain(@"Line with \\ slash and \(parens\)");
    }
}
