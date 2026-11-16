# GFX: попередній аудит Godot ↔ Unity та план повної перевірки

Дата: 2026-11-12. Статус: поверхневий аудит коду, план виконання; НЕ сертифікація відповідності.

Оновлення виконання: перший блок фактичних перевірок рушіїв описано в [GFX_PARITY_AUDIT_RESULTS.md](GFX_PARITY_AUDIT_RESULTS.md): 1008 CPU fixtures, Unity pixel readback, instance isolation та prefab lifecycle. Уточнення A04: `create_id_101.gd:208–211` вимикає depth test за **header 2DRender**, а не за ZEnable. У Unity відтворено саме ігнорування depth disable для header 2DRender; сам ZEnable=0 у sprite не є доведеним міжрушієвим розходженням.

Оновлення блоку 2: додано 4644 particle fixtures (4633 збіги, 11 розбіжностей), prefab lifecycle частинок/Dummy, реальний Godot Compatibility/Forward+ render для A01/A02/A04. Відтворено A08/A14, нові N03 Quota=0 і N04 раннє витіснення живої частинки з pool. Загалом CPU: 5652 fixtures, 5624 збіги, 28 розбіжностей. Деталі, межі покриття й незалежна перевірка висновків GLM — у звіті; решта етапів плану відкрита.

Блоки 3–4: 13 parser fixtures (11 повних збігів; дві структурні відмінності без доведеного visual bug), A03 BindEle end-to-end, A05/A15 під parent transform, A06/A12 scene roundtrip, N05 справжній script/domain reload (Build(document) також втрачає runtime keypoints через animationRestored), A13 explicit instanced GPU draw із позитивними контролями. Повний Dummy frame-loop і Play Mode reload variants ще відкриті. Докази та відхилені/уточнені висновки GLM — у GFX_PARITY_AUDIT_RESULTS.md.

Блок5 розділено з GLM на незалежні пакети: основний агент — geometry122/125 (560/560) та step count(65/65), GLM — affector/HSV mapping і пошук дефектів; спільний engine harness за його специфікацією:166/232. Підтверджено N06 ColorDelta /255 (включно з Ctrl105 елемента9 реального GFX), N07 noise defaults, N08 short affector110. Загальний CPU6509:6415 збігів/94 розбіжності. Виправлення ще не внесені.

## Межі цього проходу

Переглянуто архітектуру, ключові ділянки парсингу/планування, спрайти, частинки, шейдери, камеру, вкладені ефекти, серіалізацію та характер тестів. Інвентаризація: 78 Godot-файлів `gfx*.gd`/`create_id_*.gd`, 83 Unity GFX-файли runtime/editor/shaders, 21 файл `Gfx*Tests.cs`, 157 атрибутів `[Test]`. Це кількість декларацій, не кількість запущених тест-кейсів і не відсоток покриття. Детальний знімок із SHA-256: `.validation/gfx-parity/inventory.json`.

У цьому проході не запускалися Godot, Unity Test Runner або синхронне порівняння рендерів. Результати 174/174 і 14/14 належать попередній роботі, а не новому аудиту. Godot executable не знайдено в PATH, `project.godot` не знайдено в репозиторії: для виконання потрібен окремий тестовий Godot-проєкт. Це не означає, що Godot не встановлено на машині.

Переглянуто також 10 незакомічених файлів попереднього виправлення полум’я; у цьому проході реалізацію не змінювали. Статичні контрприклади збережено в `.validation/gfx-parity/counterexamples.json`; це арифметика з формул коду, не вимірювання рушіїв.

Три незалежні цілі:

1. **Godot ↔ Unity:** відтворити поведінку поточного Godot-плагіна.
2. **Плагіни ↔ оригінальна гра:** встановити, чи помилка спільна для обох портів.
3. **Стабільність Unity:** імпорт, scene/prefab, reload, повторні інстанси та build повинні зберігати ефект.

Godot не вважати безумовно правильним еталоном. Для кожної розбіжності фіксувати: помилка Unity / помилка Godot / спільне обмеження / свідоме розширення / невідомо. Відсутність будь-яких багів довести неможливо; результатом має бути відтворюваний обсяг перевіреного й явний перелік неперевіреного.

## Попередні знахідки

«Підтверджено кодом» означає, що є конкретний шлях або математична суперечність; візуальний прояв ще потрібно відтворити в рушіях. P1 — перевірити до масового використання імпорту; P2 — наступний пріоритет.

