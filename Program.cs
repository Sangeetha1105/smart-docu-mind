using System.Text.Json.Serialization;
using SmartDocuMind.Services;

var builder = WebApplication.CreateBuilder(args);

// Convert enums to string instead of numbers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Swagger configuration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// your existing DI
builder.Services.AddSingleton<IFileProcessor, FileProcessor>();
builder.Services.AddSingleton<IChatMemoryService, ChatMemoryService>();

builder.Services.AddHttpClient<OllamaService>();
builder.Services.AddHttpClient<OpenAIService>();

builder.Services.AddTransient<IAIService>(sp => sp.GetRequiredService<OllamaService>());
builder.Services.AddTransient<IAIService>(sp => sp.GetRequiredService<OpenAIService>());

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

//app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.Run();
