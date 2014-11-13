LyncMeetingTranscript Bot Application README
================================================
Application Name: Lync Meeting Transcript
Application ID: 5c25bcb7-4df6-4746-8b71-740ed37ab47f

Prerequisites (for compiling and running in Visual Studio)
- Visual Studio 2012
- Microsoft UCMA 4.0 SDK
- Microsoft Speech 11.0 SDZK

Prerequisites (for running installed sample on client machines)
- Microsoft Lync 

================================================
Install instructions:
================================================
1. The following table describes the three registry entries. Add the context application GUID ({5c25bcb7-4df6-4746-8b71-740ed37ab47f}) as a key under either of these two paths:
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
1. Run LyncMeeTtingTranscriptBotApplication.exe to start TranscriptSessionManager
2. To start a transcript recording session:
	a. Call application endpoint user URI (specified in App.config). 
		i. Note: Requires LyncMeetingTranscriptBotApp to be compiled with "CONVERSATION_DIALOUT_ENABLED" flag set (*set by default*).
	b. Invite application endpoint user URI (specified in App.config) to active conference. 
		i. Note: Requires LyncMeetingTranscriptBotApp to be compiled with "CONFERENCE_DIALOUT_ENABLED" flag set (*set by default*).
	c. Lync Meeting Transcript dials out to UserUri2 (specified in App.config) and starts a transcript recording session on that conversation. 
		i. Note: Requires LyncMeetingTranscriptBotApp to be compiled with "CONVERSATION_DIALIN_ENABLED" flag set
		ii. Note: UserUri2 (in App.config) must be set to a valid user Uri
	d. Lync Meeting Transcript dials out to ConferenceUri (specified in App.config) and starts a transcript recording session on that conference.
		i. Note: Requires LyncMeetingTranscriptBotApp to be compiled with "CONFERENCE_DIALIN_ENABLED" flag set
		ii. Note: ConferenceUri (in App.config) must be set to a valid conference Uri
================================================
Questions/feedback?
Please contact: SamiL AT microsoft.com
https://github.com/saluber/LyncMeetingTranscript