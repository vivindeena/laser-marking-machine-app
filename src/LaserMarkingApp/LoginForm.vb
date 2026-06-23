Imports System
Imports System.Drawing
Imports System.Windows.Forms

Public Class LoginForm
    Inherits Form

    Private ReadOnly _database As DatabaseService
    Private ReadOnly _usernameBox As TextBox
    Private ReadOnly _passwordBox As TextBox
    Private ReadOnly _statusLabel As Label

    Public Property AuthenticatedUser As UserRecord

    Public Sub New(database As DatabaseService)
        _database = database

        Text = "Setter Login"
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.FixedDialog
        MinimizeBox = False
        MaximizeBox = False
        ClientSize = New Size(360, 180)

        Dim usernameLabel = New Label With {.Text = "Username", .Location = New Point(24, 24), .AutoSize = True}
        _usernameBox = New TextBox With {.Location = New Point(120, 20), .Width = 200, .Text = "setter"}

        Dim passwordLabel = New Label With {.Text = "Password", .Location = New Point(24, 62), .AutoSize = True}
        _passwordBox = New TextBox With {.Location = New Point(120, 58), .Width = 200, .UseSystemPasswordChar = True}

        _statusLabel = New Label With {.Location = New Point(24, 98), .Size = New Size(296, 24), .ForeColor = Color.DarkRed}

        Dim loginButton = New Button With {.Text = "Login", .Location = New Point(164, 130), .Width = 76}
        Dim cancelButton = New Button With {.Text = "Cancel", .Location = New Point(244, 130), .Width = 76, .DialogResult = DialogResult.Cancel}

        AddHandler loginButton.Click, AddressOf LoginButton_Click
        AcceptButton = loginButton
        CancelButton = cancelButton

        Controls.AddRange({usernameLabel, _usernameBox, passwordLabel, _passwordBox, _statusLabel, loginButton, cancelButton})
    End Sub

    Private Sub LoginButton_Click(sender As Object, e As EventArgs)
        Dim user = _database.FindUser(_usernameBox.Text.Trim())
        If user Is Nothing OrElse Not PasswordHasher.Verify(_passwordBox.Text, user.PasswordHash) Then
            _statusLabel.Text = "Invalid username or password."
            _passwordBox.SelectAll()
            _passwordBox.Focus()
            Return
        End If

        If user.Role = UserRole.OperatorUser Then
            _statusLabel.Text = "Operator accounts cannot open setter settings."
            Return
        End If

        AuthenticatedUser = user
        DialogResult = DialogResult.OK
        Close()
    End Sub
End Class
