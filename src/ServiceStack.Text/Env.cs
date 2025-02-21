//Copyright (c) ServiceStack, Inc. All Rights Reserved.
//License: https://raw.github.com/ServiceStack/ServiceStack/master/license.txt

using System;
using System.Globalization;
using System.IO;

namespace ServiceStack.Text
{
    public static class Env
    {
        static Env()
        {
            if (PclExport.Instance == null)
                throw new ArgumentException("PclExport.Instance needs to be initialized");

#if NETSTANDARD2_0 || NETCORE2_1
            IsNetStandard = true;
            try
            {
                IsLinux = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
                IsWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
                IsOSX  = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX);
                IsNetCore3 = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.StartsWith(".NET Core 3");
                
                var fxDesc = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
                IsMono = fxDesc.Contains("Mono");
                IsNetCore = fxDesc.StartsWith(".NET Core", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception) {} //throws PlatformNotSupportedException in AWS lambda
            IsUnix = IsOSX || IsLinux;
            HasMultiplePlatformTargets = true;
            IsUWP = IsRunningAsUwp();
#elif NET45
            IsNetFramework = true;
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.WinCE:
                    IsWindows = true;
                break;
            }
            
            var platform = (int)Environment.OSVersion.Platform;
            IsUnix = platform == 4 || platform == 6 || platform == 128;

            if (File.Exists(@"/System/Library/CoreServices/SystemVersion.plist"))
                IsOSX = true;
            var osType = File.Exists(@"/proc/sys/kernel/ostype") 
                ? File.ReadAllText(@"/proc/sys/kernel/ostype") 
                : null;
            IsLinux = osType?.IndexOf("Linux", StringComparison.OrdinalIgnoreCase) >= 0;
            try
            {
                IsMono = AssemblyUtils.FindType("Mono.Runtime") != null;
            }
            catch (Exception) {}

            SupportsDynamic = true;
#endif

#if NETCORE2_1
            IsNetStandard = false;
            IsNetCore = true;
            SupportsDynamic = true;
#endif

            if (!IsUWP)
            {
                try
                {
                    IsAndroid = AssemblyUtils.FindType("Android.Manifest") != null;
                    if (IsOSX && IsMono)
                    {
                        var runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
                        //iOS detection no longer trustworthy so assuming iOS based on some current heuristics. TODO: improve iOS detection
                        IsIOS = runtimeDir.StartsWith("/private/var") ||
                                runtimeDir.Contains("/CoreSimulator/Devices/"); 
                    }
                }
                catch (Exception) {}
            }
            
            SupportsExpressions = true;
            SupportsEmit = !(IsUWP || IsIOS);

            if (!SupportsEmit)
            {
                ReflectionOptimizer.Instance = ExpressionReflectionOptimizer.Provider;
            }

            ServerUserAgent = "ServiceStack/" +
                ServiceStackVersion + " "
                + PclExport.Instance.PlatformName
                + (IsMono ? "/Mono" : "")
                + (IsLinux ? "/Linux" : IsOSX ? "/OSX" : IsUnix ? "/Unix" : IsWindows ? "/Windows" : "/UnknownOS")
                + (IsIOS ? "/iOS" : IsAndroid ? "/Android" : IsUWP ? "/UWP" : "");

            VersionString = ServiceStackVersion.ToString(CultureInfo.InvariantCulture);

            __releaseDate = new DateTime(2018,12,16);
        }

        public static string VersionString { get; set; }

        public static decimal ServiceStackVersion = 5.7m;

        public static bool IsLinux { get; set; }

        public static bool IsOSX { get; set; }

        public static bool IsUnix { get; set; }

        public static bool IsWindows { get; set; }

        public static bool IsMono { get; set; }

        public static bool IsIOS { get; set; }

        public static bool IsAndroid { get; set; }

        public static bool IsNetNative { get; set; }

        public static bool IsUWP { get; private set; }

        public static bool IsNetStandard { get; set; }

        public static bool IsNetFramework { get; set; }

        public static bool IsNetCore { get; set; }
        
        public static bool IsNetCore3 { get; set; }

        public static bool SupportsExpressions { get; private set; }

        public static bool SupportsEmit { get; private set; }

        public static bool SupportsDynamic { get; private set; }

        private static bool strictMode;
        public static bool StrictMode
        {
            get => strictMode;
            set => Config.Instance.ThrowOnError = strictMode = value;
        }

        public static string ServerUserAgent { get; set; }

        public static bool HasMultiplePlatformTargets { get; set; }

        private static readonly DateTime __releaseDate;
        public static DateTime GetReleaseDate()
        {
            return __releaseDate;
        }

        [Obsolete("Use ReferenceAssemblyPath")]
        public static string ReferenceAssembyPath => ReferenceAssemblyPath; 
        
        private static string referenceAssemblyPath;

