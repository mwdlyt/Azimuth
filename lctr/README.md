# lctr

A new, standalone app living alongside Azimuth in this repository.

This is a fresh scaffold — a .NET 8 WPF application using the
[WPF-UI](https://github.com/lepoco/wpfui) Fluent control set and a dark theme,
matching the house style of the repo. It builds and runs independently of
Azimuth.

## Getting Started

Requirements: Windows 10 or later (x64), [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```
dotnet run --project lctr
```

To publish a self-contained single executable:

```
dotnet publish lctr -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

The output lands in `lctr/bin/Release/net8.0-windows/win-x64/publish/`.

## Structure

```
lctr/
  App.xaml / App.xaml.cs       Application entry point + global exception handling
  MainWindow.xaml / .xaml.cs   Main window shell
  Lctr.csproj                  Project file
```
