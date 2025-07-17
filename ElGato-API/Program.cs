using ElGato_API.Data;
using ElGato_API.Data.JWT;
using ElGato_API.Interfaces;
using ElGato_API.Interfaces.Orchesters;
using ElGato_API.Interfaces.Scrapping;
using ElGato_API.Models.User;
using ElGato_API.Services;
using ElGato_API.Services.Orchesters;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using Serilog;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
    loggerConfiguration
        .WriteTo.Console()
        .WriteTo.File("logs/logs-.txt", rollingInterval: RollingInterval.Day));


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

//DB CONN sq
var ConnectionString = builder.Configuration.GetConnectionString("DeafultConnectionString");

// Register the DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(ConnectionString));

// Register the DbContextFactory
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlServer(ConnectionString), ServiceLifetime.Scoped);


//DB CONN MONGO
var connectionString = builder.Configuration.GetConnectionString("MongoDBConnection");
var mongoClient = new MongoClient(connectionString);
var mongoDatabase = mongoClient.GetDatabase("off");

builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton(mongoDatabase);

builder.Services
    .AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ElGato API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "\"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Include XML comments in Swagger
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

//SERVICES
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IDietService, DietService>();
builder.Services.AddScoped<IMongoInits, MongoInits>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserRequestService, UserRequestService>();
builder.Services.AddScoped<IMealService, MealService>();
builder.Services.AddScoped<IAchievmentService, AchievmentService>();
builder.Services.AddScoped<ITrainingService, TrainingService>();
builder.Services.AddScoped<ICardioService, CardioService>();
builder.Services.AddScoped<ICommunityService, CommunityService>();

builder.Services.AddScoped<IScrapService, ScrapService>();
builder.Services.AddScoped<IHelperService, HelperService>();

builder.Services.AddScoped<ITrainingOrchester, TrainingOrchester>();
builder.Services.AddScoped<ICardioOrchester, CardioOrchester>();
builder.Services.AddScoped<IUserDataOrchester, UserDataOrchester>();
builder.Services.AddScoped<IAccountOrchester, AccountOrchester>();
builder.Services.AddScoped<IMealOrchester, MealOrchester>();

//IDENTITY
builder.Services.AddIdentity<AppUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

var roleManager = builder.Services.BuildServiceProvider().GetRequiredService<RoleManager<IdentityRole>>();
var roles = new[] { "admin", "user", "paiduser" };

foreach (var role in roles)
{
    if (!roleManager.RoleExistsAsync(role).Result)
    {
        roleManager.CreateAsync(new IdentityRole(role)).Wait();
    }
}


//JWT 
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var configuration = builder.Configuration;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidAudience = configuration["AuthSettings:Audience"],
        ValidIssuer = configuration["AuthSettings:Issuer"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["AuthSettings:Key"])),
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("admin"));
    options.AddPolicy("User", policy => policy.RequireRole("user"));
    options.AddPolicy("PaidUser", policy => policy.RequireRole("paiduser"));
});


//PASS REQ
builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
});

//builder.Services.AddHttpsRedirection(options =>
//{
//    options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
//    options.HttpsPort = 443;
//});

var app = builder.Build();

app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "Assets/Images/Achievments")),
    RequestPath = "/achievments"
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "Assets/Images/MealImages")),
    RequestPath = "/meal-images"
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "Assets/Images/UserPfp")),
    RequestPath = "/pfp-images"
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "Assets/Images/Exercises")),
    RequestPath = "/exercise-images"
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "Assets/Images/Badges")),
    RequestPath = "/badges"
});

app.UseHttpsRedirection();

app.UseCors("AllowAllOrigins");

app.UseAuthorization();

app.MapControllers();

app.Run();
