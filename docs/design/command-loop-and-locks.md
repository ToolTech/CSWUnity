# CSWUnity: loop, buffertar, builders och lås

**Diskussionsunderlag, 2026-10-26.** Beskriver command-lane i den nuvarande
arbetskopian, inklusive ändringen till inputbatch och `TryLockEdit(0)`.
Kamerans senaste renderpose presenteras som standard varje Unity-frame i den
committade scenreferensen. Valbart synkläge presenterar i stället bara den
fullbordade native-framens pose och projektion. Detta är inte ett
förslag om en ny geometribyggtråd.

Två saker måste hållas isär:

- **Vänta på ett lås:** huvudtråden står still tills någon annan släpper det.
  Normal asynkron Tick gör ett låsförsök utan väntetid och fortsätter en senare frame
  om SDK-låset är upptaget.
- **Hålla ett lås under arbete:** även ett omedelbart lyckat låsförsök kan följas
  av ett långt arbete. Synkront mesh-/texturbygge under editlåset finns kvar.
  Andra SDK-trådar kan då behöva vänta på Unity.

## 1. Färger och låstyper

| Markering | Betydelse |
| --- | --- |
| Röd | SDK:s **globala EDIT-lås**. Inte ett privat lås per buffert eller karta. |
| Lila | SDK:s **globala RENDER-lås** för native traversal/läsning. |
| Blå | Kort lokalt kölås för input, output eller managed inbox. |
| Grön | Unity-publicering utan att den här tråden håller SDK-låset. |
| Gul | Logisk avvaktan eller kvarvarande kostnad; inte automatiskt en blockerande väntan. |

`buffer.TryLockEdit(...)` går vidare till `gzNodeLock::tryLockEdit(...)`.
`GetCommand()` låser inte själv: det omgivande buffertlåset skyddar iteration,
läsning av SDK-data och frigöring av kommandots grafreferenser.

**Referensägande över trådgränsen:** native `cswCommandBuffer` och
`cswSceneCommand` måste använda SDK:ns `gzThreadSafeReference`. Dess korta
objektspecifika referenslås är inte grafens EDIT-/RENDER-lås. Inputköns mutex
skyddar inte Unitys efterföljande `Dispose` mot workerns refökning/minskning.
En gammal native implementation med vanlig `gzReference` gav tappade
referenser och native fast-fail under långkörning. Native core och bridge
måste därför byggas och konsumeras som ett matchande par med nya headers.
Trådsäkert referensägande gör inte buffertinnehåll eller SDK-noder låsfria.

## 2. Hela rundan mellan Unity och CSW

Det här diagrammet och nästa visar **standardläget**, med
`SynchronizeFrames = false`. Det valbara synkflödet beskrivs i avsnitt 4.1.

```mermaid
flowchart LR
    subgraph UNITY["Unity huvudtråd"]
        direction TB
        U0["Update<br/>input och kamerasnapshot"]
        U1["Pump<br/>poll av ägd outputbuffert"]
        U2["TryLockEdit(0)<br/>capture av begränsad slice"]
        U3["Unlock i finally<br/>sedan publicering"]
        U4["Matchad EndFrame<br/>commit av scen och kamerareferens"]
        U5["En inputbuffert<br/>Omni + Position + Settings + LOD + Refresh"]
        U6["LateUpdate<br/>senaste pose i committad referens + PostPresent"]
        U0 --> U1 --> U2 --> U3 --> U4 --> U5
        U3 --> U6
    end
    subgraph NATIVE["CSW-worker"]
        direction TB
        N0["Inputkö + en trigger<br/>AddCommandBuffer(buffer, true)"]
        N1["Vänta på inputevent<br/>inget SDK-lås under eventväntan"]
        N2["Poppa input<br/>pausa kölåset under bearbetning"]
        N3["Applicera kamera/settings<br/>SDK EDIT i respektive operation"]
        N4["Refresh: traversal och LOD<br/>SDK RENDER"]
        N5["Outputkö och dispatch<br/>pausa kölåset under callback"]
        N6["processCommandBufferOut<br/>SDK EDIT runt callback"]
        N0 --> N1 --> N2 --> N3 --> N4 --> N5 --> N6
        N6 --> N1
    end
    subgraph HANDOFF["Callback och managed inbox"]
        I0["Behåll den ägda bufferten<br/>kort inboxlås, ingen Unity-API"]
    end
    U5 --> N0
    N6 --> I0
    I0 -->|"senare Update"| U1
    classDef edit fill:#ffe8e8,stroke:#c62828,color:#111;
    classDef render fill:#efe5ff,stroke:#7c3aed,color:#111;
    classDef queue fill:#e6efff,stroke:#2563eb,color:#111;
    classDef publish fill:#e5f7eb,stroke:#15803d,color:#111;
    classDef pending fill:#fff4cc,stroke:#a16207,color:#111;
    class U2,N3,N6 edit;
    class N4 render;
    class U1,N0,N2,N5,I0 queue;
    class U3,U4,U6 publish;
    class N1 pending;
```

