Imports System
Imports System.IO

Public NotInheritable Class AppPaths
    Private Sub New()
    End Sub

    Public Shared ReadOnly Property DataDirectory As String
        Get
            Dim path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LaserMarkingApp")
            Directory.CreateDirectory(path)
            Return path
        End Get
    End Property

    Public Shared ReadOnly Property DatabasePath As String
        Get
            Return Path.Combine(DataDirectory, "laser_marking.db")
        End Get
    End Property
End Class
