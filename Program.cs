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

builder.Services.AddHttpClient<OllamaAIService>();
builder.Services.AddHttpClient<OpenAIAIService>();

builder.Services.AddTransient<IAIService>(sp => sp.GetRequiredService<OllamaAIService>());
builder.Services.AddTransient<IAIService>(sp => sp.GetRequiredService<OpenAIAIService>());

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

//app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.Run();