Diagrammet visar normalfallet där `EndFrame` nås. Om den inte nås behålls
`RefreshOutstanding`: Unity lämnar tillbaka kontrollen till spelarloopen utan
att skicka ytterligare en refresh.

Native laddartrådar kan också producera New/Update/Delete-arbete till
SceneManagers outputkö. I den undersökta native implementationen köar
`addCommandBufferOut()` bara resultatet; den metoden triggar inte inputeventet.
Workerloopen behandlar input och därefter output. Refreshtriggningen är därför
också viktig för det fortlöpande utbytet av laddarresultat.

## 3. Vad en Unity-Tick faktiskt gör

```mermaid
flowchart TD
    T0["UpdateFlightInput om aktiverad<br/>Camera.Sample om pose finns"]
    T1["MapManager.Tick<br/>kontrollera session, tråd och generation"]
    B{"Kommandobudget och<br/>tidsbudget kvar?"}
    Q{"Aktuell buffert eller<br/>TryDequeue från inbox?"}
    L{"buffer.TryLockEdit(0)<br/>lyckas direkt?"}
    SKIP["Behåll buffert och läsposition<br/>avsluta Pump utan SDK-väntan"]
    C["GetCommand + Capture<br/>kopiera värden / bygg resurser<br/>Dispose command under EDIT"]
    MORE{"Fler oberoende framevärden<br/>och budget kvar?"}
    CLOSE["Om bufferten är tom:<br/>Dispose medan EDIT hålls"]
    UNLOCK["Släpp SDK-låset i finally"]
    PUB["Publicera fångade ändringar<br/>utan SDK-lås"]
    EF{"Matchad EndFrame?"}
    COMMIT["Applicera giltiga transformändringar<br/>sedan aktiveringar<br/>CommitFrame: scen och kamerareferens"]
    R{"Ingen refresh outstanding<br/>CRS konfigurerat<br/>och kamerasnapshot finns?"}
    SEND["Bygg en value-only inputbuffert<br/>Omni, Position, Settings, LOD, Refresh<br/>en native trigger"]
    DONE["Returnera från Tick<br/>LateUpdate: live renderpose + PostPresent"]
    FAIL["Fel: rapportera och stoppa sessionen<br/>separat synkron livscykelväg"]
    T0 --> T1 --> B
    B -->|Ja| Q
    B -->|Nej| R
    Q -->|Ja| L
    Q -->|Nej| R
    L -->|Nej| SKIP --> R
    L -->|Ja| C --> CLOSE --> MORE
    MORE -->|Ja| C
    MORE -->|Nej| UNLOCK --> PUB --> EF
    EF -->|Ja| COMMIT --> B
    EF -->|Nej| B
    R -->|Ja| SEND --> DONE
    R -->|Nej| DONE
    C -.->|Exception: unlock i finally| FAIL
    PUB -.->|Fel eller fatal respons| FAIL
    classDef edit fill:#ffe8e8,stroke:#c62828,color:#111;
    classDef queue fill:#e6efff,stroke:#2563eb,color:#111;
    classDef publish fill:#e5f7eb,stroke:#15803d,color:#111;
    classDef pending fill:#fff4cc,stroke:#a16207,color:#111;
    class L,C,CLOSE,MORE edit;
    class Q,SEND queue;
    class UNLOCK,PUB,COMMIT publish;
    class SKIP,R,FAIL pending;
```

**Ordningsbarriärer:** New, full Update, Delete och GeoInfo publiceras innan
nästa beroende kommando fångas. Annars kan till exempel ett barns Capture
försöka använda en förälder som ännu inte registrerats. `EndFrame` är också en
barriär. Transform/Activation inom en frame kan däremot fångas tillsammans,
eftersom deras synliga ändringar ändå väntar på framecommit. Utanför en frame
publiceras dessa direkt mellan kommandona.

**Budgeten är inte en hård tidsgräns.** Ett påbörjat mesh-/texturbygge avbryts
inte när budgeten går ut. Den kontrolleras mellan kommandon, och atomisk
framecommit kan också kosta mer än den återstående budgeten.

## 4. Samma buffert över flera Unity-frames

