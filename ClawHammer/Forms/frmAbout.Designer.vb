<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frmabout
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
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(frmabout))
        TextBox1 = New TextBox()
        Button1 = New Button()
        RichTextBox1 = New RichTextBox()
        PictureBox1 = New PictureBox()
        lblversion = New Label()
        CType(PictureBox1, ComponentModel.ISupportInitialize).BeginInit()
        SuspendLayout()
        ' 
        ' TextBox1
        ' 
        TextBox1.BackColor = SystemColors.MenuBar
        TextBox1.BorderStyle = BorderStyle.None
        TextBox1.Enabled = False
        TextBox1.Font = New Font("Consolas", 9.75F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        TextBox1.Location = New Point(0, 420)
        TextBox1.Margin = New Padding(4, 3, 4, 3)
        TextBox1.Multiline = True
        TextBox1.Name = "TextBox1"
        TextBox1.Size = New Size(566, 89)
        TextBox1.TabIndex = 3
        ' 
        ' Button1
        ' 
        Button1.Location = New Point(464, 515)
        Button1.Margin = New Padding(4, 3, 4, 3)
        Button1.Name = "Button1"
        Button1.Size = New Size(88, 27)
        Button1.TabIndex = 6
        Button1.Text = "&Close"
        Button1.UseVisualStyleBackColor = True
        ' 
        ' RichTextBox1
        ' 
        RichTextBox1.BackColor = SystemColors.MenuBar
        RichTextBox1.BorderStyle = BorderStyle.None
        RichTextBox1.Font = New Font("Arial", 9F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        RichTextBox1.ForeColor = Color.Black
        RichTextBox1.Location = New Point(0, 86)
        RichTextBox1.Margin = New Padding(4, 3, 4, 3)
        RichTextBox1.Name = "RichTextBox1"
        RichTextBox1.ReadOnly = True
        RichTextBox1.Size = New Size(562, 328)
        RichTextBox1.TabIndex = 1
        RichTextBox1.Text = resources.GetString("RichTextBox1.Text")
        ' 
        ' PictureBox1
        ' 
        PictureBox1.BackColor = Color.FromArgb(CByte(44), CByte(44), CByte(44))
        PictureBox1.Dock = DockStyle.Top
        PictureBox1.Image = CType(resources.GetObject("PictureBox1.Image"), Image)
        PictureBox1.InitialImage = Nothing
        PictureBox1.Location = New Point(0, 0)
        PictureBox1.Margin = New Padding(4, 3, 4, 3)
        PictureBox1.Name = "PictureBox1"
        PictureBox1.Size = New Size(566, 80)
        PictureBox1.SizeMode = PictureBoxSizeMode.AutoSize
        PictureBox1.TabIndex = 0
        PictureBox1.TabStop = False
        ' 
        ' lblversion
        ' 
        lblversion.BackColor = Color.FromArgb(CByte(44), CByte(44), CByte(44))
        lblversion.Font = New Font("Lucida Console", 9F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        lblversion.ForeColor = Color.White
        lblversion.Location = New Point(354, 53)
        lblversion.Margin = New Padding(4, 0, 4, 0)
        lblversion.Name = "lblversion"
        lblversion.Size = New Size(198, 27)
        lblversion.TabIndex = 7
        ' 
        ' frmabout
        ' 
        AutoScaleDimensions = New SizeF(96F, 96F)
        AutoScaleMode = AutoScaleMode.Dpi
        BackColor = SystemColors.Menu
        ClientSize = New Size(566, 555)
        Controls.Add(lblversion)
        Controls.Add(RichTextBox1)
        Controls.Add(Button1)
        Controls.Add(TextBox1)
        Controls.Add(PictureBox1)
        FormBorderStyle = FormBorderStyle.FixedSingle
        Icon = CType(resources.GetObject("$this.Icon"), Icon)
        Margin = New Padding(4, 3, 4, 3)
        MaximizeBox = False
        MinimizeBox = False
        Name = "frmabout"
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        Text = "About ClawHammer"
        CType(PictureBox1, ComponentModel.ISupportInitialize).EndInit()
        ResumeLayout(False)
        PerformLayout()

    End Sub
    Friend WithEvents PictureBox1 As System.Windows.Forms.PictureBox
    Friend WithEvents TextBox1 As System.Windows.Forms.TextBox
    Friend WithEvents Button1 As System.Windows.Forms.Button
    Friend WithEvents RichTextBox1 As System.Windows.Forms.RichTextBox
    Friend WithEvents lblversion As System.Windows.Forms.Label
End Class
