using System.Globalization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MudBlazor.Services;
using PicoERP.Infrastructure;
using Serilog;
using System.Text;

// Set Persian (fa-IR) as the default application culture so that
// MudBlazor date pickers display Shamsi dates, month names, and
// right-to-left numerals consistently everywhere.
var persianCulture = new CultureInfo("fa-IR");
CultureInfo.DefaultThreadCurrentCulture   = persianCulture;
CultureInfo.DefaultThreadCurrentUICulture = persianCulture;

var builder = WebApplication.CreateBuilder(args);

// Resolve writable paths relative to the app root so the app works correctly
// under IIS/Plesk where the current working directory is not the app folder.
var appRoot  = builder.Environment.ContentRootPath;
var dataDir  = Path.Combine(appRoot, "Data");
var logsDir  = Path.Combine(appRoot, "logs");
Directory.CreateDirectory(dataDir);
Directory.CreateDirectory(logsDir);

// Override the SQLite connection string to use the absolute path so EF Core
// never accidentally creates the .db file in System32 or the IIS temp folder.
builder.Configuration["ConnectionStrings:DefaultConnection"] =
    $"Data Source={Path.Combine(dataDir, "picoerp.db")}";

// Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(logsDir, "picoerp-.log"), rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Host.UseSerilog();

// Razor + Blazor Server + MVC (for webhook controller)
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = MudBlazor.Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.NewestOnTop = true;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 3500;
    config.SnackbarConfiguration.HideTransitionDuration = 300;
    config.SnackbarConfiguration.ShowTransitionDuration = 200;
});

// Infrastructure & DB
builder.Services.AddInfrastructure(builder.Configuration);

// JWT Auth
var jwtKey = builder.Configuration["Jwt:Key"] ?? "PicoERP-Super-Secret-Key-2024!!";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

// HTTP Context for user info in Blazor
builder.Services.AddHttpContextAccessor();

// App State Service (Blazor-scoped)
builder.Services.AddScoped<PicoERP.Web.Services.AppStateService>();
builder.Services.AddScoped<PicoERP.Web.Services.NotificationService>();

// HubSpot webhook queue — singleton so the controller and Blazor circuits share it
builder.Services.AddSingleton<PicoERP.Web.Services.PendingDealQueue>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();          // webhook endpoint
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// Initialize database and migrations
await app.Services.InitializeDatabaseAsync();

app.Run();
