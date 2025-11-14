using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.FileProviders;
using Npgsql;


var builder = WebApplication.CreateBuilder(args);

var conn = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<AppDb>(o =>
  o.UseNpgsql(conn)
   .UseSnakeCaseNamingConvention());

// Storage service wiring
//var storageMode = builder.Configuration["Storage:Mode"] ?? "Local";
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

      o.Events.OnRedirectToLogin = ctx =>
      {
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
          ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
          return Task.CompletedTask;
        }
        ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
      };

      o.Events.OnRedirectToAccessDenied = ctx =>
      {
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
          ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
          return Task.CompletedTask;
        }
        ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
      };
    });

builder.Services.AddAuthorization();
// builder.Services.AddCors(options =>
// {
//     options.AddPolicy(name: "AllowStaticWebApp",
//                       policy =>
//                       {
//                           policy.WithOrigins("https://red-wave-0d7e0a21e.3.azurestaticapps.net").AllowAnyHeader().AllowAnyMethod();
//                       });
// });
builder.Services.AddCors(o => o.AddPolicy("AllowStaticWebApp", p => p
  .WithOrigins(builder.Configuration["Cors:Origin"]!)
  .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();
// builder.Services.AddSwaggerGen(c =>
// {
//     c.SwaggerDoc("v1", new OpenApiInfo
//     {
//         Title = "My API",
//         Version = "v1"
//     });
// });
builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddSingleton<IPdfFillService, PdfFillService>();
builder.Services.AddSingleton<ISignatureService, SignatureService>();
builder.Services.AddSingleton<IPdfFieldDiscovery, PdfFieldDiscovery>();
// builder.Services.AddApplicationInsightsTelemetry();
// builder.Logging.AddApplicationInsights();

var app = builder.Build();

var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
log.LogInformation("Starting IDSForms application");
log.LogInformation("Using DB host {Host}", new NpgsqlConnectionStringBuilder(conn).Host);

// var spaDistPath = Path.GetFullPath(
//     Path.Combine(builder.Environment.ContentRootPath, "..", "ui", "dist"));

// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();                         // 👈 enable JSON generation
//     app.UseSwaggerUI(c =>                     // 👈 enable /swagger UI
//     {
//         c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
//         // Optional: serve Swagger UI at root
//         // c.RoutePrefix = string.Empty;
//     });
//}

// Log which storage mode/implementation is active for clarity
// try
// {
//     var storageSvc = app.Services.GetRequiredService<IStorageService>();
//     var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
//     log.LogInformation("Storage mode: {Mode}; Implementation: {Impl}", storageMode, storageSvc.GetType().FullName);
// }
// catch { }

var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("CorsDebug");

// app.Use(async (ctx, next) =>
// {
//     if (ctx.Request.Headers.TryGetValue("Origin", out var origin))
//     {
//         logger.LogInformation("Request origin: {Origin}", origin.ToString());
//     }
//     else
//     {
//         logger.LogInformation("Request without Origin header: {Path}", ctx.Request.Path);
//     }

//     await next();
// });

app.UseRouting();
app.UseCors("AllowStaticWebApp");
app.UseAuthentication();
app.UseAuthorization();

// if (Directory.Exists(spaDistPath))
// {
//     var spaFileProvider = new PhysicalFileProvider(spaDistPath);
//     app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = spaFileProvider });
//     app.UseStaticFiles(new StaticFileOptions { FileProvider = spaFileProvider });
//}

app.MapControllers();

// if (Directory.Exists(spaDistPath))
// {
//     app.MapFallback(async ctx =>
//     {
//         ctx.Response.ContentType = "text/html";
//         await ctx.Response.SendFileAsync(Path.Combine(spaDistPath, "index.html"));
//     });
// }

// app.UseHttpsRedirection();

app.Run();
