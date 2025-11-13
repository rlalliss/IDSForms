using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.FileProviders;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDb>(o =>
  o.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
   .UseSnakeCaseNamingConvention());

// Storage service wiring
var storageMode = builder.Configuration["Storage:Mode"] ?? "Local";
builder.Services.AddSingleton<IStorageService, LocalStorageService>();

builder.Services.AddAuthentication("cookie")
  .AddCookie("cookie", o =>
  {
      o.LoginPath = "/api/auth/login";
      o.Cookie.Name = "pdfapp";
      var isDev = builder.Environment.IsDevelopment();
      // In development, we run the UI via Vite on http://localhost:5173 with a dev proxy to /api.
      // Use Lax and non-secure so the cookie can be set over HTTP same-origin in dev.
      // In production, require cross-site cookie with Secure.
      o.Cookie.SameSite = isDev ? SameSiteMode.Lax : SameSiteMode.None;
      o.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
  });
builder.Services.AddAuthorization();
builder.Services.AddCors(o => o.AddPolicy("ui", p => p
  .WithOrigins(builder.Configuration["Cors:Origin"]!)
  .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "My API",
        Version = "v1"
    });
});
builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddSingleton<IPdfFillService, PdfFillService>();
builder.Services.AddSingleton<ISignatureService, SignatureService>();
builder.Services.AddSingleton<IPdfFieldDiscovery, PdfFieldDiscovery>();


var app = builder.Build();

var spaDistPath = Path.GetFullPath(
    Path.Combine(builder.Environment.ContentRootPath, "..", "ui", "dist"));

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();                         // 👈 enable JSON generation
    app.UseSwaggerUI(c =>                     // 👈 enable /swagger UI
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
        // Optional: serve Swagger UI at root
        // c.RoutePrefix = string.Empty;
    });
}

// Log which storage mode/implementation is active for clarity
try
{
    var storageSvc = app.Services.GetRequiredService<IStorageService>();
    var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    log.LogInformation("Storage mode: {Mode}; Implementation: {Impl}", storageMode, storageSvc.GetType().FullName);
}
catch { }

app.UseCors("ui");
app.UseAuthentication();
app.UseAuthorization();

if (Directory.Exists(spaDistPath))
{
    var spaFileProvider = new PhysicalFileProvider(spaDistPath);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = spaFileProvider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = spaFileProvider });
}

app.MapControllers();

if (Directory.Exists(spaDistPath))
{
    app.MapFallback(async ctx =>
    {
        ctx.Response.ContentType = "text/html";
        await ctx.Response.SendFileAsync(Path.Combine(spaDistPath, "index.html"));
    });
}

app.UseHttpsRedirection();

app.Run();
