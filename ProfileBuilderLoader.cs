//!CompilerOption:AddRef:System.Diagnostics.FileVersionInfo.dll

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Clio.Utilities;
using ff14bot.AClasses;
using ff14bot.Behavior;
using ff14bot.Helpers;
using ff14bot.Managers;
using ICSharpCode.SharpZipLib.Zip;
using LlamaLibrary;
using LlamaLibrary.Helpers;
using LlamaLibrary.Logging;
using TreeSharp;
using Action = System.Action;

#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
#pragma warning disable SA1309 // Field names should not begin with underscore
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

[assembly: AssemblyVersion("1")]

namespace ProfileBuilderLoader
{
    public class ProfileBuilderLoader : BotPlugin
    {
        private const string ProjectName = "ProfileBuilder";
        private const string CompiledAssemblyName = "ProfileBuilder.dll";

        private static readonly LLogger Log = new("ProfileBuilderLoader", Colors.LightSkyBlue);
        private static readonly string ProjectAssembly = Path.Combine(LoaderFolderName, CompiledAssemblyName);
        private static readonly object Locker = new();
        private static string _name;
        private static string _englishName;
        private static PulseFlags _pulseFlags;
        private static Action? _onButtonPress;
        private static Action? _enable;
        private static Action? _disable;
        private static Action? _initialize;
        private static Action? _shutdown;
        private static bool _wantButton;
        private static bool debugEnabled;
        private static bool _updated;

        private static string? _description;
        private static Version _version = new(1, 0);
        public Assembly? assembly;
        private Func<Composite> _getRoot;

        public ProfileBuilderLoader()
        {
            Log.Information("Starting ProfileBuilder Loader");

            // Log.Debug("Checking for updates...");
            //
            // lock (Locker)
            // {
            //     if (_updated) return;
            //
            //     _updated = true;
            // }
            //
            // Task.Run(async () => { await Update(LoaderFolderName); }).Wait();
            //
            // try
            // {
            //     Unblock(ProjectAssembly);
            // }
            // catch (Exception e)
            // {
            //     Log.Error("Failed to unblock");
            //     Log.Exception(e);
            // }

            Load();
        }

        private static string LoaderFolderName => GeneralFunctions.SourceDirectory().FullName;

        public override string Author => "Mastahg, modified by Sodimm, further modified by TuckMeIntoBread";
        public override string Name => _name;

        public override Version Version => _version;

        public override bool WantButton => _wantButton;

