Imports System.IO
Imports System.IO.Compression

Public Module PluginInstallManager
    Private Const PendingFolderName As String = "pending"
    Private Const DeleteMarkerExtension As String = ".delete"

    Public Function GetPendingDirectory() As String
        Return Path.Combine(PluginSettingsStore.GetPluginDirectory(), PendingFolderName)
    End Function

    Public Sub ApplyPending(Optional log As Action(Of String) = Nothing)
        Dim pluginDir As String = PluginSettingsStore.GetPluginDirectory()
        Dim pendingDir As String = GetPendingDirectory()
        If Not Directory.Exists(pendingDir) Then
            Return
        End If

        For Each filePath As String In Directory.EnumerateFiles(pendingDir, "*", SearchOption.TopDirectoryOnly)
            Dim extension As String = Path.GetExtension(filePath)
            If String.Equals(extension, DeleteMarkerExtension, StringComparison.OrdinalIgnoreCase) Then
                ApplyDeleteMarker(filePath, log)
                Continue For
            End If

            Try
                If String.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase) Then
                    ExtractZipToDirectory(filePath, pluginDir, log)
                ElseIf String.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase) Then
                    CopyToDirectory(filePath, pluginDir)
                End If
            Catch ex As Exception
                log?.Invoke($"Pending plugin install failed: {Path.GetFileName(filePath)}: {ex.Message}")
                Continue For
            End Try

            Try
                File.Delete(filePath)
            Catch
            End Try
        Next
    End Sub

    Public Function StageInstall(sourcePath As String) As String
        Dim pendingDir As String = GetPendingDirectory()
        Directory.CreateDirectory(pendingDir)

        Dim fileName As String = Path.GetFileName(sourcePath)
        Dim destPath As String = EnsureUniquePath(Path.Combine(pendingDir, fileName))
        File.Copy(sourcePath, destPath, True)
        Return destPath
    End Function

    Public Function GetPendingFilePath(fileName As String) As String
        Dim pendingDir As String = GetPendingDirectory()
        Directory.CreateDirectory(pendingDir)
        Return EnsureUniquePath(Path.Combine(pendingDir, fileName))
    End Function

    Public Sub ScheduleDelete(targetPath As String)
        If String.IsNullOrWhiteSpace(targetPath) Then
            Return
        End If

        Dim pendingDir As String = GetPendingDirectory()
        Directory.CreateDirectory(pendingDir)
        Dim markerName As String = Path.GetFileName(targetPath) & DeleteMarkerExtension
        Dim markerPath As String = Path.Combine(pendingDir, markerName)
        File.WriteAllText(markerPath, targetPath)
    End Sub

    Private Sub ApplyDeleteMarker(markerPath As String, log As Action(Of String))
        Try
            Dim targetPath As String = File.ReadAllText(markerPath).Trim()
            If Not String.IsNullOrWhiteSpace(targetPath) AndAlso File.Exists(targetPath) Then
                File.Delete(targetPath)
            End If
            File.Delete(markerPath)
        Catch ex As Exception
            log?.Invoke($"Pending plugin delete failed: {Path.GetFileName(markerPath)}: {ex.Message}")
        End Try
    End Sub

    Private Sub ExtractZipToDirectory(zipPath As String, targetDir As String, log As Action(Of String))
        Using archive As ZipArchive = ZipFile.OpenRead(zipPath)
            For Each entry As ZipArchiveEntry In archive.Entries
                If String.IsNullOrWhiteSpace(entry.Name) Then
                    Continue For
                End If

                Dim destinationPath As String = Path.Combine(targetDir, entry.FullName)
                Dim destinationDir As String = Path.GetDirectoryName(destinationPath)
                If Not String.IsNullOrWhiteSpace(destinationDir) Then
                    Directory.CreateDirectory(destinationDir)
                End If

                Try
                    entry.ExtractToFile(destinationPath, True)
                Catch ex As Exception
                    log?.Invoke($"Plugin file extract failed: {entry.FullName}: {ex.Message}")
                End Try
            Next
        End Using
    End Sub

    Private Sub CopyToDirectory(sourcePath As String, targetDir As String)
        Dim fileName As String = Path.GetFileName(sourcePath)
        Dim destinationPath As String = Path.Combine(targetDir, fileName)
        File.Copy(sourcePath, destinationPath, True)
    End Sub

    Private Function EnsureUniquePath(filePath As String) As String
        If Not File.Exists(filePath) Then
            Return filePath
        End If

        Dim directoryPath As String = System.IO.Path.GetDirectoryName(filePath)
        Dim baseName As String = System.IO.Path.GetFileNameWithoutExtension(filePath)
        Dim extension As String = System.IO.Path.GetExtension(filePath)
        Dim stamp As String = DateTime.Now.ToString("yyyyMMddHHmmss")
        Return System.IO.Path.Combine(directoryPath, $"{baseName}_{stamp}{extension}")
    End Function
End Module


