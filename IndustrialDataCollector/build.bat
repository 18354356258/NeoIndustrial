@echo off
"C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" "C:\Users\Administrator\Desktop\C#\IndustrialDataCollectionⅡ\IndustrialDataCollection\IndustrialDataCollection.csproj" /t:Rebuild /p:Configuration=Debug /verbosity:minimal
exit %ERRORLEVEL%
