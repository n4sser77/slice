
**Finns det tillräckligt med minne?** och **Vilken port är ledig?**

förslag på C#-struktur och en tvådagars-sprint för att bygga just denna grund.

### 1. Arkitektur & Klasser (Kärnlogiken)

Vi håller det till enkla interface och records för att tydligt separera ansvarsområden (Clean Architecture-tänk uppskattas ofta i examensarbeten).

```csharp
// 1. Tillstånd och Data (Modeller)
public record AppDefinition(string Name, string ExecutablePath);
public record AppInstance(string Id, string Name, int AssignedPort, int ProcessId, AppState State);
public enum AppState { Starting, Running, Stopped, Failed, ResourceDenied }

// 2. Resurslogik (Beslutsfattare 1)
public interface IResourceManager 
{
    // Logik: Läs av /proc/meminfo (Linux) eller sys-info. 
    // Returnera false om t.ex. mindre än 15% RAM finns kvar.
    bool HasEnoughMemory(int requiredMegabytes = 100); 
}

// 3. Nätverkslogik (Beslutsfattare 2)
public interface IPortManager 
{
    // Logik: Scanna System.Net.NetworkInformation.GetActiveTcpListeners()
    // Hitta och returnera första lediga port i ett givet spann (t.ex. 5000-5100).
    int GetNextAvailablePort(); 
}

// 4. Utföraren (Musklerna)
public interface IProcessRunner 
{
    // Logik: Använd System.Diagnostics.Process. 
    // Starta appen, skicka in porten som miljövariabel, fånga ProcessID (PID).
    // Viktigt: Lyssna på process.Exited för att uppdatera state om den kraschar.
    AppInstance StartApp(AppDefinition app, int port);
}

// 5. Orkestratorn (Systemets Hjärna - hit kommer begäran)
public class AppOrchestrator 
{
    // Injecta IResourceManager, IPortManager, IProcessRunner.
    
    // Logikflöde för Deploy:
    // 1. if (!resourceManager.HasEnoughMemory()) return AppState.ResourceDenied;
    // 2. var port = portManager.GetNextAvailablePort();
    // 3. var instance = processRunner.StartApp(app, port);
    // 4. Spara instance i minnet/databas.
}

```

### 2. Mini-Backlog för Sprint 1 (2 Dagar)

**Mål för sprinten:** Att via kod (t.ex. ett enkelt Console App-anrop till `AppOrchestrator`) kunna starta två instanser av en "dummy-app", där systemet automatiskt ger dem olika portar och verifierar RAM.

* **Task 1: Implementera `IPortManager` (Tid: ~3 timmar)**
* Skriv en metod som hittar en ledig port dynamiskt via C#. Undvik hårdkodade krockar.


* **Task 2: Implementera `IResourceManager` (Tid: ~3 timmar)**
* Skriv logik för att läsa av ledigt RAM. (För dag 1: du kan mocka denna till att alltid returnera `true` först, och bygga Linux-specifik logik dag 2).


* **Task 3: Implementera `IProcessRunner` (Tid: ~6 timmar)**
* Använd `System.Diagnostics.Process`.
* Sätt upp argument så den startar en befintlig .dll-fil.
* Hämta ut PID (Process ID) när den startat.


* **Task 4: Knyt ihop i `AppOrchestrator` (Tid: ~4 timmar)**
* Bygg flödet: Kolla RAM -> Hämta Port -> Starta Process -> Returnera status.





