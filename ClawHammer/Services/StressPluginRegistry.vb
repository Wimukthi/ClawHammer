Imports System.IO
Imports System.Reflection
Imports System.Runtime.Loader
Imports System.Numerics
Imports ClawHammer.PluginContracts

Public Class StressPluginRegistry
    Private ReadOnly _plugins As New List(Of IStressPlugin)()

    Public Property PrimeRangeMin As Long = 2
    Public Property PrimeRangeMax As Long = 25000000
    Public Property MemoryBufferBytes As Integer = 4 * 1024 * 1024

    Public Sub LoadPlugins(Optional log As Action(Of String) = Nothing, Optional disabledIds As ISet(Of String) = Nothing)
        _plugins.Clear()

        Dim baseDir As String = AppContext.BaseDirectory
        Dim pluginDir As String = Path.Combine(baseDir, "plugins")
        Directory.CreateDirectory(pluginDir)

        Dim pluginPaths As New List(Of String)()
        For Each dll As String In Directory.EnumerateFiles(pluginDir, "*.dll")
            pluginPaths.Add(dll)
        Next

        Dim seenIds As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        For Each pluginPath As String In pluginPaths
            Try
                Dim asm As Assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(pluginPath)
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
                        log?.Invoke($"Plugin load failed: {pluginType.FullName}: {ex.Message}")
                        Continue For
                    End Try

                    If instance Is Nothing OrElse String.IsNullOrWhiteSpace(instance.Id) Then
                        log?.Invoke($"Plugin skipped: {pluginType.FullName} has no Id.")
                        Continue For
                    End If

                    If Not seenIds.Add(instance.Id) Then
                        log?.Invoke($"Plugin skipped: duplicate Id {instance.Id}.")
                        Continue For
                    End If

                    If disabledIds IsNot Nothing AndAlso disabledIds.Contains(instance.Id) Then
                        log?.Invoke($"Plugin disabled: {instance.Id}.")
                        Continue For
                    End If

                    _plugins.Add(instance)
                Next
            Catch ex As Exception
                log?.Invoke($"Plugin load failed: {Path.GetFileName(pluginPath)}: {ex.Message}")
            End Try
        Next

        If _plugins.Count = 0 Then
            log?.Invoke($"No stress plugins were found in {pluginDir}.")
        End If
    End Sub

    Public Function GetPlugins() As IReadOnlyList(Of IStressPlugin)
        Return _plugins
    End Function

    Public Function TryGetPlugin(id As String, ByRef plugin As IStressPlugin) As Boolean
        plugin = Nothing
        If String.IsNullOrWhiteSpace(id) Then
            Return False
        End If

        For Each item As IStressPlugin In _plugins
            If String.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase) Then
                plugin = item
                Return True
            End If
        Next

        Return False
    End Function

    Public Function CreateContext(totalWorkers As Integer) As StressPluginContext
        Dim avxSupported As Boolean = Vector.IsHardwareAccelerated
        Return New StressPluginContext(totalWorkers, PrimeRangeMin, PrimeRangeMax, MemoryBufferBytes, avxSupported)
    End Function
End Class






