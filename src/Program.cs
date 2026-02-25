using Azure.AI.ContentSafety;
using Azure.Identity;
using ZavaStorefront.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Register application services
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ProductService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddSingleton<Azure.Core.TokenCredential>(new DefaultAzureCredential());
builder.Services.AddSingleton<ContentSafetyClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var endpoint = config["AiFoundry:Endpoint"]
        ?? throw new InvalidOperationException("AiFoundry:Endpoint is not configured.");
    var credential = sp.GetRequiredService<Azure.Core.TokenCredential>();
    return new ContentSafetyClient(new Uri(endpoint), credential);
});
builder.Services.AddHttpClient<ChatService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
