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
                    policy =>
                    {
                        policy.WithOrigins("https://670c971af5598e0009c5bd18--restore-test.netlify.app/")
                                            .AllowAnyHeader()
                                            .AllowAnyMethod();
                    });
});

builder.Services.AddControllers();
builder.Services.AddScoped<IDataService, DataService>();

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

// Register HttpClient
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
//app.UseHttpsRedirection(); //comment out for CORS preflight error

app.UseRouting(); // added UseRouting

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