using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using TripExpenseApi.Config;
using TripExpenseApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(option =>
{
    //   "DefaultSQLConnection": "Server=tcp:notesdb-rizoooo.database.windows.net,1433;Initial Catalog=zosdb;Persist Security Info=False;User ID=drizojose0396;Password=!Access123;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
    option.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultSQLConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null
            );
        }
    );
});

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ⭐ NEW: Add AuthService
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IInviteCodeService, InviteCodeService>();

// ⭐ NEW: Add JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ExpenseSplitter";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ExpenseSplitterUsers";

builder
    .Services.AddAuthentication(item =>
    {
        item.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        item.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(item =>
    {
        item.RequireHttpsMetadata = false;
        item.SaveToken = true;
        item.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
        };
    });

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        }
    );
});
builder.Services.AddAutoMapper(typeof(MappingConfig));
builder.Services.AddHttpContextAccessor();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseCors("AllowAll");

app.MapControllers();

app.Run();