| ID | Пріоритет / впевненість | Спостереження та докази | Мінімальна перевірка |
|---|---|---|---|
| GFX-A01 | P1, підтверджено формулою | `Runtime/GfxSpriteDriver.cs:185–193` включає header alpha в `_BaseColor.a` і ще раз передає в `_GfxAlpha`; `Shaders/AngelicaGfxSprite.shader:90–91` множить повторно. Godot `create_id_101.gd:1503–1526` + `gfx_parser.gd:74–83` застосовує header alpha один раз. Alpha=0.5 дає 0.25 замість 0.5 для білого additive sprite. | Біла текстура, key alpha=1, header alpha={0,0.25,0.5,1}; числове значення матеріалу + linear render без bloom. Окремо Warp. |
| GFX-A02 | P1, підтверджено формулою | `Runtime/GfxSpriteUvEval.cs:56–64` міняє коефіцієнти scale/offset замість вхідних U/V. Діагональна `_UvRect` не реалізує `uv.yx`. Godot `create_id_101.gd:1394–1396` справді міняє координати. При атласі 1×1 прапорець у Unity взагалі нічого не змінює. | Асиметрична ABCD-текстура, UV=(0.2,0.8); атласи 1×1, 2×3, 5×6; усі 8 комбінацій exchange/reverse. |
| GFX-A03 | P1, підтверджено шляхом | Godot `create_id_100/101.gd:591–677,741–786` читає `BindEle` та підставляє часові параметри цілі. Пошук `BindEle` у Unity Runtime/Editor C# не знаходить споживача. Це не `DummyEle` і не particle `IsBind`. | Два sprite: у A інший StartTime/RepeatDelay та рух, B BindEle=A. Перевірити час, рух, відсутню/дубльовану ціль і цикл прив’язок. |
| GFX-A04 | P1, підтверджено шляхом | Godot додає `depth_test_disabled` для ZEnable=0 (`create_id_101.gd:208–211`, `create_id_120.gd:297–299`). Unity factories змінюють `_ZWrite`, але sprite shader не має керованого ZTest. Вимкнення запису глибини не вимикає перевірку глибини. | Непрозора стіна перед ефектом, ZEnable=0/1, 2DRender=0/1; URP/HDRP окремо. |
| GFX-A05 | P1, підтверджено координатами | `GfxPlayer.cs:229–239,263–273` використовує localPosition для відстані/face і фіксований camera forward=(0,0,1). Godot `gfx_data_node.gd:374–396` використовує global_position/global_basis. `GfxSpriteDriver.cs:166–174` рахує орієнтацію зі світових координат, а записує localRotation. | GFX і камера під різними parent; parent translation=100, rotation=90°, scale=(2,1,3). Fade/Face/Shake, orbit camera, без MainCamera, Scene view і Game view. |
| GFX-A06 | P1, підтверджена прогалина відновлення | `GfxContainerDriver.cs:25–28,63,96` тримає subGfx і кеші лише в пам’яті, без повторного пошуку після deserialize. `GfxGridDriver.cs:28,104–105` аналогічно губить cachedParams і ховає mesh. `GfxPhysDriver.cs:19–25` має несеріалізовані params/sim; `GfxLtnBoltDriver.cs:14–18` — params/strokes. Відновлення keypoints не відновлює ці дані. | Створити 200/210/211/220/151, save scene/prefab, закрити/відкрити, domain reload, build; порівняти не тільки кількість GO, а параметри, children, vertices, alpha та анімацію. |
| GFX-A07 | P1, підтверджено шляхом | `GfxContainerDriver.cs:140,167–177` зводить keypoint alpha до visible boolean, а `PushTime` передає лише GfxAlpha. Godot `gfx_container_200.gd:228–233` передає величину alpha матеріалам. Значення 0.25 і 1 у Unity не дають відповідного плавного згасання дочірнього ефекту. | Контейнер з key alpha 1→0.5→0 і білим child sprite; потім вкладені контейнери, щоб визначити точну композицію alpha. |
| GFX-A08 | P1, підтверджена прогалина шляху | `GfxParticleDriver.cs:143–157` після reload відновлює sim/affectors, але не шлях Configure для DummyEle: dummyActive/dummyDriver несеріалізовані. `GfxParticleParamsSerial` не містить усіх runtime масивів curve/mesh sampler. | DummyEle + 122 RefModel + 125 curve: до/після reload, а також після Instantiate(prefab). Не обмежуватись ID123 із прикладу полум’я. |
| GFX-A09 | P1, сильний ризик, потрібен runtime repro | `GfxSpriteDriver.cs:180–193` та particle driver повторно беруть sharedMaterial і змінюють time/color/UV. Два екземпляри одного збереженого prefab можуть змінювати один матеріал. | Інстанси A/B з різним часом і alpha, tick A→B та B→A. Перевірити, що результат не залежить від порядку, і material asset не забруднюється після Play. |
| GFX-A10 | P2, підтверджена відсутність обробки | Глобальний particle UV у `GfxParticleDriver.cs:195` виконується лише без IsUseParUV/InitRandomTexture. Гілка Shuriken sheet не застосовує UReverse/VReverse/UVExchg/scroll. | 2×3 атлас із номерами кадрів, per-particle age, random start, TTL=0.3/2, reverse/exchange/TileMode; bake mesh UV та render. |
| GFX-A11 | P2, задокументована апроксимація | `GfxGridDriver.cs:10–14,74–78` явно не реалізує clip-space шлях 211; локальна XY-площина не є screen-space еквівалентом. | Змінити FOV, resolution, aspect, положення камери; 2D decal повинен залишатись у відповідному екранному просторі. |
| GFX-A12 | P2, прогалина API | `GfxSceneBuilder.Build(document)` додає animationSource, а public `Build(plan)` — ні; direct SetKeypoints також тримає список лише в пам’яті. Test `GfxLightningTests.cs:328` користується Build(plan). | Побудувати один документ обома overload, зберегти/перезавантажити, порівняти. Визначити контракт підтримки публічного API. |

