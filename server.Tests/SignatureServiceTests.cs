using System;
using Xunit;
using FluentAssertions;

public class SignatureServiceTests
{
    [Theory]
    [InlineData("")]
    [InlineData("data:text/plain;base64,aaaa")]
    [InlineData("not-a-data-url")]
    public void StampSignaturePngIntoField_BadDataUrl_Throws(string dataUrl)
    {
        // Arrange
        var sut = new SignatureService();

        // Act
        Action act = () => sut.StampSignaturePngIntoField("in.pdf", "out.pdf", "CustomerSignature", dataUrl);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }
}

