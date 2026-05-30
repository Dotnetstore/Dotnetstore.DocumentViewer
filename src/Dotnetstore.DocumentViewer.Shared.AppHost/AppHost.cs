var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var documentViewerDb = postgres.AddDatabase("documentviewer");

// Gotenberg wraps LibreOffice (and Chromium) behind a stable HTTP API. WebApi resolves it
// via Aspire service discovery (http://gotenberg) and posts DOCX to /forms/libreoffice/convert.
var gotenberg = builder.AddContainer("gotenberg", "gotenberg/gotenberg", "8")
    .WithHttpEndpoint(targetPort: 3000, name: "http");

// The same API key is used by the WebApi (to validate inbound requests) and the Avalonia
// client (to attach it on outbound requests). One parameter, two consumers.
var apiKey = builder.AddParameter("api-key", secret: true);

var webApi = builder.AddProject<Projects.Dotnetstore_DocumentViewer_WebApi>("webApi")
    .WithReference(documentViewerDb)
    .WaitFor(documentViewerDb)
    .WithReference(gotenberg.GetEndpoint("http"))
    .WithEnvironment("ApiKey__Value", apiKey);

builder.AddProject<Projects.Dotnetstore_DocumentViewer_UI_AvalonUi>("ui")
    .WithReference(webApi)
    .WaitFor(webApi)
    .WithEnvironment("DocumentViewerApi__BaseAddress", webApi.GetEndpoint("https"))
    .WithEnvironment("DocumentViewerApi__ApiKey", apiKey);

builder.Build().Run();