Префікси шляхів таблиці: Unity — `unity-package/`; Godot — `gfx_importer/`. Рядки відповідають робочому дереву на дату огляду.

## Подібність, яку видно в структурі

- Обидва плагіни мають parser → element/keypoints/controllers → player/header → renderer. Схожа декомпозиція не доводить еквівалентність.
- Unity реєструє 24 ID: 100,101,102,110,120–125,130,140,150–152,160,170,180,190,200,210,211,220,230. Godot registry visual builders містить 23 без 102; 102 має element script, а Unity додає cross-decal. Це кандидат на свідоме розширення, не автоматично дефект.
- Є відповідники timing, controller 100–111, curves, particles, trails, model/audio, container/grid/phys; чисті C# evaluator-тести вже є.
- Найслабше покрито взаємодію CPU evaluator → serialized object → Unity renderer → camera. Пошук у Gfx*Tests не виявив render/readback порівнянь або повного SaveScene/OpenScene циклу; один новий тест робить EditorJsonUtility roundtrip частинки.
- Документ `UNITY_MIGRATION_ONBOARDING.md` містить суперечливі твердження про повноту parity й застарілі stub-коментарі. Статус слід визначати з доказів, а не з цих заголовків.

## План повної перевірки

### 0. Зафіксувати базу й безпечне середовище

- Зафіксувати commit + dirty diff hash обох плагінів, Unity/Godot версії, renderer/GPU/API, color space, pipeline assets, importer settings.
- Зберегти поточні зміни; не змішувати аудит із виправленнями. Для кожного виправлення окремий regression fixture.
- Створити ізольовані Godot та Unity test hosts. Не змінювати робочу сцену користувача для массових прогонів.
- Знайти встановлений Godot і використану версію; тільки після цього обрати headless CPU run та GPU host. Godot headless без renderer не є візуальною перевіркою.
- Ігрові ресурси зберігати поза git, fixture manifest у репозиторії — hash, ID, feature tags, dependency status, provenance. Синтетичні ресурси можна тримати в test fixtures.
- Зберігати JSONL результати, stderr/console, PNG/відео, конфігурацію та команди відтворення. Warning не прирівнювати до успіху.

### 1. Побудувати карту покриття формату

Для кожного поля: raw token → decoder/parser → typed params → serialization → evaluator → renderer → test fixture. Стани: used / intentionally ignored / unsupported / unknown. Виявляти поля, що парсяться, але ніколи не використовуються.

Матриця: версії MOXTVersion, повторні ключі/суфікси, CtrlMethodCount/AffectorCount, PS blocks, StartTime_2, Grid animations, UTF-8/BOM/GBK/mixed, CRLF/LF, коментарі, порожні та обрізані блоки, NaN/Inf/overflow, невідомий ID. Порівнювати нормалізовані дерева, не рядкові dumps різного формату. Невалідний файл повинен давати контрольовану помилку без зміни поточної сцени.

### 2. Побудувати диференційний CPU harness

Ті самі synthetic GFX, час, seed і вхідні параметри → JSON з обох runtime/evaluator. Godot GPU-only гілки додатково перевіряти на renderer, не замінювати самописною копією C# формули.

