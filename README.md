# USBBackup - Das einfache Backup-Tool

USBBackup ist ein einfaches Programm, das als EXE-Datei auf einen USB-Stick, oder eine USB-Festplatte kopiert werden kann.
Ist USBBackup auf einem PC installiert, wird man beim Einstecken des USB-Sticks, von dem das Programm installiert wurde,
aufgefordert, ob man ein Backup erstellen/aktualisieren möchte.
Das Backup ist ein Differenzial-Backup, also wird ein bereits erstelltes Backuup mit neuen, noch nicht gesicherten Dateien, angereichert.
USBBackup ist hauptsächlich für technisch unaffine Nutzer gedacht, denen eine einfache Möglichkeit geboten werden soll, Backups durchzuführen.

## Installation

1. Die EXE-Datei muss auf den USB-Stick oder die USB-Festplatte kopiert werden, auf den/die das Backup erstellt werden soll.
2. USBBackup muss von dort aus gestartet werden und USBBackup zu installieren.
3. Nach der Installation startet USBBackup automatisch und fragt das erste Mal nach, ob ein Backup erstellt werden soll.

## Benutzung

1. Den USB-Stick, oder die USB-Festplatte einstecken, von dem aus USBBackup installiert wurde.
   oder rechts auf das USBBackup-Tray-Icon klicken und auf "Jetzt nach USB-Zielen suchen" klicken.
2. Auf das Popup unten rechts klicken.
3. Die Nachfrage, ob ein Backup erstellt werden soll, mit "Ja" beantworten.
4. Der Fortschritt des Backups kann über den Tooltip des Tray-Icons überprüft werden.

## Konfiguration

* USBBackup wird in das Verzeichnis "C:\ProgramData\USBBackup" installiert.
* Die Konfiguration wird über die Registrierungs-Datenbank (Registry) unter dem Schlüssel "HK_CURRENT_USER\Software\USBBackup" vorgenommen:
	* BackupFolder - Semikolon-getrennte Liste von Ordnern, die gesichert werden sollen. Nach der Installation ist dies das Nutzerverzeichnis.
	* Exclude - Semikolon-getrennte Liste von Ordner-Namen und Datei-Namen, die von der Sicherung ausgeschlossen werden sollen. Nach der Installation ist dies "AppData".
	* UniqueIdentifyer - Bei der Installation zufällig generierte GUID, die als Identifikation des korrekten USB-Mediums dient.
* Ein weiterer Schlüssel wird unter "HK_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run" angelegt, um USBBackup bei Systemstart starten zu lassen.

* Damit USBBackup ein Backup-Ziel identifiziert, muss auf dem entsprechenden USB-Gerät der Pfad X:\USBBackup\<UniqueIdentifyer> existieren.

# Vielen Dank für die Nutzung und das Interesse an USBBackup!