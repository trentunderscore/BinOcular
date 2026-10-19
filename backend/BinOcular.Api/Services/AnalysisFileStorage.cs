namespace BinOcular.Api.Services;

public sealed class AnalysisFileStorage(IWebHostEnvironment environment)
{
    private readonly string _uploadDirectory =
        Path.Combine(environment.ContentRootPath, "App_Data", "uploads");

    public async Task<string> SaveAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_uploadDirectory);

        var storedFileName = $"{Guid.NewGuid():N}.bin";
        var filePath = Path.Combine(_uploadDirectory, storedFileName);

        await using var output = new FileStream(
            filePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await file.CopyToAsync(output, cancellationToken);

        return storedFileName;
    }

    public string GetPath(string storedFileName)
{
    return Path.Combine(_uploadDirectory, storedFileName);
}
}