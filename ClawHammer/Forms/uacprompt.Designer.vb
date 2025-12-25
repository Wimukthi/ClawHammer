<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class uacprompt
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        btnuac = New Button()
        Label1 = New Label()
        SuspendLayout()
        ' 
        ' btnuac
        ' 
        btnuac.Location = New Point(21, 58)
        btnuac.Margin = New Padding(4, 3, 4, 3)
        btnuac.Name = "btnuac"
        btnuac.Size = New Size(425, 66)
        btnuac.TabIndex = 0
        btnuac.Text = "Restart As Administrator"
        btnuac.UseVisualStyleBackColor = True
        ' 
        ' Label1
        ' 
        Label1.AutoSize = True
        Label1.Font = New Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        Label1.Location = New Point(14, 14)
        Label1.Margin = New Padding(4, 0, 4, 0)
        Label1.Name = "Label1"
        Label1.Size = New Size(371, 28)
        Label1.TabIndex = 1
        Label1.Text = "You need Administrator permissions to Use this tool," & vbCrLf & "click the button below to elevate permissions."
        ' 
        ' uacprompt
        ' 
        AutoScaleDimensions = New SizeF(7F, 15F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(465, 154)
        ControlBox = False
        Controls.Add(Label1)
        Controls.Add(btnuac)
        FormBorderStyle = FormBorderStyle.FixedDialog
        Margin = New Padding(4, 3, 4, 3)
        MaximizeBox = False
        MinimizeBox = False
        Name = "uacprompt"
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterScreen
        Text = "User Access Control"
        TopMost = True
        ResumeLayout(False)
        PerformLayout()

    End Sub
    Friend WithEvents btnuac As System.Windows.Forms.Button
    Friend WithEvents Label1 As System.Windows.Forms.Label
    ' Friend WithEvents PictureBox1 As System.Windows.Forms.PictureBox ' Removed
End Class
