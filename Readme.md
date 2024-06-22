# EmpyrionBackpackExtender

## Installation
Sie können diesen Mod direkt mit dem MOD-Manager von EWA (Empyrion Web Access) laden. <br/>
Ohne den EWA funktioniert der Mod (vermutlich) nur innerhalb des EmpyrionModHost

## Konfigurieren Sie Ihre erlaubten Backpacks
Nach der Installation und dem Start der Mods ist hier die Konfiguration abgelegt, die angepasst werden kann.
\[SaveGamePath\]\\Mods\\EmpyrionBackpackExtender\\Configuration.json

## Screenshots
![](Screenshots/Help.png)
![](Screenshots/Buy.png)
![](Screenshots/BackpackOpen.png)

## Verwendungszweck
Es gibt verschiedene Backpacks, für die alle die gleichen Kommandos gelten
* vb = persönliches Backpack
* fb = Fraktionsbackpack
* ob = Backpack für alle Mitglieder die vom selben Startplaneten kommen
* gb = globales Backpack welche für alle Spieler auf dem Server zugänglich ist

Zusammen mit einem Backslash am Anfang und im Fraktionschat sind die Kommandos also wie folgt (am Beispiel des "vb")

* \\vb help = eine Liste aller verfügbaren Kommandos und Informationen
* \\vb = öffnet das zuletzt geöffnete persönliche Backpack
* \\vb N = öffnet das N-te persönliche Backpack
* \\vb buy = kauft ein weiteres persönliches Backpack

## Superstackfunktion (wenn aktiviert)
Ein virtueller Rucksack enthält 49 "Zellen".  Aber diese Zellen sind nicht dieselben wie im Spielerinventar oder in Containern.  Wenn man einen vollen Stapel eines Gegenstands in den virtuellen Rucksack legt, nimmt er vorübergehend eine Zelle ein. Wenn du den Rucksack jedoch schließt und wieder öffnest, stellt die Zelle des virtuellen Rucksacks nun eine Zusammenfassung aller Stapel dieses Gegenstands im virtuellen Rucksack dar.  Superstapeln ist also ein anderes Wort für Zusammenfassung.  

Hier ist ein Beispiel.  

Der maximale Inventarstapel für eine Geldkarte beträgt 50.000 Credits.  Sie können 49 davon in eine leere vb legen.  Jede dieser Karten nimmt vorübergehend eine Zelle ein, und es sieht so aus, als hätten Sie die vb vollständig gefüllt.  Wenn du den Rucksack jedoch wieder öffnest, wird nur eine der Zellen als belegt angezeigt, mit einem Stapel von 50.000 * 49 Credits oder 2.450.000 Credits.  

Jetzt können Sie weitere 48 * 50K Geldkarten in dieselbe vb legen, und wenn Sie sie wieder öffnen, werden Sie feststellen, dass eine einzige Zelle mit 4.850K Credits belegt ist.  Dies ist eine Zusammenfassung des Inhalts der vb für diesen Posten.  Die vb enthält tatsächlich (49+48) Stapel von 50K Geldkarten. Wenn Sie sie also entfernen wollen, kommen sie auf diese Weise in Ihr Inventar oder Ihren Container.  

Um Exploits zu verhindern, funktioniert diese neue Superstapel-Funktion nicht bei Gegenständen, die sich im Inventar nicht stapeln lassen (wie z. B. ein Rüstungsbooster), bei Gegenständen, die verfallen, oder bei Gegenständen, die Munition verwenden (obwohl es bei der Munition selbst funktioniert).  Diese nehmen den gleichen Platz wie im Inventar ein.  Wenn du zwei 50K Gemüsestapel in deinem Inventar hast, nehmen sie zwei Zellen in der vb ein und werden nicht zusammengefasst.  

Ein weiterer Nebeneffekt dieses Systems ist, dass man mindestens eine leere Zelle in der vb haben muss, um einen neuen Stapel aus dem Inventar hinzuzufügen.

