
set projectDir=%1
set grammarName=%2
set targetDir=%3

set grammarDir="%projectDir%Resources"
set grammarToolsDir="%projectDir%Resources\GrammarTools"
set outDir="%projectDir%Resources\GeneratedSources"

echo Building Grammar %grammarName%...
cd %grammarToolsDir%

echo Step 1: Validating Grammar %grammarName%...
GrammarValidator -In "%grammarDir%\%grammarName%.grxml"

echo Step 2: Compiling Grammar %grammarName%...
CompileGrammar -In "%grammarDir%\%grammarName%.grxml" -InFormat SRGS -Out "%outDir%\%grammarName%.cfg"

if not exist "%outDir%\%grammarName%.cfg" (
	echo FAILED. CompileGrammar step failed. Exiting...
	goto :eof
)

echo Step 3: Preparing Grammar %grammarName%...
PrepareGrammar -In "%outDir%\%grammarName%.cfg" -Out "%outDir%\%grammarName%.cfgpp"

if not exist "%outDir%\%grammarName%.cfgpp" (
	echo FAILED. PrepareGrammar step failed. Exiting...
	goto :eof
)

echo Copying generated source to target output folder...
copy "%outDir%\%grammarName%.cfgpp" "%targetDir%%grammarName%.cfgpp"

echo SUCCESS.