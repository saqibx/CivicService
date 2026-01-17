using System.Text.Json.Serialization;
using CivicService.Data;
using CivicService.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();

// register services
builder.Services.AddScoped<IServiceRequestService, ServiceRequestService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// get the database provider from config
var dbProvider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";
var connectionString = builder.Configuration.GetConnectionString(dbProvider);

// setup database
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if(dbProvider == "Sqlite")
    {
        options.UseSqlite(connectionString);
    }
    // TODO: add postgres later if needed
    else
    {
        options.UseSqlite(connectionString);
    }
});


var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
