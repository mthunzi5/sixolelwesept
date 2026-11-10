using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
    // Every form post must carry an antiforgery token.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

// MVC session: keeps the logged-in user's ID, name and role, plus the API's own session cookie,
// for as long as the user is signed in to the website.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RaceDay.Web.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

builder.Services.AddHttpContextAccessor();

// Typed HttpClient for the RaceDay API. Cookies are switched off on the shared handler so one
// user's API session can never leak to another; RaceDayApiClient forwards each user's own API
// session cookie from their MVC session instead.
builder.Services.AddHttpClient<RaceDayApiClient>(client =>
    {
        var baseUrl = builder.Configuration["RaceDayApi:BaseUrl"] ?? "http://localhost:5272";
        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });

var app = builder.Build();

// Friendly error pages everywhere: users never see a stack trace or raw status code.
app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}");
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Forms and number inputs always use "." for decimals, whatever the host machine's region settings are.
var culture = new CultureInfo("en-US");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(culture),
    SupportedCultures = [culture],
    SupportedUICultures = [culture]
});

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
