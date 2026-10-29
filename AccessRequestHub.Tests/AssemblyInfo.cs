using System.Runtime.CompilerServices;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

// Satu HubAppFactory per test membuka FileSystemWatcher konfigurasi (reloadOnChange);
// tanpa ini, mesin dengan inotify.max_user_instances kecil (mis. 128) gagal spurious.
internal static class PollingWatcherInit
{
    [ModuleInitializer]
    internal static void Init()
        => Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1");
}
