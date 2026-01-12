Imports System.IO
Imports System.Text.Json

Public Class PluginSettings
    Public Property CatalogUrl As String = String.Empty
    Public Property DisabledPluginIds As List(Of String) = New List(Of String)()
End Class

Public Module PluginSettingsStore
    Private Const SettingsFileName As String = "plugin-settings.json"

    Public Function GetPluginDirectory() As String
        Return Path.Combine(AppContext.BaseDirectory, "plugins")
    End Function

    Public Function GetSettingsPath() As String
        Return Path.Combine(GetPluginDirectory(), SettingsFileName)
    End Function

    Public Function LoadSettings() As PluginSettings
        Dim settingsPath As String = GetSettingsPath()
        If Not File.Exists(settingsPath) Then
            Return New PluginSettings()
        End If

        Try
            Dim json As String = File.ReadAllText(settingsPath)
            Dim options As New JsonSerializerOptions With {
                .PropertyNameCaseInsensitive = True
            }
            Dim settings As PluginSettings = JsonSerializer.Deserialize(Of PluginSettings)(json, options)
            If settings Is Nothing Then
                Return New PluginSettings()
            End If
            If settings.DisabledPluginIds Is Nothing Then
                settings.DisabledPluginIds = New List(Of String)()
            End If
            If settings.CatalogUrl Is Nothing Then
                settings.CatalogUrl = String.Empty
            End If
            Return settings
        Catch
            Return New PluginSettings()
        End Try
    End Function

    Public Sub SaveSettings(settings As PluginSettings)
        If settings Is Nothing Then
            Return
        End If

        Dim settingsPath As String = GetSettingsPath()
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(settingsPath))
        Dim options As New JsonSerializerOptions With {
            .WriteIndented = True
        }
        Dim json As String = JsonSerializer.Serialize(settings, options)
        File.WriteAllText(settingsPath, json)
    End Sub
End Module

