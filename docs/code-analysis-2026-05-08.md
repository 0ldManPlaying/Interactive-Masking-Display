# Code-analyse — InteractiveMask v1.1.1

Datum: 2026-05-08
Scope: volledige codebase (Display, Gdk, Ipc, WebHost), focus op leaks, races, loops en efficiëntie.

## Samenvatting

10 bevindingen waarvan 3 kritiek, 4 hoog, 3 middel. De drie kritieke kunnen onder load tot een crash of een security-gat leiden. De rest zijn netheid en kleine resource-leaks.

| # | Categorie | Onderwerp | Prio |
|---|---|---|---|
| 1 | Race | `CameraTile.HandleFrame` leest buffers buiten lock; kan disposed pointer dereferencen | CRITICAL |
| 2 | Race | `NvrSession._tilesByCamera` is een gewone `Dictionary` maar wordt vanuit GDK-callbackthread én UI-thread benaderd | CRITICAL |
| 3 | Security | `WebAccessSessionStore.RevokeAll()` wordt nooit aangeroepen als admin de access-modus of PIN wijzigt | CRITICAL |
| 4 | Leak | `TileViewModel` abonneert zich op `Strings.Instance.PropertyChanged` met anonieme lambda; nooit afgemeld | HIGH |
| 5 | Loop | `IpcServer.AcceptLoop` `catch`-blokken `continue` ook bij cancellation; theoretisch blijft de loop draaien | HIGH |
| 6 | Performance | Razor pages alloceren `JsonSerializerOptions` per request | HIGH |
| 7 | Race | `IpcCommandSender.Attach` heeft geen guard tegen dubbele aanroep | HIGH |
| 8 | Leak | `IpcClient._cts` en `NvrSession._reconnectCts` worden niet altijd disposed | MEDIUM |
| 9 | Performance | `AuditLog.Write` doet `File.AppendAllText` (open/write/close) per call, op UI-thread | MEDIUM |
| 10 | Hygiene | `WebAccessSessionStore` heeft geen periodieke sweep van verlopen tokens | MEDIUM |

---

## Bevindingen in detail

### 1. CRITICAL — `CameraTile.HandleFrame` race op de buffer-pointer

`HandleFrame` neemt `_bufferLock` om de buffers te (re-)alloceren, geeft de lock direct vrij en gebruikt `_yv12Buffer` daarna in `decompress` en `picture_scale`. Een gelijktijdige `Dispose()` kan tussen het verlaten van de lock en het gebruik in `decoder.decompress` de buffer al gefreed hebben. Resultaat: native code schrijft naar gerecycled geheugen → potentiële crash.

```csharp
lock (_bufferLock) {
    if (_disposed) return;
    EnsureBufferLocked(...);   // alloc als nodig
}                              // lock vrij
// HIER: Dispose() kan _yv12Buffer freeen
var param = new G2DECODER_VIDEO_PARAM_V3 {
    _buf = _yv12Buffer,        // race read
    ...
};
decoder.decompress(ref param, ...);   // schrijft naar evt. freed memory
```

Realistisch wanneer: `NvrSession.UpdateCameras` verwijdert een camera, ondertussen levert de GDK nog een laatste frame voor diezelfde camera-index af. De v1.1.0 live-apply maakt dit pad bereikbaar tijdens runtime.

**Fix:** verleng de scope van `_bufferLock` tot het einde van het decode-pad. Dispose gebruikt dezelfde lock en wacht dus tot een lopende decode klaar is.

### 2. CRITICAL — Concurrent toegang tot `NvrSession._tilesByCamera`

`_tilesByCamera` is een gewone `Dictionary<int, CameraTile>`. Wordt geschreven door `RegisterTile` (UI-thread) en `UpdateCameras` (UI-thread), gelezen door `on_g2watch_receive_frame_data` (GDK-callbackthread). Lezen uit een dictionary die geconcurrent gewijzigd wordt is officieel UB; in de praktijk doet .NET dat meestal goed maar het is geen garantie.

**Fix:** vervang door `ConcurrentDictionary<int, CameraTile>`. Lookup blijft atomair, removes worden lock-vrij.

### 3. CRITICAL — Web-access sessies blijven geldig na admin-wijziging

`WebAccessSessionStore.RevokeAll()` bestaat maar wordt nergens aangeroepen. Als de admin in Setup van mode `off` naar `pin` schakelt, of de toegangs-PIN wijzigt, blijven oude sessies geldig (sliding 8 uur TTL). Aanvallers met een gestolen cookie kunnen na een PIN-rotatie nog ingelogd blijven.

**Fix:** `WebSettingsProvider` vuurt een `AccessChanged` event af zodra `AccessMode`, `AccessPin` of `AccessDomain` op disk wijzigt; in `Program.cs` koppelen we dat aan `WebAccessSessionStore.RevokeAll()`.

