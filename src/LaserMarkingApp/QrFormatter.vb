Imports System

Public NotInheritable Class QrFormatter
    Private Sub New()
    End Sub

    Public Shared Function Build(part As PartRecord, serial As String) As String
        If part Is Nothing Then
            Throw New ArgumentNullException(NameOf(part))
        End If

        Dim format = If(String.IsNullOrWhiteSpace(part.QrFormat), "{VendorCode}|{PartNumber}|{Serial}", part.QrFormat)
        Return format.
            Replace("{VendorCode}", part.VendorCode).
            Replace("{PartNumber}", part.PartNumber).
            Replace("{PlantCode}", part.PlantCode).
            Replace("{CustomerCode}", part.CustomerCode).
            Replace("{QRPrefix}", part.QrPrefix).
            Replace("{Serial}", serial)
    End Function
End Class