Числові контрольні точки: -1, 0, start−ε/start/start+ε, кінець keypoint−ε/точно/+ε, TTL−ε/точно/+ε, repeat/delay/loop boundaries; ε=0.01 ms для sampling, окремо float32 rounding. TimeSpan=-1/0/positive; RepeatCount=-1/0/1/2; PlaySpeed=0/0.5/1/2/negative (визначити очікуваний контракт).

Порівнювати position/direction/scale/color/alpha, visibility, local/global time, frame index, particle ID/age/position/rotation/size, vertices/UV/indices. Для seed: спершу встановити однаковий алгоритм; якщо алгоритми різні навмисно, використовувати статистичні метрики та документувати, чому exact equality не застосовується.

Початкові допуски: scalar/vector `abs ≤ 1e-5 + 1e-4*abs(reference)`; quaternion — angular distance, із врахуванням q та −q; індекси, counts, active/hidden — точна рівність. Для великих координат і довгих симуляцій потрібен окремий погоджений бюджет похибки. Не підганяти tolerances під наявні помилки.

### 3. Закрити P1 інтеграцію до візуальної підгонки

Першими відтворити GFX-A01…A09 та A12. Для кожного: мінімальний failing test → root cause → рішення щодо очікуваної поведінки → fix → regression. Перевірити всі ID при:

- fresh import; scene save/reopen; prefab save/instantiate; domain reload; Play enter/exit;
- Domain Reload on/off та Scene Reload on/off;
- disable/enable GO/component; reparent; duplicated prefab; Undo/Redo; reimport;
- два/десять екземплярів з різним time/alpha; nested containers; DummyEle;
- standalone development build без Editor API й без вихідного game-data folder.

Порівнювати стан до/після за тією самою міткою часу. Не обмежуватися перевіркою, що компоненти не null. Для generated materials/meshes перевірити GUID/reference persistence, ownership, cleanup і відсутність зміни shared assets під час програвання.

### 4. Контрольований візуальний стенд

- Фіксовані transform/camera, FOV, aspect, resolution, near/far, exposure, color space, clear color; вимкнути bloom/tonemapping/AA/dynamic resolution для базового порівняння.
- Далі окремі сценарії з bloom/tonemapping, sRGB↔linear DDS, mipmaps/filter/wrap, alpha compression, opaque texture on/off.
- Atlas fixtures із великими номерами й асиметричними кутами; окремі RGB/alpha ramps, warp normal map, depth occluders, overlapping translucent layers.
- Захоплювати до postprocessing (linear, коли можливо) і фінальний кадр. Порівнювати bbox, coverage mask, centroid, alpha, luminance/chroma, UV landmarks. SSIM/MAE — допоміжні, а не єдині критерії.
- Для детермінованих fixture: exact frame/visibility/geometry спочатку; pixel differences оцінювати з урахуванням raster edges. Не приймати ефект лише тому, що SSIM перевищив довільний поріг.
- Анімація: 30/60/120 fps, delta spikes, pause/resume, forward seek/backward seek; sample time у ms незалежно від wall clock. Підтвердити рух frame counter перед записом.
- Камери: front/side/top/orbit, parent rotation/nonuniform/negative scale, nested offsets, без MainCamera, Scene/Game views, multi-camera. Unity billboard має орієнтуватись на камеру, що рендерить, якщо це очікуваний контракт.

### 5. Сімейства ефектів — обов’язкові сценарії

| Сімейство | Перевірки |
|---|---|
| Header/parser/ctrl | Усі поля header, DefaultScale typo, fade/alpha/face/no-change-dir/shake; Ctrl100–111, порядок контролерів, sec↔ms, interpolation, curve degeneracy. |
| 100/101/102 | OrgPt/size, billboard та local/world rotation, BindEle, full UV matrix, Src/DestBlend, ZEnable, Warp, Priority/RenderLayer, 2D; для 102 окремо зафіксувати розширення Unity. |
| 110/180 | SegLife/history, teleport/reverse seek, spline, world/local Bind, stationary emitter, UV tiling, fade, zero-width/degenerate ribbons. |
| 120–125 | Кожна форма emitter; quota=0/-1/max, zero rate/TTL, affectors order, non-square size, rotation, per-particle/global/random UV, IsBind/IsDrag/ScaleNoOff/NoScale/OrgPt, 3D/Facing, 122 mesh normals, 125 curve, DummyEle. |
| 130 | Light type/color/range/intensity/attenuation/spot cones, parent transforms, URP light limits, render path difference окремо від evaluator parity. |
| 140/190 | Segments/angles/radius/height/coefficient, boundaries, winding/cull, UV/sheet/tile, negative/zero scale. |
| 150/151/152 | Seed/noise, strokes/append/tails, controllers/curve, StartTime_2, age/range, bolt child ownership, reload. |
| 160/230 | Missing deps, action seek/play, skin animation, FacingDir, pivot/scale, alpha/blend/depth, hooks/CoGfx, reload caches. |
| 170 | Clip resolve, deterministic selection, volume/pitch/distances, Force2D, loop/start/stop/seek, no-audio-device, nested clock. Порівняти числові параметри й запис waveform коли можливо. |
| 200 | LoopFlag/PlaySpeed/SubScale, nested clocks, alpha, Dummy, recursion/cycle/missing file, hide/show, reload. |
| 210/211 | W/H/grid animations, interpolation, clip/screen coordinates, viewport resize, AffByScl, 2DBackLayer. |
| 220 | Params/sim persistence, dt sensitivity, max pool, lifetime, reverse seek; фізичні можливості, відсутні також у Godot, класифікувати окремо. |

