using Google.Apis.Auth.OAuth2;
using Microsoft.OpenApi.Models;
using ReStore___backend.Services.Implementations;
using ReStore___backend.Services.Interfaces;

using Swashbuckle.AspNetCore.SwaggerGen;

var AllowMyOrigins = "_allowMyorigins";
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: AllowMyOrigins,
                    builder =>
                    {
                        builder.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                    });
});

// Add controllers
builder.Services.AddControllers();

// Register your services (make sure DataService implements IDataService)
builder.Services.AddScoped<IDataService, DataService>();

// Add HttpClient
builder.Services.AddHttpClient();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1",
        new OpenApiInfo
        {
            Title = "Restore API",
            Version = "v1"
        });
    c.OperationFilter<FileUploadOperationFilter>();
});

// Add google credential env
var builders = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

var configuration = builders.Build();

var googleCredential = GoogleCredential.FromFile(configuration["Google:CredentialsFile"]);

// Register any other necessary services
builder.Services.AddSingleton(new PayMongoSettings
{
    ApiKey = Environment.GetEnvironmentVariable("PAYMONGO_API_KEY")
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

app.UseCors(AllowMyOrigins);

app.UseAuthorization();

app.MapControllers();

app.Run();

public class FileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var parameters = context.ApiDescription.ActionDescriptor.Parameters;

        if (parameters.Any(p => p.ParameterType == typeof(IFormFile)))
        {
            operation.Parameters.Clear();
            operation.RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, OpenApiSchema>
                            {
                                ["file"] = new OpenApiSchema
                                {
                                    Type = "string",
                                    Format = "binary"
                                },
                                ["username"] = new OpenApiSchema
                                {
                                    Type = "string"
                                }
                            },
                            Required = new HashSet<string> { "file", "username" }
                        }
                    }
                }
            };
        }
    }
}

public class PayMongoSettings
{
    public string ApiKey { get; set; }
}