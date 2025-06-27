using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.IIS;
using MatchingApp.Data;
using MatchingApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure file upload limits for large CSV files (200MB)
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 200_000_000; // 200MB
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 200_000_000; // 200MB
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 200_000_000; // 200MB
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(5);
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? Environment.GetEnvironmentVariable("DATABASE_URL");

if (!string.IsNullOrEmpty(connectionString) && (connectionString.StartsWith("postgres://") || connectionString.StartsWith("postgresql://")))
{
    var uri = new Uri(connectionString);
    var host = uri.Host;
    var port = uri.Port;
    var database = uri.AbsolutePath.Trim('/');
    var username = uri.UserInfo.Split(':')[0];
    var password = uri.UserInfo.Split(':')[1];
    
    connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
}
else if (string.IsNullOrEmpty(connectionString))
{
    connectionString = "Host=localhost;Database=matchingapp;Username=postgres;Password=postgres";
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<MatchingApp.Services.MatchingService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Apply database migrations
try
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("Applying database migrations...");
        
        await context.Database.MigrateAsync();
        
        logger.LogInformation("Database migrations applied successfully");
        
        var canConnect = await context.Database.CanConnectAsync();
        if (canConnect)
        {
            var count = await context.Descriptions.CountAsync();
            logger.LogInformation($"Database ready! Descriptions table has {count} records");
        }
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Database migration failed: {Message}", ex.Message);
    throw; 
}

app.Run();