using SmartDocuMind.Services;

namespace SmartDocuMind.Tests.Services;

public class FileProcessorTests
{
    private readonly FileProcessor _service = new();

    [Fact]
    public async Task ExtractTextAsync_WhenFileIsText_ReturnsFileContents()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");
        await File.WriteAllTextAsync(path, "alpha" + Environment.NewLine + "beta");

        try
        {
            var result = await _service.ExtractTextAsync(path);

            Assert.Equal("alpha" + Environment.NewLine + "beta", result);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExtractTextAsync_WhenExtensionIsUnsupported_ThrowsInvalidOperationException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csv");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ExtractTextAsync(path));

        Assert.Equal("Unsupported file type.", exception.Message);
    }

    [Fact]
    public void SplitIntoChunks_WhenTextIsEmpty_ReturnsEmptyList()
    {
        var result = _service.SplitIntoChunks(string.Empty);

        Assert.Empty(result);
    }

    [Fact]
    public void SplitIntoChunks_WhenTextExceedsChunkSize_SplitsPredictably()
    {
        var result = _service.SplitIntoChunks("abcdefghij", 4);

        Assert.Equal(3, result.Count);
        Assert.Equal("abcd", result[0]);
        Assert.Equal("efgh", result[1]);
        Assert.Equal("ij", result[2]);
    }

    [Fact]
    public void CountLines_WhenTextContainsBlankLines_IgnoresEmptyEntries()
    {
        var result = _service.CountLines("first\n\nsecond\n");

        Assert.Equal(2, result);
    }

    [Fact]
    public void CountLines_WhenTextIsWhitespace_ReturnsZero()
    {
        var result = _service.CountLines("   ");

        Assert.Equal(0, result);
    }
}
