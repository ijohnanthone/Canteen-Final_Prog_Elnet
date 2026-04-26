using CANTEEN_SYSTEM.Data;
using CANTEEN_SYSTEM.Services.Sync;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// SQLite is the primary app database so the MVC app keeps working locally
// even when the cloud database is unreachable.
var localConnectionString = builder.Configuration.GetConnectionString("LocalSqlite")
    ?? "Data Source=canteen.db";
var azureConnectionString = builder.Configuration.GetConnectionString("AzureSql");
builder.Services.Configure<CloudSyncOptions>(options =>
{
    options.AzureSqlConnectionString = azureConnectionString;
    options.IntervalSeconds = builder.Configuration.GetValue<int?>($"{CloudSyncOptions.SectionName}:IntervalSeconds") ?? 15;
});

builder.Services.AddDbContext<CanteenDbContext>(options =>
{
    options.UseSqlite(localConnectionString);
});
builder.Services.AddScoped<SyncQueueService>();
builder.Services.AddScoped<CloudSyncService>();
builder.Services.AddHostedService<CloudSyncWorker>();

var app = builder.Build();

// Ensure the chosen database exists and has starter records.
await DbInitializer.InitializeAsync(app.Services);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
