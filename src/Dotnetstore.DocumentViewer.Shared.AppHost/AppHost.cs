var builder = DistributedApplication.CreateBuilder(args);

var webApi = builder.AddProject<Projects.Dotnetstore_DocumentViewer_WebApi>("webApi");

builder.AddProject<Projects.Dotnetstore_DocumentViewer_UI_AvalonUi>("ui")
    .WithReference(webApi)
    .WaitFor(webApi);

builder.Build().Run();