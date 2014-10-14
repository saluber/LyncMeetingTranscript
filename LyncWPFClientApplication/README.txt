LyncMeetingTranscript Client Application README
================================================
Application Name: Lync Meeting Transcript
Application ID: 5c25bcb7-4df6-4746-8b71-740ed37ab47f

Prerequisites (for compiling and running in Visual Studio)
- Visual Studio 2012
- Microsoft Lync SDK
- Microsoft UCMA 4.0 SDK
- Microsoft Speech 11.0 SDZK

Prerequisites (for running installed sample on client machines)
- Microsoft Lync 

================================================
Install instructions:
================================================
1. The following table describes the two registry entries. Add the context application GUID ({5c25bcb7-4df6-4746-8b71-740ed37ab47f}) as a key under either of these two paths:
HKEY_CURRENT_USER\Software\Microsoft\Communicator\ContextPackages
HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Communicator\ContextPackages

Example:
[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Communicator\ContextPackages\{5c25bcb7-4df6-4746-8b71-740ed37ab47f}]
"Name"="Lync Meeting Transcript"

Troubleshooting Resources:
	- Registering client application to recieve messages over ConversationContext channel: http://msdn.microsoft.com/en-us/library/hh243694.aspx

================================================
How to use the application:
================================================
0. Prior to started application, start Lync (client application) and login in with credentials
1. Run LyncMeetingTranscriptBotApplication.exe
2. Call app endpoint URI specified in App.config

================================================
Questions/feedback?
Please contact: SamiL AT microsoft.com
https://github.com/saluber/LyncMeetingTranscript