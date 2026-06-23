Imports System
Imports System.Drawing
Imports System.Windows.Forms

Public Class UserManagementForm
    Inherits Form

    Private ReadOnly _database As DatabaseService
    Private ReadOnly _usersCombo As ComboBox
    Private ReadOnly _usernameBox As TextBox
    Private ReadOnly _passwordBox As TextBox
    Private ReadOnly _roleCombo As ComboBox
    Private ReadOnly _statusLabel As Label

    Public Sub New(database As DatabaseService)
        _database = database

        Text = "User Management"
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.FixedDialog
        MinimizeBox = False
        MaximizeBox = False
        ClientSize = New Size(420, 250)

        Dim existingLabel = New Label With {.Text = "Existing User", .Location = New Point(24, 24), .AutoSize = True}
        _usersCombo = New ComboBox With {.Location = New Point(136, 20), .Width = 230, .DropDownStyle = ComboBoxStyle.DropDownList}

        Dim usernameLabel = New Label With {.Text = "Username", .Location = New Point(24, 68), .AutoSize = True}
        _usernameBox = New TextBox With {.Location = New Point(136, 64), .Width = 230}

        Dim passwordLabel = New Label With {.Text = "New Password", .Location = New Point(24, 108), .AutoSize = True}
        _passwordBox = New TextBox With {.Location = New Point(136, 104), .Width = 230, .UseSystemPasswordChar = True}

        Dim roleLabel = New Label With {.Text = "Role", .Location = New Point(24, 148), .AutoSize = True}
        _roleCombo = New ComboBox With {.Location = New Point(136, 144), .Width = 230, .DropDownStyle = ComboBoxStyle.DropDownList}
        _roleCombo.Items.Add(UserRole.OperatorUser)
        _roleCombo.Items.Add(UserRole.Setter)
        _roleCombo.Items.Add(UserRole.Admin)
        _roleCombo.SelectedItem = UserRole.OperatorUser

        _statusLabel = New Label With {.Location = New Point(24, 190), .Size = New Size(230, 24), .ForeColor = Color.DarkGreen}

        Dim saveButton = New Button With {.Text = "Save User", .Location = New Point(264, 186), .Size = New Size(102, 32)}
        Dim closeButton = New Button With {.Text = "Close", .Location = New Point(292, 218), .Size = New Size(74, 24), .DialogResult = DialogResult.Cancel}

        AddHandler _usersCombo.SelectedIndexChanged, AddressOf UsersCombo_SelectedIndexChanged
        AddHandler saveButton.Click, AddressOf SaveButton_Click

        Controls.AddRange({
            existingLabel, _usersCombo, usernameLabel, _usernameBox, passwordLabel, _passwordBox,
            roleLabel, _roleCombo, _statusLabel, saveButton, closeButton
        })

        LoadUsers()
    End Sub

    Private Sub LoadUsers()
        _usersCombo.Items.Clear()
        For Each user In _database.GetUsers()
            _usersCombo.Items.Add(user)
        Next
    End Sub

    Private Sub UsersCombo_SelectedIndexChanged(sender As Object, e As EventArgs)
        Dim user = TryCast(_usersCombo.SelectedItem, UserRecord)
        If user Is Nothing Then
            Return
        End If

        _usernameBox.Text = user.Username
        _roleCombo.SelectedItem = user.Role
        _passwordBox.Clear()
        _passwordBox.Focus()
    End Sub

    Private Sub SaveButton_Click(sender As Object, e As EventArgs)
        Try
            Dim selectedRole = CType(_roleCombo.SelectedItem, UserRole)
            _database.SaveUser(_usernameBox.Text.Trim(), _passwordBox.Text, selectedRole)
            _statusLabel.ForeColor = Color.DarkGreen
            _statusLabel.Text = "User saved."
            _passwordBox.Clear()
            LoadUsers()
        Catch ex As Exception
            _statusLabel.ForeColor = Color.DarkRed
            _statusLabel.Text = ex.Message
        End Try
    End Sub
End Class
