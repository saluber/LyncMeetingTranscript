------------------------------------------------------------------
Application Name: Lync Meeting Transcript
Application ID: 5c25bcb7-4df6-4746-8b71-740ed37ab47f
Author: Samantha Luber (samil AT microsoft.com)
Project site: https://github.com/saluber/LyncMeetingTranscript
------------------------------------------------------------------
Overview:
"Real-time transcripts of Lync meetings"

Lync is a great tool for communication and collaboration across various business units. 
It offers a wide variety of capabilities including conferencing. Lync conferences allows users to have application sharing, 
IM chats and video sharing during the meeting. But do you know whether a visually impaired person be able to see the instant messages 
that is coming across the meeting? Or will a hearing impaired person be able to hear what is discussed in the meetings? 
Or would it simply be nice to have a live-generated transcript of a current or past meeting to review what was discussed? 
Lync Meeting Transcript is created with the aim of helping Lync users to the generates real-time transcripts of Lync meetings.
------------------------------------------------------------------
Setup:
Client Application:
Installing the client appliation: http://msdn.microsoft.com/en-us/library/office/jj933101(v=office.15).aspx

Server Application:
Register server application: http://stackoverflow.com/questions/25075372/ucma-steps-to-create-trusted-application

The following table describes the two registry entries. Add the context application GUID ({5c25bcb7-4df6-4746-8b71-740ed37ab47f}) as a key under either of these two paths:
HKEY_CURRENT_USER\Software\Microsoft\Communicator\ContextPackages (CurrentUserLyncMeetingTranscriptAppRegistrationKey.reg)
HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Communicator\ContextPackages (LocalMachineLyncMeetingTranscriptAppRegistrationKey.reg)

Example:
[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Communicator\ContextPackages\{5c25bcb7-4df6-4746-8b71-740ed37ab47f}]
"Name"="Lync Meeting Transcript"

Troubleshooting Resources:
	- Registering client application to recieve messages over ConversationContext channel: http://msdn.microsoft.com/en-us/library/hh243694.aspx
------------------------------------------------------------------
Usage:
Server-side application: LyncMeetingTranscriptBotApp
	- See LyncMeetingTranscriptBotApp\README.txt
Client-side application: LyncWPFClientApplication
	- See LyncWPFClientApplication\README.txt
------------------------------------------------------------------