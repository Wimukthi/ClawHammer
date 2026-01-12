# ClawHammer Plugin SDK

This folder contains everything needed to build custom stress test plugins for ClawHammer.

## Quickstart
1. Choose a starting point:
   - Templates: install with `dotnet new install .\sdk\templates\ClawHammer.PluginTemplate.CSharp` or `dotnet new install .\sdk\templates\ClawHammer.PluginTemplate.VB`.
   - Samples: copy from `sdk\samples` and rename the project.
2. Reference `ClawHammer.PluginContracts` from your plugin project.
   - Inside this repo, the samples already include a `ProjectReference`.
   - Outside this repo, replace the reference with your own path or a NuGet package.
3. Build the plugin (output is a DLL).
4. Install:
   - Copy the DLL into the `plugins` folder next to `ClawHammer.exe`, or
   - Use Plugin Manager -> Install From File (DLL or ZIP).

## Plugin discovery
ClawHammer scans the `plugins` folder at startup. Every non-abstract class implementing
`IStressPlugin` is loaded as a workload. One DLL can contain multiple workloads.

## Validation status updates
If you want status lines in the Validation Monitor, emit:
`STATUS|<workerId>|<kernelName>|<detail>` via the `reportStatus` callback.

## Packaging
Plugin Manager accepts a DLL directly or a ZIP containing the DLL.
Use `sdk\pack-plugin.ps1` to zip your build output.

## Docs
- `sdk/PLUGIN_API.md` for the full API reference.
- `sdk/samples` for working VB.NET and C# plugins.
- `sdk/templates` for `dotnet new` scaffolding.

## Tools
- `sdk/tools/PluginRunner`: console app to load a plugin DLL and run it for a few seconds.