```mermaid
sequenceDiagram
    autonumber
    participant U as Unity huvudtråd
    participant B as Ägd outputbuffert
    participant N as CSW-worker
    Note over U,N: En refresh är outstanding tills matchad EndFrame konsumerats
    U->>B: Tick A: TryLockEdit(0) lyckas
    B-->>U: StartFrame och några transformvärden
    U->>B: Släpp EDIT efter capture-slicen
    Note over U: Publicera staging, ingen framecommit ännu
    U->>B: Tick B: TryLockEdit(0) misslyckas
    Note over U: Ingen SDK-väntan, behåll läspositionen
    U->>B: Tick C: TryLockEdit(0) lyckas
    B-->>U: Återstående aktiveringar och EndFrame
    U->>B: Dispose tom buffert under EDIT, sedan unlock
    U->>U: Publicera matchad frame atomiskt
    Note over U: RefreshOutstanding nollställs
    U->>N: Nästa kompletta inputbatch, en trigger
    Note over N: Inputeventet väcker workerloopen
```

Det är **protokollstyrd backpressure**, inte att Unity gör `Wait()` på en frame.
Nativeworkern behöver inte få ett separat ackkommando: nästa refresh är det
fortsatta arbetet. Under tiden fortsätter Unity rendera senast committade
scen med den senaste kamerarotationen och förflyttningen. Input får inte vänta
på native `EndFrame`; en äldre frame får inte heller skriva tillbaka en äldre
renderpose.

### Renderpose och ROI-referens

ROI-transformer, terrängmetadata och shaderorigo ligger kvar i samma committade
native referens. `CameraBridge.PresentPose` sätter kamerans tillfälliga
Unity-position till `Axes.ToUnityPosition(liveGlobalPosition, committedGlobalPosition)`.
Double-positionerna subtraheras **före** floatkonverteringen. Kameran står
alltså i noll när de två positionerna är lika, inte under all löpande flygning.

```mermaid
flowchart LR
    INPUT["Senaste double-position L<br/>och senaste rotation"] --> LIVE["LateUpdate: PresentPose<br/>kameraposition B gånger L minus R"]
    OLD["Committad referens R"] --> LIVE
    OLD --> SCENE["ROI-transformer, terräng<br/>och shaderorigo i R"]
    LIVE --> VIEW["En konsekvent renderad vy"]
    SCENE --> VIEW
    FENCE["Matchad EndFrame<br/>ny referens R2"] --> REBASE["Applicera native ROI-transformer<br/>och flytta kamera till B gånger L minus R2"]
    REBASE --> SHADER["Commit av shaderorigo i R2"]
    SHADER --> VIEW
```

`B` är den befintliga Gizmo-till-Unity-basen, med en enda Z-reflektion.
Kameraoffseten är vyekvivalent med en gemensam korrektion på alla ROI-toppar,
men ändrar inte varje lövs transform eller terrängens texturreferens.
Vid nästa native commit byts ROI-transformer och kamerareferens tillsammans;
rörelsen får inte läggas till en gång till. Ingen SDK-graf läses och inget
SDK-lås tas av `PresentPose`. Kartbyte nollställer den gamla kamerareferensen.

Ägandet av bufferten håller kommandoreferenser vid liv. Det gör inte levande
SDK-noder låsfria att läsa. Nativevärden fångas fortfarande under korrekt
editaccess, medan publiceringen använder kopierade värden och ägda resurser.

### 4.1 Omni/frustum och fullbordad frame före rendering

De två valen är oberoende:

| Inställning | `false` | `true` |
| --- | --- | --- |
| `OmniView` | Native traversal använder kamerans frustum. | Native traversal och LOD-val åt alla håll, som CSWUnreals Omni-val. Unity gör fortfarande sin egen renderculling. |
| `SynchronizeFrames` | Budgeterad pump i Update; senaste önskade pose kan ligga före native-framen. | Pump och snapshot i LateUpdate; matchad frame fullbordas och dess pose/projektion presenteras. |

Båda ligger i värdens `Settings` och kan ändras i runtime. `OmniView` är
**true som standard**, som i CSWUnreal; `SynchronizeFrames` är false.

Med frustumtraversal laddar och aktiverar native bara kartan inom
SceneManager-kamerans frustum. Mätt på Stensholm före borttagningen av
legacy-lanet (150 m höjd, 60 °/s sväng, observatör ovanifrån med 1000 m radie
runt kameran):

| Lane | Läge | Huvudvy, max hålpixlar | Observatör, max hålpixlar |
| --- | --- | --- | --- |
| Legacy | Frustum | 0 | ~51 000 |
| Command | Frustum | ~53 000 vid sväng, ~30 % av marken | ~52 000 |
| Legacy och command | Omni | 0 | 0 |

Det borttagna legacy-lanet traverserade synkront i samma frame och fyllde
därför huvudvyn, men kartan bakom och bredvid kameran saknades ändå i
Scene-vyn och i andra kameror. Command lane är asynkron: den presenterade
kameran ligger före native-framen, så en sväng visar områden som ännu inte
aktiverats. Omni gav 0 hål även vid 360 °/s.
Omni kostar mer traversal-/byggarbete (~5 100 → ~7 800 meshar vid start) och
ersätter inte synkronisering.

