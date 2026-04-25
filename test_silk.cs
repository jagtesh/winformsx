using System;
using Silk.NET.Windowing;

var options = WindowOptions.Default;
Console.WriteLine($"State: {options.WindowState}, IsVisible: {options.IsVisible}, API: {options.API.API}");
