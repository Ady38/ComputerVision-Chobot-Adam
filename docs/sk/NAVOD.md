# Vytvorenie repozitára zo šablóny a používanie knižnice

ComputerVision spracúva obraz čiary a poskytuje jej polohu a smer.
Repozitár obsahuje C# knižnicu, konzolový nástroj, testy a obrazový dataset.
Na spustenie nepotrebujete kameru, simulátor ani fyzického robota.

## 1. Nainštalujte potrebné nástroje

- [Git](https://git-scm.com/downloads/) na prácu s repozitárom.
- [.NET 10 SDK](https://learn.microsoft.com/en-us/dotnet/core/install/),
  ktoré obsahuje nástroje na zostavenie aj spustenie. Samotný runtime nestačí.
- Editor C#, napríklad aktuálne Visual Studio 2026 alebo VS Code s podporou C#.
- [PowerShell 7](https://learn.microsoft.com/en-us/powershell/scripting/install/install-powershell-on-windows)
  pre nižšie uvedené príkazy a skript na kontrolu štýlu.

Pri Visual Studiu vyberte pracovnú záťaž **.NET desktop development**
a overte dostupnosť .NET 10 SDK. Projekt s `net10.0` vyžaduje Visual Studio
2026 (18.0 alebo novšie); odporúča sa aktuálna aktualizácia so zodpovedajúcim
SDK. Pozrite [kompatibilitu Visual Studia a .NET](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs).

Otvorte nový terminál PowerShell a overte inštaláciu:

```powershell
git --version
dotnet --list-sdks
pwsh --version
```

Medzi SDK musí byť stabilná verzia `10.0.x`. Súbor `global.json` v projekte
určuje výber kompatibilného SDK.

## 2. Získajte prístup k šablóne

Prihláste sa vlastným účtom GitHub a prijmite pozvanie od vyučujúceho.
Otvorte [ComputerVision-SemesterProject](https://github.com/mikelekyr/ComputerVision-SemesterProject),
prípadne novú adresu, ktorú vám vyučujúci poskytne.

Pri súkromnom repozitári môže chyba 404 znamenať neprijaté pozvanie alebo
prihlásenie iným účtom. Pracujte vo vlastnej kópii; zmeny neodosielajte
do spoločnej šablóny.

## 3. Vytvorte vlastný súkromný repozitár

1. Na stránke šablóny vyberte **Use this template → Create a new repository**.
2. Ako vlastníka **Owner** vyberte svoj účet.
3. Zadajte názov, napríklad `ComputerVision-Priezvisko-Meno`.
4. Vyberte **Private** a nechajte vypnuté **Include all branches**.
5. Potvrďte vytvorenie repozitára.

Vznikne samostatný repozitár s jedným počiatočným commitom. Jeho história
je nezávislá od šablóny a ďalšie zmeny šablóny sa doň neprenesú automaticky.
[Oficiálny postup GitHubu](https://docs.github.com/en/repositories/creating-and-managing-repositories/creating-a-repository-from-a-template).

## 4. Stiahnite svoju kópiu a vytvorte vetvu

Vo **svojom repozitári** skopírujte adresu z **Code → HTTPS**.
V nasledujúcich príkazoch nahraďte ukážkové názvy svojimi:

```powershell
git clone https://github.com/VAS_UCET/VAS_REPOZITAR.git
cd VAS_REPOZITAR
git remote -v
git switch -c solution
```

`origin` musí smerovať na váš repozitár. Vetvu `main` ponechajte ako základ,
na zmeny používajte `solution`. Vetvu vytvorte iba raz; pri ďalšej práci
sa na existujúcu vetvu prepnete pomocou `git switch solution`.

Git for Windows obsahuje Git Credential Manager a pri prvom prístupe môže
otvoriť prihlasovanie v prehliadači. Dokončite ho svojím účtom.
[Prihlasovanie ku GitHubu cez HTTPS](https://docs.github.com/en/get-started/git-basics/caching-your-github-credentials-in-git).

Ak Git pri prvom commite žiada meno a e-mail, nastavte ich pre tento
repozitár. Použite vlastné meno a overený e-mail alebo GitHub noreply adresu:

```powershell
git config user.name "Vaše meno"
git config user.email "vas-email"
```

## 5. Otvorte a zostavte riešenie

Vo Visual Studiu otvorte **File → Open → Project/Solution** a súbor
`ComputerVision.Student.slnx`. V inom editore otvorte koreňový priečinok.
Všetky príkazy nižšie spúšťajte z tohto priečinka:

```powershell
dotnet restore ComputerVision.Student.slnx
dotnet build ComputerVision.Student.slnx -c Release --no-restore
dotnet test tests/ComputerVision.Tests/ComputerVision.Tests.csproj -c Release --no-build
```

Obnova stiahne balíky NuGet. Zostavenie vytvorí knižnicu, runner a testy.
Testovacia sada má 65 prípadov. Vo Visual Studiu ich možno spúšťať aj cez
**Test → Test Explorer**.

Po chybe najprv vyriešte jej príčinu. `--no-build` používajte iba po úspešnom
zostavení aktuálneho kódu v rovnakej konfigurácii, aby ste nespustili starú DLL.

## 6. Spracujte dodané obrázky

Konzolový nástroj nemá grafické okno. Jeho pomoc a vyhodnotenie spustíte takto:

```powershell
dotnet run --project ComputerVision.Runner -c Release --no-build -- --help
dotnet run --project ComputerVision.Runner -c Release --no-build -- evaluate data/course results/evaluation.json
```

Nástroj overí rozmery a kontrolné súčty 503 snímok a uloží ich výsledky.
V JSON reporte sledujte `Points`, `DetectedPoints`, `Threshold`, `HasLine`,
`Offset` a `HeadingAngle`.

Každý súbor `.y` má 12 288 bajtov: 128 × 96 hodnôt jasu bez hlavičky.
Pixel `(x, y)` je na pozícii `y * 128 + x`; nula je čierna a 255 biela.
Súbor nie je PNG ani text. Prehliadač surových obrazov nastavte na 8-bitový
grayscale, šírku 128, výšku 96, nulovú hlavičku a žiadne odsadenie riadkov.

Dodané snímky nemajú anotované stredy. `CenterByRow: null` znamená
neanotovanú snímku, nie neprítomnosť čiary. Počet `HasLine = true` preto
nie je percentuálna presnosť. Formát vysvetľuje [DATASET.md](../DATASET.md).

## 7. Zmerajte čas spracovania

```powershell
dotnet run --project ComputerVision.Runner -c Release --no-build -- benchmark data/course results/benchmark.json 10 default
```

Po zahriatí sa zmeria desať prechodov datasetom, teda 5 030 snímok.
Sledujte medián `MedianMs`, p95 `P95Ms` a alokácie
`AllocatedBytesPerFrameCallingThread`. Posledný údaj zahŕňa iba volajúce
vlákno. Meranie nezahŕňa kameru, vykresľovanie ani ovládanie motorov.

Merajte v `Release` bez debuggeru a výsledok overte opakovanými behmi.
Čas na notebooku nie je čas na Raspberry Pi. Význam režimov a všetkých
údajov je v [PERFORMANCE.md](../PERFORMANCE.md).

## 8. Ako sa vyznať v kóde

| Súbor alebo priečinok | Úloha |
| --- | --- |
| `ComputerVision/Models/YUVImage.cs` | Obraz, jeho rozmery a pamäť |
| `ComputerVision/Pipeline/LineDetectionSettings.cs` | Nastavenia |
| `ComputerVision/Pipeline/LineDetectionPipelineFactory.cs` | Zostavenie krokov pipeline |
| `ComputerVision/Pipeline/PipelineContext.cs` | Vstupy, priebežné údaje a výsledky |
| `ComputerVision/ProcessingSteps/` | Spracovanie obrazu a výpočet pozorovaní |
| `tests/ComputerVision.Tests/` | Testy správania |
| `ComputerVision.Runner/Program.cs` | Konzolové príkazy a vyhodnotenie |

Predvolený postup je gamma korekcia → histogram → Otsu prahovanie → stredy
čiary na vybraných riadkoch → priemerná poloha a smer. Nastavenia ďalších
krokov a súradnicové systémy vysvetľuje [PIPELINE.md](../PIPELINE.md).
Príklad volania z C# je v [README](../../README.md#minimal-use-from-c).

Spracovanie môže prepísať vstupné pixely; pred opakovaním obnovte pôvodný
obraz. `Offset == null` označuje chýbajúce pozorovanie, zatiaľ čo nulová
bočná súradnica môže označovať platnú vystredenú čiaru. `HeadingAngle`
vyjadruje smer k priemeru bodov, nie dotyčnicu krivky.

Na ladenie použite breakpoint v príslušnej metóde `Execute`. Ako prvý
príklad otvorte test `BlackLineSurvivesOtsuAndHasCorrectCenter`.
Pre kontrolu formátovania spustite po obnove balíkov:

```powershell
pwsh -File tools/Check-CodeStyle.ps1
```

Podrobnosti vrátane výnimky pri medzere pred dvojbodkou konštruktora
opisuje [CODE_STYLE.md](../CODE_STYLE.md).

## 9. Uloženie zmien a sprístupnenie vyučujúcemu

Pred commitom skontrolujte rozdiely. Pridajte iba zamýšľané súbory:

```powershell
git status
git diff
git add ComputerVision tests
git diff --cached
git commit -m "Strucny opis zmeny"
git push -u origin solution
```

Príkaz `git add` je príklad; ak ste zmenili runner alebo dokumentáciu,
pridajte aj príslušné cesty. `results/`, `bin/` a `obj/` sa neodosielajú.

V nastavení **vášho repozitára** vyberte **Settings → Collaborators →
Add people** a pozvite účet `mikelekyr`, ak vyučujúci neurčí iný.
Pozvanie musí prijať. Postup je v [dokumentácii GitHubu](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/repository-access-and-collaboration/inviting-collaborators-to-a-personal-repository).

Na porovnanie zmien otvorte vo vlastnom repozitári **Pull requests →
New pull request**, vyberte **base: `main`**, **compare: `solution`**
a opíšte zmenu a postup spustenia. Odkaz môžete poskytnúť vyučujúcemu.
Počas kontroly nechajte PR otvorený. PR nesmeruje do spoločnej šablóny.

V **Actions** sa na GitHube vykonávajú kontrolné kroky zostavenia,
formátovania, testov a spracovania datasetu.

## 10. Prístup k spoločnej súkromnej šablóne

Prístup prideľuje vyučujúci. Na osobnom účte GitHub majú spolupracovníci
súkromného repozitára aj právo zápisu; samostatné právo iba na čítanie
tu nie je. V organizácii možno prideliť rolu **Read** aj skupine študentov.
[Osobné repozitáre](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/repository-access-and-collaboration/permission-levels-for-a-personal-account-repository),
[organizačné roly](https://docs.github.com/en/organizations/managing-user-access-to-your-organizations-repositories/managing-repository-roles/repository-roles-for-an-organization).

Ak niečo nefunguje, začnite [prehľadom častých problémov](../TESTING.md#common-setup-problems).
