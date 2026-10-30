# RenderCoreMirror (CLI)

Консольная утилита зеркала вариантов Standard-шейдера rendercore. Она собирается из тех же исходников, что и сборка Unity
`CarX.Modding.Creator.RenderCoreMirror` (`Editor/RenderCoreMirror`, без UnityEngine). Поэтому снимок можно получить и проверить без
Unity и без проекта игры: rendercore читается из папки или прямо из git-коммита (`git ls-tree` + `git show`), рабочая копия не меняется.

Папка `Tools~` Unity не импортирует. `bin/` и `obj/` в `.gitignore`.

## Запуск

Нужен .NET SDK 8 и `git` в `PATH`. Пути ниже указаны от корня `CarX.Modding.Creator`.

```
dotnet run --project Tools~/RenderCoreMirror -- <команда> <опции>
```

### extract — записать снимок

```
dotnet run --project Tools~/RenderCoreMirror -- extract \
    --git D:/UnityProjects/Dr3PC2/Packages/com.carxtech.rendercore --ref <commit пина> \
    --out Editor/RenderCoreMirror/RenderCoreLock.txt
```

- `--rendercore <папка>` — пакет из папки (например, `Packages/com.carxtech.rendercore` проекта игры). Коммит для provenance
  берётся из `git rev-parse HEAD`. Если в папке есть правки, к нему добавляется `-dirty`.
- `--git <репозиторий> --ref <commit>` — пакет из коммита.
- `--override <имя файла>=<ref>` — взять один файл (по имени) из другого ref. Опцию можно повторять. Так проверяют отдельный детектор
  на исторической правке, например `--override Standard.shader=e574be390`.
- `--scope-from <снимок>` — взять область (исключённые префиксы и имена) из существующего снимка. Иначе действует
  `RenderCoreScope.Default`: `MAT_CAR_*`, `MAT_PARTICLE*`, `MAT_SLOD`, `MAT_MATTESHADOW`, `MAT_SKIDMARK`, `MAT_UNSUPPORTED`.

Файл перезаписывается, только если текст изменился (UTF-8 без BOM, LF).

### check — сравнить rendercore со снимком

```
dotnet run --project Tools~/RenderCoreMirror -- check --rendercore <папка> --lock Editor/RenderCoreMirror/RenderCoreLock.txt
dotnet run --project Tools~/RenderCoreMirror -- check --git <репозиторий> --ref <commit> --sdk .
```

Команда ничего не пишет и проверяет то же, что меню `ModSystem/Render Core/Check` и проверка сборки игры:

- отличия rendercore от снимка (RCM1xx; изменения вариантов без аннотации — всегда Info);
- ворота генератора на живом снимке против `RenderCoreMirrorAnnotations` (RCM0xx);
- с `--sdk` — ещё и что закоммиченная `ModRenderCoreVariantTable.g.cs` равна generate(снимок, аннотации) (RCM091/RCM092).

Извлечение идёт с областью из снимка, поэтому видны только изменения rendercore. Находки печатаются по уровням.

| Код выхода | Значение |
| --- | --- |
| 0 | чисто или только Info |
| 1 | неверные аргументы |
| 2 | Stale: нужно обновить снимок и перегенерировать таблицу |
| 3 | Breaking: меняется смысл запечённого контента, нужен человек |
| 4 | Error: вход не найден или не разобран |

### generate — таблица из закоммиченного снимка

```
dotnet run --project Tools~/RenderCoreMirror -- generate --sdk . [--check]
```

Пишет `Runtime/RenderCore/ModRenderCoreVariantTable.g.cs` = generate(`RenderCoreLock.txt`, `RenderCoreMirrorAnnotations`), если текст
изменился. То же делает меню `ModSystem/Render Core/Regenerate Table`. С `--check` ничего не пишет: 0 — таблица актуальна, 2 — устарела.
Breaking/Error — таблица не пишется (коды 3/4). Аннотации — скомпилированные в утилиту, поэтому после правки аннотаций нужна пересборка
(`dotnet run` пересобирает сам).

### update — снимок из rendercore и таблица

```
dotnet run --project Tools~/RenderCoreMirror -- update --git D:/UnityProjects/Dr3PC2/Packages/com.carxtech.rendercore --ref <commit пина> --sdk .
```

