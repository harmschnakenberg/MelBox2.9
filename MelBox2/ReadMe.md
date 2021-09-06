# MelBox2 für .NET Framework 4.8

MelBox2 ersetzt MelBox, das auf einem externen ActiveX beruht.
Das Programm Empfängt SMS über ein GSM-Modem an COM-Port.
Nachrichten werden in einer SQLite Datenbank gespeichert und einstellbare Empfänger weitergeleitet.
Die Empfänger werden durch einen Kalender festgelegt. Weiterleitung über SMS oder Email. 
Für gesendete SMS wird die Sendungsverfolgung in der Datenbank dokumentiert.

Bedienung (Einstellungen Empfänger & Empfangszeiten) über eine Web-Oberfläche.

-	Welches Characterset verwenden? UCS2 (unicode, 140 Zeichen) für Umlaute oder GSM-Characterset (ASCII + Spezial, 160 Zeichen) für längere Nachrichten

TODO: 
-	Für Intranetnutzung die url des localhosts per Powershell freigeben:
	URL-Reservierung:
	netsh http add urlacl url=http://+:1234/ user=schnakenbUrg

-	Firewall TCP-Port öffnen --> Programme und Dienste --> Alle Programme - sonst gings bei mir nicht. Fehler?

-	DeliveryCode prüfen: Siehe GSM 03.40 section 9.2.3.15 (TP-Status) Seite 51

-	Rufumleitung:	Autom. Rufumleitung zu Bereitschaft testen! 
					Manuelle Möglichkeit Rufumleitung zu ändern vorsehen?