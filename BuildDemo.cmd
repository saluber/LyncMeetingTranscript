set solutionDir=%1
set projectName=%2
set projectDir=%3
set targetDir=%4

echo Building Demo for %projectName%...
cd "%solutionDir%demo\"
if not exist %projectName% (
	mkdir %projectName%
)
	
cd %projectName%
copy /Y /B *.dll %targetDir%
copy /Y %targetDir%

echo SUCCESS.