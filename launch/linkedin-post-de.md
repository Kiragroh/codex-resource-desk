Warum laufen so viele Node-Prozesse, obwohl ich die zugehörigen Tools gerade gar nicht nutze?

Diese Frage war der Ausgangspunkt für Resource Desk: ein kleines, portables Windows-Tool, das ich mit Codex gebaut habe, um den Ressourcenverbrauch von Codex und seinen lokalen MCP-Integrationen besser zu verstehen und zu steuern.

Es zeigt den gesamten System-RAM, den zugeordneten privaten Codex-RAM und die Prozesszahl pro Integration. Ein Klick auf die Speicheraufteilung führt zu den Details. Unterstützte lokale MCPs lassen sich deaktivieren; ihre laufenden Prozesse kann man gezielt beenden. Deaktivierte Einträge bleiben zum späteren Einschalten sichtbar.

Eine wichtige Erkenntnis beim Bauen: „enabled = false“ bedeutet noch nicht, dass bereits laufende Prozesse beendet sind. Genau deshalb zeigt das Tool Konfiguration und laufende Prozesse getrennt an.

Die erste Version ist jetzt als Open Source auf GitHub verfügbar: native Windows-Oberfläche, portable ZIP, keine Telemetrie. Noch eine Early Preview für globale lokale MCP-Konfigurationen. Gemeinsame Codex-Helfer bleiben geschützt; ein Neustart von Codex kann nötig sein, damit laufende Sitzungen neue Einstellungen übernehmen.

Wer Codex unter Windows nutzt: Welche Integration würdet ihr als Erstes genauer anschauen?

https://github.com/Kiragroh/codex-resource-desk

Bild 1: Titelgrafik mit GPT Image. Bild 2: unveränderter Screenshot der laufenden App; die Werte sind eine Momentaufnahme meines Rechners.

Unabhängiges Projekt, nicht mit OpenAI verbunden.

#Codex #MCP #OpenSource
