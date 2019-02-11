Copy-Item .\Westwind.Scripting\bin\Release\Westwind.Scripting.dll .\Nuget\Package\lib\net45
Copy-Item .\Westwind.Scripting\bin\Release\Westwind.Scripting.xml .\Nuget\Package\lib\net45

signtool.exe sign /v /n "West Wind Technologies" /sm /s MY /tr "http://timestamp.digicert.com" /td SHA256 /fd SHA256 ".\Nuget\Package\lib\net45\Westwind.Scripting.dll"