### 4. HIGH — `TileViewModel` lekt static event-subscription

```csharp
public TileViewModel(int slotIndex, Dispatcher dispatcher)
{
    ...
    Strings.Instance.PropertyChanged += (_, _) => RefreshLanguageStrings();
}
```

Anonieme lambda is niet meer afmeldbaar. Bij een grid-resize (1×1 → 4×4) worden 1 oude + 16 nieuwe TileViewModels aangemaakt; de oude blijven gerefereerd vanuit het static singleton-event en worden nooit GC'd.

**Fix:** sla de handler op als veld en meld 'm af in een `Dispose()`-methode. `DisplayViewModel.InitializeGrid` roept deze aan voordat `Tiles.Clear()` wordt uitgevoerd.

### 5. HIGH — `IpcServer.AcceptLoop` zwakt cancellation af

```csharp
catch
{
    server.Dispose();
    continue;
}
```

Als de exception een cancellation was (bijv. tijdens `Dispose()`), draait de while-loop nog een ronde verder. De volgende `new NamedPipeServerStream(...)` slaagt mogelijk en blijft hangen op `WaitForConnectionAsync`. De cancel-token in dat call cancelt 'm wel meteen, dus de loop eindigt eventueel — maar het patroon vertraagt shutdown onnodig.

**Fix:** vang `OperationCanceledException` apart en `return`; alleen voor andere exceptions `continue`.

### 6. HIGH — Razor pages alloceren JsonSerializerOptions per request

```csharp
window.__t = @Html.Raw(JsonSerializer.Serialize(Model.T, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
```

`JsonSerializerOptions` is duur om aan te maken (reflection-cache). Eens per request × 1000 polls = nodeloze CPU + GC.

**Fix:** statisch shared instance in `WebJson.CamelCase`.

### 7. HIGH — `IpcCommandSender.Attach` zonder dubbele-aanroep guard

Twee aanroepen abonneren tweemaal op `client.MessageReceived` → elk antwoord wordt twee keer verwerkt. Eerste `TryRemove` slaagt, tweede is no-op, maar het is een latente bug.

**Fix:** throw bij `_client != null`.

### 8. MEDIUM — CTS handles niet gedisposed

- `IpcClient._cts` wordt gecanceld maar nooit gedisposed.
- `NvrSession._reconnectCts` wordt vervangen zonder de oude te disposen (alleen Cancel).

CTS dispose is goedkoop maar consistent vrijgeven van handles voorkomt finalizer-druk.

**Fix:** `_cts.Dispose()` na `_cts.Cancel()` in beide klassen.

### 9. MEDIUM — `AuditLog.Write` doet sync file-IO op UI-thread

`File.AppendAllText` opent, schrijft en sluit het bestand bij elke event. Op UI-thread. Bij bv. langzame disk + virusscan geeft dit een merkbare hapering bij elke privacy-actie. In de praktijk gebeurt dat zelden maar het ontwerp leunt op disks die altijd snel zijn.

**Fix (optioneel):** lange-levende `StreamWriter` open in append-mode, flush per write. Op shutdown sluiten.

Voor nu: laat staan — privacy-acties zijn seconden uit elkaar, niet ms.

### 10. MEDIUM — `WebAccessSessionStore` heeft geen periodieke sweep

Verlopen tokens worden alleen verwijderd als ze opnieuw aangeboden worden. Tokens die niemand meer gebruikt blijven in de dictionary tot service-restart. Linear groei over weken.

**Fix:** een eenvoudige timer die elke uur expired entries verwijdert. Klein.

---

## Toegepaste fixes

In dit codeschrijf-rondje:

- ✅ Bevinding 1: lock-scope verlengd in `CameraTile.HandleFrame`
- ✅ Bevinding 2: `_tilesByCamera` → `ConcurrentDictionary`
- ✅ Bevinding 3: `WebSettingsProvider.AccessChanged` event + Program.cs koppeling
- ✅ Bevinding 4: `TileViewModel` met `Dispose()` + `DisplayViewModel.InitializeGrid` cleanup
- ✅ Bevinding 5: `IpcServer.AcceptLoop` vangt cancellation apart af
- ✅ Bevinding 6: `WebJson.CamelCase` static + Razor pages refactor
- ✅ Bevinding 7: `IpcCommandSender.Attach` dubbele-aanroep guard
- ✅ Bevinding 8: CTS `Dispose` calls toegevoegd
- ⏭ Bevinding 9: niet aangepast (afweging waard, niet kritiek)
- ⏭ Bevinding 10: niet aangepast (klein, kan later)

Niet-toegepast omdat het buiten dit dev-rondje valt: installer / signing / version bump / release. Volgens jouw expliciete instructie pas weer als je dat zelf vraagt.