### 6. Корпус реальних GFX і стрес

- Просканувати доступний game-data corpus без копіювання в git: histogram ID/version/fields/controllers, dependency graph, rare combinations. Один вогонь не представляє весь формат.
- Вибірка повинна покривати всі спостережені гілки і рідкісні комбінації; додати синтетичні fixture для відсутніх. Pairwise для незалежних параметрів, повний перебір для небезпечних взаємодій (UV flags, parent transforms, lifecycle, alpha chain).
- Long-run 10–30 хв, 1/10/100 інстансів із реалістичним particle budget. CPU/GPU time, allocations/frame, memory after destroy/reimport, shader/material counts. Контрольовані бюджети встановити за baseline обладнання, не довільними числами.
- Fuzz malformed parser inputs і nested dependencies з лімітами часу/пам’яті. Зависання/безмежна рекурсія/NaN geometry — fail.

### 7. Критерії завершення

- Кожна підтримувана ID-гілка має fixture, CPU evidence де застосовно, lifecycle roundtrip і visual/audio evidence.
- Усі P1 відтворені й виправлені або явно віднесені до погоджених обмежень; жодне «не перевірено» не позначене зеленим.
- Тести незалежні від формули реалізації: еталон із Godot/гри або аналітичного контрольного прикладу, не скопійований Unity код.
- Перевірка тестів внесенням тимчасової контрольованої помилки в ізольованій копії: UV swap/alpha/reload regression повинні зламати тест.
- URP — обов’язкова основна ціль; HDRP окремий запуск і статус, не успадковує pass від URP. Godot Forward+/Mobile/Compatibility — лише ті режими, що заявлені плагіном, з окремими результатами.
- Evidence index містить fixture hash, engine versions, commands, timestamps, measured values, captures, expected/actual, verdict і issue ID.
- Фінальний звіт окремо показує passed/failed/unsupported/not-run; не використовує «100% parity» без визначеного корпусу й контракту.

## Рекомендований порядок виконання

1. База, інвентаризація полів і fixture manifest.
2. Alpha/UV/BindEle/depth/camera мінімальні repro.
3. Lifecycle всіх сімейств, shared materials, containers/dummy.
4. Differential CPU harness і граничні значення.
5. Синхронні render captures, окремо particle geometry/UV.
6. Реальний corpus, original-game references, performance/stress/build.
7. Регресії, повторний незалежний review, доказовий підсумок.

Виправлення не входять у цей попередній огляд. Обсяг і тривалість повного проходу оцінювати після інвентаризації корпусу та запуску двох стендів; передчасна оцінка у годинах створить хибну впевненість.

## Додаткові точки ризику для повного аудиту

- `OrgPt_1/2` у particle params серіалізуються, але `GfxParticleDriver.ApplySlots` не використовує їх для pivot; Godot `create_id_120.gd:665–666` читає ці поля для геометрії. Потрібен off-center pivot fixture.
- `Particle3D` у Shuriken обирає Mesh render mode, але треба довести, що mesh і vertex streams справді налаштовані. Facing/Stretch із нульовою velocity також потребує render test, а не лише перевірки enum.
- Шейдер sprite тепер використовується частинками та містить `_UseVertexColor`; треба перевірити COLOR stream, кольорові/alpha affectors і всі інші користувачі shader (у тому числі phys220). Виправлення одного вогню може змінити інші сімейства.
- `GfxParticleParamsSerial` не дорівнює повному `GfxParticleParams`: для кожного пропущеного поля визначити, чи воно відновлюється, чи не використовується, чи губиться. Не позначати всі пропуски багами без простеження споживача.
- `GfxModelDriver` кешує modelRoot/materialTints/animation лише під час Configure; lifecycle матриця має перевірити також tint/visibility/animation після reload, навіть якщо mesh лишився видимим.
- Build(document) спочатку виконує Build(plan), а тоді призначає animationSource. Dummy/template cloning і generated stroke children можуть створюватись раніше — простежити порядок ініціалізації та серіалізації кожного клона.
- Порівняти seed залежність від імені/порядкового номера: unknown IDs або перенумерація елементів можуть змінювати випадкові частинки при незмінних параметрах.

