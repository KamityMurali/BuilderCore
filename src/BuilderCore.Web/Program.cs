using BuilderCore.Web.Components;
using BuilderCore.Web.Data;
using BuilderCore.Web.Data.Repositories;
using BuilderCore.Web.Hubs;
using BuilderCore.Web.Security;
using BuilderCore.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddBuilderAuthentication(builder.Configuration, builder.Environment);

builder.Services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddScoped<DemoDataSeeder>();
builder.Services.AddScoped<CostCodeRepository>();
builder.Services.AddScoped<VendorRepository>();
builder.Services.AddScoped<EstimateRepository>();
builder.Services.AddScoped<JobRepository>();
builder.Services.AddScoped<PurchaseOrderRepository>();
builder.Services.AddScoped<JobCostRepository>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<ICostCodeService, CostCodeService>();
builder.Services.AddScoped<IVendorService, VendorService>();
builder.Services.AddScoped<IEstimateService, EstimateService>();
builder.Services.AddScoped<IJobService, JobService>();
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
builder.Services.AddScoped<IJobCostService, JobCostService>();
builder.Services.AddSignalR();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();

    if (app.Configuration.GetValue<bool>("DemoData:Enabled"))
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
        await seeder.SeedAsync();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

var authMode = app.Configuration["Authentication:Mode"] ?? "Development";
if (string.Equals(authMode, "Cognito", StringComparison.OrdinalIgnoreCase))
{
    app.MapGet("/login", () => Results.Challenge(new Microsoft.AspNetCore.Authentication.AuthenticationProperties
    {
        RedirectUri = "/"
    }, [OpenIdConnectDefaults.AuthenticationScheme]));

    app.MapGet("/logout", () => Results.SignOut(new Microsoft.AspNetCore.Authentication.AuthenticationProperties
    {
        RedirectUri = "/"
    }, [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]));
}

app.MapStaticAssets();
app.MapHub<JobCostHub>("/hubs/jobcost");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
