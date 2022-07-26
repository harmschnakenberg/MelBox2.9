# MelBox2 für .NET Framework 4.8

MelBox2 ersetzt MelBox, das auf einem externen ActiveX beruht.
Das Programm Empfängt SMS über ein GSM-Modem an COM-Port.
Nachrichten werden in einer SQLite Datenbank gespeichert und an einstellbare Empfänger weitergeleitet.
Die Empfänger werden durch einen Kalender festgelegt. Weiterleitung über SMS oder Email. 
Für gesendete SMS wird die Sendungsverfolgung in der Datenbank dokumentiert.

Bedienung (Einstellungen Empfänger & Empfangszeiten) über eine Web-Oberfläche.

EINRICHTEN FÜR INTRANET:
-	Die url des localhosts per Powershell freigeben:
	URL-Reservierung:
	netsh http add urlacl url=http://+:1234/ user=schnakenbUrg

-	Firewall TCP-Port öffnen --> Eingehande Regeln -> 
									- Protokolle und Ports	--> Lokaler Port : TCP 1234
									- Programme und Dienste --> Alle Programme - sonst gings bei mir nicht. Fehler?
									- Optional: Bereich Lokale IP-Adresse : 192.168.xxx.0/24

FRAGEN:
-	Welches Characterset verwenden? UCS2 (unicode, 140 Zeichen) für Umlaute oder GSM-Characterset (ASCII + Spezial, 160 Zeichen) für längere Nachrichten?
	-> Standard ist Characterset GSM. Bei Sonderzeichen in SMS schaltet das Modem selbsttätig auf UCS2. Ist im Programm abgefangen. Muss die Praxis zeigen.

TODO: 
-	Automatische Rufnummern-Link in Browser unterdrücken? 
		Wenn ja im Header ergänzen:
	    <meta name="format-detection" content="telephone=no">


PRAXISTEST:
-	DeliveryCode prüfen: Siehe GSM 03.40 section 9.2.3.15 (TP-Status) Seite 51
		- Statusreport für gesendete SMS: Wird immer der passende Deleviery-Code bei Fehlern/Verzögerung angezeigt?

-	Rufumleitung:	Autom. Rufumleitung zu Bereitschaft in der Praxis testen! 					