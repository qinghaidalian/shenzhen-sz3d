using System.Runtime.Loader;
using Sz3D.Core.Infrastructure.Contracts;

namespace Sz3D.Core.Infrastructure.Plugins;

/// <summary>每个 NodePlugin DLL 使用独立 AssemblyLoadContext 隔离（可热重载）。</summary>
internal sealed class PluginLoadContext(string assemblyPath)
    : AssemblyLoadContext(name: Path.GetFileNameWithoutExtension(assemblyPath), isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(assemblyPath);

    protected override System.Reflection.Assembly? Load(System.Reflection.AssemblyName name)
    {
        var path = _resolver.ResolveAssemblyToPath(name);
        return path is not null ? LoadFromAssemblyPath(path) : null;
    }
}

/// <summary>
/// 插件加载器——扫描 plugins/ 目录，隔离加载每个 INodePlugin DLL。
/// </summary>
public sealed class PluginLoader(string pluginsDirectory)
{
    private readonly Dictionary<string, PluginLoadContext> _contexts = [];
    private readonly List<INodePlugin>                     _plugins  = [];

    public IReadOnlyList<INodePlugin> LoadedPlugins => _plugins;

    public event EventHandler<INodePlugin>? PluginLoaded;
    public event EventHandler<(string Path, Exception Ex)>? PluginLoadFailed;

    public async Task ScanAndLoadAsync()
    {
        if (!Directory.Exists(pluginsDirectory)) return;

        var dlls = Directory.EnumerateFiles(pluginsDirectory, "*.dll");
        foreach (var dll in dlls)
            await TryLoadAsync(dll);
    }

    private Task TryLoadAsync(string dllPath)
    {
        try
        {
            var ctx      = new PluginLoadContext(dllPath);
            var assembly = ctx.LoadFromAssemblyPath(dllPath);

            foreach (var type in assembly.GetExportedTypes()
                         .Where(t => t.IsClass && !t.IsAbstract && typeof(INodePlugin).IsAssignableFrom(t)))
            {
                var plugin = (INodePlugin)Activator.CreateInstance(type)!;
                _contexts[plugin.PluginId] = ctx;
                _plugins.Add(plugin);
                PluginLoaded?.Invoke(this, plugin);
            }
        }
        catch (Exception ex)
        {
            PluginLoadFailed?.Invoke(this, (dllPath, ex));
        }
        return Task.CompletedTask;
    }

    /// <summary>热重载：重新扫描插件目录，新增插件追加注册。</summary>
    public Task RescanAsync() => ScanAndLoadAsync();
}
