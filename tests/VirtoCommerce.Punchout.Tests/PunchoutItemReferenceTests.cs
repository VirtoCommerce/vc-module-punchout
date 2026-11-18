using VirtoCommerce.Punchout.Core.Models;
using Xunit;

namespace VirtoCommerce.Punchout.Tests;

[Trait("Category", "Unit")]
public class PunchoutItemReferenceTests
{
    [Theory]
    [InlineData("line-1", "session-1", "line-1;session-1")]
    [InlineData("line-1", null, "line-1")]
    [InlineData("line-1", "", "line-1")]
    public void Format_ReturnsLineItemIdAndSessionId(string lineItemId, string sessionId, string expected)
    {
        Assert.Equal(expected, PunchoutItemReference.Format(lineItemId, sessionId));
    }

    [Theory]
    [InlineData("line-1;session-1", "line-1", "session-1")]
    [InlineData(" line-1 ; session-1 ", "line-1", "session-1")]
    [InlineData("line-1", "line-1", null)]
    [InlineData("line-1;", "line-1", null)]
    [InlineData(";session-1", null, "session-1")]
    [InlineData("line-1;session-1;x", "line-1", "session-1;x")]
    [InlineData("", null, null)]
    [InlineData(null, null, null)]
    public void Parse_ReturnsLineItemIdAndSessionId(string value, string expectedLineItemId, string expectedSessionId)
    {
        var (lineItemId, sessionId) = PunchoutItemReference.Parse(value);

        Assert.Equal(expectedLineItemId, lineItemId);
        Assert.Equal(expectedSessionId, sessionId);
    }

    [Fact]
    public void Parse_ReturnsFormattedValues()
    {
        var (lineItemId, sessionId) = PunchoutItemReference.Parse(PunchoutItemReference.Format("line-1", "session-1"));

        Assert.Equal("line-1", lineItemId);
        Assert.Equal("session-1", sessionId);
    }
}
