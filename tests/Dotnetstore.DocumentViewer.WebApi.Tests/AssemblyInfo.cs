using System.Runtime.Versioning;

// Mirror the WebApi assembly's supported platforms so CA1416 sees calls into WebApi types
// (which carry [SupportedOSPlatform] via the WebApi's [assembly: ...] attributes) as compatible.
[assembly: SupportedOSPlatform("windows")]
[assembly: SupportedOSPlatform("linux")]
[assembly: SupportedOSPlatform("macos")]
