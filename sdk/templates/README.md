# Plugin Templates

Install a template:
- C#: `dotnet new install .\sdk\templates\ClawHammer.PluginTemplate.CSharp`
- VB.NET: `dotnet new install .\sdk\templates\ClawHammer.PluginTemplate.VB`

Create a plugin:
- C#: `dotnet new clawhammer-plugin-cs -n MyPlugin --PluginId com.example.myplugin`
- VB.NET: `dotnet new clawhammer-plugin-vb -n MyPlugin --PluginId com.example.myplugin`

Note: The template uses a `ProjectReference` to `ClawHammer.PluginContracts` for this repo.
If you copy the plugin to another folder, update the reference as needed.
