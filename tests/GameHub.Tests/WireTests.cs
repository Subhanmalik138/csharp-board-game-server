using GameHub.Protocol;

namespace GameHub.Tests;

public sealed class WireTests
{
    [Fact]
    public void Encode_ProducesFlatJsonObject()
    {
        var json = Wire.Encode("move", new() { ["column"] = 3 });
        Assert.Equal("""{"type":"move","column":3}""", json);
    }

    [Fact]
    public void Encode_WithoutFields_ProducesTypeOnly()
    {
        Assert.Equal("""{"type":"queue"}""", Wire.Encode("queue"));
    }

    [Fact]
    public void TryParse_RoundTripsEncodedMessage()
    {
        var json = Wire.Encode("chat", new() { ["text"] = "hello!" });
        Assert.True(Wire.TryParse(json, out var type, out var data));
        Assert.Equal("chat", type);
        Assert.Equal("hello!", Wire.GetString(data, "text"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"no_type":1}""")]
    [InlineData("""{"type":42}""")]
    public void TryParse_RejectsMalformedMessages(string line)
    {
        Assert.False(Wire.TryParse(line, out _, out _));
    }

    [Fact]
    public void GetString_ReturnsFallback_WhenFieldMissingOrWrongType()
    {
        Wire.TryParse("""{"type":"hello"}""", out _, out var data);
        Assert.Equal("", Wire.GetString(data, "name"));
        Assert.Equal("fallback", Wire.GetString(data, "name", "fallback"));

        Wire.TryParse("""{"type":"hello","name":7}""", out _, out data);
        Assert.Equal("fallback", Wire.GetString(data, "name", "fallback"));
    }

    [Fact]
    public void GetInt_ReturnsFallback_WhenFieldMissingOrWrongType()
    {
        Wire.TryParse("""{"type":"move"}""", out _, out var data);
        Assert.Equal(-1, Wire.GetInt(data, "column"));

        Wire.TryParse("""{"type":"move","column":"3"}""", out _, out data);
        Assert.Equal(-1, Wire.GetInt(data, "column"));
    }
}
