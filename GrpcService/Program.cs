using GrpcService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc(options =>
{
    options.MaxReceiveMessageSize = 1024 * 1024 * 25; // 25 MB
});
builder.Services.AddSingleton<ChatHub>();

//We use JWT token authorization in this example
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(JwtBearerDefaults.AuthenticationScheme, policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireClaim(ClaimTypes.Name);
    });
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateActor = false,
                ValidateLifetime = true,
                IssuerSigningKey = SecurityKey
            };
    });

//Configure Kestrel to prepare it for a big objects. We intent to send the 10MB images
//Performance advice https://learn.microsoft.com/en-us/aspnet/core/grpc/performance?view=aspnetcore-6.0#flow-control
/* 
 * Avoid such a large files (10MB+) to be sent through gRPC, because 10 MB binary payload allocates a 10 MB byte array. 
 * gRPC is a message-based RPC framework, which means:
 * - The entire message is loaded into memory before gRPC can send it.
 * - When the message is received, the entire message is deserialized into memory.
 * Consider this https://learn.microsoft.com/en-us/aspnet/core/grpc/performance?view=aspnetcore-6.0#grpc-services-and-large-binary-payloads
 */
builder.WebHost.ConfigureKestrel(options =>
{
    var http2 = options.Limits.Http2;
    http2.InitialConnectionWindowSize = 1024 * 1024 * 10; // 10 MB
    http2.InitialStreamWindowSize = 1024 * 1024 * 10; // 10 MB
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Configure the HTTP request pipeline.
/*
 * BEWARE of mapping the several services like that it shown below, it could lead to ambiguity.
 * Instead, consider to use the "Aggregator" example  https://github.com/AwesomeYuer/grpc-dotnet-examples?tab=readme-ov-file#aggregator
 */
app.MapGrpcService<OnlinerService>();
app.MapGrpcService<TransferService>();
app.MapGrpcService<ChatService>();
app.MapGrpcService<TestTransferService>();

app.MapGet("/auth", context =>
{
    return context.Response.WriteAsync(GenerateJwtToken(context.Request.Query["name"]));
});

app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();




/// Method generates the JWT token with the users credentials and a secret key
static string GenerateJwtToken(string name)
{
    //We won't provide any complex authentication with passwords comparison, just a name is enough
    if (string.IsNullOrEmpty(name))
    {
        throw new InvalidOperationException("Name is not specified.");
    }

    var claims = new[] { new Claim(ClaimTypes.Name, name) };
    var creds = new SigningCredentials(SecurityKey, SecurityAlgorithms.HmacSha256);
    var token = new JwtSecurityToken("ExampleServer", "ExampleClients", claims, expires: DateTime.Now.AddSeconds(600), signingCredentials: creds);

    return JwtTokenHandler.WriteToken(token);
}

public partial class Program
{
    //Auth
    private static readonly JwtSecurityTokenHandler JwtTokenHandler = new JwtSecurityTokenHandler();
    private static readonly SymmetricSecurityKey SecurityKey = new SymmetricSecurityKey(Guid.NewGuid().ToByteArray());
}