## Незалежний GLM Flash review: перевірені доповнення

Користувач явно дозволив передачу коду GFX зовнішній моделі. Використано OpenCode CLI 1.18.34, `opencode/glm-5.3-flash-free`, agent plan, без змін реалізації. Сирий журнал: `.validation/gfx-parity/glm-review.jsonl`. Висновки моделі не приймаються автоматично: нижче наведено перевірені читанням коду доповнення.

| ID | Пріоритет / доказ | Висновок | Перевірка |
|---|---|---|---|
| GFX-A13 | P1, підтверджено пошуком producer/consumer | `GfxDummyInstancedBatch.cs:13,40,51` передає `_DummyCustom` (age/alpha), але пошук у всьому Unity Runtime знаходить лише оголошення і запис у цьому файлі, жодного shader consumer. Godot `gfx_dummy_particles.gd:205–214` створює shader variant з INSTANCE_CUSTOM.x/y. Unity не може застосувати вік/alpha з цього масиву без відповідного шейдерного шляху. Додатково `TryCollect` не перевіряє shader capability. | Дві dummy частинки різного віку й alpha на анімованому атласі; порівняти node-copy та instanced path і Godot. Окремо shader instancing variants / graphics errors / culling. |
| GFX-A14 | P1, підтверджено шляхом | `GfxDummyParticlesDriver.cs:411–425` приймає IsDummy лише для container/particle. Для sprite/інших повертає false навіть після name match. Коментар про inactive siblings не відповідає виконуваному коду. Godot `create_id_120.gd:223–231` допускає parsed sibling із Name/IsDummy. | Sprite100/101 із IsDummy=1, emitter DummyEle=його Name; потім trail110 і container200 як контроль. |
| GFX-A15 | P2, підтверджено відсутністю споживача | Unity `NoChangeDir` знайдено лише в `GfxHeaderDriver` як поле й парсинг; виконання немає. Godot `gfx_data_node.gd:364–371` скидає світову орієнтацію, `gfx_container_200.gd:186–189` застосовує до nested GFX. | Повернутий parent/контейнер, NoChangeDir=0/1, зберегти scale і position; перевірити node/instanced Dummy. |

GLM також відзначив відсутність Godot-подібного frustum early-out у Dummy driver Unity. Це поки performance-кандидат, а не доказ неправильної картинки. Подібність формул SubGfxMs і shared limitation OutColor підтверджені оглядом відповідних файлів; слова «повна parity» до цих вузлів не застосовувати, бо lifecycle/alpha/NoChangeDir вже відрізняються.

Статус зовнішнього огляду: отримано завершений підзвіт GLM про Dummy/Container; основний процес не надав фінального зведення за приблизно 8 хвилин і був зупинений після цього ліміту попереднього огляду. Підзвіт збережено в `.validation/gfx-parity/glm-independent-findings.md`. Це часткове незалежне покриття, не завершений другий аудит усіх GFX.

Поточний прогін містив лише читання коду, інвентаризацію, арифметичні контрольні приклади й підготовку цього плану. Нових виправлень runtime/shader та нових заяв про успішне виконання engine tests тут немає.

## Оновлення після блоку 6

Детальні результати й межі покриття — у GFX_PARITY_AUDIT_RESULTS.md, блок6. Завершено sprite Enter Play Mode matrix для всіх чотирьох reload options; уточнено A12/N05. GLM паралельно виконав окремий огляд trails/lightning/ring. Додано Unity spot-checks N09 (scale² width), N10 (перезапис position); C01/C02 залишаються статичними render-кандидатами. Hash-name підозру перевірено й не підтверджено. Наступний крок: Godot/Unity render fixtures цих кандидатів, потім реальний відеореференс, lifecycle інших driver-ів та live instancing. Повний аудит не завершений.

Оновлення блоку7 (2026-11-13): отримано video contact sheet і12 fresh/saved captures користувацького GFX; усі12 піксельно ідентичні після корекції preview-scene native particle buffer у harness. Початкову підозру про зламаний fresh import спростовано. Виконано делегований GLM Lightning UV test:4 controls match,2 source-formula divergences; Godot GPU перевірка лишається відкритою. Наступний контроль відео потребує узгодження ракурсу/фази/постобробки, без вигаданого відсотка схожості.

