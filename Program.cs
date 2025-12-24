using System.Reflection;
using System.IO;
using Microsoft.OpenApi;
using UserManagementAPI.Services;
using UserManagementAPI.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));

    // Add bearer auth so Swagger UI shows the Authorize button
    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT Authorization header using the Bearer scheme."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("bearer", document)] = []
    });
});
builder.Services.AddControllers();
builder.Services.AddSingleton<IUserRepository, UserFileRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Enable middleware to serve generated Swagger as JSON endpoint and the Swagger UI
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "UserManagementAPI v1"));
}

// Global error handling (first)
app.UseMiddleware<ErrorHandlingMiddleware>();

// Token auth (next)
app.UseMiddleware<ApiTokenAuthMiddleware>();

// Audit logging (last)
app.UseMiddleware<RequestResponseLoggingMiddleware>();

// Map attribute routed controllers (e.g. Controllers/UsersController.cs)
app.MapControllers();

app.UseHttpsRedirection();

app.Run();
