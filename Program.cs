using CivicService.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddOpenApi();

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

// only show openapi in dev mode
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