Как меню `ModSystem/Render Core/Update Lock From Project Render Core`: извлекает снимок, печатает отличия от прежнего (для справки) и,
если ворота генератора против аннотаций пройдены, пишет `RenderCoreLock.txt` и таблицу. Отличия от прежнего снимка запись не блокируют:
иначе однажды проверенное изменение (например, новый отпечаток декодирования, уже внесённый в аннотации) блокировало бы её навсегда.
С `check` это согласовано: всё, что сравнение снимков называет Breaking, не пропускают и ворота генератора (код ворот указан в
`Lock/RenderCoreFindingCodes.cs`), поэтому записанный `update` снимок не бывает Breaking для `check`, Validation-теста и проверки сборки.
То, что ворота проверяют лишь частично (потоки, объявления keyword'ов, типы свойств), в сравнении — Stale, а опасную часть `check`
показывает находками ворот (RCM020, RCM032, RCM060).

### legacy-diff — сравнить таблицу из dll с генерацией

```
dotnet run --project Tools~/RenderCoreMirror -- legacy-diff --sdk . --old-dll <CarX.Modding.Creator.dll> \
    --unity-managed "C:/Program Files/Unity/Hub/Editor/<версия>/Editor/Data/Managed/UnityEngine"
```

Читает `ModRenderCoreVariants` из собранной dll по reflection (без Unity: `UnityEngine.CoreModule.dll` берётся из `--unity-managed`)
и сравнивает поле за полем с generate(снимок, аннотации): варианты, id, очередь, RenderType, keyword'ы, цвет вершин, потоки, доступность,
ревизия и у каждого свойства label, tooltip, тип, источник, слой, значение по умолчанию, подписи и каналы. Понимает и старую форму таблицы
(поле `emission`). Нужна для доказательства миграции и для проверки, что записанный `.g.cs` даёт ту же таблицу, что модель генератора.

## Что в снимке

Формат описан в `Lock/RenderCoreLockText.cs`, модель и краткий API — в шапке `Lock/RenderCoreLock.cs`. Записи:

- `variant` / `prop` — кандидаты (член enum вне области, есть данные в `MaterialEditorSettings.asset`) и их свойства;
- `rendertype` — члены enum `RenderType`: тег `RenderType` материала rendercore пишет именем члена;
- `shaderprop` — блок Properties шейдера: тип, значение по умолчанию, `[Gamma]`;
- `defines` / `streams` — `StandardMaterialDefinitions.hlsl`, вычисленный для `MAT_X` (+ его additionalKeywords, объявленные в Forward)
  и `SHADERPASS_FORWARD`: файл Defs, `MATERIALFEATURE_*`, прочие define'ы, библиотеки (`libs` — include'ы вне папки Standard в активных
  ветках Definitions; внутрь зеркало не заходит), `INPUT_NEED_*`;
- `decode` — отпечаток библиотек Definitions и активных строк Func-файла при define'ах варианта (сами строки в снимок не пишутся: SDK
  публичный, это код rendercore). Правки в мёртвых ветках `#ifdef` отпечаток не меняют. Ветки на keyword'ах прохода, которые решает не
  вариант (`SHADING_*`, `APPLY_SSAO`, `PACKED_MESH*`, `DOTS_INSTANCING_ON` …), и на макросах платформы (`SHADER_API_*`, `UNITY_*`) —
  «неизвестно»: в отпечаток идут все их ветки вместе с директивами. Имя, определённое только в такой ветке, пишется с суффиксом `?`
  (`MATERIALFEATURE_X?`) — правила его не знают, и ворота отказывают;
