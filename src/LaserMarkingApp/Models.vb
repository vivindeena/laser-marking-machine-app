Imports System

Public Enum UserRole
    OperatorUser = 0
    Setter = 1
    Admin = 2
End Enum

Public Class PartRecord
    Public Property Id As Integer
    Public Property PartNumber As String = ""
    Public Property CustomerItemCode As String = "7201097"
    Public Property VendorCode As String = ""
    Public Property PlantCode As String = ""
    Public Property CustomerCode As String = ""
    Public Property QrPrefix As String = ""
    Public Property QrFormat As String = "{VendorCode}|{PartNumber}|{Serial}"
    Public Property Material As String = "FG260"
    Public Property Pattern As String = "#"
    Public Property ProductName As String = "FLYWHEEL"
    Public Property SupplierName As String = "SREERAMENGG"
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
    Public Property SerialRegex As String = ""
    Public Property ExternalCommand As String = ""
End Class

Public Class PartSelectionLogRecord
    Public Property Id As Integer
    Public Property PartId As Integer
    Public Property PartNumber As String = ""
    Public Property SelectedBy As String = ""
    Public Property SelectedRole As UserRole = UserRole.Setter
    Public Property TimestampUtc As String = ""
End Class

Public Class MarkLogRecord
    Public Property Id As Integer
    Public Property PartNumber As String = ""
    Public Property GeneratedSerial As Integer
    Public Property HeatLotNumber As String = ""
    Public Property EngravingData As String = ""
    Public Property TimestampUtc As String = ""
    Public Property Username As String = ""
    Public Property Result As String = ""
End Class
