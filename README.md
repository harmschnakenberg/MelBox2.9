# MelBox2 für .NET Framework 4.8

MelBox2 ersetzt MelBox, das auf einem externen ActiveX beruht.
Das Programm Empfängt SMS über ein GSM-Modem an COM-Port.
Nachrichten werden in einer SQLite Datenbank gespeichert und einstellbare Empfänger weitergeleitet.
Die Empfänger werden durch einen Kalender festgelegt. Weiterleitung über SMS oder Email. 
Für gesendete SMS wird die Sendungsverfolgung in der Datenbank dokumentiert.

Bedienung (Einstellungen Empfänger & Empfangszeiten) über eine Web-Oberfläche.

TODO: 
-	Für Intranetnutzung die url des localhosts per Powershell freigeben:
	URL-Reservierung:
	netsh http add urlacl url=http://+:1234/ user=schnakenbUrg

-	DeliveryCode prüfen: Bei Sendebestätigung (STATUS-REPORT) von GSM-Modem wird GSM3.40 nicht eingehalten? 
	DeliveryCode <st> scheint nicht  GSM3.40 zu entsprechen. 
	Bei ausgeschalteten EMpfangsgerät Code 3, später Code 6 ?
