using System.Security.Cryptography.X509Certificates;
using AirlineSeatReservationSystem.Data.Concrete.Efcore;
using AirlineSeatReservationSystem.Data.Abstract;
using AirlineSeatReservationSystem.Data.Concrete;
using AirlineSeatReservationSystem.Entity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using AirlineSeatReservationSystem.Services;
using System.Reflection;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;


var builder = WebApplication.CreateBuilder(args);
var configuredPort = builder.Configuration.GetValue<int?>("App:Port");
var port = configuredPort.HasValue && configuredPort.Value > 0 ? configuredPort.Value : 5000;
var configuredBasePath = builder.Configuration["App:BasePath"];
var basePath = string.IsNullOrWhiteSpace(configuredBasePath)
    ? "/airline"
    : $"/{configuredBasePath.Trim().Trim('/')}";

if (OperatingSystem.IsWindows())
{
    builder.Host.UseWindowsService();
}

builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

#region Localizer
builder.Services.AddSingleton<LanguageService>();
builder.Services.AddLocalization(options => options.ResourcesPath= "Resources");
builder.Services.AddMvc().AddViewLocalization().AddDataAnnotationsLocalization(options => options.DataAnnotationLocalizerProvider = (type,factory) =>
{
    var assemblyName= new AssemblyName(typeof(SharedResource).GetTypeInfo().Assembly.FullName ?? "AirlineSeatReservationSystem");
    return factory.Create(nameof(SharedResource), assemblyName.Name ?? "AirlineSeatReservationSystem");

});

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportCultures= new List<CultureInfo> {
        new CultureInfo("en-US"),
        new CultureInfo("tr-TR")
    };
    options.DefaultRequestCulture = new RequestCulture(culture:"en-US", uiCulture: "en-US");
    options.SupportedCultures= supportCultures;
    options.SupportedUICultures= supportCultures;
    options.RequestCultureProviders.Insert(0, new QueryStringRequestCultureProvider());
});
#endregion

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddOptions<AdminSettings>()
    .Bind(builder.Configuration.GetSection(AdminSettings.ConfigurationSectionName))
    .ValidateDataAnnotations()
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.Email) && !string.IsNullOrWhiteSpace(settings.Password),
        "Admin:Email and Admin:Password are required.")
    .ValidateOnStart();

builder.Services.AddDbContext<DataContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("database"));
});

builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<ISeatRepository, EfSeatRepository>();
builder.Services.AddScoped<IBookingRepository, EfBookingRepository>();
builder.Services.AddScoped<IFlightRepository, EfFlightRepository>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/Users/SignIn";
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        ValidateIssuer = false,
        ValidateAudience = false,
    };
});

var app = builder.Build();
var adminSettings = app.Services.GetRequiredService<IOptions<AdminSettings>>().Value;

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();
    var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    var adminPasswordHash = userRepository.HashPassword(adminSettings.Password);
    var currentAdminUser = await dbContext.Users.FirstOrDefaultAsync(user => user.Email == adminSettings.NormalizedEmail);
    var seededAdminUsers = await dbContext.Users
        .Where(user => user.Phone == AdminSettings.SeedPhone)
        .OrderBy(user => user.UserNo)
        .ToListAsync();
    var adminUser = currentAdminUser ?? seededAdminUsers.FirstOrDefault();

    foreach (var duplicateSeededAdmin in seededAdminUsers)
    {
        if (adminUser == null || duplicateSeededAdmin.UserNo != adminUser.UserNo)
        {
            dbContext.Users.Remove(duplicateSeededAdmin);
        }
    }

    if (adminUser == null)
    {
        dbContext.Users.Add(new User
        {
            UserName = adminSettings.NormalizedEmail,
            Email = adminSettings.NormalizedEmail,
            Phone = AdminSettings.SeedPhone,
            Password = adminPasswordHash
        });
    }
    else
    {
        adminUser.UserName = adminSettings.NormalizedEmail;
        adminUser.Email = adminSettings.NormalizedEmail;
        adminUser.Phone = AdminSettings.SeedPhone;
        adminUser.Password = adminPasswordHash;
    }

    await dbContext.SaveChangesAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/")
    {
        context.Response.Redirect(basePath);
        return;
    }

    await next();
});

app.UsePathBase(basePath);
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization(app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value);

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Flight}/{action=Index}/{id?}");

app.Run();
