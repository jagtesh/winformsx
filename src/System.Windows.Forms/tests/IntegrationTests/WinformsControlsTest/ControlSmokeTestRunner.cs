// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;
using System.Reflection;
using System.Windows.Forms.Platform;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace WinFormsControlsTest;

internal static class ControlSmokeTestRunner
{
    public static int Run()
    {
        List<ControlSmokeTestResult> results = [];

        foreach (ControlSmokeTestCase testCase in ControlSmokeTestCatalog.TestCases)
        {
            results.Add(RunTestCaseInChildProcess(testCase));
            ControlSmokeTestResult result = results[^1];

            WriteResult(result);
        }

        int passed = results.Count(static result => result.Passed);
        int skipped = results.Count(static result => result.Skipped);
        int failed = results.Count - passed - skipped;
        int controls = results.Sum(static result => result.ControlCount);
        int handles = results.Sum(static result => result.CreatedHandleCount);

        Console.WriteLine(
            $"CONTROL_SMOKE_SUMMARY total={results.Count} passed={passed} failed={failed} skipped={skipped} controls={controls} handles={handles}");

        return failed == 0 ? 0 : 1;
    }

    public static int RunSingle(string testCaseName)
    {
        ControlSmokeTestCase? testCase = null;
        if (Enum.TryParse(testCaseName, ignoreCase: true, out System.Windows.Forms.IntegrationTests.Common.MainFormControlsTabOrder tabOrder))
        {
            ControlSmokeTestCatalog.TestCasesByTabOrder.TryGetValue(tabOrder, out testCase);
        }

        testCase ??= ControlSmokeTestCatalog.TestCases.FirstOrDefault(testCase =>
            string.Equals(testCase.Name, testCaseName, StringComparison.OrdinalIgnoreCase));

        if (testCase is null)
        {
            WriteResult(ControlSmokeTestResult.Fail(
                testCaseName.Length == 0 ? "<missing>" : testCaseName,
                TimeSpan.Zero,
                0,
                0,
                "InvalidTestCase",
                "No matching control smoke test case was found."));

            return 1;
        }

        ControlSmokeTestResult result = RunTestCase(testCase);
        WriteResult(result);
        return result.Passed || result.Skipped ? 0 : 1;
    }