        public static string ReferenceAssemblyPath
        {
            get
            {
                if (!IsMono && referenceAssemblyPath == null)
                {
                    var programFilesPath = PclExport.Instance.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)";
                    var netFxReferenceBasePath = programFilesPath + @"\Reference Assemblies\Microsoft\Framework\.NETFramework\";
                    if ((netFxReferenceBasePath + @"v4.5.2\").DirectoryExists())
                        referenceAssemblyPath = netFxReferenceBasePath + @"v4.5.2\";
                    else if ((netFxReferenceBasePath + @"v4.5.1\").DirectoryExists())
                        referenceAssemblyPath = netFxReferenceBasePath + @"v4.5.1\";
                    else if ((netFxReferenceBasePath + @"v4.5\").DirectoryExists())
                        referenceAssemblyPath = netFxReferenceBasePath + @"v4.5\";
                    else if ((netFxReferenceBasePath + @"v4.0\").DirectoryExists())
                        referenceAssemblyPath = netFxReferenceBasePath + @"v4.0\";
                    else
                    {
                        var v4Dirs = PclExport.Instance.GetDirectoryNames(netFxReferenceBasePath, "v4*");
                        if (v4Dirs.Length == 0)
                        {
                            var winPath = PclExport.Instance.GetEnvironmentVariable("SYSTEMROOT") ?? @"C:\Windows";
                            var gacPath = winPath + @"\Microsoft.NET\Framework\";
                            v4Dirs = PclExport.Instance.GetDirectoryNames(gacPath, "v4*");                            
                        }
                        if (v4Dirs.Length > 0)
                        {
                            referenceAssemblyPath = v4Dirs[v4Dirs.Length - 1] + @"\"; //latest v4
                        }
                        else
                        {
                            throw new FileNotFoundException(
                                "Could not infer .NET Reference Assemblies path, e.g '{0}'.\n".Fmt(netFxReferenceBasePath + @"v4.0\") +
                                "Provide path manually 'Env.ReferenceAssembyPath'.");
                        }
                    }
                }
                return referenceAssemblyPath;
            }
            set => referenceAssemblyPath = value;
        }

#if NETSTANDARD2_0
        private static bool IsRunningAsUwp()
        {
            try
            {
                IsNetNative = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription.StartsWith(".NET Native", StringComparison.OrdinalIgnoreCase);
                return IsInAppContainer || IsNetNative;
            }
            catch (Exception) {}
            return false;
        }
        
        private static bool IsWindows7OrLower
        {
            get
            {
                int versionMajor = Environment.OSVersion.Version.Major;
                int versionMinor = Environment.OSVersion.Version.Minor;
                double version = versionMajor + (double)versionMinor / 10;
                return version <= 6.1;
            }
        } 
        
        // From: https://github.com/dotnet/corefx/blob/master/src/CoreFx.Private.TestUtilities/src/System/PlatformDetection.Windows.cs
        private static int s_isInAppContainer = -1;
        private static bool IsInAppContainer
        {
            // This actually checks whether code is running in a modern app. 
            // Currently this is the only situation where we run in app container.
            // If we want to distinguish the two cases in future,
            // EnvironmentHelpers.IsAppContainerProcess in desktop code shows how to check for the AC token.
            get
            {
                if (s_isInAppContainer != -1)
                    return s_isInAppContainer == 1;

                if (!IsWindows || IsWindows7OrLower)
                {
                    s_isInAppContainer = 0;
                    return false;
                }

                byte[] buffer = TypeConstants.EmptyByteArray;
                uint bufferSize = 0;
                try
                {
                    int result = GetCurrentApplicationUserModelId(ref bufferSize, buffer);
                    switch (result)
                    {
                        case 15703: // APPMODEL_ERROR_NO_APPLICATION
                            s_isInAppContainer = 0;
                            break;
                        case 0:     // ERROR_SUCCESS
                        case 122:   // ERROR_INSUFFICIENT_BUFFER
                                    // Success is actually insufficent buffer as we're really only looking for
                                    // not NO_APPLICATION and we're not actually giving a buffer here. The
                                    // API will always return NO_APPLICATION if we're not running under a
                                    // WinRT process, no matter what size the buffer is.
                            s_isInAppContainer = 1;
                            break;
                        default:
                            throw new InvalidOperationException($"Failed to get AppId, result was {result}.");
                    }
                }
                catch (Exception e)
                {
                    // We could catch this here, being friendly with older portable surface area should we
                    // desire to use this method elsewhere.
                    if (e.GetType().FullName.Equals("System.EntryPointNotFoundException", StringComparison.Ordinal))
                    {
                        // API doesn't exist, likely pre Win8
                        s_isInAppContainer = 0;
                    }
                    else
                    {
                        throw;
                    }
                }

                return s_isInAppContainer == 1;
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern int GetCurrentApplicationUserModelId(ref uint applicationUserModelIdLength, byte[] applicationUserModelId);
 #endif
        
    }
}