using BinOcular.Api.Tools;
using BinOcular.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Add a simple upload endpoint for testing purposes
app.MapPost("/upload", async (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new ApiError
        {
            Code = 400,
            Message = "Invalid form content type."
        });
    }

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");

    if (file == null || file.Length == 0)
    {
        return Results.BadRequest(new ApiError
        {
            Code = 400,
            Message = "No file uploaded or file is empty."
        });
    }

    // Save the uploaded file to a temporary location
    var tempFilePath = Path.GetTempFileName();
    using (var stream = new FileStream(tempFilePath, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }

    // Here you can call your analysis tools on the uploaded file
    // For example, you could use the Sha256Tool to analyze the file
    var sha256Tool = new Sha256Tool();
    if (sha256Tool.IsCompatible(tempFilePath))
    {
        var result = await sha256Tool.AnalyzeAsync(tempFilePath);
        return Results.Ok(new ApiResponse<ToolResult>
        {
            Success = true,
            Data = result
        });
    }
    else
    {
        return Results.BadRequest(new ApiError
        {
            Code = 400,
            Message = "The uploaded file is not compatible with the analysis tool."
        });
    }
});

app.Run();