```mermaid
flowchart TD
    INPUT["Update<br/>uppdatera önskad pose, pumpa inte scenen"]
    SAMPLE["LateUpdate<br/>snapshot efter normal Update-input"]
    OLD{"Äldre refresh outstanding?"}
    DRAIN["Fullborda den äldre framen först<br/>samma låsfria väntemekanism"]
    SEND["Omni + Position + Settings + LOD + Refresh<br/>en färdig batch och en trigger"]
    PUMP["Pumpa nästa begränsade slice<br/>TryLockEdit(0), capture, unlock, publicera"]
    FENCE{"Matchad EndFrame fullbordad?"}
    WAIT["Ingen framdrift: invänta inboxaktivitet<br/>monitorn släpps, inget SDK-lås hålls<br/>upptaget graflås provas igen efter högst 1 ms"]
    COMMIT["Scen, ROI, aktiveringar och kamerareferens<br/>committas tillsammans"]
    PRESENT["Presentera committad pose och projektion<br/>PostPresent, sedan Unity-rendering"]
    FAIL["Deadline eller fel<br/>rapportera, stoppa sessionen och töm scenen"]
    INPUT --> SAMPLE --> OLD
    OLD -->|Ja| DRAIN --> SEND
    OLD -->|Nej| SEND
    SEND --> PUMP --> FENCE
    FENCE -->|Nej| WAIT --> PUMP
    FENCE -->|Ja| COMMIT --> PRESENT
    DRAIN -.->|Timeout eller fel| FAIL
    PUMP -.->|Timeout eller fel| FAIL
    classDef edit fill:#ffe8e8,stroke:#c62828,color:#111;
    classDef queue fill:#e6efff,stroke:#2563eb,color:#111;
    classDef publish fill:#e5f7eb,stroke:#15803d,color:#111;
    classDef pending fill:#fff4cc,stroke:#a16207,color:#111;
    class PUMP edit;
    class SEND queue;
    class COMMIT,PRESENT publish;
    class OLD,DRAIN,FENCE,WAIT,FAIL pending;
```

Slicarnas låsgränser är oförändrade, men flera slicar körs i **samma**
Unity-frame. Den vanliga totalbudgeten får alltså överskridas. Ingen ny
refresh skickas innan den äldre är fullbordad. Pumpen stannar vid målets
`EndFrame`, även om senare kommandon redan finns i bufferten.
`FrameWaitTimeout` på managern är fem sekunder som standard. Deadline kontrolleras
mellan operationer; ett enskilt bygge och livscykelcleanup kan inte avbrytas mitt
i arbetet. En timeout rapporteras som fel och tömmer presentationen, inte som
en framgångsrik partiell frame.

Kameran står i noll relativt den committade globala referensen och använder
just den framens rotation och perspektivprojektion. Ny input lagras separat.
Före vald kameras rendering återappliceras samma pose/projektion, både i
Built-in och vid SRP:s begin-camera-render. Ett manuellt renderanrop före
LateUpdate visar därför föregående **fullbordade** frame. Renderhooken skickar
ingen extra refresh och tar inget SDK-graflås. Avstängning av synkläget,
kartbyte och stopp släpper projektionens tillfälliga override.