2026-11-13: розпочато production fixes, прохід1. N06/N07/A01/N05 виправлені й перевірені цільовими engine tests; A12 виправлений для planner-generated plans і підтверджений domain/scene/mode2 reload. Affectors212/232,20 відомих110-path розбіжностей. NUnit discovery повернув0tests — потрібен test host setup, не зараховувати як pass. Деталі/обмеження в розділі «Виправлення — прохід1» звіту. Наступний пакет — driver-specific persistence та rendering defects.

2026-11-13, прохід2: A01/N06/N07/N05/A12 CLOSED у межах задокументованих regression tests. A06 виправлено для новозбережених Grid/Phys/Bolt/Container; prefab/scene roundtrip та payload comparisons пройшли. A07 numeric alpha до GFX children виправлено (.25, nested .2). Старі assets без serialized params потребують rebuild; migration/domain/play matrix іншихdriver-ів не оголошується завершеною. Наступний пакет: A08 particle/Dummy persistence та A09 material isolation, потім render parity.

2026-11-13, прохід3: A08 CLOSED у перевіреному prefab сценарії (IsDummy, geometry arrays, Dummy pool без дублікатів); A09 CLOSED для sprite clone alpha та particle UV (MPB + URP pixel tests). A01/A07 regression пройшли. A14/A13 лишаються відкритими. HDRP, повна lifecycle matrix і perf MPB не перевірені. Див. результати проходу3; saved-reference captures оновлено новим кодом.

Прохід4, 2026-11-13: GLM Flash через OpenCode variant=max самостійно виконав A13 review, основний агент A14 implementation/tests. A14 CLOSED за7×2 lookup controls і prefab regression. A13 MITIGATED node fallback: actual GPU red1/.5/0, child time150ms/atlasoffset.5; GPU instancing implementation лишається відкритою. Поточні materials capability gate не пропускає, enableInstancing на shared asset не мутується. CPU/draw-call performance fallback не вимірювалася.

Прохід5, 2026-11-13: A02 UVExchg CLOSED для URP (96/96 GPU matrix; Godot GPU цих fixtures і HDRP render відкриті). A04 CLOSED для URP: інтегровано read-only план GLM variant=max (`glm-depth-fix-max.jsonl`) з узгодженим з користувачем рішенням «паритет за builder-ами» — sprite/trail test вимикається лише за header 2DRender (create_id_100/101/110), particle — за ZEnable=0 або 2DRender=1 (create_id_120, z_test_enabled); `_ZWrite` не змінено. Додано `_ZTest` у4 шейдери, три factory-зміни, стінну матрицю2×2 (a04Pass=true, header2dRed 0→1) і GfxDepthTestTests. Звірка enum: LessEqual=4, Always=8 (перша спроба LEqual — CS0117, виправлено). Уперше запущено повний NUnit suite:404 тести,398 passed,6 failed — усі6 невдач поза GFX-доменом; discovery через `unity command test list/run` працює. Залишки класифіковано як «невідомо, потрібен оригінальний Angelica reference»: Godot102/150/180/190/210 ніколи не вимикають test, ring140+ZEnable=0 та trail180+2DRender=1 розходяться з вибраними правилами, ZEnable=0 для sprite відносно оригіналу відкрите. Деталі й межі — у GFX_PARITY_AUDIT_RESULTS.md, розділ проходу5. Наступний пакет: trail N09/N10/C01/C02, A03, A05/A15, N03, A10.

Прохід6, 2026-11-13: N03 (Quota=0) і N04 (рання втрата живої частинки) CLOSED одним виправленням `GfxParticleParams.PoolSize` — точний перенос Godot `_calc_pool_size` (`create_id_120.gd:235–243`): `pool = int(TTL*rate)+1`, quota≠−1 обмежує, clamp [0,750]; Quota=0 → порожній pool, автоматика лише за −1. Знахідка середовища: Mono 6.13 обчислює `float*float` у double (`(int)(0.7f*10f)`=6 від 6.99999988), потрібен явний `(float)` cast перед обрізанням; без нього була регресія 386/4644. Виправлено харнесс-баг: `godot_affectors.gd` хардкодив `Pool_Size:8` замість production `_calc_pool_size`. Докази: particles 4644/4644 (було 4633/11, pool diffs 24→0), affectors 212/232 — без змін, сукуп N08 (pool diffs 232→0 після фіксу оракула), повний NUnit 407/401/6 (ті самі 6 поза-GFX), console errors=0. Сукупний CPU після проходу6: 6509 fixtures, 6472 збіги, 37 розбіжностей (N01 12, N02 5, N08 20); цього проходу перезапущено лише групи particles/affector. Межі: Godot GPU для Quota=0 не запускався, оригінал Angelica для Quota не консультувався, вплив Mono-квірка на інші `(int)(floatExpr)` не аудитувався. Деталі — у GFX_PARITY_AUDIT_RESULTS.md, розділ проходу6. Наступний пакет: A03 BindEle (P1), A05/A15, trail N09/N10/C01/C02, A10, A11, N08.


