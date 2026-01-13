Imports System
Imports System.Collections.Generic
Imports System.Runtime.InteropServices

Public Class PhysicalCoreInfo
    Public Property CoreId As Integer
    Public Property EfficiencyClass As Integer
    Public ReadOnly Property LogicalProcessors As List(Of Integer)

    Public Sub New(coreId As Integer, efficiencyClass As Integer)
        Me.CoreId = coreId
        Me.EfficiencyClass = efficiencyClass
        LogicalProcessors = New List(Of Integer)()
    End Sub
End Class

Public Class CpuTopologySnapshot
    Public Property PhysicalCores As List(Of PhysicalCoreInfo) = New List(Of PhysicalCoreInfo)()
    Public Property LogicalToCore As Dictionary(Of Integer, PhysicalCoreInfo) = New Dictionary(Of Integer, PhysicalCoreInfo)()
    Public Property HasEfficiencyClasses As Boolean
    Public Property HasOtherGroups As Boolean
    Public Property Warning As String
    Public Property IsValid As Boolean

    Public Function TryGetCoreForLogical(logicalId As Integer, ByRef core As PhysicalCoreInfo) As Boolean
        core = Nothing
        If LogicalToCore Is Nothing Then
            Return False
        End If
        Return LogicalToCore.TryGetValue(logicalId, core)
    End Function
End Class

Public Module CpuTopologyService
    Private ReadOnly _cacheLock As New Object()
    Private _cached As CpuTopologySnapshot

    Public Function GetTopology() As CpuTopologySnapshot
        SyncLock _cacheLock
            If _cached Is Nothing Then
                _cached = BuildTopology()
            End If
            Return _cached
        End SyncLock
    End Function

    Public Sub ResetCache()
        SyncLock _cacheLock
            _cached = Nothing
        End SyncLock
    End Sub

    Private Function BuildTopology() As CpuTopologySnapshot
        Dim snapshot As New CpuTopologySnapshot()

        Dim length As Integer = 0
        GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore, IntPtr.Zero, length)
        If length <= 0 Then
            Return snapshot
        End If

        Dim buffer As IntPtr = Marshal.AllocHGlobal(length)
        Try
            If Not GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore, buffer, length) Then
                Return snapshot
            End If

            Dim offset As Integer = 0
            Dim coreId As Integer = 0
            While offset < length
                Dim header As SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX = Marshal.PtrToStructure(Of SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX)(IntPtr.Add(buffer, offset))
                If header.Size <= 0 Then
                    Exit While
                End If

                If header.Relationship = LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore Then
                    Dim coreInfoPtr As IntPtr = IntPtr.Add(buffer, offset + Marshal.SizeOf(Of SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX)())
                    Dim coreInfo As PROCESSOR_RELATIONSHIP = Marshal.PtrToStructure(Of PROCESSOR_RELATIONSHIP)(coreInfoPtr)
                    Dim physicalCore As New PhysicalCoreInfo(coreId, coreInfo.EfficiencyClass)

                    Dim groupPtr As IntPtr = IntPtr.Add(coreInfoPtr, Marshal.SizeOf(Of PROCESSOR_RELATIONSHIP)())
                    For i As Integer = 0 To coreInfo.GroupCount - 1
                        Dim groupAffinity As GROUP_AFFINITY = Marshal.PtrToStructure(Of GROUP_AFFINITY)(IntPtr.Add(groupPtr, i * Marshal.SizeOf(Of GROUP_AFFINITY)()))
                        Dim mask As ULong = groupAffinity.Mask.ToUInt64()

                        If groupAffinity.Group <> 0US Then
                            snapshot.HasOtherGroups = True
                            Continue For
                        End If

                        For bit As Integer = 0 To 63
                            Dim flag As ULong = 1UL << bit
                            If (mask And flag) <> 0UL Then
                                physicalCore.LogicalProcessors.Add(bit)
                                If Not snapshot.LogicalToCore.ContainsKey(bit) Then
                                    snapshot.LogicalToCore.Add(bit, physicalCore)
                                End If
                            End If
                        Next
                    Next

                    physicalCore.LogicalProcessors.Sort()
                    snapshot.PhysicalCores.Add(physicalCore)
                    coreId += 1
                End If

                offset += header.Size
            End While
        Finally
            Marshal.FreeHGlobal(buffer)
        End Try

        If snapshot.PhysicalCores.Count > 0 Then
            snapshot.IsValid = True
            Dim classes As New HashSet(Of Integer)()
            For Each core As PhysicalCoreInfo In snapshot.PhysicalCores
                classes.Add(core.EfficiencyClass)
            Next
            snapshot.HasEfficiencyClasses = classes.Count > 1
            If snapshot.HasOtherGroups Then
                snapshot.Warning = "Only processor group 0 is shown (affinity mask limit)."
            End If
        End If

        Return snapshot
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Function GetLogicalProcessorInformationEx(relationshipType As LOGICAL_PROCESSOR_RELATIONSHIP,
                                                      buffer As IntPtr,
                                                      ByRef returnedLength As Integer) As Boolean
    End Function

    Friend Enum LOGICAL_PROCESSOR_RELATIONSHIP
        RelationProcessorCore = 0
    End Enum

    <StructLayout(LayoutKind.Sequential)>
    Friend Structure SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX
        Public Relationship As LOGICAL_PROCESSOR_RELATIONSHIP
        Public Size As Integer
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Friend Structure PROCESSOR_RELATIONSHIP
        Public Flags As Byte
        Public EfficiencyClass As Byte
        <MarshalAs(UnmanagedType.ByValArray, SizeConst:=20)>
        Public Reserved As Byte()
        Public GroupCount As UShort
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Friend Structure GROUP_AFFINITY
        Public Mask As UIntPtr
        Public Group As UShort
        Public Reserved1 As UShort
        Public Reserved2 As UShort
        Public Reserved3 As UShort
    End Structure
End Module
