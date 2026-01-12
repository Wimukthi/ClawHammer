Imports System.IO
Imports System.Reflection
Imports System.Runtime.Loader
Imports ClawHammer.PluginContracts

Public Class PluginInfo
    Public Property Id As String
    Public Property Name As String
    Public Property Description As String
    Public Property Category As String
    Public Property Version As Version
    Public Property AssemblyPath As String
End Class

Public Module PluginDiscovery
    Public Function DiscoverPlugins(Optional log As Action(Of String) = Nothing) As List(Of PluginInfo)
        Dim results As New List(Of PluginInfo)()
        Dim pluginDir As String = PluginSettingsStore.GetPluginDirectory()
        If Not Directory.Exists(pluginDir) Then
            Return results
        End If

        Dim seenIds As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        For Each dll As String In Directory.EnumerateFiles(pluginDir, "*.dll", SearchOption.TopDirectoryOnly)
            Try
                Dim asm As Assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(dll)
                Dim asmVersion As Version = asm.GetName().Version

                For Each pluginType As Type In asm.GetTypes()
                    If pluginType.IsAbstract Then
                        Continue For
                    End If
                    If Not GetType(IStressPlugin).IsAssignableFrom(pluginType) Then
                        Continue For
                    End If

                    Dim instance As IStressPlugin = Nothing
                    Try
                        instance = CType(Activator.CreateInstance(pluginType), IStressPlugin)
                    Catch ex As Exception
                        log?.Invoke($"Plugin scan failed: {pluginType.FullName}: {ex.Message}")
                        Continue For
                    End Try

                    If instance Is Nothing OrElse String.IsNullOrWhiteSpace(instance.Id) Then
                        Continue For
                    End If

                    If Not seenIds.Add(instance.Id) Then
                        Continue For
                    End If

                    results.Add(New PluginInfo With {
                        .Id = instance.Id,
                        .Name = If(String.IsNullOrWhiteSpace(instance.DisplayName), instance.Id, instance.DisplayName),
                        .Description = If(instance.Description, String.Empty),
                        .Category = If(instance.Category, String.Empty),
                        .Version = asmVersion,
                        .AssemblyPath = dll
                    })
                Next
            Catch ex As Exception
                log?.Invoke($"Plugin scan failed: {Path.GetFileName(dll)}: {ex.Message}")
            End Try
        Next

        Return results
    End Function
End Module
