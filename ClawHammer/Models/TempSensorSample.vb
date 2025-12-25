Public Structure TempSensorSample
    Public ReadOnly Label As String
    Public ReadOnly ValueC As Single
    Public ReadOnly HasValue As Boolean

    Public Sub New(label As String, valueC As Single, hasValue As Boolean)
        Me.Label = label
        Me.ValueC = valueC
        Me.HasValue = hasValue
    End Sub
End Structure
