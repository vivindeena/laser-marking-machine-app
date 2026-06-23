Imports System

Public Enum UserRole
    OperatorUser = 0
    Setter = 1
    Admin = 2
End Enum

Public Class PartRecord
    Public Property Id As Integer
    Public Property PartNumber As String = ""
    Public Property VendorCode As String = ""
    Public Property PlantCode As String = ""
    Public Property CustomerCode As String = ""
    Public Property QrPrefix As String = ""
    Public Property QrFormat As String = "{VendorCode}|{PartNumber}|{Serial}"
    Public Property TemplateFile As String = ""
    Public Property IsActive As Boolean

    Public Overrides Function ToString() As String
        If String.IsNullOrWhiteSpace(PartNumber) Then
            Return "(new part)"
        End If

        Return PartNumber
    End Function
End Class

Public Class UserRecord
    Public Property Id As Integer
    Public Property Username As String = ""
    Public Property PasswordHash As String = ""
    Public Property Role As UserRole = UserRole.OperatorUser

    Public Overrides Function ToString() As String
        Return $"{Username} ({Role})"
    End Function
End Class

Public Class AppSettingsRecord
    Public Property QrOutputPath As String = "C:\Laser\QRDATA.TXT"
    Public Property ActiveTemplateDirectory As String = "C:\Laser\ActiveTemplate"
    Public Property AutoLogoutMinutes As Integer = 2
    Public Property SerialRegex As String = "^\d{6}$"
    Public Property ExternalCommand As String = ""
End Class