Прохід7, 2026-11-13: N01 CLOSED у ElementTime, signed seek збережено через `%`, спільний Fmod не змінено. Поточні Godot/Unity CPU: 480/480 timing, 478/483 keypoints (лише N02), 45/45 curves; NUnit Gfx 190/190. Агрегат попередніх груп: 6509/6484/25, не повний новий прогін. A03 окремо делеговано GLM Flash variant=max для read-only плану, статус відкритий. Деталі й докази у проході7 звіту.


Прохід8, 2026-11-14: A03 PARTIAL. GLM Flash variant=max завершив незалежний read-only огляд; основний агент відтворив і виправив пропуск transform-контролерів пізніших keypoint-ів після wrap власного часу. Новий render-fixture: до 99/102, після 102/102; NUnit Gfx 199/199, у тому числі 9 BindEle tests з prefab для100/101. Наступні A03 підпункти: Ctrl110 bound duration, CtrlStartTime+Acc (shader-vs-CPU Godot), interpolation/orbit та domain reload. A05 окремо: проєкційна ширина не дорівнює геометричній, ближній контроль збережений. Деталі проходу8 у звіті.


Прохід9, 2026-11-14: закрито shader-підпункт A03 Ctrl StartTime+Acc для sprite100/101. Додано spriteShaderTiming, збережено спільний CPU-контракт. До правки14/150 render failures; після розширення на обидва ID198/198 збігів, NUnit Gfx206/206. CPU без нових розбіжностей. Загальний A03 PARTIAL: Ctrl110 і складні комбінації залишаються; незалежний read-only GLM Flash variant=max аналізує тільки Ctrl110, основний агент виконав прискорення та перевірки. Деталі — прохід9 звіту.


Завершення проходу9: GLM read-only Ctrl110 огляд отримано й використано. Виправлено sprite-only duration/progress; до340/390, після390/390; NUnit Gfx213/213, CPU без нових відхилень. Наступний A03 пакет: raw-linear geometry vs GenPath, KP0 position addition, pre-start path/CalcDir, orientation composition і suppression Ctrl100/104; потім interpolation/orbit та domain reload. A03 лишається PARTIAL. Межі й точні докази — завершення проходу9 звіту.


Прохід10: sprite100/101 Ctrl110 raw-index geometry, додавання base position й двоточкові шляхи перевірено та виправлено. До452/486, після486/486; NUnit Gfx218/218 з prefab roundtrip двох типів. CPU без нових розбіжностей, particle-affectors212/232 (N08 не закрито). A03 PARTIAL — CalcDir/pre-start/suppression ще в окремому GLM read-only огляді; interpolation/orbit/domain reload залишаються. Подробиці проходу10 у звіті.


GLM orientation/pre-start/suppression огляд проходу10 завершено. Наступний пакет конкретизовано: pre-start110 і suppression100/104 за kp-base, потім CalcDir composition з KP0/billboard. Перед CalcDir GPU-тестами виправити oracle (Unity projected width/render замість mesh width), quaternion45° та фактор2 у Width у GLM-прикладі. Ці рекомендації ще не production-фікс.


Прохід11: pre-start110/suppression100/104 виправлено;606/606 synthetic observations,NUnit223/223,CPU без нових розбіжностей. Вперше реальний користувацький GFX(all10 elements,7original DDS) прогнано і зіставлено GPU Godot/Unity:12timestamps+10isolated elements. Mean IoU0.6975,MAE0.05662 — описові метрики, візуальний паритет НЕ закрито. Наступний пріоритет: реальний element9 smoke particle розподіл/яскравість; sprite1/7 orientation/UV і sparks6/10. Усі наступні фікси перевіряти реальним capture harness. Деталі проходу11 у звіті.


Прохід12: виправлено particle seed джерело на фактичне Godot4.7.2 Node.name (Element_9_0_ID_123). РеальнийGFX capture виконано;40particle emissions при1000ms мають однакові seed/age/pos/size. Візуальні розбіжності не закриті (IoU0.6816,MAE0.05735); наступна ізоляція handedness/billboard/UV/blend за однакової симуляції. Старі assets потребують rebuild для нового seed; lightning/bolt names не змінені.
