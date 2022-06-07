var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.MapRazorPages();
app.MapControllers();

ApplyHotReloadWorkaround(app);

app.Map("/webassembly", app =>
{
    app.UseBlazorFrameworkFiles();
    app.UseStaticFiles();
    app.UseEndpoints(endpoints => endpoints.MapFallbackToFile("index.html"));
});

app.Run();

// This is a temporary workaround needed until the next patch release of ASP.NET Core
// The underlying bug was fixed in https://github.com/dotnet/sdk/pull/25534 but that update hasn't shipped yet
void ApplyHotReloadWorkaround(WebApplication app)
{
    app.Use((ctx, next) =>
    {
        if (ctx.Request.Path == "/webassembly/_framework/blazor-hotreload")
        {
            ctx.Response.Redirect("/_framework/blazor-hotreload");
            return Task.CompletedTask;
        }

        return next(ctx);
    });
}
