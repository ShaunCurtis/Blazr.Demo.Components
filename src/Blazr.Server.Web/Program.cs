global using Blazr.Components.ComponentScopedServices;
using Blazr.Server.Web.Data;
using Blazr.Server.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<WeatherForecastService>();
builder.Services.AddScoped<ScopedAService>();
builder.Services.AddScoped<ScopedBService>();
builder.Services.AddScoped<IScopedService, ScopedAService>();
builder.Services.AddTransient<TransientAService>();
builder.Services.AddScoped<IComponentServiceProvider, ComponentServiceProvider>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
