using SmartDocuMind.Services;

var builder = WebApplication.CreateBuilder(args);

// =========================
// Add services to DI container
// =========================
builder.Services.AddControllers();

// Register your services
builder.Services.AddSingleton<IFileProcessor, FileProcessor>();
builder.Services.AddSingleton<IChatMemoryService, ChatMemoryService>();

// HttpClient for streaming API
builder.Services.AddHttpClient();

// Swagger for testing
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// =========================
// Middleware
// =========================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.Run();