Detta löser skillnaden mellan renderfrustum och den frame vars aktiveringar
har publicerats. **EndFrame betyder inte att alla asynkrona kartladdningar är
klara.** Synkläget kan ge extra latens eller bildtidssteg; det är ingen
60-fps-garanti. Se även [integrationsguidens inställningar](../howto/integrate-into-host-project.md#choose-native-coverage-and-frame-synchronization-independently).

## 5. Geometri: används samma builders och factories?

**Delvis, inte samma fullständiga byggkontrakt.**

```mermaid
flowchart LR
    subgraph OLD["Borttagna Unity-vägen (historik)"]
        direction TB
        O0["NodeBuilder / CanBuild"]
        O1["Builder.Build<br/>NodeHandle + aktiv State"]
        O2["GeometryHelper + builderns statehook<br/>komponenter och material"]
        O0 --> O1 --> O2
    end
    subgraph CURRENT["Nuvarande command-lane"]
        direction TB
        C0["Samma builderkomponenter<br/>CanBuild väljer builder"]
        C1["SceneResources.Capture<br/>GeometryHelper + StateHelper<br/>synkront under SDK EDIT"]
        C2["UnityScene.Create / Update<br/>resursleases och hierarki"]
        C3["builder.CreateSceneMaterial<br/>materialmall, fallback, texturer, färg"]
        C4["UnityScene.BindResources<br/>MeshFilter, MeshRenderer, lager"]
        C0 --> C1 --> C2 --> C3 --> C4
    end
    classDef edit fill:#ffe8e8,stroke:#c62828,color:#111;
    classDef publish fill:#e5f7eb,stroke:#15803d,color:#111;
    class C0,C1 edit;
    class C2,C3,C4 publish;
```

- **Återanvänds:** builderval via `CanBuild`, intersectionmask, featuretyp,
  lager, material- och fallbackmallar samt de gemensamma exporthjälparna.
- **Borttagna med legacy-lanet:** gamla `Build(...)`,
  `CreateStateNodeResources`, `CreateMaterialFromState`, `InitPoolObject`,
  `BuiltObjectReturnedToPool` och `Priority`. Egna overrides av dessa har ingen
  motsvarighet i command-vägen. Pumpens budget styrs av värdens
  `MaximumCommandsPerTick` och `Settings.MaxBuildTime`.

CSWUnreal har en registrerad factorykedja per native nodtyp med bland annat
`preBuildReference`, `updateReference`, `newObject` och enginekomponentens
`build/update/destroy`. Den fullständiga motsvarigheten finns **inte** i den här
Unity-adaptern. Att återställa ett gemensamt utbyggbart builder-/factorykontrakt
är en separat arkitekturfråga, inte något låsändringen redan löser.

Resurspubliceringen använder index över förekomster som delar meshkälla eller
statekälla. En uppdaterad delad textur inkluderar även berörda statekällor.
Orelaterade scenförekomster skannas inte vid varje geometripublicering.
Alla berörda konsumenter avnotifieras före generationsbytet, ersättningsleases
binds innan gamla resurser pensioneras och poolning tar bort indexmedlemskapet.

## 6. Exakta låsgränser och kvarvarande diskussionspunkter

| Moment | SDK-lås på anropande tråd | Viktig gräns |
| --- | --- | --- |
| Köa Omni/kamera/settings/LOD/refresh | Inget | Kort native inputkölås och objektspecifik referenssynkronisering; bufferten är färdig före triggern. |
| Behålla/frigöra buffer-/kommandoreferens | Inget | `gzThreadSafeReference` använder ett objektspecifikt mutex, inte grafens globala lås. Grafbärande innehåll kräver separat access. |
| Native väntar på inputevent | Inget | Worker väntar, inte Unity-huvudtråden. |
| Native kameramutation | EDIT | I respektive native operation efter att inputkölåset pausats. |
| Native traversal / frameproduktion | RENDER | Släpps innan framebufferten köas för output. |
| Outputcallback | EDIT, lånat från avsändaren | Bara behåll/enqueue; inget Unity-bygge. |
| Pollning av managed inbox | Inget eget SDK-lås | Kort lokalt inboxlås; släpps före SDK-låsförsöket. |
| Iterera och fånga output | EDIT via `TryLockEdit(0)` | Ingen väntetid; ingen låsning över Unity-framegränsen. |
| Synkront resursbygge i Capture | EDIT | Mesh-export, MeshData/meshbygge och state-/texturarbete kan göra låshållningen lång. |
| Hierarki, bindning, materialinstans och framecommit | Inget | Sker efter capture-slicens unlock. |
| Kamerans renderpose i LateUpdate | Inget | Asynkront: senaste pose relativt committad referens. Synkront: bara committad pose/projektion; före PostPresent. |
| Synklägets väntan på frame/inbox | Inget | Monitor.Wait släpper inboxlåset; ingen SDK-access hålls under väntan. |
| Frigöra tom outputbuffert | Redan hållet EDIT | Native `EDIT_LOCK`-cleanup behöver inte förvärva låset på nytt efter unlock. |
| Startup, byte av karta, fault och stop | Kan vänta | Native Shutdown utanför SDK-lås, sedan ägd cleanup under editaccess. |

Att diskutera med bilderna framför oss:

1. Var går den minsta säkra gränsen mellan native Capture och Unity-bygge?
   `TryLockEdit(0)` tar bort huvudtrådens låsväntan men kortar inte automatiskt
   själva mesh-/texturarbetet under låset.
2. Vilket builder-/factorykontrakt ska Unity-vägen erbjuda, inklusive egna
   builders och deras livscykel?
3. Hur mycket arbete ska en slice innehålla innan andra SDK-trådar släpps fram?
   Kommandobudget, tidsbudget och kostnaden för ett enskilt bygge är olika saker.
4. Ska alla outputkategorier använda editaccess? Unity-pumpen gör det i dag.
   CSWUnreal behandlar rena felbuffertar utan SDK-lås.

Dokumentet beskriver ingen ny cullingfunktion, ingen ny geometribyggtråd och
lovar inte en generell bildfrekvensvinst. Profiler-markörerna `CSW-Tick`,
`CSW-Capture`, `CSW-Publish`, `CSW-PublishResources` och `CSW-CommitFrame`
skiljer pumpens kostnader från rendering och avsiktlig FPS-väntan.

## 7. Unitys bildväntan ligger utanför SDK-loopen

```mermaid
flowchart LR
    W["Unity TimeUpdate<br/>WaitForTargetFPS<br/>WaitForLastPresentation / DXGI.WaitOnSwapChain"]
    U["Update<br/>input och CSW-Tick"]
    L["LateUpdate<br/>senaste pose och PostPresent"]
    R["Culling, rendering och Present"]
    W --> U --> L --> R --> W
    classDef sdk fill:#ffe8e8,stroke:#c62828,color:#111;
    classDef engine fill:#e8f0ff,stroke:#2563eb,color:#111;
    classDef present fill:#e5f7eb,stroke:#15803d,color:#111;
    class U sdk;
    class W,R engine;
    class L present;
```

Ett långt bildintervall är inte automatiskt ett långt `CSW-Tick`.
Full CPU-profilering kan till exempel placera väntan i
`TimeUpdate.WaitForLastPresentationAndUpdateTime` och
`DXGI.WaitOnSwapChain`, även med VSync av. Det är en annan väntmekanism än
GizmoSDK:s EDIT-/RENDER-lås. Inget graflås från pumpen hålls genom nästa
Unity-frame eller dess presentationsväntan.

VSync, applikationens FPS-begränsning och Windows swapchain-modell måste därför
rapporteras tillsammans med scen-/byggkostnaderna. En okapad körning visar
arbetsmarginal men bevisar inte bildkadensen på skärmen. Applikationen äger
dessa presentationsinställningar; command-lane ändrar dem inte.
VSync på är inte heller ett bevis för att enstaka långa presentationsväntor
har försvunnit; varje sådan körning måste fortfarande mätas.

En callback från en fönsterinsamling är inte heller ett bevis för en ny visad
bild. En sådan observatör måste kontrolleras med monotona bild-ID:n, inklusive
en fristående kontroll utan CSW. Tappade, upprepade eller bakåtgående ID:n
ska redovisas, inte sorteras bort. Samma avvikelse i native kontrollprogram
och Unity räcker inte för att tillskriva felet SDK-loopen eller för att
godkänna fysisk skärmkadens.

### Fångad presentationsväntan: signal, inte huvudtrådstimeout

En ny körning med den shutdown-säkra SDK-kohorten fångade en verklig
512,924-ms-frame under ren rotation på fast position, utan debugger.
Råprofilen och processlokal Win32-loggning visar samma intervall:

```mermaid
sequenceDiagram
    participant Main as Unitys huvudtråd
    participant Win32 as Windows-väntan
    participant Render as Unitys rendertråd
    participant SDK as CSW-Tick
    Main->>Win32: WaitForMultipleObjectsEx<br/>2 handles, INFINITE, alertable
    Note over Main,Win32: DXGI.WaitOnSwapChain: 511,110 ms
    Note over Render: Väntar på kommandon från huvudtråden<br/>511,480 ms
    Win32-->>Main: WAIT_OBJECT_0 efter 511,088 ms
    Main->>SDK: Tick: 0,056 ms
    Note over Main,SDK: Unity-pumpen håller inget SDK-graflås över presentationsväntan
```

Det är alltså inte belagt att huvudtråden gör en 500-ms-timeout. Anropets
timeout är `INFINITE` och resultatet är signal, inte `WAIT_TIMEOUT`.
Rendertrådens långa frame är inte heller liktydig med tung rendering:
nästan hela intervallet ligger i `Gfx.WaitForGfxCommandsFromMainThread`.
Vilken komponent som fördröjer presentationssignalen är ännu inte fastställt.

Matchande publika Unity-symboler identifierar anropsstället som
`SwapChainWaitingContext::WaitOnSwapChain+0xf1`. Läsning av samma oförändrade
UnityPlayer-binary och kontroll mot installerad DXGI-ABI visar att första
handle kommer från `IDXGISwapChain2::GetFrameLatencyWaitableObject`.
`WAIT_OBJECT_0` väljer alltså **DXGIs frame-latency-event**, inte det andra
event som Unity använder för att avbryta väntan. Det identifierar eventet,
men förklarar ännu inte varför signalen kommer sent.

Den privata observatören ändrar inte väntans argument eller returvärden,
behåller `GetLastError` och skriver först efter mätscenariot. Den laddas
från den privata testproben; Unitys exe, UnityPlayer och SDK-/CSW-DLL:erna
är oförändrade. Loggens täckning är UnityPlayers fem importerade Win32-
väntfunktioner, inte alla väntmekanismer i Windows eller drivrutinen.

Vid denna fångst gav inputskrivbordet `ERROR_ACCESS_DENIED`; Windows egen
sessionsfråga rapporterade låst session. Separata flip-kontroller där Win32
rapporterade synligt fönster utan cloaking gav noll räknade visade bilder i både
`GetFrameStatistics` och `GetFrameStatisticsMedia`, trots stigande
present-ID:n. Det blockerar godkännande av fysisk bildkadens. Det bevisar
inte i sig att skrivbordslåset orsakar just denna väntan, och får inte
användas för att förklara bort äldre spikar utan motsvarande kontext.

### Läsande diagnostik på Unitys befintliga rendertråd

Den installerade Unity-versionen har `GetSwapChain`, `GetSyncInterval` och
`GetPresentFlags` i `IUnityGraphicsD3D11`. En separat privat diagnostikplugin
kan därför läsa **appens riktiga swapchain** via stödd native-API, utan
gissade privata objektpekare, COM-vtablehookar eller en ny geometribyggtråd.

```mermaid
sequenceDiagram
    participant Main as Unitys huvudtråd
    participant Render as Unitys befintliga rendertråd
    participant Probe as Privat presentationsobservatör
    participant DXGI as Appens riktiga swapchain
    Main->>Render: Efter kameran: GL.IssuePluginEvent, frame N
    Render->>Probe: Native rendercallback
    Probe->>DXGI: GetSwapChain och läsande statistikfrågor
    DXGI-->>Probe: Submission-ID, visningsräknare och QPC
    Probe-->>Render: Återgå utan att ändra grafik- eller väntinställningar
    Render->>DXGI: Unitys eget Present för frame N
    Note over Probe,DXGI: Statistikfrågan ligger före denna Present<br/>och avser redan redovisade tidigare presentationer
```

Observatören loggar både `GetFrameStatistics` och
`GetFrameStatisticsMedia`, faktiska sync-/presentflaggor, fönsteridentitet
och frågornas varaktighet. Resultat och HRESULT behålls även när räknarna
är noll eller en statistikfunktion inte är tillgänglig. Data buffras i
minne och skrivs efter mätningen; inga SDK-graflås tas.

En fristående D3D11-kontroll jämför samma frågor med sina egna anrop för
VSync 1, VSync 2 och en avsiktlig 150-ms-lucka. En verklig kartkörning
verifierar att Unity-callbackarna kör på rendertråden, inte huvudtråden.
**Noll visningsräknare i den låsta sessionen är fortfarande ingen godkänd
bildkadens**, även när faktiska submission-ID:n stiger och syncintervallet
är 1. Observatören ändrar inte pacing för att få ett bättre resultat.

Den senaste 120-sekunderskörningen gav 7 200 rotationsintervall med helt
fast global position, inga hållna kameraposer och inga intervall över
25 ms. CPU-intervallens p99/max var 16,8789/20,1642 ms. Samtidigt lästes
7 444 native samples på appens rendertråd, med faktiskt syncintervall 1,
två swapchainbuffertar och maximum latency 2. DXGI-frågornas p99/max var
0,0179/0,1226 ms; dessa tider omfattar inte Unitys accessorer, ködispatch
eller efterföljande Present. Visningsräknarna var fortfarande noll.
**En ren senare körning upphäver inte tidigare 33- och 512-ms-stopp och
godkänner inte kontinuerliga 60 visade fps.**

## 8. SDK-shutdown: inget globalt EDIT över en blockerande join

Ett separat stoppfel fångades efter clear/reload av den riktiga kartan.
`TheGizmoManager::shutdown` behöll sitt globala EDIT-lås när huvudtråden
väntade på `gzThreadNodeDeallocator::stopDeallocator`. Deallocatortråden
förstörde samtidigt en `gzDynamicLoader` vars `releasePendingLoads` behövde
samma EDIT-lås:

```mermaid
flowchart LR
    MAIN["Huvudtråd<br/>äger global EDIT"]
    JOIN["stopDeallocator<br/>blockerande join"]
    WORKER["Deallocator<br/>gzDynamicLoader-destructor"]
    EDIT["releasePendingLoads<br/>väntar på global EDIT"]
    MAIN --> JOIN -->|"väntar på"| WORKER
    WORKER --> EDIT -->|"ägs av"| MAIN
    classDef blocked fill:#ffe8e8,stroke:#c62828,color:#111;
    class MAIN,JOIN,WORKER,EDIT blocked;
```

Korrigeringen ligger i GizmoSDK, inte i Unitys buffertpollning. Vanlig
SDK-cleanup och slutlig static-reference-cleanup behåller editaccess, men
de två blockerande deallocator-joinsen ligger mellan dessa scopes:

```mermaid
flowchart TD
    CLEAN["EDIT<br/>vanlig cleanup, factories och application"]
    UNLOCK["Släpp EDIT"]
    STOP1["Stoppa och joina noddeallocator<br/>utan SDK-lås på anropande tråd"]
    QUEUE1["Eget EDIT, sedan lokalt kölås<br/>befintlig incoming-queue-cleanup"]
    STOP2["Stoppa och joina edit-locked deallocator<br/>utan SDK-lås på anropande tråd"]
    QUEUE2["Eget EDIT, sedan lokalt kölås<br/>befintlig incoming-queue-cleanup"]
    FINAL["EDIT<br/>slutlig static-reference-cleanup"]
    CLEAN --> UNLOCK --> STOP1 --> QUEUE1 --> STOP2 --> QUEUE2 --> FINAL
    classDef edit fill:#ffe8e8,stroke:#c62828,color:#111;
    classDef join fill:#fff4cc,stroke:#a16207,color:#111;
    classDef free fill:#e5f7eb,stroke:#15803d,color:#111;
    class CLEAN,QUEUE1,QUEUE2,FINAL edit;
    class STOP1,STOP2 join;
    class UNLOCK free;
```

Varje kö-cleanup släpper sina lås innan nästa steg. Deallocatortrådarna kan
därmed få editaccess medan huvudtråden väntar på deras avslut. Befintlig
teardownordning och kösemantik är bevarade; detta inför inte ett nytt generellt
löfte om tömning av alla native köer.

**Anroparen får inte själv hålla EDIT eller RENDER över shutdown.** Ett
inre upplåst scope kan inte släppa ett yttre anroparlås. Kartbyte och stop är
fortfarande synkrona livscykeloperationer, inte tidsbudgeterade Tick-slices.

Den permanenta native regressionen styr factory-cleanup och loaderdestruktion
med event, anropar den riktiga `releasePendingLoads` och kräver att hela
library-shutdown återvänder. Gamla SDK-bytes ger den avsedda deadlocken;
fixade bytes passerar. Det är ett annat fel än den tidigare loader-poolens
kölås, CSW-kommandonas referensräkning och Unitys DXGI-presentationsväntan.
Den separata [shutdown-säkra beroendekohorten](../howto/stage-nuget-dependencies.md#shutdown-safe-sdk-handoff)
måste användas för att få denna SDK-korrigering.

## Källor i denna arbetskopia

| Kod | Läs särskilt |
| --- | --- |
| [CswMapStreamerHost](../../com.saab.map-streamer/Runtime/Saab.Foundation.Unity/Saab.Foundation.Unity.MapStreamer/Csw/CswMapStreamerHost.cs) | `Update`, `LateUpdate`, start och stop. |
| [MapManager](../../com.saab.map-streamer/Runtime/Saab.Foundation.Unity/Saab.Foundation.Unity.MapStreamer/Csw/MapManager.cs) | `Tick`, `HandleFrame`, `StopSession`. |
| [Inbox och native session](../../com.saab.map-streamer/Runtime/Saab.Foundation.Unity/Saab.Foundation.Unity.MapStreamer/Csw/SceneCommandInbox.cs) | `Receive`, `TryDequeue`, `SubmitFrame`, `Dispose`. |
| [SceneCommandPump](../../com.saab.map-streamer/Runtime/Saab.Foundation.Unity/Saab.Foundation.Unity.MapStreamer/Csw/SceneCommandPump.cs) | `Pump`, `CanDeferPublication`, `CaptureNode`, `Publish`. |
| [SceneResources](../../com.saab.map-streamer/Runtime/Saab.Foundation.Unity/Saab.Foundation.Unity.MapStreamer/Csw/SceneResources.cs) | `Capture`, `Publish`, `Acquire` och leasefrigöring. |
| [UnityScene](../../com.saab.map-streamer/Runtime/Saab.Foundation.Unity/Saab.Foundation.Unity.MapStreamer/Csw/UnityScene.cs) | `Create`, `Update`, `BindResources`, `CommitFrame`. |
| [GeometryBuilder](../../com.saab.map-streamer/Runtime/Saab.Foundation.Unity/Saab.Foundation.Unity.MapStreamer/GeometryBuilder.cs) | `CanBuild`, materialmallar och `CreateSceneMaterial`. |

Native referens: CSWUnreals `cswScene.cpp` (`fetchBuffers`,
`processPendingBuffers`, buffertprocessorerna och `processFrames`) samt CSW:s
`cswCommandBuffer.cpp` och `cswSceneManager.cpp` för vidareanrop, kölås och event.
