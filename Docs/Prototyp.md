# Mecha Game — Gameplay-Prototyp

Spielbarer 3D-Mecha-Prototyp: freies, flüssiges Fliegen durch eine offene Testwelt,
Schießen auf Ziele mit leichtem Aim Assist und eine Werkstatt zum Austauschen der
Mecha-Bauteile. Umgesetzt in **Unity 6 (6000.5.3f1)** mit URP und dem neuen Input System.

## Projekt starten

1. Projekt in Unity 6000.5.3f1 öffnen (erster Import kompiliert die Skripte).
2. Szene `Assets/_Game/Scenes/Game.unity` öffnen.
3. Play drücken.

Beide Szenen (`Game`, `Workshop`) sind in den Build Settings eingetragen; der
Szenenwechsel funktioniert daher auch im Editor-Playmode und im Build.

## Steuerung

**Maus + Tastatur**

| Eingabe | Aktion |
|---|---|
| W / S | vorwärts / rückwärts (in Blickrichtung) |
| A / D | seitwärts |
| Leertaste | aufsteigen |
| Strg oder Shift | sinken |
| Maus | Kamera / Zielen |
| Linke Maustaste | schießen |
| Rechte Maustaste (halten) | Energieschild |
| Tab | Werkstatt öffnen |
| Esc | Pausenmenü / Maus freigeben |

**Controller** (parallel unterstützt, aktives Gerät wird automatisch erkannt)

| Eingabe | Aktion |
|---|---|
| Linker Stick | Bewegung |
| Rechter Stick | Kamera / Zielen |
| RB / LB | aufsteigen / sinken |
| RT | schießen |
| LT (halten) | Energieschild |
| Y | Werkstatt |
| Start | Pausenmenü |
| B (in der Werkstatt) | zurück ins Testgebiet |

## Projektstruktur

```
Assets/_Game/
├── Scenes/
│   ├── Game.unity          # Testwelt (enthält nur das GameBootstrap-Objekt)
│   └── Workshop.unity      # Werkstatt (enthält nur das WorkshopBootstrap-Objekt)
├── Resources/
│   └── GameSettings.asset  # Zentrale Gameplay-Werte (im Inspector editierbar)
└── Scripts/
    ├── Core/       GameBootstrap, WorkshopBootstrap, GameSettings
    ├── Input/      InputReader (Maus/Tastatur + Gamepad, Geräteerkennung, Deadzone)
    ├── Player/     MechaController (Flug), MechaCameraRig (Third-Person-Kamera)
    ├── Combat/     HitscanWeapon, AimAssist, TargetDummy, TracerEffect, HitFlash
    ├── Mecha/      MechaParts (Komponenten-Datenmodell nach Plan, siehe
    │               MechaKomponenten.md), MechaPartLibrary (Katalog),
    │               MechaStats (Gesamtwerte/Energiebilanz),
    │               MechaLoadout (Session-/PlayerPrefs-Konfiguration),
    │               MechaAssembler (baut den Block-Mecha), MaterialCache
    ├── Workshop/   WorkshopController (Werkstatt-Logik + UI)
    └── UI/         HudController (Fadenkreuz, HUD, Pausenmenü), UiFactory
```

Beide Szenen sind bewusst fast leer: je ein Bootstrap-Skript baut Welt, Mecha,
Kamera, Ziele und UI zur Laufzeit aus Primitives auf. Dadurch ist alles im Code
nachvollziehbar und versionierbar; später können die Bootstraps Stück für Stück
durch echte Szeneninhalte/Prefabs ersetzt werden.

## Wichtige Werte anpassen

Alle zentralen Gameplay-Werte liegen in **`Assets/_Game/Resources/GameSettings.asset`**
(Klasse `GameSettings`): Fluggeschwindigkeit, Beschleunigung/Abbremsung,
Sensitivitäten, Deadzone, Kameraabstand/-glättung, Feuerrate, Schaden,
Ziel-Lebenspunkte sowie alle Aim-Assist-Parameter (Winkel, Reichweite, Stärke
getrennt für Maus/Controller, Glättung). Änderungen wirken sofort im Playmode.

Die Bauteil-Varianten (je 2–5 pro Komponente: Rumpf, Sensor, Halterungen,
Waffen, Erweiterungen, Rückenmodule, Chassis, Booster, Generator, FCS) sind
zentral in `MechaPartLibrary.cs` definiert. Das Komponenten-System folgt dem
"Mech Layout"-Plan — Details und welche Werte bereits Gameplay-Wirkung haben:
siehe [MechaKomponenten.md](MechaKomponenten.md).

## Systeme und Platzhalter

- **Mecha-Optik**: farbige Blöcke pro Bauteil (`MechaPartDef.BuildVisual`). Echte
  Modelle können später hier instanziiert werden, ohne Werkstatt/Assembler/Loadout
  anzufassen. Die Slot-Anker (Kopf, Rumpf, Arme, Beine) sitzen am `MechaAssembler`.
- **Ziele**: statische Kugeln, färben sich mit Schaden von Grün nach Rot, blitzen
  bei Treffern auf, respawnen nach 5 s. Keine KI.
- **Waffe**: zuverlässiges Hitscan-System mit Tracer, Mündungslicht und
  Treffereffekt — Platzhalter für spätere Projektil-/Partikelsysteme.
- **Energieschild**: halbtransparente, gekrümmte Scheibe vor dem Mecha
  (rechte Maustaste / LT halten). Hält maximal `shieldMaxSeconds` (3 s) am
  Stück, lädt kontinuierlich wieder auf (`shieldRechargeSeconds`); nach
  völliger Erschöpfung erst ab `shieldReactivateFraction` wieder nutzbar.
  Blockt noch keinen Schaden — es gibt noch keine Gegner, die schießen.
- **UI**: komplett in Code erzeugte uGUI (Fadenkreuz, Geschwindigkeit,
  Zielstatus, Hitmarker, Steuerungshinweis, Pausenmenü, Werkstatt-Menü).
- **Konfiguration**: bleibt über Szenenwechsel erhalten (statisch) und wird
  zusätzlich in PlayerPrefs gespeichert.
- **Hinweis für Builds**: Materialien/Shader werden zur Laufzeit per
  `Shader.Find` erzeugt. Für Standalone-Builds sollten `Universal Render
  Pipeline/Lit`, `Universal Render Pipeline/Unlit` und `Sprites/Default` unter
  *Project Settings → Graphics → Always Included Shaders* eingetragen oder durch
  Material-Assets ersetzt werden.

## Empfohlene nächste Schritte

1. Mecha-Teile auf ScriptableObjects + Prefabs umstellen (Datenmodell steht bereits).
2. Teil-Stats (Gewicht, Flugleistung …) in `MechaController` einrechnen.
3. Erste Gegner mit einfacher KI auf Basis von `TargetDummy`.
4. Waffen-Varianten (Projektile, Streuung, Overheat) über das Waffen-Interface.
5. Sound- und Partikeleffekte an den vorhandenen Effekt-Hooks.
6. Boost/Dash für mehr Flug-Dynamik.
7. Menü-/Missionsfluss (Hauptmenü, Missionsauswahl) über weitere Bootstrap-Szenen.