- `pass` — объявление каждого keyword'а в Forward, ForwardGhost, DepthOnly и ShadowCaster;
- `passconfig` — что компилируют проходы выреза DepthOnly и ShadowCaster для вариантов альфа-теста (Forward рисует с ZTest Equal, вырез
  делают они): ключ — keyword'ы материала, которые проход объявляет (обычно общий `ALPHATEST`). Программа прохода из `Standard.shader`
  вычисляется сама, в конфигурации по умолчанию (keyword'ы прохода выключены), и подключает Definitions/Functions там, где их включает:
  Defs, особенности, прочие define'ы, потоки, библиотеки, отпечаток Func и отпечаток программы (`program` — её активные строки без
  `#pragma`, ветки на keyword'ах прохода и платформе — целиком; car-ветки отброшены). Генератор сверяет всё, кроме прочих define'ов,
  с `MirrorPassReview` в аннотациях (RCM045);
- `setup` — отпечаток нормализованного тела `EditorUtils.SetupMaterialVariant` (строки тоже не пишутся);
- `provenance` — версия пакета, коммит, версия утилиты. Не сравнивается и не входит в `LockContentHash`.

Руками снимок не правят. Строк кода в нём нет, поэтому изменение отпечатка `decode`, `passconfig` или `setup` разбирается через
`git diff` rendercore между пином снимка и текущим.

Чего зеркало не видит (сознательно): содержимое библиотек вне папки Standard (видно только, какие подключены); код материалов, который
компилируется лишь в небазовой конфигурации программы прохода — например, ветки `SHADERPASS_SIMPLEFORWARD` в Defs/Func, по которым
ShadowCaster рисует кубмапы отражений с `SIMPLE_SHADING` (правка самой программы прохода в этой конфигурации видна по `program`);
пролог программы Forward (для Forward определены только `MAT_X` и `SHADERPASS_FORWARD`).

## Уровни находок сравнения (RCM1xx)

- **Info** — тексты (label, tooltip, vectorLabels), прочие define'ы, литералы по умолчанию, пути файлов, имя файла Func при том же
  отпечатке, новый кандидат или конфигурация прохода, порядок свойств, любые изменения вариантов, которые не предлагаются.
- **Stale** — id, очередь, RenderType варианта, additionalKeywords, добавленное, убранное или перетипизированное свойство, вершинные потоки,
  тип в Properties, `[Gamma]` не-текстуры, объявление keyword'а в проходе, другая область. Лечится `update`; то, от чего вариант
  действительно зависит, проверяют ворота (RCM020, RCM032, RCM060, RCM062).
- **Breaking** — пропал вариант, метка записи asset ≠ имя enum, Defs или `MATERIALFEATURE_*`, декодирование, библиотеки Definitions,
  `[Gamma]` текстуры предлагаемого варианта, конфигурация прохода выреза (RCM141), члены enum `RenderType` (RCM108), тело
  `SetupMaterialVariant`. Каждый такой код не пропускают и ворота генератора.
- **Error** — RCM001 (файл не найден или неоднозначен), RCM002 (синтаксис вне поддержанного подмножества, в том числе лишний `#endif` или
  незакрытый `#if` в программе прохода), RCM003 (снимок испорчен).

Полный список кодов — в `Lock/RenderCoreFindingCodes.cs`. Коды RCM010–RCM099 — это ворота генератора против аннотаций (`Generation/`).

## Ворота генератора (RCM0xx)

Генератор сверяет снимок с `Generation/RenderCoreMirrorAnnotations.cs` — единственным ручным входом зеркала. Любая находка Breaking
или Error блокирует таблицу. Коды — в `Generation/MirrorFindingCodes.cs`, главные:

- RCM010 — предлагаемого варианта нет в rendercore (с подсказкой, если похоже на переименование);
- RCM012 (Error) — два предлагаемых варианта с одним id; RCM014 — метка записи asset ≠ имя enum;
- RCM020 — keyword варианта не `multi_compile*` в Forward, `ALPHATEST` — в DepthOnly/ShadowCaster, или `shader_feature*` где-либо;
- RCM021 — `ALPHATEST` и RenderType AlphaTest расходятся; RCM022 — члены enum `RenderType` rendercore не равны `MirrorRenderType`
  (тег `RenderType` материалов rendercore и модов разойдётся);
- RCM031/032/033 — свойство вне правил слотов, чужой тип, неверное переопределение;
- RCM040/041 — ключ раскладки или отпечаток декодирования (библиотеки Definitions и активные строки Func) не совпадают с проверенными;
- RCM045 — конфигурация прохода выреза (DepthOnly/ShadowCaster) варианта альфа-теста не совпадает с проверенной `PassReview(...)` в
  аннотациях, её нет или нет проверки (находка печатает готовую строку);
- RCM042/043/044 — неизвестный `MATERIALFEATURE_*` или Defs, каналы противоречат define'ам, каналы без packed map;
- RCM050 — `[Gamma]` текстуры расходится с замороженным правилом префикса `u_sTexture`;
- RCM052/053/054 — запечённый контракт изменился без новой ревизии, пропало свойство контракта, нет контракта ревизии;
- RCM060/061/062 — потоки шире мешей модов без причины недоступности, цвет вершин расходится с выведенным (Base с EMISSION —
  EmissionMultiplier, Layered — LayerBlend, Interior и прочие — None), неизвестный `INPUT_NEED_*`;
- RCM070 — тело `SetupMaterialVariant` изменилось (сверить `MaterialMappingPBR.ApplyRenderCoreVariant` клиента);
- RCM080 — подсказка Export from material ведёт на недоступный вариант;
- RCM090 (Info) — кандидат без аннотации: в таблицу не попадает, находка печатает готовую строку `Variant(...)`.

Новый вариант: обновить снимок, перенести напечатанную строку `Variant(...)` в аннотации, проверить ключ, каналы и цвет вершин, дописать
`Contract(...)` ревизии 1 (его печатает RCM054) и строку реестра в `RenderCoreMirrorTableTests`, перегенерировать таблицу.

## Как добавить команды

Команды генератора (`generate`, `update`, `legacy-diff`) живут в `GenerationCommands.cs` этой папки; новые добавляются так же:

```csharp
internal static class GenerationCommands
{
	public static void Register(Dictionary<string, RenderCoreMirrorCommand> commands)
	{
		commands["generate"] = new RenderCoreMirrorCommand("generate --sdk <CarX.Modding.Creator folder> [--check]", Generate);
	}
}
```

Общие шаги уже есть в `Program`: `CreateSource`, `ResolveLockPath`, `ReadLock`, `WriteText`, `ReportFindings`, `ExitCodeFor`.
`csproj` подхватывает все `*.cs` этой папки и всё из `Editor/RenderCoreMirror/**`.