        private static void Clean(string directory)
        {
            foreach (FileInfo file in new DirectoryInfo(directory).GetFiles())
            {
                file.Delete();
            }

            foreach (DirectoryInfo dir in new DirectoryInfo(directory).GetDirectories())
            {
                dir.Delete(true);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteFile(string name);

        private static void Extract(byte[] files, string directory)
        {
            using (var stream = new MemoryStream(files))
            {
                var zip = new FastZip();
                zip.ExtractZip(stream, directory, FastZip.Overwrite.Always, null, null, null, false, true);
            }
        }

        private static FileVersionInfo? GetLocalVersion()
        {
            try
            {
                if (!File.Exists(ProjectAssembly))
                {
                    return null;
                }

                return FileVersionInfo.GetVersionInfo(ProjectAssembly);
            }
            catch (Exception ex)
            {
                Log.Error($"{ex}");
                return null;
            }
        }

        private static Assembly? LoadAssembly(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            Assembly? assembly = null;
            try
            {
                assembly = Assembly.LoadFrom(path);
            }
            catch (Exception e)
            {
                Logging.WriteException(e);
            }

            return assembly;
        }
        //
        // private static async Task<byte[]> TryUpdate()
        // {
        //     try
        //     {
        //         using var client = new HttpClient();
        //         var stopwatch = Stopwatch.StartNew();
        //         FileVersionInfo currentFileVersionInfo = GetLocalVersion();
        //         Version currentVersion;
        //         string currentVersionQueryString;
        //
        //         if (currentFileVersionInfo == null)
        //         {
        //             Log.Warning("ProfileBuilder.dll is not present or is corrupted. Installing a fresh copy.");
        //             currentVersion = new Version("0.0.0.0");
        //             currentVersionQueryString = "0.0.0.0";
        //         }
        //         else
        //         {
        //             currentVersion = new Version(string.Format("{0}.{1}.{2}.{3}", currentFileVersionInfo.FileMajorPart, currentFileVersionInfo.FileMinorPart, currentFileVersionInfo.FileBuildPart, currentFileVersionInfo.FilePrivatePart));
        //             currentVersionQueryString = string.Format("{0}.{1}.{2}.{3}", currentFileVersionInfo.FileMajorPart, currentFileVersionInfo.FileMinorPart, currentFileVersionInfo.FileBuildPart, currentFileVersionInfo.FilePrivatePart);
        //         }
        //
        //         var latestVersionString = await client.GetStringAsync($"{VersionUrl}?ver={currentVersionQueryString}&locale={DataManager.CurrentLanguage}");
        //
        //         Version latestVersion;
        //
        //         try
        //         {
        //             Log.Debug($"Local: {currentFileVersionInfo} | Latest: {latestVersionString}");
        //
        //             latestVersion = new Version(latestVersionString);
        //
        //             if (latestVersion <= currentVersion)
        //             {
        //                 // No need to update
        //                 return null;
        //             }
        //         }
        //         catch (Exception ex)
        //         {
        //             Log.Error($"Unable to verify version: {ex}");
        //             return null;
        //         }
        //
        //         Log.Information($"{ProjectName} local version is {currentVersion}. Updating to {latestVersion}.");
        //
        //         using HttpResponseMessage response = await client.GetAsync($"{DataUrl}/ProfileBuilder_{latestVersion.Major}.{latestVersion.Minor}.{latestVersion.Build}.{latestVersion.MinorRevision}.zip");
        //
        //         if (!response.IsSuccessStatusCode)
        //         {
        //             Log.Warning($"[Error] Could not download {ProjectName}: {response.StatusCode}");
        //             return null;
        //         }
        //
        //         using Stream inputStream = await response.Content.ReadAsStreamAsync();
        //         using var memoryStream = new MemoryStream();
        //         await inputStream.CopyToAsync(memoryStream);
        //
        //         stopwatch.Stop();
        //         Log.Information($"Download took {stopwatch.ElapsedMilliseconds} ms.");
        //
        //         return memoryStream.ToArray();
        //     }
        //     catch (Exception e)
        //     {
        //         Log.Error($"[Error] {e}");
        //         return null;
        //     }
        // }

        private static bool Unblock(string fileName) => DeleteFile(fileName + ":Zone.Identifier");

        // private static async Task Update(string loaderFolderName)
        // {
        //     var data = await TryUpdate();
        //
        //     if (data == null) return;
        //
        //     try
        //     {
        //         Clean(loaderFolderName);
        //     }
        //     catch (Exception e)
        //     {
        //         Log.Exception(e);
        //     }
        //
        //     try
        //     {
        //         Extract(data, loaderFolderName);
        //     }
        //     catch (Exception e)
        //     {
        //         Log.Exception(e);
        //     }
        // }

        public override void OnButtonPress() => _onButtonPress?.Invoke();
        public override void OnDisabled() => _disable?.Invoke();
        public override void OnEnabled() => _enable?.Invoke();
        public override void OnInitialize() => _initialize?.Invoke();
        public override void OnShutdown() => _shutdown?.Invoke();

        private void Load()
        {
            Log.Debug("Starting constructor");

            RedirectAssembly();

            Log.Debug("Redirected assemblies");

            if (!File.Exists(ProjectAssembly))
            {
                Log.Error($"Can't find {ProjectAssembly}");
                return;
            }

            Assembly assembly = LoadAssembly(ProjectAssembly);
            if (assembly == null)
            {
                return;
            }

            Log.Information($"{assembly.GetName().Name} v{assembly.GetName().Version} loaded");
            Type baseType;
            try
            {
                baseType = assembly.DefinedTypes.FirstOrDefault(i => typeof(BotPlugin).IsAssignableFrom(i));
            }
            catch (ReflectionTypeLoadException ex)
            {
                var sb = new StringBuilder();
                foreach (Exception exSub in ex.LoaderExceptions)
                {
                    sb.AppendLine(exSub.Message);
                    var exFileNotFound = exSub as FileNotFoundException;
                    if (exFileNotFound != null && !string.IsNullOrEmpty(exFileNotFound.FusionLog))
                    {
                        sb.AppendLine("Fusion Log:");
                        sb.AppendLine(exFileNotFound.FusionLog);
                    }

                    sb.AppendLine();
                }

                var errorMessage = sb.ToString();
                Log.Error(errorMessage);
                return;

                // Display or log the error based on your application.
            }
            catch (Exception e)
            {
                Log.Error("Other Exception");
                Log.Exception(e);
                return;
            }

            if (baseType == null)
            {
                Log.Error("Base type is null");
                return;
            }

            BotPlugin? compilePlugin;
            try
            {
                compilePlugin = Activator.CreateInstance(baseType) as BotPlugin;
                if (compilePlugin == null)
                {
                    Log.Error("CompilePlugin is null");
                    return;
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                var sb = new StringBuilder();
                foreach (Exception? exSub in ex.LoaderExceptions)
                {
                    sb.AppendLine(exSub.Message);
                    if (exSub is FileNotFoundException exFileNotFound && !string.IsNullOrEmpty(exFileNotFound.FusionLog))
                    {
                        sb.AppendLine("Fusion Log:");
                        sb.AppendLine(exFileNotFound.FusionLog);
                    }

                    sb.AppendLine();
                }

                var errorMessage = sb.ToString();
                Log.Error(errorMessage);
                return;
                //Display or log the error based on your application.
            }
            catch (Exception e)
            {
                Log.Error("Other Exception2");
                Log.Exception(e);
                return;
            }

            _name = compilePlugin.Name;
            _englishName = typeof(BotPlugin).GetProperty("EnglishName")?.GetValue(compilePlugin) as string;
            _enable = compilePlugin.OnEnabled;
            _disable = compilePlugin.OnDisabled;
            _onButtonPress = compilePlugin.OnButtonPress;
            _wantButton = compilePlugin.WantButton;
            _initialize = compilePlugin.OnInitialize;
            _shutdown = compilePlugin.OnShutdown;
            _version = compilePlugin.Version;
            _description = compilePlugin.Description;
        }

        private void RedirectAssembly()
        {
            AssemblyProxy.Init();
            AppDomain.CurrentDomain.AppendPrivatePath(LoaderFolderName);
            AssemblyProxy.AddAssembly("Microsoft.Bcl.AsyncInterfaces", LoadAssembly(Path.Combine(Utilities.AssemblyDirectory, "Microsoft.Bcl.AsyncInterfaces.dll")));
        }
    }
}