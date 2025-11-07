using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDb>(o =>
  o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddAuthentication("cookie")
  .AddCookie("cookie", o => { o.LoginPath = "/api/auth/login"; o.Cookie.Name = "pdfapp"; });
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

app.UseCors("ui");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.UseHttpsRedirection();

app.Run();
