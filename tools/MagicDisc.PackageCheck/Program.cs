using System.Reflection;
using System.Runtime.Loader;

// Inspect the actual published application without starting its UI or registering anything on this Mac.
var application = Path.GetFullPath(args.Single());
var directory = Path.GetDirectoryName(application)!;
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    var dependency = Path.Combine(directory, name.Name + ".dll");
    return File.Exists(dependency) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(dependency) : null;
};
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(application);
var integrationEnabled = (bool)assembly.GetType("Filee.App.Services.UpdateService", throwOnError: true)!
    .GetProperty("IsReleaseBuild", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
if (!integrationEnabled)
    throw new InvalidOperationException("The package disables system integration. Publish with -p:MagicDiscRelease=true.");
Console.WriteLine("PASS: packaged MagicDisc enables login startup integration.");
