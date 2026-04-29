// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;
using WinFormsControlsTest;

if (OperatingSystem.IsWindows())
{
    // Set STAThread
    Thread.CurrentThread.SetApartmentState(ApartmentState.Unknown);
    Thread.CurrentThread.SetApartmentState(ApartmentState.STA);
}

ApplicationConfiguration.Initialize();

Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
Thread.CurrentThread.CurrentUICulture = Thread.CurrentThread.CurrentCulture;

int smokeTestCaseArgumentIndex = Array.FindIndex(
    args,
    static argument => string.Equals(argument, "--control-smoke-test-case", StringComparison.OrdinalIgnoreCase));
bool isSmokeTest = smokeTestCaseArgumentIndex >= 0 || args.Contains("--control-smoke-test", StringComparer.OrdinalIgnoreCase);

if (isSmokeTest)
{
    Directory.SetCurrentDirectory(AppContext.BaseDirectory);
    Environment.SetEnvironmentVariable("WINFORMSX_CONTROL_SMOKE", "1");
}

if (smokeTestCaseArgumentIndex >= 0)
{
    string testCaseName = smokeTestCaseArgumentIndex + 1 < args.Length ? args[smokeTestCaseArgumentIndex + 1] : string.Empty;
    Environment.Exit(ControlSmokeTestRunner.RunSingle(testCaseName));
}

if (isSmokeTest)
{
    Environment.Exit(ControlSmokeTestRunner.Run());
}

try
{
    MainForm form = new()
    {
        Icon = SystemIcons.GetStockIcon(StockIconId.Shield, StockIconOptions.SmallIcon)
    };

    Application.Run(form);
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    Environment.Exit(-1);
}

Environment.Exit(0);
