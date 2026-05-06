InteractiveMask installer
=========================

Wat het doet
------------
Een per-machine MSI dat in C:\Program Files\InteractiveMask\ installeert:
  - Display\  - de WPF kiosk-applicatie
  - Service\  - de WebHost, geregistreerd als Windows-service
                "InteractiveMaskWebHost" (auto-start, LocalSystem)
Plus een firewall-regel voor TCP 8080 en een start-menu shortcut.

Bouwen
------
Vanuit de project-root:

    .\build-installer.ps1                       (Version 1.0.0)
    .\build-installer.ps1 -Version 1.2.0        (custom versie)

Eindresultaat: build\publish\InteractiveMask-<version>.msi

Vereisten
---------
- .NET SDK 9 of nieuwer
- Windows host (WiX werkt momenteel alleen op Windows)
- WixToolset.Sdk wordt automatisch via NuGet opgehaald

Installeren
-----------
Dubbelklik op de MSI of via CLI:
    msiexec /i InteractiveMask-1.0.0.msi
Stille installatie:
    msiexec /i InteractiveMask-1.0.0.msi /qn /norestart

De service start automatisch na installatie. Display.exe start je via het
start-menu (of laat IT auto-login + auto-start configureren via shell:startup).

Verwijderen
-----------
Via Windows-instellingen "Apps en onderdelen" -> InteractiveMask.
Of CLI:
    msiexec /x InteractiveMask-1.0.0.msi /qn

Beperkingen / vervolg
---------------------
- De installer maakt geen kiosk-Windows-account aan. Voor productie:
  * maak een aparte gebruiker "InteractiveMask" aan op de host
  * gebruik netplwiz om die account auto-login te geven
  * plaats een snelkoppeling naar Display.exe in shell:startup van die account
  * activeer Kiosk-modus in de Setup van de applicatie zelf
- HTTPS-certificaat voor de WebHost moet via de Setup-wizard geimporteerd
  worden (volgt in fase 10).