    private static ControlSmokeTestResult RunTestCaseInChildProcess(ControlSmokeTestCase testCase)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        string assemblyPath = Assembly.GetEntryAssembly()?.Location ?? throw new InvalidOperationException("Entry assembly path was not found.");
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        };

        process.StartInfo.ArgumentList.Add(assemblyPath);
        process.StartInfo.ArgumentList.Add("--control-smoke-test-case");
        process.StartInfo.ArgumentList.Add(testCase.TabOrder.ToString());

        process.Start();
        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(15_000))
        {
            process.Kill(entireProcessTree: true);
            return ControlSmokeTestResult.Fail(
                testCase.Name,
                stopwatch.Elapsed,
                0,
                0,
                "Timeout",
                "The control smoke test case did not exit within 15 seconds.");
        }

        string standardOutput = standardOutputTask.GetAwaiter().GetResult();
        string standardError = standardErrorTask.GetAwaiter().GetResult();

        string resultLine = ReadResultLine(standardOutput);
        if (resultLine.Length > 0)
        {
            return ParseResultLine(testCase.Name, resultLine, stopwatch.Elapsed);
        }

        string diagnostic = FirstNonEmptyLine(standardError);
        if (diagnostic.Length == 0)
        {
            diagnostic = FirstNonEmptyLine(standardOutput);
        }

        return ControlSmokeTestResult.Fail(
            testCase.Name,
            stopwatch.Elapsed,
            0,
            0,
            $"ExitCode {process.ExitCode}",
            diagnostic.Length == 0 ? "Child process exited without a result line." : diagnostic);
    }

    private static ControlSmokeTestResult RunTestCase(ControlSmokeTestCase testCase)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Form form = null;
        int controlCount = 0;
        int createdHandleCount = 0;

        if (!testCase.IsSupported)
        {
            return ControlSmokeTestResult.Skip(testCase.Name, stopwatch.Elapsed, testCase.SkipReason);
        }

        try
        {
            form = testCase.CreateForm();
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-32000, -32000);
            controlCount = CountControls(form);

            form.Show();
            Application.DoEvents();
            VerifyVisibleTopLevelFormsPresented();

            createdHandleCount = CountCreatedHandles(form);

            return ControlSmokeTestResult.Pass(
                testCase.Name,
                stopwatch.Elapsed,
                controlCount,
                createdHandleCount);
        }
        catch (Exception ex)
        {
            return ControlSmokeTestResult.Fail(
                testCase.Name,
                stopwatch.Elapsed,
                controlCount,
                createdHandleCount,
                ex.GetType().FullName ?? ex.GetType().Name,
                FormatErrorMessage(ex));
        }
        finally
        {
            if (form is not null)
            {
                try
                {
                    form.Close();
                    form.Dispose();
                    Application.DoEvents();
                }
                catch
                {
                    form.Dispose();
                }
            }
        }
    }

    private static void WriteResult(ControlSmokeTestResult result)
    {
        if (result.Skipped)
        {
            Console.WriteLine($"SKIP {result.Name} reason={result.ErrorMessage}");
        }
        else if (result.Passed)
        {
            Console.WriteLine(
                $"PASS {result.Name} controls={result.ControlCount} handles={result.CreatedHandleCount} elapsedMs={result.Elapsed.TotalMilliseconds:F0}");
        }
        else
        {
            Console.WriteLine(
                $"FAIL {result.Name} controls={result.ControlCount} handles={result.CreatedHandleCount} elapsedMs={result.Elapsed.TotalMilliseconds:F0} error={result.ErrorType}: {result.ErrorMessage}");
        }
    }

    private static string ReadResultLine(string output)
    {
        using StringReader reader = new(output);

        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("PASS ", StringComparison.Ordinal)
                || line.StartsWith("FAIL ", StringComparison.Ordinal)
                || line.StartsWith("SKIP ", StringComparison.Ordinal))
            {
                return line;
            }
        }

        return string.Empty;
    }

    private static ControlSmokeTestResult ParseResultLine(string fallbackName, string resultLine, TimeSpan elapsed)
    {
        string name = ReadName(fallbackName, resultLine);
        int controls = ReadMetric(resultLine, "controls=");
        int handles = ReadMetric(resultLine, "handles=");

        if (resultLine.StartsWith("PASS ", StringComparison.Ordinal))
        {
            return ControlSmokeTestResult.Pass(name, elapsed, controls, handles);
        }

        if (resultLine.StartsWith("SKIP ", StringComparison.Ordinal))
        {
            return ControlSmokeTestResult.Skip(name, elapsed, ReadTrailingValue(resultLine, "reason="));
        }

        string error = ReadTrailingValue(resultLine, "error=");
        int separatorIndex = error.IndexOf(": ", StringComparison.Ordinal);
        string errorType = separatorIndex >= 0 ? error[..separatorIndex] : "Failure";
        string errorMessage = separatorIndex >= 0 ? error[(separatorIndex + 2)..] : error;

        return ControlSmokeTestResult.Fail(name, elapsed, controls, handles, errorType, errorMessage);
    }

    private static string ReadName(string fallbackName, string resultLine)
    {
        int nameStart = resultLine.IndexOf(' ') + 1;
        int controlsStart = resultLine.IndexOf(" controls=", StringComparison.Ordinal);
        int reasonStart = resultLine.IndexOf(" reason=", StringComparison.Ordinal);
        int nameEnd = controlsStart >= 0 ? controlsStart : reasonStart;

        return nameEnd > nameStart ? resultLine[nameStart..nameEnd] : fallbackName;
    }

    private static int ReadMetric(string resultLine, string key)
    {
        string value = ReadValue(resultLine, key);
        return int.TryParse(value, out int metric) ? metric : 0;
    }

    private static string ReadValue(string resultLine, string key)
    {
        int start = resultLine.IndexOf(key, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        start += key.Length;
        int end = resultLine.IndexOf(' ', start);

        return end < 0 ? resultLine[start..] : resultLine[start..end];
    }

    private static string ReadTrailingValue(string resultLine, string key)
    {
        int start = resultLine.IndexOf(key, StringComparison.Ordinal);
        return start < 0 ? string.Empty : resultLine[(start + key.Length)..];
    }

    private static string FirstNonEmptyLine(string text)
    {
        using StringReader reader = new(text);

        while (reader.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                return line.Trim();
            }
        }

        return string.Empty;
    }

    private static string FormatErrorMessage(Exception ex)
    {
        string message = IsVerboseFailureOutputEnabled()
            ? ex.ToString()
            : FirstNonEmptyLine(ex.Message);

        return message.ReplaceLineEndings(" | ");
    }

    private static bool IsVerboseFailureOutputEnabled()
        => string.Equals(
            Environment.GetEnvironmentVariable("WINFORMSX_CONTROL_SMOKE_VERBOSE"),
            "1",
            StringComparison.Ordinal);

    private static int CountControls(Control control)
    {
        int count = 1;

        foreach (Control child in control.Controls)
        {
            count += CountControls(child);
        }

        return count;
    }

    private static int CountCreatedHandles(Control control)
    {
        int count = control.IsHandleCreated ? 1 : 0;

        foreach (Control child in control.Controls)
        {
            count += CountCreatedHandles(child);
        }

        return count;
    }

    private static void VerifyVisibleTopLevelFormsPresented()
    {
        if (PlatformApi.Window is not ImpellerWindowInterop windowInterop)
        {
            return;
        }

        foreach (Form form in Application.OpenForms.Cast<Form>().ToArray())
        {
            if (!form.Visible || !form.IsHandleCreated)
            {
                continue;
            }

            HWND hwnd = (HWND)(nint)form.Handle;
            ImpellerWindowState? state = windowInterop.GetWindowState(hwnd);
            if (state is null)
            {
                throw new InvalidOperationException($"Visible form '{form.Text}' handle 0x{(nint)hwnd:X} was not registered with the Impeller window provider.");
            }

            if (!ImpellerWindowInterop.IsTopLevelWindowStyle(state.Style))
            {
                continue;
            }

            for (int i = 0; i < 20; i++)
            {
                windowInterop.UpdateWindow(hwnd);
                windowInterop.PumpEvents();
                Application.DoEvents();
                if (windowInterop.GetWindowState(hwnd)?.PresentedFrameCount > 0)
                {
                    break;
                }
            }

            if (state.SilkWindow is null)
            {
                throw new InvalidOperationException($"Visible form '{form.Text}' handle 0x{(nint)hwnd:X} did not create an Impeller/Silk render surface.");
            }

            if (state.PresentedFrameCount <= 0)
            {
                throw new InvalidOperationException($"Visible form '{form.Text}' handle 0x{(nint)hwnd:X} did not present an Impeller frame.");
            }

            if (state.LastPaintedRootHandle != hwnd)
            {
                throw new InvalidOperationException(
                    $"Visible form '{form.Text}' handle 0x{(nint)hwnd:X} painted root 0x{(nint)state.LastPaintedRootHandle:X} instead of itself.");
            }
        }
    }

    private sealed record ControlSmokeTestResult(
        string Name,
        bool Passed,
        TimeSpan Elapsed,
        int ControlCount,
        int CreatedHandleCount,
        string ErrorType,
        string ErrorMessage,
        bool Skipped = false)
    {
        public static ControlSmokeTestResult Pass(
            string name,
            TimeSpan elapsed,
            int controlCount,
            int createdHandleCount) =>
            new(name, true, elapsed, controlCount, createdHandleCount, string.Empty, string.Empty);

        public static ControlSmokeTestResult Fail(
            string name,
            TimeSpan elapsed,
            int controlCount,
            int createdHandleCount,
            string errorType,
            string errorMessage) =>
            new(name, false, elapsed, controlCount, createdHandleCount, errorType, errorMessage);

        public static ControlSmokeTestResult Skip(
            string name,
            TimeSpan elapsed,
            string skipReason) =>
            new(name, false, elapsed, 0, 0, null, skipReason, Skipped: true);
    }
}
