using System.Text;
using DataHub.Api.Auth;
using DataHub.Core.Constants;
using DataHub.Core.Interfaces;
using DataHub.Infrastructure;
using DataHub.Infrastructure.Data;
using DataHub.Infrastructure.Seeding;
using DataHub.Infrastructure.Services;
using DataHub.Core.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ----- Services -----
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi(opts =>
{
    opts.AddDocumentTransformer((doc, ctx, ct) =>
    {
        doc.Components ??= new();
        doc.Components.SecuritySchemes ??= new Dictionary<string, Microsoft.OpenApi.IOpenApiSecurityScheme>();
        doc.Components.SecuritySchemes["Bearer"] = new Microsoft.OpenApi.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.ParameterLocation.Header,
            Description = "Paste the access token from POST /api/auth/login (do NOT include the word 'Bearer').",
        };
        doc.Security ??= new List<Microsoft.OpenApi.OpenApiSecurityRequirement>();
        doc.Security.Add(new Microsoft.OpenApi.OpenApiSecurityRequirement
        {
            [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", doc)] = new List<string>(),
        });
        return Task.CompletedTask;
    });
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

builder.Services.AddDataHubInfrastructure(builder.Configuration);

// JWT auth
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt section is missing.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization(options =>
{
    foreach (var perm in Permissions.All)
    {
        options.AddPolicy(perm, p => p.RequireClaim("permission", perm));
    }
});

// CORS for the React dev server
const string DevCorsPolicy = "DevCors";
builder.Services.AddCors(opt =>
{
    opt.AddPolicy(DevCorsPolicy, p => p
        .WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

// ----- Migrate + seed on startup -----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DataHubDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db, hasher);
}

// ----- Pipeline -----
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(opts =>
    {
        opts.Title = "DataHub API";
        opts.WithTheme(ScalarTheme.Mars);
    });
    app.UseCors(DevCorsPolicy);
}
else
{
    // Only force HTTPS outside Development. In dev the API may be running on
    // the http-only launch profile, and forcing redirects breaks the Vite proxy.
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

// Serve static geographic boundary files under /geo/* (countries, US states,
// US counties). These are versioned, immutable assets; long-cache them and
// register the geo+json content type for .geojson files.
{
    var geoRoot = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "geo");
    Directory.CreateDirectory(geoRoot);
    var contentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
    contentTypes.Mappings[".geojson"] = "application/geo+json";
    app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(geoRoot),
        RequestPath = "/geo",
        ContentTypeProvider = contentTypes,
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        }
    });
}

app.MapControllers();

app.Run();
