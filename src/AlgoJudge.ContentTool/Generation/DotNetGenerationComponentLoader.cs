using System.Reflection;
using System.Runtime.Loader;

namespace AlgoJudge.ContentTool.Generation;

public sealed class DotNetGenerationComponentLoader
{
    public T Load<T>(string problemDirectory, DotNetComponentManifest component)
        where T : class
    {
        var root = GeneratorManifestReader.ResolveProblemDirectory(problemDirectory);
        var assemblyPath = GeneratorManifestReader.ResolveContainedFile(
            root,
            component.Assembly,
            "Generator component assembly");

        Assembly assembly;
        try
        {
            assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        }
        catch (Exception exception) when (exception is BadImageFormatException or FileLoadException)
        {
            throw new TestGenerationException(
                $"Generator component assembly could not be loaded: {component.Assembly}.");
        }

        var type = assembly.GetType(component.Entry, throwOnError: false, ignoreCase: false);
        if (type is null || type.IsAbstract || !typeof(T).IsAssignableFrom(type))
        {
            throw new TestGenerationException(
                $"Generator entry {component.Entry} must implement {typeof(T).FullName}.");
        }

        try
        {
            return (T)(Activator.CreateInstance(type) ??
                throw new TestGenerationException(
                    $"Generator entry {component.Entry} could not be created."));
        }
        catch (TargetInvocationException)
        {
            throw new TestGenerationException(
                $"Generator entry {component.Entry} constructor failed.");
        }
        catch (MissingMethodException)
        {
            throw new TestGenerationException(
                $"Generator entry {component.Entry} requires a public parameterless constructor.");
        }
    }
}
