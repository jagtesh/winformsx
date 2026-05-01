// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace WinFormsControlsTest;

internal static class TestAssetPaths
{
    public static string DataPath(params string[] segments)
    {
        string[] allSegments = new string[segments.Length + 1];
        allSegments[0] = "Data";
        Array.Copy(segments, 0, allSegments, 1, segments.Length);

        return FullPath(Path.Combine(allSegments));
    }

    public static string FullPath(string relativePath, [CallerFilePath] string sourceFilePath = "")
    {
        string appBasePath = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (File.Exists(appBasePath) || Directory.Exists(appBasePath))
        {
            return appBasePath;
        }

        string workingDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
        if (File.Exists(workingDirectoryPath) || Directory.Exists(workingDirectoryPath))
        {
            return workingDirectoryPath;
        }

        string sourceDirectory = Path.GetDirectoryName(sourceFilePath);
        if (!string.IsNullOrEmpty(sourceDirectory))
        {
            string sourcePath = Path.Combine(sourceDirectory, relativePath);
            if (File.Exists(sourcePath) || Directory.Exists(sourcePath))
            {
                return sourcePath;
            }
        }

        return appBasePath;
    }
}
