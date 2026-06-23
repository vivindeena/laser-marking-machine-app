Imports System
Imports System.Windows.Forms

Public Module Program
    <STAThread>
    Public Sub Main()
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)

        Dim dbPath = AppPaths.DatabasePath
        Dim database = New DatabaseService(dbPath)
        database.Initialize()

        Application.Run(New MainForm(database))
    End Sub
End Module