## Konfiguration
* ChatCommandPrefix = Zeichen mit dem das Chatkommando beginnen muss
* Einstellungen für die verschiedenen Backpacktypen
  * ChatCommand = Chatkommandotext
  * MaxBackpacks = Anzahl der maximal erlaubten Backpacks
  * Price = Preis für ein Backpack
  * OpenCooldownSecTimer = Zeit in der das Backpack nicht erneut geöffnet werden kann
  * AllowSuperstack = sollen die Items zusammengefasst werden (OBACHT: Nur Gegenstände ohne Munition, Verfall und mit einer Stapelgröße > 1 werden als Superstapel zusammengefasst)
  * AllowedPlayfields = Liste der Playfields auf denen das Backpack erlaubt ist (Instanznamen müssen OHNE die Nummer #nnn angegeben werden)
  * ForbiddenPlayfields = Liste der Playfields auf denen das Backpack verboten ist (Instanznamen müssen OHNE die Nummer #nnn angegeben werden)
  * HideAllowedPlayfields = Liste der AllowedPlayfields verbergen
  * HideForbiddenPlayfields = Liste der ForbiddenPlayfields verbergen
  * FilenamePattern = Speicherort und Dateiname der Backpacks
  * ForbiddenItems = { Id: 1234, Count:MaxAllowed, ItemName:"Description for Player" }
  * AllowedItems = { Id: 1234, Count:MaxAllowed, ItemName:"Description for Player" }

  Hinweis: Bei den Einschränkungen auf bestimmte Playfields braucht nur der jeweilig "einfacherer" Eintrag gefüllt zu werden


# EmpyrionBackpackExtender

## Installation
You can load this mod directly with the EWA (Empyrion Web Access) MOD Manager. <br/>
Without the EWA, the mod may only works within the EmpyrionModHost

## Configure your allowed backpacks
After the installation and the start of the mods the configuration is stored here, which can be adapted.
\[SaveGamePath\]\\Mods\\EmpyrionBackpackExtender\\Configuration.json

## Screenshots
![](Screenshots/Help.png)
![](Screenshots/Buy.png)
![](Screenshots/BackpackOpen.png)

## Usage
There are different backpacks, all of which have the same commands
* vb = personal backpack
* fb = faction backpack
* ob = Backpack for all members coming from the same starting planet
* gb = global backpack accessible to all players on the server

Together with a backslash at the beginning and in the fractional table, the commands are as follows (using the example of the "vb")

* \\vb help = a list of all available commands and information
* \\vb = opens the last opened personal backpack
* \\vb N = opens the Nth Personal Backpack
* \\vb buy = buy another personal backpack

## Superstack feature (if enabled)
There are 49 “cells” in a Virtual Backpack.  But these cells are not the same as in player inventory or containers.  If you drop a full stack of an item into the vb, it temporarily takes up a cell. However, after you close the backpack and reopen it, the vb cell now represents a summary of all stacks of that item in the vb.  So superstacking is another word for summarization.  

Here is an example.  

The max inventory stack for a cash card is 50,000 credits.  You can put 49 of these into an empty vb.  Each will temporarily take up a cell, and it will look like you have completely filled the vb.  But when you reopen the backpack, only one of the cells is shown as used with a stack of 50K * 49 credits or 2,450K credits.  

At this point, you can put an additional 48 * 50K cash cards in the same vb, and when you reopen it, you will find a single cell is used containing 4,850K credits.  This is a summary of the vb contents of that item.  The vb actually contains (49+48) stacks of 50K cash cards, so when you go to remove them, they will come out that way into your inventory or container.  

To prevents exploits, this new superstacking feature will not work for items that do not stack in your inventory (like an armor booster), items that decay, or items that use ammo (although it does work for the ammo itself).  These will take up the same space as in inventory.  If you have two 50K stacks of vegetables in your inventory, they will take up two cells in the vb and not be summarized.  

Another side effect of this system is that you need to have at least one empty cell in your vb to add a new stack from inventory.

## Configuration
* ChatCommandPrefix = Character with which the chat command must start
* Settings for the different backpack types
  * ChatCommand = chat command text
  * MaxBackpacks = number of maximum allowed backpacks
  * Price = price for a backpack
  * OpenCooldownSecTimer = Time in which the backpack can not be reopened
  * AllowSuperstack = the items should be summarized (CARE: Only items without ammo, decay and with a stack size > 1 are summarized as superstack)
  * AllowedPlayfields = List of playfields where the backpack is allowed
  * HideAllowedPlayfields = Hide list of the AllowedPlayfields (Instance names must be specified WITHOUT the number #nnn)
  * HideForbiddenPlayfields = Hide list of the ForbiddenPlayfields (Instance names must be specified WITHOUT the number #nnn)
  * ForbiddenPlayfields = List of playfields on which the backpack is prohibited
  * FilenamePattern = Location and filename of the backpacks
  * ForbiddenItems = { Id: 1234, Count:MaxAllowed, ItemName:"Description for Player" }
  * AllowedItems = { Id: 1234, Count:MaxAllowed, ItemName:"Description for Player" }

  Note: With the restrictions on certain Playfields only the respective "simpler" entry needs to be filled
