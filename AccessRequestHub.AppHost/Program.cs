using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Secret diberikan ke kedua aplikasi melalui environment, bukan ditulis dalam source.
var clientSecret = builder.AddParameter("oidc-client-secret", secret: true);

// Path relatif dihitung dari folder AppHost, bukan working directory masing-masing aplikasi.
string DatabaseConnection(string configurationKey)
{
    var configuredPath = builder.Configuration[configurationKey];
    if (string.IsNullOrWhiteSpace(configuredPath))
        throw new InvalidOperationException($"{configurationKey} wajib diisi.");

    var fullPath = Path.GetFullPath(configuredPath, builder.AppHostDirectory);
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
    return $"Data Source=\"{fullPath.Replace("\"", "\"\"")}\"";
}

var hubConnection = DatabaseConnection("DatabasePaths:Hub");
var identityConnection = DatabaseConnection("DatabasePaths:Identity");

var identity = builder.AddProject<Projects.IdentityServer>("identityserver", launchProfileName: null)
    .WithHttpsEndpoint(port: 7043, targetPort: 7043, name: "https", isProxied: false)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
    .WithEnvironment("ConnectionStrings__Identity", identityConnection)
    .WithEnvironment("ConnectionStrings__Hub", hubConnection)
    .WithEnvironment("Oidc__ClientSecret", clientSecret)
    .WithHttpHealthCheck("/.well-known/openid-configuration", endpointName: "https");

builder.AddProject<Projects.Backend>("backend", launchProfileName: null)
    // Port ini sesuai RedirectUris yang sudah ada di Identity Server Anda.
    .WithHttpsEndpoint(port: 7019, targetPort: 7019, name: "https", isProxied: false)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
    .WithEnvironment("ConnectionStrings__Default", hubConnection)
    .WithEnvironment("Oidc__ClientSecret", clientSecret)
    .WithEnvironment("Oidc__ClientId", "accessrequesthub-web")
    .WithEnvironment("Oidc__Authority", identity.GetEndpoint("https"))
    .WithEnvironment("Aspire__IdentityAuthority", identity.GetEndpoint("https"))
    .WaitFor(identity);

builder.Build().Run();
