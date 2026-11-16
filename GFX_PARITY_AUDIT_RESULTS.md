# Детальна перевірка GFX: результати фактичного виконання

Актуальне доповнення проходу9: sprite100/101 acceleration та Ctrl110 timing виправлено; BindEle matrix390/390, NUnit Gfx213/213. A03 PARTIAL — geometry/CalcDir та складні комбінації відкриті; подробиці в кінці звіту.

Актуальне доповнення проходу7: N01 закрито; зведений CPU-рахунок 6509/6484/25 (прогони різних проходів), поточний NUnit Gfx 190/190. Деталі та обмеження — в кінці звіту.

Дата: 2026-11-12. Завершено шість блоків виконання плану, **не сертифікація повної відповідності**. Runtime/shader під час цих блоків не виправляли: спочатку зафіксовано відтворення дефектів. Попередні незакомічені зміни збережено. Пізніші блоки нижче уточнюють початкові статуси. Сукупний CPU результат станом на прохід6 — **6509 fixtures, 6472 збіги, 37 розбіжностей** (N01 12 + N02 5 + N08 20); цього проходу перезапущено лише групи particles (4644/4644) і affector (212/232), решта груп має результати попередніх блоків/проходів і не перераховувалась. Раніше зафіксовані 6509/6415/94 — до виправлень проходів1–6. Lifecycle, render і локальні spot-checks до цієї суми не входять.

## Середовище та метод

- Godot `4.7.2.stable.official.ed1daf0bf`: ізольований headless-проєкт; незмінені копії production `gfx_keypoint_eval.gd` та `gfx_curve_path.gd`.
- Unity `6000.0.60f1`, проєкт `A:/Github/Unity Native Reader`, локальний пакет цього репозиторію; NVIDIA GeForce RTX 3050 6GB Laptop GPU.
- Числові fixtures спільні для обох рушіїв. Викликаються production evaluator-и, а не переписані тестом формули.
- Допуск `abs(error) <= 1e-5 + 1e-4*abs(Godot)`; для quaternion враховано еквівалентність q та −q.
- Unity render: ізольована preview scene, ортографічна камера, RT 64×64 ARGBFloat Linear, фактичний `Camera.Render` та `ReadPixels`, production sprite material/shader. Тести alpha використовують білу текстуру, additive blend і чорний фон.
- Lifecycle: справжні `SaveAsPrefabAsset` / `LoadPrefabContents`. Для Grid/Phys/Bolt збережені також mesh/material assets; джерело keypoints серіалізовано через `SetAnimationSource` до збереження. Отже, результат не пояснюється лише відсутньою текстурою чи прямим несеріалізованим `SetKeypoints`.
- Сцену користувача не зберігали й не замінювали. Початкова спроба additive scene зупинилася через unsaved untitled scene; harness переведено на preview scene, повторний запуск успішний.

## Порівняльні CPU тести

| Група | Кейси | У допуску | Розбіжності |
|---|---:|---:|---:|
| Timing | 480 | 468 | 12 |
| Keypoints/controllers | 483 | 478 | 5 |
| Curve path | 45 | 45 | 0 |
| Разом | 1008 | 991 | 17 |

Покрито Ctrl100–111, чотири комбінації порядку контролерів, InterpolateMode 0/1/2, TimeSpan −1/0/1000, межі часу, repeat −1/0/1/2, delay 0/200, п'ять геометрій кривих, endpoint/out-of-range ratios. Це вибірка, не повний перебір можливих параметрів.

**N01 — signed modulo, P2.** Для 12 fixtures із часом −1 ms Godot повертає −1, Unity — 2999. Unity `GfxMath.Fmod` робить результат додатним, GDScript `fmod` зберігає знак. Сфера відтворення — від'ємний manual/seek time; звичайне додатне відтворення цим тестом не визнано несправним.

**N02 — точність шуму масштабу, P2.** П'ять додаткових Ctrl109 fixtures на довгому часі перевищують допуск. При `wave_len=0.01`, `t=167000.125 ms`: Godot scale `1.16565516311981`, Unity `1.04055548`. При `wave_len=0.5`, `t=100000.05 ms`: `1.18082567845186` проти `1.1812638`. Це числова розбіжність float32/float64, а не доказ того, який результат ближчий до оригінальної гри. Потрібен продуктовий допуск для таких довгих/високочастотних ефектів.

## Відтворені інтеграційні проблеми

| ID плану | Спостереження | Висновок і межа доказу |
|---|---|---|
| A01 | Pixel red: alpha=1 → 1; alpha=0.5 → 0.25. `_BaseColor.a=0.5`, `_GfxAlpha=0.5`. | Alpha застосована двічі. Очікуване відношення для цього additive fixture — 0.5. Фактичний render Unity. |
| A02 | Для асиметричної текстури 2×2, атласу 1×1, UVExchg false/true дають однаковий зелений піксель у позадіагональній точці; UV rect в обох випадках `[1,1,0,0]`. | UVExchg не переставляє координати семплування. Godot `create_id_101.gd` генерує відповідний UV swap; окремий Godot render ще потрібний. |
| A04 | За непрозорим кубом red=0 і при ZEnable=false, і при header 2DRender=true; без куба red=1. | Header 2DRender не вимикає depth test у Unity. **Уточнення плану:** рядки 208–211 Godot sprite стосуються header 2DRender, не ZEnable. Сам по собі ZEnable=false у sprite не доводить міжрушієву розбіжність. |
| A09 | `Instantiate` зберігає sharedMaterial. Tick оригіналу з alpha=0.25, потім клону з alpha=0.75 змінює `_GfxAlpha` матеріалу оригіналу з 0.25 на 0.75. | Екземпляри впливають один на одного. Підтверджено runtime cloning; окремий scene/domain-reload шлях ще не пройдено. |
| A07 | Container keypoint alpha=0.25, зовнішня alpha=1 → child GfxAlpha=1 та child material alpha=1. | Числова прозорість контейнера не передається дочірньому спрайту. |
| A06 container | Після prefab load `Sub_GFX` у hierarchy існує, `SubGfx` reference — null. | Втрачено зв'язок керування вкладеним ефектом. |
| A06 grid210 | До save Params != null; після load Params == null. | Параметри не відновлено. Renderer у виміряному стані лишився enabled — не можна стверджувати, що він обов'язково прихований; можливе завмерле зображення. |
| A06 phys220 | До save Params != null, particles=1; після load Params=null, particles=0. | Симуляція після завантаження не відновлюється. |
| A06 bolt151 | До save Params != null, Strokes=2; після load Params=null, Strokes=0, але child objects=2. | Геометрична hierarchy збереглася, керований список розрядів — ні. |

## Незалежний огляд GLM

Використано OpenCode CLI, `opencode/glm-5.3-flash-free`, read-only agent plan. Передача GFX-коду явно дозволена користувачем. Завершений вузький огляд математичних evaluator-ів збережено в `.validation/gfx-parity/glm-detailed-findings.md`; сирий transcript — `glm-detailed.jsonl`.

- Signed modulo підтверджено незалежними запусками рушіїв.
- Гіпотеза втрати точності шуму підтвердилася на 5 із 24 додаткових fixtures; для Ctrl107 у цій вибірці розбіжностей понад допуск не отримано.
- Числовий приклад GLM «identity → 2°, midpoint: Unity 0.87° замість 1°» **відхилено**: дев'ять додаткових near-parallel interpolation fixtures пройшли допуск. Нормалізований lerp у midpoint для одиничних quaternion не дає заявленої помилки. Відмінний поріг nlerp існує в коді, але наведений дефект не доведено.
- Зауваження про invalid/null input і Bezier rounding лишаються результатом огляду, не engine repro; їх не зараховано як нові підтверджені баги.

## Артефакти та повторення

Harness у `Tools/Validation/GfxParity/`: `generate_cases.py`, `godot_cpu.gd`, `UnityCpuAudit.cs`, `compare_cpu.py`, `UnityIntegrationAudit.cs`, `UnityLifecycleAudit.cs`.

Машинні результати в ігнорованій `.validation/gfx-parity/`: `cases.json`, `godot-cpu.json`, `unity-cpu.json`, `cpu-comparison.json`, `unity-integration.json`, `unity-lifecycle.json`, `execution-manifest.json`. Оскільки каталог ігнорується Git, для передачі аудиту іншій машині ці результати потрібно передати окремо або відтворити harness.

Prefab докази в Unity: `Assets/GfxParityAuditEvidence/Container 2.prefab`, `Drivers.prefab` та mesh/material залежності. Попередні повтори Container.prefab/Container 1.prefab збережено як докази, не видалено.

Команди:

```powershell
python Tools/Validation/GfxParity/generate_cases.py
python Tools/Validation/GfxParity/prepare_host.py
# godot-host містить project.godot, harness та дві незмінені production копії
& 'B:/Godot/Godot_v4.7.2-stable_win64_console.exe' --headless --path .validation/gfx-parity/godot-host --script godot_cpu.gd --log-file '../godot-cpu.log'
unity command script run --project-path 'A:/Github/Unity Native Reader' --format json -- --file 'B:/Git/Godot_Forsaken_World_Editor/Tools/Validation/GfxParity/UnityCpuAudit.cs' --entry GfxCpuAudit.Main
python Tools/Validation/GfxParity/compare_cpu.py
# Аналогічно запускати UnityIntegrationAudit.cs / GfxIntegrationAudit.Main
# та UnityLifecycleAudit.cs / GfxLifecycleAudit.Main.
```

## Що ще не закрито

1. Порівняння кадрів з оригінальним відео, синхронізація камери/часу/експозиції та розширення render fixtures. Базові Godot render fixtures alpha/UVExchg/depth виконано в другому блоці нижче.
2. Реальний parser/build pipeline для corpus архіву й усіх ID, field-consumer matrix, malformed/encoding cases. Поточні CPU fixtures вже типізовані й не перевіряють парсер.
3. Particle simulation/affectors, детермінізм seed, birth/death, timestep/seek, per-particle UV, Dummy/BindEle, NoChangeDir, вкладені трансформації.
4. Scene reopen, domain reload, Enter Play Mode з reload on/off, повторний import, export/runtime build; поточний lifecycle блок — prefab save/load трьох driver-ів і контейнера.
5. HDRP, інші GPU/API, stress/performance, memory/resource cleanup, multi-instance tests для інших матеріалів, blending/warp/priority та повна UV matrix.

Жодна кількість пройдених fixtures не гарантує відсутності всіх багів. Наступний блок має закрити particle/Dummy lifecycle і Godot render reference до початку остаточного візуального зіставлення з грою.

## Блок 2 — частинки, Dummy та фактичний Godot render

### Порівняльна симуляція: 4644 кейси

**4633 збіги, 11 розбіжностей.** Разом із першим CPU блоком: **5652 кейси, 5624 збіги, 28 розбіжностей**. Це кількості fixtures, не кількість різних багів. Інтеграційні/render перевірки в ці числа не включено.

Базова матриця: ID 120/121/123/124 × IsBind false/true × IsSurface false/true × seed 0/41/65535 × 12 часових точок × 8 варіантів affectors (відсутній або 100/101/102/103/104/106/109). Усі **4608** базових fixtures збіглися. Додатково виконано 36 boundary fixtures Quota −1/0/1 для ID120; 11 з них розійшлися (усі виправлено проходом6 — див. розділ «Виправлення — прохід6»).

Порівнюються living emissions за `e_idx`, їхні age, position, move, rotation та size з допуском `1e-5 + 1e-4*abs(Godot)`. Слот у circular pool може відрізнятися між рушіями, тому масиви не зіставляються механічно за індексом; відмінності розміру пулу записано окремо. Це **не** тест color/HSV, UV, Facing, комбінованих affectors, 105/107/108/110/111, рухомого emitter-а чи ID122/125 emission parity. У Godot використано production `gfx_particle_eval.gd` та `_calc_pool_size` з `create_id_120.gd`, у Unity — `GfxParticleSim` і `GfxParticleParams.PoolSize`. Вхідні shader uniforms/typed params задано harness-ом, парсер не покрито.

**N03 — Quota=0, P1.** *(статус: **CLOSED** у проході6 — див. «Виправлення — прохід6»; нижче описано відтворення до виправлення.)* Production Godot pool=0; Unity трактує q<=0 як автоматичний розмір. При rate=10, TTL=0.7, t=500ms Godot не має частинок, Unity має 5. Відтворено на 9 часових точках. Джерело: `GfxParticleParams.cs:108–117` проти `create_id_120.gd:235–243`. Це міжрушієва розбіжність; семантику оригінального Angelica тут окремо не перевірено.

**N04 — рання втрата живої частинки біля межі emission, P2.** *(статус: **CLOSED** у проході6 — див. «Виправлення — прохід6»; нижче описано відтворення до виправлення.)* Quota=−1, rate=10, TTL=0.7: Godot pool=8, Unity pool=7. При t=799.99ms emission 0 ще жива у Godot (age=0.69999), але відсутня в Unity. Повторено при 1499.99ms. Поєднання округлення emitted із `+0.0001` та меншого circular pool витісняє стару частинку перед тим, як наступна має невід'ємний age. Різниця пулу не завжди нешкідлива; твердження GLM про відсутність візуального впливу відхилено цим repro. Вікно дуже вузьке, тому пріоритет нижчий за Quota=0.

### Particle/Dummy prefab lifecycle

`UnityParticleAudit.cs` створює ізольований prefab із серіалізованим animationSource, persistent material/mesh dependencies та завантажує його через PrefabUtility. Остаточний доказ: `Assets/GfxParityAuditEvidence/Particles 2.prefab`.

| Перевірка | До save | Після load |
|---|---|---|
| Particle template IsDummy | true | false |
| Emitter з DummyEle=Template | Dummy kind=Element, slots=8, Shuriken particles=0 | Dummy kind=None, slots=0, Shuriken particles=5 |
| Curve125 | CurvePoints=2, перша позиція ≈(1.51779,0,0) | CurvePoints=0, позиція (0,0,0) |
| Mesh122 | MultiPlanePoints=1, перша позиція (2,0,0) | MultiPlanePoints=0, позиція (0,0,0) |
| Звичайний emitter120 | 5 частинок із ненульовими координатами | ті самі 5 координат |

Це фактичні підтвердження A08: втрата даних змінює геометрію emission та перемикає Dummy-emitter на звичайні quads. Не робимо висновку, що всі emitter-и ламаються: ordinary control зберігся.

Для A14 додано sprite source з `IsDummy=1`, Name=SpriteTemplate, неактивний GameObject. `NameMatches=true`, але `FindDummy=null`. Відповідний `IsDummyElement` повертає false для sprite незалежно від цих даних. Це тест реального lookup, ще не end-to-end імпорт з файлу.

Seek control: seed=41, Speed=3, Angle=0.4, послідовність 500→1000→500→0→500ms. Усі три результати для 500ms мають однакові координати п'яти частинок; 0ms дає порожній набір. Пройдений конкретний fixture, не загальна гарантія seek для всіх affectors/контейнерів.

### Рендер у Godot: A01/A02/A04

Реальний production builder `create_id_101.gd` генерує shader; `gfx_element_101.gd` забезпечує property conversion. Зображення прочитані з SubViewport 64×64. Виконано Compatibility/OpenGL та Forward+/Vulkan на тій самій NVIDIA GPU. Остаточний Forward+ запуск без shader/cache errors; `valid_control=true`. PNG 0..5: full alpha, half alpha, UV normal, UV exchange, depth normal, depth 2D.

| Fixture | Godot | Unity з блоку 1 |
|---|---|---|
| Alpha=0.5 | Forward+ encoded pixel 0.7372549, після sRGB→linear ≈0.5028865 | Linear RT pixel 0.25 |
| UVExchg 1×1 | Зелений семпл стає синім | Зелений залишається зеленим |
| Непрозорий blocker, 2DRender=false | black | black |
| Той самий blocker, 2DRender=true | white | black |

Compatibility raw alpha pixel≈0.498; його додатково обчислене `linearizedRatio` не слід порівнювати з Unity Linear RT, бо blend/color pipeline інший. Для кількісного висновку про alpha використано **Forward+ із sRGB decode**, а не сирі 8-bit значення з різних pipeline. UV-текстура в Godot лінійно фільтрується, тому доведено зміну каналу/семплування, а не pixel-perfect equality країв. Це синтетичні контролі; відео гри ще не зіставлено.

Перші дві спроби Godot fixture були невалідні: відсутні обов'язкові RepeatDelay/StartTime/RotFromView і неправильний розділювач quaternion у тестових даних. Їхні чорні кадри відхилено; fixture виправлено без змін production коду. Початковий log збережений як `godot-render-invalid-fixture.log`. Такі помилки самі по собі не зараховано до багів імпорту реальних файлів.

### Незалежний GLM огляд другого блоку

Завершений read-only огляд `opencode/glm-5.3-flash-free`: `.validation/gfx-parity/glm-particles-findings.md`, transcript `glm-particles.jsonl`. GLM підтвердив шляхи A08/A14 і запропонував Quota=0 — перевірено фактично. Його припущення про нешкідливість розміру пулу спростовано N04. `DefaultAlpha` у instanced Dummy path лишається кандидатом для render repro; точне «2× opacity» не прийнято без тесту, зокрема через вже відомий відсутній shader consumer `_DummyCustom`. `NoChangeDir` дублює A15; не зараховано як новий баг.

### Відтворення й наступні прогалини

Нові harness-и: `generate_particle_cases.py`, `godot_particles.gd`, `UnityParticleCpuAudit.cs`, `compare_particles.py`, `UnityParticleAudit.cs`, `godot_render.gd`. `prepare_host.py --extended` копіює незмінені Godot .gd dependencies і harness-и та зберігає SHA256. Підготовка не переносить game assets і не змінює основний Godot project.

```powershell
python Tools/Validation/GfxParity/prepare_host.py --extended
python Tools/Validation/GfxParity/generate_particle_cases.py
& 'B:/Godot/Godot_v4.7.2-stable_win64_console.exe' --headless --path .validation/gfx-parity/godot-host --script godot_particles.gd
unity command script run --project-path 'A:/Github/Unity Native Reader' --format json -- --file 'B:/Git/Godot_Forsaken_World_Editor/Tools/Validation/GfxParity/UnityParticleCpuAudit.cs' --entry GfxParticleCpuAudit.Main
python Tools/Validation/GfxParity/compare_particles.py
# Godot render: прибрати --headless, --script godot_render.gd --rendering-method forward_plus
```

Машинні докази: `particle-cases.json`, `godot-particles.json`, `unity-particles-cpu.json`, `particle-comparison.json`, `unity-particles.json`, `godot-render-forward_plus.json`, `godot-forward_plus-0.png` … `-5.png`, generated shader, logs та `execution-manifest-block2.json` в `.validation/gfx-parity`.

Наступні пріоритети після блоку 2: реальний corpus/парсер, ID122/125 та решта affectors, Dummy instancing render, BindEle/NoChangeDir із повернутими parent-ами, scene/domain reload, повне кадрове зіставлення з відео. Частину закрито блоком 3 нижче. HDRP/build/stress залишаються відкритими.

## Блок 3 — реальний файл, парсер, прив'язки та батьківські transform

### Corpus і незалежний огляд

Архів користувача містить **один GFX-файл із 10 елементами**, а не великий corpus. Його байти без перекодування скопійовано в `.validation/gfx-parity/corpus/000.gfx`, SHA256 і оригінальне ім'я записано у `corpus-manifest.json`. Архівні шляхи не використовуються як шляхи розпакування: тестові файли мають контрольовані числові імена.

Production парсери виконали 13 fixtures: файл користувача, UTF-8/UTF-8 BOM/GBK × LF/CRLF, grid animation block, standalone controllers, дві BindEle-конфігурації, colonless grid і malformed count. Порівняно всі header/element properties, keypoints, flattened/per-keypoint controllers, affectors, PS constants/targets, grid animation strings. **11 fixtures повністю збіглися**, включно з усіма 10 елементами користувача. Дві структурні відмінності:

- Colonless grid: перший рядок має ключ `GridVector_1` у Godot та `GridVector` у Unity. Підтверджено парсерами, але `GfxGridParams.cs:65–69` уже має fallback для обох ключів. Твердження GLM, що це обов'язково ламає Unity consumer, відхилено; візуальний дефект не доведено.
- `AffectorCount: 2abc`: Godot читає 2 affectors, Unity — 0 із явним warning. Це malformed input, не баг правильного GFX. Різниця не впливає на досліджений файл користувача.

GLM `opencode/glm-5.3-flash-free` завершив read-only parser review: `glm-parser-findings.md` і `glm-parser.jsonl`. Його третя гіпотеза — strict UTF-8 проти слабшої ручної валідації некоректних послідовностей — ще не перевірена рушіями; не включено до підтверджених дефектів. Harness спочатку читав UTF-8 JSON через локальну Python GBK locale; це помилка harness, виправлена явним encoding, не дефект GFX decoder.

### A03 BindEle: реальний parser → builder → виконання

Два fixtures відрізняються лише `BindEle: Target` у follower. Follower: TimeSpan=500ms, RepeatCount=−1, Ctrl100 Dir=(1,0,0), Vel=1. Вимірювання при t=750ms:

| Fixture | Godot render centroid X | Unity transform X |
|---|---:|---:|
| BindEle порожній | 0.25 | 0.25 |
| BindEle=Target | 0.75 | 0.25 |

У Godot використано `gfx_parser.build_file_node` і production sprite builder; виміряно центр 36 білих пікселів у SubViewport 128×128. У Unity — `GfxParser` + `GfxSceneBuilder.Build(document)` + `GfxPlayer.Tick`. В обох parsed документах BindEle збережений; Unity не застосовує прив'язку при виконанні. **A03 підтверджено end-to-end для цього сценарію.**

Важлива межа: Godot builder шукає `KeyPoint_0` у target dictionary, тоді як parser створює `_keypoints`; у цьому fixture fallback дає fb_total=−1 і неперервний transform time. Це доводить фактичну розбіжність рушіїв, але не доводить правильність усієї Godot BindEle семантики відносно Angelica. Довільні target controllers та коректність target loop ще потребують окремих fixtures.

### A05/A15: fade, NoChangeDir і facing під parent

Godot використовує production `gfx_data_node.gd`. Unity використовує реальні `GfxPlayer` і `GfxSpriteDriver`, а не лише helper math. Camera=(0,0,5); test camera явно перевірена як Camera.main.

| Перевірка | Godot | Unity |
|---|---|---|
| FadeStart=0, FadeEnd=10, початковий distance=5 | alpha=0.5 | alpha=0.5 |
| Parent зміщений на (100,0,0), world distance≈100.125 | alpha=0 | alpha=0.5 |
| Parent повернений на Y=90°, NoChangeDir=1 | world forward=(0,0,1) | world forward≈(1,0,0) |
| Той самий parent, FaceToViewer=1 | alignment із камерою=1 | alignment≈0 |
| Sprite101 billboard, parent Y=90° | окремий Godot sprite measurement не виконано | alignment≈0 замість 1 |

A05 підтверджено для header fade/FaceToViewer; A15 підтверджено для NoChangeDir. Billboard має самостійний Unity repro: normal перпендикулярна напряму на камеру. Для camera-dependent перевірки preview scene не підходить: Camera.main її не бачить. Перша спроба зупинилася guard-ом, друга — забороною SceneManager.CreateScene в edit mode. Остаточний тест створює тимчасову hierarchy у відкритій сцені, тимчасово вимикає попередні main cameras та відновлює їх у finally; test hierarchy видалена, сцену не збережено. Scene dirty flag може лишитися, його навмисно не скидали, щоб не приховати попередні користувацькі зміни.

### Збережена сцена реального ефекту

`ReferenceFirePreview.unity` прочитано через `OpenPreviewScene`, знято 5 snapshots при 0/250/500/1000/2000ms і закрито без збереження. У всіх 10 елементів є матеріал і текстура `_BaseMap`. Спрайти мають по 4 вершини, три trails — по 102. Чотири particle emitter-и при 500ms мають 15/4/14/6 частинок, при 1000ms — 15/4/0/6, при 2000ms — 0/0/0/0. Це підтверджує завантаження залежностей і зміну симуляції, **не** правильність рендеру/таймінгу відносно відео. Preview scene не має доступної Camera.main, тому facing тут не оцінювався.

Початковий тест помилково перевіряв Material.mainTexture; shader не має відповідного MainTexture атрибута. Перевірку виправлено на фактичний `_BaseMap`, усі текстури знайдено. Відсутність текстур не зараховувалася до багів. Domain reload, Play Mode reload variants та повне scene reopen в активному Editor все ще відкриті.

### Артефакти й повторення блоку 3

Harness: `prepare_corpus.py`, `godot_parser_audit.gd`, `UnityParserAudit.cs`, `compare_parser.py`, `godot_transforms.gd`, `UnityTransformAudit.cs`, `godot_bind.gd`, `UnityBindAudit.cs`, `UnityReferenceSceneAudit.cs`. `prepare_host.py --extended` тепер також копіює production `gbk_table.bin`.

Результати: `corpus-manifest.json`, `godot-parser.json`, `unity-parser.json`, `parser-comparison.json`, `godot-transforms.json`, `unity-transforms.json`, `godot-bind.json`, `unity-bind.json`, `godot-bind-on.png`, `godot-bind-off.png`, `unity-reference-scene.json`, logs і `execution-manifest-block3.json` в `.validation/gfx-parity`.

Unity CLI `1.0.0-beta.17` перевірено як актуальну; Editor `6000.0.60f1` ready. Після появи skill unity-cli подальші команди позначені `--caller plugin --skill unity-cli`. Production runtime/shader у цьому блоці не змінювали. Нові fixtures не додаються механічно до попередніх CPU totals, оскільки вимірюють інші рівні системи.

## Блок 4 — scene roundtrip, domain reload та instancing

### A12: Build(plan) втрачає анімацію після збереження сцени

Однаковий parsed документ зі sprite101 і Ctrl100 Vel=2 збудовано двома overload-ами. При 250ms обидва мають 1 keypoint і X=0.5. Після справжнього SaveScene та OpenPreviewScene:

- Build(document): 1 keypoint, X=0.5.
- Build(plan): 0 keypoints, X=0.

Mesh/material залежності збережені як assets. Доказ — `Assets/GfxParityAuditEvidence/Roundtrip 1.unity`, `unity-scene-roundtrip.json`. Runtime API дефект підтверджено, але не слід стверджувати, що звичайний Browser import використовує цей overload: `GameDataBrowserWindow` і `GfxBatchImport` викликають document overload. Document overload внутрішньо використовує plan overload і потім додає animationSource.

У тому самому scene roundtrip підтверджено A06 для Grid/Phys/Bolt/Container: Params true→false у перших трьох, Phys particles 1→0, Bolt strokes 2→0, Container child лишається, reference true→false. Отже, проблема не обмежена prefab. Preview scene не можна зберегти напряму; початкова спроба SaveScene відхилена Unity, після чого використано окрему копію scene asset, відкриту additive. Сцену користувача не замінювали і не зберігали; тимчасова сцена закрита.

### N05: script/domain reload ламає навіть Build(document), P1

Два незалежні запуски `EditorUtility.RequestScriptReload()` із живими об'єктами, створеними через document/plan overload. У журналі Editor зафіксовано `Reloading assemblies due to reload request`, `Mono: successfully reloaded assembly`, `Scripting: domain reloads=1`. AppDomain.Id лишився 1, тому він **не використовується** як доказ перезавантаження.

Для document перед reload: sourceKeypoints=1, runtime keypoints=1, animationRestored=true, X=0.5. Після reload: sourceKeypoints=1, runtime keypoints=0, animationRestored=true, X=0. Джерело анімації не втрачено, але прапорець стверджує, що порожній runtime cache вже відновлено. `GetKeypoints`/`RestoreAnimationSource` через цей прапорець не перебудовують список. Контрольна дія **лише на тимчасовому fixture**: повторний SetAnimationSource із тим самим збереженим джерелом → keypoints=1, X=0.5. Це підтверджує механізм дефекту; production виправлення не внесено.

Plan після reload також має 0 keypoints, але джерела немає/воно порожнє, тому там окрема проблема A12. Докази: `unity-domain-before.json`, `unity-domain-after.json`, `domain-reload-log-evidence.txt`, harness `UnityDomainReloadAudit.cs` (Setup, після завершення reload — Inspect). Inspect закриває тестову сцену й видаляє SessionState marker. Play Mode із reload on/off ще не перевірено; script reload не прирівнюється до всіх Enter Play Mode сценаріїв.

### A13: несумісний instancing shader приймається збирачем

`GfxDummyInstancing.TryCollect` прийняв sprite mesh/material і створив 1 batch. Production sprite shader не має `#pragma multi_compile_instancing`, instance-ID setup та consumer `_DummyCustom`.

GPU контроль у Linear ARGBFloat RT 64×64 через explicit CommandBuffer:

| Виклик | Center red |
|---|---:|
| Звичайний DrawMesh з GFX shader | 1 |
| DrawMeshInstanced зі стандартним URP/Unlit, Cull Off | 1 |
| DrawMeshInstanced з GFX shader, custom alpha=1 | 0 |
| Те саме, custom alpha=0.5 / 0 | 0 / 0 |

Це підтверджує проблему instanced draw із прийнятим GFX shader. Воно **не вимірює** відношення індивідуальної alpha/age: shader спочатку має коректно рендерити instancing. Також це explicit DrawMeshInstanced probe, а не повна перевірка RenderMeshInstanced у живому frame-loop.

Початковий RenderMeshInstanced+Camera.Render у preview scene дав black і на контрольному URP матеріалі; ці samples збережені, але **невалідні для висновку**. Перший explicit control також був black через backface culling; після Cull Off контроль став white. Результат не спирається на чорні кадри без позитивного контролю. Артефакт: `unity-dummy-render.json`; valid поля — directGfxControlRed=1, immediateControlRed=1, immediateSamples. Deferred samples не зараховуються як доказ.

Production код не змінювали. Нові harness-и: `UnitySceneRoundtripAudit.cs`, `UnityDomainReloadAudit.cs`, `UnityDummyRenderAudit.cs`. Наступні незакриті питання: повний Dummy frame-loop з age/alpha, Play Mode reload variants, ID122/125/решта affectors, HDRP, stress/build і синхронізоване порівняння з відео.

GLM завершив незалежний read-only review (`glm-instancing-findings.md`, `glm-instancing.jsonl`): підтвердив відсутність instancing variant/custom-data consumer і джерела анімації у Build(plan). Висновок «document callers unaffected» застосовний лише до A12 scene serialization; як загальний висновок він **неправильний через N05**, відтворений фактичним domain reload. Твердження GLM «TryCollect correct» також не прийняте як оцінка поведінки: збирач приймає shader, який не пройшов instanced render control. Запропоноване GLM виправлення не виконували й не вважали перевіреним.

## Блок 5 — розподілена перевірка emitter-ів та affectors

За уточненням користувача роботу розділено, а не продубльовано: основний агент відповідає за geometry emitter122/125 та запуск рушіїв; GLM отримав окреме завдання — перевірка affectors105/107/108/110/111/HSV і підготовка точних fixtures (uniforms, info, C# mapping). Його область не переглядалася повторно основним агентом; результати інтегруються через тестові специфікації.

### Emitter122/125: 560 порівняльних fixtures

**560 збігів, 0 розбіжностей** у допуску `1e-5 + 1e-4*abs(Godot)` для живих emissions, зіставлених за e_idx. Порівнюються position, move, rotation (q/−q), size та age. Початковий набір 320 замінений фінальним 560 і не рахується вдруге.

Матриця: ID122/125, IsBind on/off, IsSurface on/off, seed0/41, t=100/250/500/700/800ms, початковий pose identity або position=(1,2,3), rotation Y=90°, scale=2. ID122: три точки й три одиничні normal, UseNormal off / PerpToNormal on / direction along normal. ID125: три точки, ratios 0/0.5/1, радіус0/0.4/0.001, average sequential/random і звичайне випадкове народження, AvgGenSeg=4/AvgStepCount=5. Граничний Radius=0.001 перевіряє вимкнення радіального offset.

Це готові геометричні дані в симуляторі, а не тест імпорту RefModel/побудови curve із Ctrl110. Не перевірені неповні arrays, невалідні normals, довільні AvgGenSeg, жива анімація emitter pose, mesh render і залежності зовнішніх моделей. Втрата цих arrays після prefab/scene/domain serialization залишається окремою підтвердженою проблемою.

Harness: `generate_geometry_cases.py`, `godot_geometry.gd`, `UnityGeometryCpuAudit.cs`, `compare_geometry.py`. Докази: `geometry-cases.json`, `godot-geometry.json`, `unity-geometry.json`, `geometry-comparison.json`, `godot-geometry.log` у `.validation/gfx-parity`.

Додатково production `create_id_125._avg_step_count` та `GfxParticleParams.AvgStepCountForSeg` дали однакові результати для **65 значень AvgGenSeg=0..64**. Дані: `godot-curve-step-counts.json`, `unity-curve-step-counts.json`. Це окрема перевірка float32 накопичення кроку; вона не замінює повну emission matrix з усіма цими сегментами.

### Делегована GLM частина: affectors105/107/108/110/111 та HSV

GLM самостійно підготував `glm-affectors-work.md`: таблиці raw→uniform→GfxCtrlSpec, time units, stage layout, HSV й три кандидати. Основний агент не повторював огляд цілком: використав таблиці для harness, перевірив тільки conversion105, path-array110 та completeness stage arrays. Виконаний GLM deliverable — окрема частина роботи, не друга копія emitter122/125 аудиту.

Harness використовує **production getters** `gfx_ctrl_<type>.gd`, production `_declare_path_array` і `gfx_particle_eval`; Unity — `GfxKeypointBuilder.FromDictionary` та `GfxParticleSim`. Тобто conversion bug не приховано ручним нормалізованим GfxCtrlSpec. Тестовий shader лише оголошує uniforms, render тут не перевіряється.

Фінальний набір: **232 fixtures, 166 збігів, 66 розбіжностей**. Початковий набір220 є підмножиною і не додається вдруге. Порівнюються position/move/rotation/age/size/color/keyAlpha. Варіанти:105 raw,107 explicit/default,108 full/AlphaOnly,111,110 two/three points, комбінована послідовність107→105→108→111, HSV і Ctrl105 з параметрами елемента9 користувача. Повні масиви count+1 для stages; shared quota8, clock10000ms усувають випадковий rollover/pool mismatch. У початковому GLM прикладі stage arrays були неповні, а clock3000 конфліктував із прикладом3250ms — ці неузгодженості специфікації виправлені до запуску, його числові очікування не використовувалися як oracle.

| Кандидат GLM / ID аудиту | Фактичне відтворення | Класифікація |
|---|---|---|
| D1 / N06 | Ctrl105 raw `20,-30,40,-128`, age0.25: Godot RGBA≈(1,.970588,1,.874510), Unity=(1,0,1,0). 18 isolated failures. | P1: пропущено /255 у Unity conversion. Окремо підтверджено на параметрах реального GFX. |
| D2 / N07 | Ctrl107 без OctaveNum/Persistence, age0.25: alpha Godot≈.789332, Unity≈.983435; explicit3/.6 збігається. 18 failures. | P2: різні fallback defaults; для повністю заданих полів цього дефекту не відтворено. |
| D3 / N08 | Ctrl110 Count2, endpoint(2,1,0): Godot particle position=(2,1,0), Unity≈(0,0,.001). Count3 control збігається. 20 failures. | P2: Unity FromDictionary відкидає affector через GenPath(null), Godot приймає raw array і snap до останньої точки. Валідність такого короткого path для оригінальної гри ще не доведено. |

HSV red→yellow, stages108/111, AlphaOnly,107 із explicit params,110 Count3 та комбінований fixture не розійшлися в допуску. Комбінований fixture **не спростовує N06**: пізніший108 замінює color і може приховати помилку105. Перевірено одну HSV color pair, не весь hue space. Не зараховуємо відсутність різниці в цій вибірці як повну parity.

### Вплив N06 на файл користувача

У parsed елементі9 (ID123) знайдено emitter Ctrl105: StartTime=.8, EndTime=−1, ColorDelta=`0, 0, 0, -500`. Це **CtrlMethod**, а не particle affector. Додано окремий emitter fixture із цими raw параметрами й keypoint duration2000ms; перевірено EmitterState через keyAlpha живої частинки. Позиційний Ctrl100 з оригінального елемента тут ізольовано виключено, тому це цільовий repro alpha, не повна реконструкція елемента.

| Element time, ms | Godot keyAlpha | Unity keyAlpha |
|---:|---:|---:|
|800|1|1|
|801|.998039|.499977|
|810|.980392|0|
|900|.803922|0|
|1000|.607843|0|
|1300|.019608|0|

Два seed дали однакову різницю: 10 додаткових failures, два baseline збіги. Це підтверджує причину передчасного alpha fade у параметрах реального ефекту; кадрове зіставлення з відео ще не виконано. Артефакт `user-effect-affector-impact.json` фіксує точні raw controllers із файлу користувача.

Нові harness-и: `generate_affector_cases.py`, `godot_affectors.gd`, `UnityAffectorCpuAudit.cs`, `compare_affectors.py`. Результати: `affector-cases.json`, `godot-affectors.json`, `unity-affectors.json`, `affector-comparison.json`, log. Документ GLM — `glm-affectors-work.md`, transcript — `glm-affectors.jsonl`. Unity Native Reader був закритий між повідомленнями; для завершення engine tests його запущено заново у batchmode (6000.0.60f1). Інший відкритий проєкт не змінювався.

Production runtime/shader не змінювали. Підсумок цього блоку: geometry560/560, step-count65/65, affectors166/232. Загальний раніше зафіксований CPU набір5652 разом із новими560+65+232 дає **6509 fixtures: 6415 збігів, 94 розбіжності** (fixtures, не кількість багів; parser/render/lifecycle поза цією сумою).

## Блок 6: Enter Play Mode та делегована перевірка шлейфів

### Чотири режими перезавантаження Unity

`UnityPlayModeAudit.cs`: окрема additive test scene, два свіжі екземпляри одного parsed рухомого sprite — `Build(document)` і `Build(plan)`. До входу обидва мають одну ключову точку та X=.5 при ручному tick250ms. GfxPlayer вимкнено навмисно: вимірюється збереження даних через перехід, а не швидкість автоматичного програвання. Перевірено, що справжній Play Mode просуває кадри між двома замірами. Позначення таблиці: кількість keypoints / X після tick250ms.

| Enter Play Mode options | Document у Play | Plan у Play | Document після Stop | Plan після Stop | Кадри двох замірів |
|---|---|---|---|---|---|
| 0: reload domain + scene | 1 / .5 | 0 / 0 | 1 / .5 | 0 / 0 | 1248 → 31025 |
| 1: disable domain reload | 1 / .5 | 0 / 0 | 1 / .5 | 0 / 0 | 658 → 2101 |
| 2: disable scene reload | 0 / 0 | 0 / 0 | 1 / .5 | 0 / 0 | 35295 → 37447 |
| 3: disable both | 1 / .5 | 0 / 0 | 1 / .5 | 0 / 0 | 39984 → 42237 |

**N05 уточнено:** Document також ламається під час Enter Play Mode з увімкненим domain reload і вимкненим scene reload: runtime keypoints=0 при animationRestored=true. Після виходу відновлюється. Звичайний Play Mode із reload scene відновлює Document; раніше підтверджений script reload не можна узагальнювати на всі переходи. **A12 уточнено:** Plan втрачає runtime keypoints у всіх чотирьох фактично запущених конфігураціях. Навіть disable both не є перевіреним workaround. Це спостереження на поточній версії Unity, не твердження про всі версії рушія.

Під час режиму2 CLI тимчасово втрачав Pipeline instance через domain reload. Перші спроби Inspect/Stop отримали EDITOR_NOT_RUNNING — це помилка підключення, не результат GFX. Після `unity status --until-ready` повторні Inspect/Stop успішні; для режиму3 reconnect уже очікувався явно. Після кожного завершеного режиму Cleanup закрив test scene та відновив початкові налаштування: enterPlayModeOptionsEnabled=false, options=None, runInBackground=false. Фінальний console get: errors=0, compilationFailed=false; цей snapshot не доводить відсутність історичних помилок до reload. У Play Mode редактор не залишено.

Артефакти: `play-{0,1,2,3}-{before,during,after}.json`, `play-matrix-cli.jsonl`, `play-console-errors.json` у `.validation/gfx-parity`. CLI transcript містить режими1–3 та невдалі reconnect-спроби; режим0 підтверджено окремими JSON і результатами tool calls, він не включений у transcript. During JSON зберігає останній замір.

### Робота GLM: окремий блок, без повторного повного огляду

Модель `opencode/glm-5.3-flash-free` через OpenCode самостійно оглянула SpeedTrail110/111, Lightning150/152, LtnTrail180, Ring140 та спільний material factory, поки основний агент виконував Play Mode matrix. У початковому завданні назва ring180 була неточною: 180 — LtnTrail, кільце — 140. GLM виправив mapping у звіті. Deliverable `glm-trails-work.md` і transcript `glm-trails.jsonl` збережені у `.validation/gfx-parity`. Його огляд статичний; нижче чітко відділено проведені spot-checks від кандидатів.

| ID | Результат | Рівень доказу / наступна перевірка |
|---|---|---|
| N09, P2 | SpeedTrail ширина отримує KP0 Scale двічі. На OrgPos edges ±1 і Scale1/2/3 Unity дає повну ширину ≈2/8/18. Формула Godot `dynamic_width=1+c_scale.x`, потім одноразове KP_0_Scale, без controllers дає 2/4/6. | Production Unity builder виконано; формулу Godot перевірено в generated shader source, GPU Godot width тут не вимірювалася. Потрібен render fixture і Ctrl106/111 matrix. |
| N10, P2 | SpeedTrail tick перезаписує localPosition: з (1,2,3) отримано (0,0,.001). Godot додає priority offset на окремому дочірньому SpeedTrailPlane. | Production Unity driver виконано; Godot hierarchy path підтверджений статично. Потрібен parent/bind fixture в обох рушіях; вплив на файл користувача не доведено. |
| C01 | SpeedTrail без OrgPos: Godot має `(UV.x-.5)*dynamic_width` у Z локальної форми; Unity встановлює Z=0 перед rotations. | Статичний кандидат GLM. Не зарахований до виконаних порівняльних fixtures; потрібні GPU render / silhouette. |
| C02 | Lightning150: Godot застосовує UVExchg після sheet/tile; Unity перед sheet/tile у EmitPair. | Статичний кандидат GLM, потрібен несиметричний атлас + tile/scroll. Числовий приклад GLM не використано як oracle. |

`UnityTrailSpotAudit.cs` містить три виміри width і один tick position; результат `unity-trail-spot.json`. Для width взято спрощений fixture **без Ctrl106**, щоб ізолювати KP0 scale від логіки controllers. Це не повна cross-engine render parity та не додаток до CPU6509.

Додаткову підозру GLM про різний hash назв перевірено окремо: production `GfxMath.GodotStringHash` і справжній Godot `hash()` дали однакові значення для порожнього рядка, Element_0, Element_17, кирилиці та CJK+emoji. П'ять збігів; підозра не підтвердилася, окремого багу немає. Harness `godot_hash_spot.gd`, дані `godot-hash-spot.json` і `unity-trail-spot.json`. Це не вичерпний тест Unicode, але спростовує твердження про загальне djb2/murmur3 розходження в перевіреному середовищі.

GLM також позначив shared limitations Warp/Speed_Vel/Spline й можливі render-priority/depth відмінності. Без цільових fixtures вони не підвищуються до нового runtime finding. Жодні production runtime/shader файли цим блоком не змінювали.

### Наступний обсяг

1. Виконати Godot/Unity render fixtures N09/N10/C01/C02, включно з scale controllers, fallback OrgPos, atlas/tile/scroll, priority/depth.
2. Окремо перевірити ці lifecycle режими на particle/Dummy/trail/container, а не переносити результат sprite автоматично.
3. Завершити живий frame-loop instancing test A13 з робочим контрольним material; негайний Camera.Render не є достатнім доказом для RenderMeshInstanced.
4. Кадрове зіставлення реального GFX із наданим відео, HDRP та тривалі stress/lifetime тести ще відкриті. Наразі немає підстав оголошувати ефект повністю ідентичним оригінальній грі або аудит завершеним.

## Блок 7 — 2026-11-13: відеореференс, fresh/saved і Lightning UV

### Відео та кадровий контроль вашого файлу

Відео `2ad0eeb4-046f-40e3-bcc9-aa8e632aa362.mp4`: 354×394, 30fps, тривалість4.566633s. Отримано contact sheet `video-contact.png` (2fps, 9 кадрів). Видно безперервне полум'я над чашею, рух камери, яскраве біло-жовте ядро й вертикальні язики. Це візуальний огляд вибірки, а не аналіз усіх137 кадрів чи встановлення початкової фази ефекту.

`UnityReferenceCapture.cs` рендерить saved scene та fresh `Build(document)` із того самого corpus/000.gfx: 12 timestamps 0/100/250/500/750/1000/1250/1500/1750/1999/2001/2250ms. Preview camera position(0,.15,-3), orthographicSize=.95, чорний фон, RT354×394, HDR/MSAA вимкнено. Fresh отримує ті самі root transform та вже імпортовані textures зі saved scene за порядком елементів. Це перевірка runtime reconstruction, **не незалежна перевірка texture importer**. Збереження сцени не виконується.

Після корекції test setup **12/12 fresh/saved кадрів піксельно ідентичні**, max/mean channel error=0. Результат `reference-fresh-vs-saved.json`; captures `reference-{fresh,saved}-capture.json` і відповідні PNG. Попередній `reference-capture.json` — ранній saved run; authoritative fresh/saved comparison використовує нові файли з явними назвами. CPU aggregate6509 не змінено: ці12 image comparisons до нього не входять.

**Важливе спростування початкової підозри:** перший fresh capture мав0 частинок, saved —15/4/14/6 при500ms. `UnityFreshParticleAudit.cs` ізолював причину:

| Стан | Native particle counts (elements6/8/9/10) | Alive GFX slots |
|---|---|---|
| До MoveGameObjectToScene |15/4/14/6|15/4/15/6|
| Після перенесення у preview scene|0/0/0/0|15/4/15/6|
| Після ParticleSystem.Simulate(0,false,true,false)|15/4/14/6|15/4/15/6|
| Після додаткового RebuildSim|15/4/14/6|15/4/15/6|

Перенесення в preview scene втрачало native buffer, а не GFX simulation. RebuildSim окремо в попередньому тесті не допоміг. У capture harness додано ініціалізацію native buffer після перенесення; production не змінено. Не записуємо це як новий баг свіжого імпорту. Різниця15alive/14native у element9 узгоджується з alpha discard в ApplySlots; саме по собі порівняння slot count із native count не є assertion рівності. Доказ `fresh-particle-diagnostic.json`, `fresh-particle-cli.json`. Ранні `reference-rebuild-*` кадри без native init є невдалим діагностичним експериментом, не фінальним результатом.

Порівняно з відео, поточний Unity front-view виглядає компактним, із помітними іскрами й клубами диму; у відео переважають вертикальне полум'я та велика засвічена серцевина. Через інший ракурс, фон, приховану чашею нижню частину, експозицію/постобробку та невідому часову фазу **це спостереження, а не нові доведені помилки форми/кольору**. Подібність пікселів між грою й Unity не обчислювалася. Дані не доводять, що архів містить усі шари/виклики ефекту з відео. Параметр header PlaySpeed=2 також не дозволяє напряму прирівнювати video timestamp до manual GFX TimeMs. Потрібні узгоджений ракурс, cycle phase та renderer settings перед кількісною оцінкою відео.

### Делегований GLM тест C02

GLM підготував окремий executable `UnityLightningUvAudit.cs` та пояснення `glm-lightning-uv-work.md`, поки основний агент займався відео/fresh-saved. Виконано production `GfxLightningEval.Build` для6 fixtures: **4 parity controls збіглися, 2 очікувані розбіжності підтверджено**, position arrays при UVExchg on/off ідентичні. 1×1 off/on, sheet2×3 off, tile/scroll off збігаються; sheet2×3 on і tile/scroll on відрізняються. Дані `unity-lightning-uv.json`.

Godot oracle тут — модель формул generated shader, перевірена за create_id_140:347–352 і create_id_150:311–318, **не виконаний GPU shader Godot**. Статус C02 підвищено до numerical reproduction against source formula, а не end-to-end render parity. GLM запропонував міняти Godot за Unity, але така рекомендація не прийнята: підстав вважати Unity еталоном Angelica немає. Жодних виправлень UV не внесено; рішення про правильний порядок потребує оригінального reference або погодженої специфікації. Перенесення висновку на152 поки статичне, окремого тесту152 не виконано.

Підсумок: знято хибну fresh-import підозру, підтверджено12/12 fresh/saved image matches, виконано6 GLM UV fixtures. Повний аудит і frame-aligned порівняння з грою ще відкриті. Production-файли не змінені цим блоком.

## Виправлення — прохід 1, 2026-11-13

Користувач дозволив перейти від аудиту до production-виправлень. Попередні незакомічені зміни збережено; не виконувались commit/reset/revert.

| Finding | Зміна | Фактична перевірка |
|---|---|---|
| N06 | GfxKeypointBuilder: comma RGBA ColorDelta /255 без clamp, включно з від'ємними значеннями; packed ARGB branch не змінено. | Affectors232: усунено18 isolated105 і10 user-emitter105 failures. |
| N07 | Ctrl107 defaults Persistence=.5, OctaveNum=1; Ctrl109 лишився .25/5. | Усунено18 default107 failures; explicit variants продовжили збігатися. |
| A01 | GfxSpriteDriver задає _GfxAlpha=1: header alpha вже включено у BaseColor/Color. | Реальний render: halfRed/fullRed=.5 замість.25. Не є перевіркою всіх trail/інших shader paths. |
| N05 | animationRestored позначено NonSerialized: прапорець більше не переживає hot reload окремо від runtime graph. | Реальний RequestScriptReload: Document kp1/X.5; Play Mode DisableSceneReload також kp1/X.5. |
| A12 | GfxElementPlan переносить SourceElement із planner; Build(plan) задає animationSource із raw element. | Document і Plan: script reload, scene save/load, Play Mode DisableSceneReload — kp1/X.5 обидва. |

A12 стосується планів, отриманих через GfxRuntimePlanner.Build(document). Ручні плани без SourceElement залишаються runtime-only. Якщо споживач після planner змінює typed Keypoints окремо від raw SourceElement, ці зміни не синхронізуються автоматично; це не перевірений workflow. Повну матрицю всіх чотирьох Play Mode options після виправлення ще не повторено — виконано конкретний раніше проблемний mode2 плюс domain reload і scene roundtrip.

GLM отримав окреме завдання read-only збереження Plan і надав діагноз/мінімальну пропозицію; код внесений основним агентом. Спроба доручити GLM production-редагування була відхилена автоматичною перевіркою, оскільки попередня згода дозволяла огляд без змін. Відхилений запуск не виконувався. Наступний read-only огляд був дозволений і завершений; файли `glm-plan-persistence-review.md/.jsonl`. GLM не дублював alpha/affector/lifecycle implementation.

Після змін affectors: **212/232 збігів,20 розбіжностей**, раніше166/232 і66. Залишок — Ctrl110 two-point path (N08), без нових failures у цьому наборі. Історичний aggregate6509 повністю не перезапущено, тому не оголошується новий all-suite pass count. Базові affectors/integration/domain outputs перед перезапуском скопійовані в `fix-pass1-before`.

Додано NUnit `GfxControllerConversionTests.cs`: signed byte normalization, controller-specific defaults, explicit override. Запуск Unity test runner повернув **Total=0**, тобто тести не були виявлені в цьому host; це НЕ успішний NUnit прогін. Виконана перевірка спирається на232 engine fixtures, render integration та справжні lifecycle transitions. Треба ввімкнути discovery package tests у тестовому host і запустити suite перед release.

Компіляція Unity:0errors/0warnings. Scene roundtrip також знову підтвердив невиправлені A06/A07: Grid/Phys/Bolt caches і Container reference губляться. Play Mode зупинено, optionsEnabled=false/options=None/runInBackground=false відновлено. `git diff --check` без помилок. Evidence: `fix-pass1-*.json`, оновлені `unity-affectors.json`, `affector-comparison.json`, `unity-integration.json`, `unity-domain-after.json`.

Наступні production-пакети: driver-specific serialization (Grid/Phys/Bolt/Container, particle geometry/Dummy), sprite UV/depth/billboard/material isolation, підтверджені trail defects, потім повторний capture реального ефекту. Не вважати систему повністю виправленою або тотожною оригінальній грі після цього проходу.

## Статус закриття після проходу1 (2026-11-13)

| ID | Статус | Межі закриття |
|---|---|---|
| A01 | CLOSED | Sprite header alpha застосовується один раз; GPU ratio .5 перевірено. |
| N06 | CLOSED | Comma RGBA delta нормалізована /255, включно з реальним emitter користувача. |
| N07 | CLOSED | Ctrl107 missing defaults1/.5; explicit params та Ctrl109 збережено. |
| N05 | CLOSED | Document raw source відновлюється після script reload та mode2 Enter Play Mode. |
| A12 | CLOSED — planner-generated plans | Raw source збережено через planner; scene/domain/mode2 tests пройшли. Ручний plan без SourceElement не входить у цю гарантію. |

Закриття findings не означає проходження всього regression suite: package NUnit discovery і решта reload matrix лишаються validation backlog. Наступний пакет у роботі — A06 persistence Grid/Phys/Bolt/Container.

## Виправлення — прохід2, 2026-11-13

На прохання користувача A01/N06/N07/N05/A12 позначені CLOSED у таблиці вище з явними межами перевірки.

### A06: persistence Grid/Phys/Bolt/Container

- Grid: GfxGridParams тепер Serializable, driver зберігає повні параметри, включно з positions/colors/keyTimes/AnimData. На tick повторно прив'язує MeshFilter/Renderer. Власний runtimeMesh не проходить hot-reload serialization, щоб не прийняти збережений asset за власний тимчасовий mesh.
- Phys: GfxPhysParams Serializable, зберігається повна конфігурація (включно з уже розібраними .gphy значеннями). Після втрати runtime sim драйвер відновлює його, particleBuffer і native ParticleSystem buffer; використовує наявний sharedMaterial, не створює новий матеріал при reload.
- Bolt: зберігає параметри, texture, gfx2d та raw parent properties. Runtime strokes list відновлюється з прямих Stroke_* дітей, включно з inactive. Restore не перебудовує і не видаляє дітей. Явний RebuildStrokes тепер видаляє лише власні stroke children, зберігаючи CtrlMethods/Affectors hosts.
- Container: ліниво знаходить існуючий Sub_GFX, повторно готує дочірні drivers/renderers. subGfx runtime reference NonSerialized, тому hot reload не залишає reference із порожніми lists. Видалення компонента більше не викликає знищення дочірньої ієрархії; явна Configure-заміна лишається відповідальністю ClearSub.

GLM виконав окремий дозволений read-only огляд Bolt/Container, основний агент паралельно реалізував Grid/Phys. Пропозиції інтегровані вибірково: не додано автоматичного створення видалених strokes чи нового tint для unowned renderers. Deliverable `glm-driver-persistence-review.md/.jsonl`. Усі production edits внесені основним агентом.

### A07: числова прозорість контейнера

PushTime передає дочірньому driver `headerAlpha * keypointAlpha`. До цього keypointAlpha керувала лише renderer.enabled, а дочірній GfxAlpha лишався1. Контроль: keyAlpha=.25 -> child GfxAlpha=.25 і BaseColor.a=.25; вкладений контейнер .8×.5×.5 -> leaf GfxAlpha=.2. Це перевірка керованих GFX drivers; довільні renderers без GfxElementDriver не отримали нового tint механізму.

### Виконані перевірки

1. Справжній prefab save/load: Grid params+visible, Phys params+1particle, Bolt params+2strokes/2children збережені. Розширений fixture має непорожні Grid animation arrays (2keys): повний gridData, gridVertices, physData, physNativePositions **точно рівні** до/після. Попередній static fixture мав тільки null→empty різницю KeyTimes/AnimData після Unity serialization; вона не впливала на HasAnimation. Фінальний fixture перевіряє реальні arrays і такої різниці не має.
2. Справжній scene save/load: усі driver flags відновлені, Phys1particle, Bolt2strokes, Container reference=true. Document/Plan kp1/X.5 також залишилися правильними.
3. Container prefab: Sub_GFX і посилання SubGfx збережені.
4. UnityOwnershipAudit: nestedAlpha=.2, видалення Container component зберігає child, Bolt rebuild зберігає CtrlMethods host і створює2strokes.
5. Sprite alpha=.5 лишається правильним у повторному integration run. Повний CPU6509 suite не перезапускався.

Під час розробки була CS1503 через IDictionary→Dictionary; виправлена копіюванням dictionary. Фінальний console groundTruth: compilationFailed=false, consoleErrors=0. Старий запис CS1503 лишився в історії Pipeline log; він не є поточною compile error. Тести після виправлення фактично виконані. `git diff --check` чистий.

Статус **A06 CLOSED для повторно побудованих та збережених prefab/scenes у перевірених fixtures; A07 CLOSED для дочірніх GFX drivers**. Старі assets, які вже були збережені без Grid/Phys/Bolt параметрів, не можуть повернути втрачені дані лише від додавання SerializeField: потрібна повторна побудова/імпорт із вихідних даних. Міграцію старих assets, повну domain/Play Mode matrix цих4driver-ів та stress tests ще не виконано; не переносити результати sprite matrix на них автоматично.

Докази: `fix-pass2-before-lifecycle.json`, `unity-lifecycle.json`, `fix-pass2-*-cli.json`, `unity-scene-roundtrip.json`, `unity-integration.json`, `fix-pass2-ownership.json`, `fix-pass2-console.json`. Harness UnityLifecycleAudit доповнено payload/mesh/native position перевіркою й animation arrays; новий UnityOwnershipAudit.cs. Наступні відкриті розбіжності — particle geometry/Dummy persistence A08, material isolation A09, UV/depth/billboard та trail N09/N10.

## Виправлення — прохід3, 2026-11-13

### A08 CLOSED — particle/Dummy prefab persistence

GfxParticleParamsSerial тепер переносить IsDummy, MultiPlanePoints/Normals, CurvePoints/Ratios/Dirs та AvgStepCount. Новий Serializable GfxFloatRow зберігає jagged float arrays через Unity-сумісний масив об'єктів із float[]; pack/unpack копіює дані. Коли particle sim відновлюється, driver також повторно визначає DummyEle і прив'язує Dummy driver. Власні згенеровані slot references серіалізуються; built NonSerialized, тому rebuild після reload не губить старий pool і не додає дублікати.

Справжній prefab save/load,5елементів: **Template/Emitter/Ordinary/Curve/Mesh повністю збіглися** за записаними state objects (positions,geometry,flags,slot counts). IsDummy=true збережено. Dummy має8slots/8children до й після, kind=Element, звичайних particle quads0. Curve лишається2points, ratios і quaternion dirs збережені, перша positionX≈1.51779044; mesh1point/normal, positionX=2. Ordinary5particles з тими самими positions. AvgStepCount=2 для curve збережено. Дані `fix-pass3-particle-comparison.json`, `unity-particles.json`; baseline `fix-pass3-before-particles.json`.

Межі: перевірено новозбережений prefab, не повну domain/Play Mode matrix. Старі assets із уже втраченими arrays потребують rebuild з вихідних даних. A14 sprite Dummy lookup усе ще відкрите: цей пакет не змінював правила FindDummy.

### A09 CLOSED — sprite clone isolation; particle UV isolation також виправлено

Динамічні sprite _BaseColor/_Color/_GfxAlpha/_UvRect та particle _UvRect/_GfxAlpha записуються в MaterialPropertyBlock рендерера. Tick більше не мутує ці properties sharedMaterial. Configure очищає попередній block. Не додаються runtime material clones, які потрібно знищувати або серіалізувати. GLM незалежно запропонував MPB і правильні критерії перевірки після A01; основний агент вніс зміни. Deliverable `glm-material-isolation-review.md/.jsonl`.

Важливе уточнення GLM: його твердження, що Dummy batch можна лишити незмінним, не прийнято буквально. Render раніше очищав Block і не читав renderer overrides. Додано SourceRenderer та копіювання його block перед записом _DummyCustom; інакше template color/UV загубилися б. Цей захід не усуває A13 shader instancing variants/custom-data consumer defect.

Фактичний URP GPU render на спільному матеріалі:
- original alpha=.25 після clone alpha=.75 лишається.25;
- окремі pixel readbacks original=.25, clone=.75;
- зворотний порядок tick: original=.2, clone=.6;
- sharedMaterial лишається спільним, material BaseColor.a=1 (не забруднений динамічним alpha);
- A01 half/full=.5 лишається правильним; A07 container child alpha=.25 також лишається правильним.

Particle UV fixture: два clones ділять material; original UOffset=0, clone=.5; original block не змінився. Перевірено SourceRenderer binding у Dummy batch, **не успішний instanced GPU render**. Harness `UnityMaterialUvAudit.cs`; `fix-pass3-uv-isolation.json`.

UnityIntegrationAudit оновлено під MPB: старе читання material._GfxAlpha після A01 було неінформативним. Існуючий NUnit particle texture-animation тест також читає renderer block замість material. NUnit suite цього проходу не виконувався; наведені результати — реально виконані ephemeral engine harness-и. HDRP, SRP-batcher performance і scene/domain matrix material isolation ще відкриті. MPB може вимикати SRP batching для відповідних renderers; це свідомий компроміс ізоляції, performance не вимірювалася.

### Реальний файл та стан

Unity був закритий на початку engine перевірки; цільовий Unity Native Reader повторно запущено batchmode,PID20132. Після import/recompile тести виконані. Повторний saved-reference capture дав12кадрів, переглянуто кадр1000ms: ефект рендериться з полум'ям/іскрами/димом. Це smoke/regression capture, не доказ тотожності відео. `reference-saved-*` тепер відповідають новому коду; історичне fresh/saved comparison з блоку7 не можна повторювати на змішаних старих/нових captures без нового Fresh run.

Фінальний console: compilationFailed=false, consoleErrors=0; є warnings, тому заяви про zero-warning build немає. `git diff --check` чистий. CPU aggregate6509 цього проходу не перезапускався. Наступні відкриті пріоритети: A14 Dummy lookup, A13 instancing, sprite UV/depth/billboard, trail N09/N10 і C02.

## Виправлення — прохід4, 2026-11-13

### Поділ роботи й максимальні зусилля GLM

OpenCode model metadata підтвердила для opencode/glm-5.3-flash-free variants low/high/max, max -> reasoningEffort=max. Окремий read-only огляд A13 запущено з `--variant max --agent plan --pure`; основний агент паралельно реалізував A14 та підготував executable tests. Повний огляд instancing основним агентом не дублювався: інтегровано висновки GLM і виконано цільові перевірки. Deliverable `glm-instancing-fix-max.md`, transcript `.jsonl`, metadata `opencode-model-metadata.txt`.

### A14 CLOSED — lookup sprite/other Dummy sources

GfxElementDriver.IsDummySource читає IsDummy зі збереженого animationSource. FindDummy зберігає typed container/particle правила, а для інших driver-ів використовує цей source flag. NameMatches тепер враховує ElementName, що потрібне для типів без окремого typed Name. Inactive без IsDummy не приймається за template.

UnityDummyLookupAudit:7контролів до й після справжнього prefab save/load — active sprite, inactive sprite, trail знаходяться; IsDummy=false, відсутній flag, self і неіснуюча назва відхиляються. Початковий UnityParticleAudit spriteDummyLookup також тепер resolved=true. Matching регістру/дублікатів і правила name alias цього пакета не змінювались; не оголошується повна Godot name semantics parity.

### A13 MITIGATED — коректний node fallback, GPU instancing лишається нереалізованим

GLM встановив, що поточні shaders не мають повного per-instance age/alpha contract; enableInstancing не є доказом такої підтримки. Додано закритий capability gate `IsMaterialInstanceable` (повертаєfalse для всіх поточних матеріалів). TryCollect не приймає unsupported material; nested CPU-driven nodes також відхиляються. Render більше не вмикає enableInstancing на shared material. На відміну від запропонованого GLM public mutable shader allowlist, **не додано публічний обхід**, який міг би дозволити неперевірені шейдери. До реалізації/валідації контракту GPU Dummy instancing недоступний; це свідоме обмеження продуктивності, не заява про завершений instancing.

Для node copies emitter тепер сам викликає дочірній GfxPlayer.Tick(0) після встановлення clock/alpha/transform, вимикаючи його незалежний Update. Це усуває залежність від порядку editor/player updates і подвійне програвання. Статичний kind Static лишається незмінним.

UnityDummyFallbackAudit використовує container Dummy із sprite atlas2×1, TexInterval100ms, вік частинки.15s. Після виправлення без ручного дочірнього Tick у тесті: instancing=false, slots1, kindContainer; childTime150ms, uvOffset.5; actual Camera.Render/ReadPixels red1/.5/0 при alpha1/.5/0. До gate fixture обирав instancing=true і slots0. Його чорний preview кадр **сам по собі не є надійним доказом поломки RenderMeshInstanced** (обмеження негайного Camera.Render вже встановлені раніше); shader/CommandBuffer evidence попереднього аудиту лишається основою дефекту. Поточний тест доводить працездатність node fallback.

Спочатку fixture був sprite Dummy напряму: kindStatic і clock0/alpha1. Це навмисний окремий шлях для IDs100/101/110, а не підстава змінювати його семантику. Діагностичний результат збережено як `fix-pass4-static-control.json`; фінальний container fixture — `fix-pass4-fallback.json`, baseline `fix-pass4-before-fallback.json`.

### Regression та межі

- Повторний particle prefab:5/5 state objects before/after точні збіги, включно з8Dummy slots без дублікатів.
- Particle UV isolation лишається правильною: offsets0/.5 на shared material; unsupported instancing явно відхиляється.
- Старий NUnit тест, який вважав mesh достатнім для instancing, змінено на fallback expectations. UnityMaterialUvAudit оновлено під rejection; UnityDummyRenderAudit тепер обробляє accepted=false без batches[0] crash. NUnit suite цього проходу не виконувався; фактичні результати — Unity script harness.
- Фінальна compilationFailed=false/consoleErrors0; warnings є. `git diff --check` чистий.
- Node copies збільшують кількість GameObject/draw calls і CPU tick work. Немає benchmark/stress доказу придатності великих pool; не прийнято неперевірене твердження GLM, що всі pools малі.
- HDRP, повний Play Mode/frame-loop/stress та GPU instancing shaders залишаються відкритими. Нова capability gate повинна лишатися закритою до перевірки age/alpha/UV/header semantics, а не лише shader variant.

Наступні розбіжності: sprite UV/depth/billboard, trail N09/N10/C02, NoChangeDir і BindEle. A14 CLOSED у перевірених lookup/prefab сценаріях; A13 mitigation виконано, реалізацію performant instancing не закривати.

## Виправлення — прохід5, 2026-11-13

### A02 — UVExchg (CLOSED для URP)

A02: UVExchg реалізовано як окремий обмін unitUV перед scale/offset. GfxSpriteUvState.Exchange переносить цей прапорець; URP/HDRP sprite shader має _UvExchange; sprite і global-particle UV передають його через MPB. Попереднє переставляння scale/offset не могло реалізувати off-diagonal mapping. SpeedTrail sheet helper передає UvExchange=false, тому цей крок не змінює його окрему UV логіку.

UnitySpriteUvMatrix виконав96 actual URP GPU samples:4режими (1×1,2×3,6×5,sheet/tile-scroll),8комбінацій exchange/reverse,3часи. Constant mesh UV(.21,.67), несиметрична64×64 pattern texture; expected UV обчислюється за порядком generated Godot shader (exchange→reverse→sheet/tile), без виклику production evaluator для oracle. **96/96 збігів**, tolerance1e-4. Godot GPU для цих96fixtures не запускався, HDRP shader шлях оновлено симетрично без HDRP render test.

Уточнення раннього integration probe: при чинній V-конвенції pixel(48,16) лежав на діагоналі unitUV і тому exchange його не змінював. Перенесено probe в(48,48): normalwhite→exchangered. Історичні green→blue Godot/Unity comparisons з різними coordinate conventions не є достатньою самостійною метрикою; контрольована96matrix усуває цю неоднозначність.

Alpha half/full=.5, clone isolation.25/.75, containeralpha.25, particleUV0/.5 та Dummy fallback time150ms/UVoffset.5/alpha1,.5,0 залишилися правильними в повторних engine runs. Новий NUnit regression для asymmetric exchange додано; suite запущено наприкінці проходу5 — див. розділ нижче. A10 per-particle atlas flags цим пакетом не закриваються. Статус A02: **CLOSED для URP** — 96/96 URP GPU matrix + наявний Godot render-контроль блоку2; межі: Godot GPU для цих самих 96 fixtures не запускався, HDRP shader оновлено без HDRP render test.

### A04 — depth test за header 2DRender (CLOSED для URP)

Готовий read-only план GLM variant=max (`glm-depth-fix-max.jsonl`, витяг `glm-depth-fix-max-plan.md`) інтегровано з рішенням «паритет за builder-ами» (узгоджено з користувачем): правило `_ZTest` визначається тим самим Godot-builder-ом, що відповідає сімейству, а не єдиним правилом для всіх (варіант «єдине правило» GLM відхилено — він свідомо розійшовся б із Godot particle/ring для ZEnable=0).

| Сімейство | Правило (test вимкнено ←) | Godot-джерело |
|---|---|---|
| Sprite 100/101 | header 2DRender=1; ZEnable для test ігнорується | `create_id_100/101.gd:210–211`: `depth_test_disabled` лише за `gfx_2d_render` |
| Particle 120–125 | ZEnable=0 **або** 2DRender=1 | `create_id_120.gd:293–299` через `gfx_parser.z_test_enabled` (= ZEnable && !2DRender) |
| Trail 110 (+140/180, ring/grid/phys через спільну factory) | header 2DRender=1; `_ZWrite` лишається `ZEnable && !2DRender` без змін | `create_id_110.gd:212–213`; для 110 це повний паритет |

Зміни production: `_ZTest` property + `ZTest [_ZTest]` у 4 шейдери (`AngelicaGfxSprite`, `AngelicaGfxTrail`, URP+HDRP; HDRP — у Pass `.shader`, не в `.hlsl`); `GfxSpriteDriver`, `GfxParticleMaterialFactory`, `GfxTrailMaterialFactory` виставляють `_ZTest` замість мертвого `var zTest` (default шейдера 4 = LEqual = поведінка до виправлення для незмінених матеріалів). Числову мапу звірено з enum редактора (diagnostic probe, видалено після використання): **LessEqual=4, Always=8**; перша спроба з `CompareFunction.LEqual` не компілювалась (CS0117 ×3), виправлено на `LessEqual`. Поведінка `_ZWrite` у всіх трьох factory не змінювалась.

Стінна матриця в `UnityIntegrationAudit` (URP, RT 64×64, непрозорий blocker): ZEnable 0/1 × 2DRender 0/1 → red **0/0/1/1**, `_ZTest` 4/4/8/8, `_ZWrite` 0/0/0/0, **`a04Pass=true`**; `a04FactoryPass=true` для particle (8/8/4/8 за порядком z0d0,z0d1,z1d0,z1d1) і trail (4/8/4/8, `_ZWrite` 1 лише z1_2d0). Порівняння з Godot-контролем блоку2: 2DRender=false → black у обох, 2DRender=true → white у обох. **`header2dRed`: 0 (до) → 1 (після).**

Регресії в тому самому прогоні: alpha ratio=.5, uvExchange pixel, clone isolation .25/.75, container .25, prefab restore — зелені. Компіляція: `unity recompile` → compilationFailed=false, 0 errors/0 warnings; console groundTruth errors=0 (3 буферні error — історичні CS0117 цієї ж сесії, виправлені). Докази: `unity-integration.json` (оновлено), `fix-pass5-a04-integration-cli.json`, baseline `fix-pass5-pre-a04-integration.json`, `fix-pass5-console.json`.

Нові package-тести `GfxDepthTestTests.cs` (3 property-asserts: sprite/particle/trail правила + `_ZWrite`) — **усі Passed** у suite нижче.

**A04 CLOSED для URP**, межі: HDRP render-нога не запускалась (HDRP-шейдери оновлено симетрично, `AngelicaHdrpShaderTests` у suite — Passed); матеріали на fallback-шейдерах (URP Unlit тощо) не керуються через material property; невирішені розбіжності правил лишаються задокументованими залишками — Godot `102/150/180/190/210` ніколи не вимикають depth test (для trail180+2DRender=1 Unity тепер вимикає, Godot ні), Godot ring140+ZEnable=0 вимикає test, а trail factory (правило A) — ні; питання «ZEnable=0 для sprite» відносно оригінальної Angelica відкрите (`gfx_parser.gd:102–105` описує test = !2DRender && ZEnable, тоді як sprite-builderи ZEnable взагалі не читають). Класифікація цих залишків: **невідомо, потрібен оригінальний reference**, не «виправлення Unity».

### Уперше запущено повний NUnit suite

`unity command test list` / `test run` у batch-редакторі: **404 тести, 398 passed, 6 failed, 0 skipped**. Discovery у цьому host працює (manifest `testables` містить пакет) — зауваження проходу1 «NUnit discovery повернув 0» виконано для `unity command` шляху. Усі 3 `GfxDepthTestTests` Passed; `GfxSpriteTests`, `GfxControllerConversionTests`, `GfxParticleTests`, `GfxDummyParticlesTests`, `GfxTrailTests` та решта GFX — Passed. Шість невдач — поза GFX-доменом, цим проходом не розслідувались: `BuildingImportBatchTests.MapReuses…` (1), `MapSectorFilterTests` (2), `MapSectorPrefabTests.ExportsIndependentSectors…` (1), `TerrainSectorImportTests…InstantiateSelectedMap,True` (1, «untitled scene unsaved» — відоме обмеження середовища, як у блоці3), `UnityApiParityTests.EditorPrefabSavesUseSuccessFlagNotReturnValue` (1, `SaveAsPrefabAsset` без out success у `GameDataDropImporter.cs`). Жодна з них не посилається на GFX-файли цього проходу; чи були вони відомі раніше — не перевірялось (suite раніше не запускався). Докази: `fix-pass5-nunit-suite.json`, `fix-pass5-test-list.json`.

## Виправлення — прохід6, 2026-11-13

### N03 Quota=0 та N04 розмір пулу (CLOSED)

Одне виправлення закриває обидві розбіжності: `GfxParticleParams.PoolSize` тепер точно переносить Godot `_calc_pool_size` з `create_id_120.gd:235–243` — `pool = int(TTL * EmissionRate) + 1`; якщо `Quota != -1` і `pool > Quota`, то `pool = Quota`; clamp `[0, MaxQuota=750]`. Quota=0 дає порожній pool (N03), автоматичний розмір обирається лише за Quota=−1; додане `+1` прибирає N04 (раніше `Ceiling` без `+1` давав 7 замість 8 при rate=10, TTL=0.7). Захист `Math.Max(0f, …)` прибрано: Godot `int()` та C# `(int)` обидва обрізають від'ємний добуток до нуля, арифметика збігається.

Окрема знахідка середовища: **Mono 6.13 обчислює `float*float` у double-проміжному значенні**. `(int)(0.7f * 10f)` дає 6 (обрізає 6.99999988), тоді як збережений float-результат — рівно 7.0f (біти `0x40E00000`). Примусове округлення `(int)(float)(...)` перед обрізанням відновлює паритет (probe: `i2=6` проти `i5=8`, `godotStylePool=8`). Без цього cast перший прогін фіксу дав регресію 386/4644. Ця особливість може впливати на інші місця `(int)(floatExpr)` у Runtime — вони окремо не аудитувалися.

Побічно виправлено харнесс-баг: `godot_affectors.gd` хардкодив `Pool_Size:8`, тоді як production (`create_id_120.gd:179,472`) викликає `_calc_pool_size` (для Quota=8, rate=2, TTL=3 → 7). Оракул тепер викликає production `_calc_pool_size`; хост перезібрано `prepare_host.py --extended`.

Перезапущені цього проходу докази:

- Particle CPU: **4644/4644 збігів, 0 розбіжностей, 0 pool-відмінностей** (було 4633/11 і 24 pool diffs: N03 quota=0 → 9 failures, N04 t=799.99/1499.99 → 2). `fix-pass6-n03-particle-cli.json`, `particle-comparison.json`.
- Affector CPU: **212/232, 20 розбіжностей — без змін, це сукуп N08** (ids 134–153, variant 110-two), `pool_differences=0` (після виправлення Unity, але до фіксу оракула, було 232). `fix-pass6-n03-affector-cli.json`, `affector-comparison.json`, `godot-affectors.log`.
- Повний NUnit suite: **407 тести, 401 passed, 6 failed** — ті самі 6 поза-GFX невдач, що й у проході5 (BuildingImportBatch 1, MapSectorFilter 2, MapSectorPrefab 1, TerrainSectorImport 1, UnityApiParity 1). Усі `GfxParticleTests`, включно з 3 новими регресіями, і `GfxDepthTestTests` — Passed. `fix-pass6-nunit-suite.json`.
- Компіляція/консоль: `unity recompile` → 0 errors; groundTruth consoleErrors=0; `git diff --check` чистий. `fix-pass6-console.json`.

Тести оновлено/додано: `ParticleParamsParsePointAndBox` очікування PoolSize 20→11; `PoolSizeClampedToMaxQuota` rate 1→1000 (зберігає перевірку клампу 750); нові `ZeroQuotaSelectsEmptyPoolNotAutoSize`, `ZeroQuotaSimulateProducesNoSlotsAtAnyTime`, `AutoPoolKeepsFirstEmissionAliveAtWindowEdge`. Probe-файли (`PoolProbe*.cs`) видалені після використання.

**N03/N04 CLOSED**, межі: Godot GPU-рендер для Quota=0 не запускався (CPU-порівняння повне, але поведінка порожнього пулу у viewport/quad не вимірювалась); семантика оригінального Angelica для Quota не перевірялась (оригінальне джерело не консультувалось) — класифікація «міжрушієва розбіжність» лишається; Mono float-in-double особливість може вражати інші `(int)(floatExpr)` місця Runtime — не аудитувалося; цього проходу перезапущено лише групи particles і affector, сукупний перерахунок у шапці зроблено з цим застереженням.

Наступний пакет за планом: A03 BindEle (P1), A05/A15 parent-transform fade/face та NoChangeDir, trail N09/N10/C01/C02 render fixtures, A10 per-particle atlas, A11, N08 (залишок 20 failures).


## Виправлення — прохід7, 2026-11-13

### N01 signed seek time — CLOSED у GfxKeypointEval.ElementTime

Від’ємний seek більше не переноситься в кінець глобального циклу: -1 ms при loop=3000 дає -1, як production Godot `gfx_keypoint_eval.gd:40`, замість 2999. Обидва залишки від ділення у ElementTime переведено на C# `%`, який зберігає знак діленого. Спільний `GfxMath.Fmod` не змінено: його виклики у ring-buffer, particle та UV потребують окремого аналізу, тому ця правка не оголошує виправленими всі від’ємні значення часу в системі.

Перевірки поточного коду:
- Новий прогін Godot 4.7.2 і Unity CPU: timing **480/480**, keypoints **478/483**, curves **45/45**. Усі 12 N01 зникли; 5 N02 (float32/float64 у Ctrl109 на довгому часі) лишилися без зміни.
- NUnit GfxKeypointEvalTests: **11/11**, включно з 6 новими випадками від’ємного/додатного seek, межі циклу та нескінченного repeat.
- NUnit з фільтром Gfx: **190/190**, 0 failed. Повний 407-тестовий suite попереднього проходу цього разу не перезапускався; його 6 поза-GFX невдач не виправлялися.
- Компіляція без помилок; наявні warnings. `git diff --check` без whitespace errors.

Докази: `fix-pass7-recompile.json`, `fix-pass7-godot.log`, `fix-pass7-cpu-cli.json`, `cpu-comparison.json`, `fix-pass7-keypoint-tests.json`, `fix-pass7-gfx-tests.json`, `fix-pass7-manifest.json`.

Зведення з урахуванням останніх результатів інших груп: **6509 fixtures, 6484 збіги, 25 розбіжностей** (N02: 5, N08: 20). Це агрегат прогонів різних проходів, не новий повний прогін усіх 6509. Візуальні/lifecycle-знахідки обліковуються окремо.

GLM Flash через OpenCode, `variant=max`, отримав окремий read-only пакет A03 BindEle: план реалізації, порядок оновлення, serialization/lifecycle та регресійні сценарії. N01 виконував основний агент; роботи не дублювались. Результат GLM не є доказом виправлення A03.


## Виправлення — прохід8, 2026-11-14

### A03 BindEle — виправлено пропуск пізніших keypoint-ів; загальний статус PARTIAL

На початку проходу в робочому дереві вже були ResolveBindEle, serialized bind-поля, друга шкала часу SpriteDriver та розділення часу контролерів у KeypointEval. Основний агент перевіряв виконання і persistence; GLM Flash через OpenCode (`variant=max`, read-only) незалежно аналізував код. Попередній порожній запуск не вважається завершеним оглядом; поточний завершився (`glm-bindele-review-max.md/jsonl`).

GLM виявив, що цикл Evaluate припиняє обробку на keypoint-і власного часу елемента, хоча bound clock міг уже активувати контролери пізніших keypoint-ів. Додано GPU-сценарій `bind-future-keypoint`: власний цикл 200ms з двох keypoint-ів по 100ms, рух лише в другому, прив’язаний час продовжується. До виправлення 3 помилки: Unity x=0 при 250/1000/1250ms, Godot x≈0.15625/0.90625/1.15625. Результат до правки: **99/102**.

Виправлення в `GfxKeypointEval`: після власного проходу обробляються ще не відвідані keypoint-и за прив’язаним часом і накопиченою базою часу. Додатковий прохід виконує лише transform-контролери; колір/масштаб/шум залишаються на власному часі. Вже оброблені контролери не виконуються вдруге. Unbound-обробка збережена.

Після виправлення:
- Parser → builder → runtime: **102/102** зіставлень у 8 fixtures (bound/unbound, missing target, delayed target, duplicate names, cycle, local scale з bound move, future keypoint), шість часових точок 250–1500ms.
- `GfxBindEleTests`: **9/9** — prefab save/load для ID100 та ID101, first-match/missing target, self/cycle, три випадки пізнішого keypoint-а з перевіркою незалежного масштабу.
- Увесь NUnit з фільтром Gfx: **199/199**, 0 failed. Компіляція без помилок; `git diff --check` без whitespace errors.
- Повторний Unity CPU: timing 480/480, keypoints 478/483, curves 45/45; залишилися лише відомі 5 N02. Godot CPU oracle — прогін проходу7, джерело не змінювалось. Повний suite з поза-GFX невдачами не перезапускався.

Виправлено test harness: старі два target-fixture не мали RepeatCount/TexFile та інших sprite-полів, що викликало Godot shader errors. Тепер input повний, production Godot не змінювався. Порівнювач явно читає/пише UTF-8 замість системного GBK.

Обмеження вимірювання: Godot — production shader/GPU проєкція, Unity — положення/геометрична ширина mesh та binding state, а не GPU readback. При ортографічній камері z=5 на початкових 90 випадках було 86/90: чотири різниці ширини 0.0625. Збільшення поля зору зі збереженням масштабу пікселя не змінило результат; при z=500 отримано 90/90 з тими самими допусками x=0.03, width=0.06. Камера віддалена для ізоляції BindEle від повороту до камери. Ці чотири випадки самі по собі не доводять production-дефект; A05 лишається відкритим. Ближній контроль відтворюється `godot_bind.gd -- --near-camera`, результати збережено окремо.

**Залишки A03 після GLM-огляду (не закриті):**
1. Ctrl110: Godot sprite shader з bound target, EndTime=-1 і невизначеною target-duration має progress=0; Unity CPU шлях рухає частинку по path. З кінцевим EndTime також різні знаменники. Потрібен окремий render-fixture; базова CPU-реалізація Godot сама розходиться зі своїм shader.
2. Ненульовий Ctrl StartTime з Acc: Unity/CPU Godot інтегрують `0.5*Acc*(end²-start²)`, sprite shader — `0.5*Acc*elapsed²`. Потрібні ізольовані render-fixtures; глобально міняти CPU helper без них не можна.
3. GLM також припустив замороження попередніх keypoint-ів; поточний Evaluate вже подає bound time у відвідані контролери, тому це ширше твердження не вважається доведеним. Комбінації interpolation/path/orbit потребують окремого покриття.

Докази: `fix-pass8-before-future-keypoint.json`, `fix-pass8-bind-cli.json`, `bind-comparison.json`, `fix-pass8-godot-bind.log`, `fix-pass8-near-camera-comparison.json`, `fix-pass8-near-camera-godot.json`, `fix-pass8-gfx-tests.json`, `fix-pass8-cpu-cli.json`, `fix-pass8-manifest.json`, GLM read-only звіт. Lifecycle перевірено через prefab, не domain reload/PlayMode matrix. Ці 102 rendering fixtures не входять у зведені 6509 CPU fixtures; агрегат 6484 збіги/25 розбіжностей не змінюється.


## Виправлення — прохід9, 2026-11-14

### A03: прискорення після Ctrl StartTime — CLOSED для sprite100/101

GLM-знахідку проходу8 відтворено окремими shader-fixtures. До правки 14 невдач із150: наприклад, StartTime=0.5s, Vel=0.25, Acc=2, t=1s давав Unity x=0.875 замість Godot x=0.375. Причина: інтеграція абсолютного часу `0.5*Acc*(end²-start²)` замість `0.5*Acc*elapsed²`.

Додано явний режим `spriteShaderTiming` у KeypointEval, увімкнений лише GfxSpriteDriver ID100/101. Інші сімейства та старі CPU-виклики зберігають математичний контракт `gfx_keypoint_eval.gd`, який відрізняється від shader-коду Godot. Перевірено finite EndTime та -1, час до/на/після старту, додатне й від’ємне Acc; регресійний тест окремо захищає стару CPU-формулу.

GPU-сценарії прискорення перевірено з BindEle та без нього для обох100/101. Godot harness тепер обирає відповідний production builder100 або101. Результат: **198/198** зіставлень усієї BindEle-матриці, **206/206** NUnit Gfx, компіляція без помилок. Godot shader errors відсутні. Повторний CPU-прогін: timing480/480, keypoints478/483, curves45/45 — лише відомі5N02. Допуски й обмеження Godot GPU vs Unity mesh/readback залишаються такими, як у проході8.

Докази: `fix-pass9-before-acceleration.json`, `fix-pass9-bind-cli.json`, `fix-pass9-godot-bind.log`, `fix-pass9-gfx-tests.json`, `fix-pass9-recompile.json`, `fix-pass9-cpu-cli.json`, `fix-pass9-manifest.json`. Загальний A03 ще PARTIAL; незалежний read-only GLM Flash variant=max працює над Ctrl110 duration/progress та parser-compatible fixture, без дублювання задачі прискорення.


### A03 Ctrl110 duration/progress — CLOSED у перевіреному sprite-сценарії

Незалежний GLM Flash variant=max огляд завершено. Він передав точний parser-compatible fixture та розділив timing, geometry і CalcDir. Основний агент використав його fixture для виконання перевірок; GLM не змінював файли й не запускав Unity. Звіт: `glm-bind-path-max.md/jsonl`.

Матрицю розширено на ID100/101 × BindEle on/off × StartTime 0/0.1 × EndTime -1/0.4, із трьома рівномірно розміщеними колінеарними точками. Геометрія та CalcDir нейтралізовані (CalcDir=0), щоб вимірювати лише timing. До правки: **340/390**, 50 нових помилок. `ProcessCtrls` у spriteShaderTiming тепер обирає duration: finite EndTime → EndTime-StartTime; unbound → keypoint span-StartTime; bound з невідомою target duration або infinite keypoint → duration=0. Progress обчислюється з часу від старту контролера, clamp[0,1]; за duration≤0.001 — 0, як production shader. Спільний CPU режим зберігає стару формулу.

Після правки: **390/390** зіставлень BindEle-матриці, **213/213** NUnit Gfx. Додано 7 точних CPU-регресій sprite-path timing (це окремий режим, не старий CPU oracle), у тому числі finite-end clamp і затриманий старт. Повторний CPU oracle: timing480/480, keypoints478/483, curves45/45 — без нових розбіжностей. Компіляція без помилок; git diff --check без whitespace errors.

**A03 загалом PARTIAL. Не закриті:**
- Godot sprite shader використовує raw path vertices та лінійну інтерполяцію за індексом; Unity використовує GenPath/arc-length. Колінеарний рівномірний fixture навмисно не доводить паритет довільної форми шляху.
- Godot додає path до KP0 Position; Unity замінює позицію. Ненульова база потребує окремого fixture.
- Pre-start path point та CalcDir: Godot застосовує стартову точку/орієнтацію навіть до Ctrl StartTime; Unity має загальний early skip. Поточні fixtures починаються з нульової точки і CalcDir=0, цього не перевіряють.
- CalcDir composition з KP0 Direction, RotFromView, suppression Ctrl100/104 у тому самому keypoint, interpolation/orbit, domain reload matrix.

Це паритет з фактичним Godot sprite shader; відповідність оригінальній Angelica для bound без EndTime не доведена. Поведінку «залишатися у точці0» не оголошено універсально правильною для інших сімейств.

Фінальні докази проходу9: `fix-pass9-before-path.json`, `fix-pass9-path-godot.log`, `fix-pass9-bind-cli.json`, `bind-comparison.json`, `fix-pass9-gfx-tests.json`, `fix-pass9-cpu-cli.json`, `fix-pass9-manifest.json`. Незмінні межі вимірювання: GPU Godot проти Unity mesh-state, не два попіксельні рендери; далека ортографічна камера ізолює billboard. Зведені 6509 CPU fixtures (6484 збіги/25 розбіжностей з різних проходів) не включають цю render-матрицю.


## Виправлення — прохід10, 2026-11-14

### A03 Ctrl110 raw geometry + base position — CLOSED у перевірених сценаріях

Додано shader-fixtures для ID100/101: нерівномірно розміщені точки, зігнутий шлях у XY, ненульова KP0 Position та ненульова перша точка, двоточковий шлях. Godot readback і Unity mesh-state тепер порівнюються за X **та Y** (толеранс0.03). До виправлення: **452/486**, 34 невдачі, у тому числі зміщення у протилежний бік від згладжування.

`GfxCtrlSpec.CurveVertices` зберігає сирі точки; builder більше не відкидає Ctrl110 з двома точками, хоча CPU CurvePath для нього залишається null. Для spriteShaderTiming вибирається інтерполяція за індексом вихідних точок, як у shader Godot; path-offset додається до поточної базової позиції, а не замінює її. Для інших сімейств залишається попередній GenPath/CPU-контракт. Ручні typed specs без CurveVertices зберігають fallback на CurvePath.

Після виправлення: **486/486** зіставлень; **218/218** NUnit Gfx. Три точні тести захищають нерівномірну/вигнуту геометрію й base position, prefab save/load розширено на двоточковий path для обох100/101 (відновлення через raw animation source). Компіляція без помилок, git diff --check без whitespace errors. CPU timing480/480, keypoints478/483, curves45/45 — без нових розбіжностей. Додатковий particle-affector regression **212/232**: ті самі20 N08(two-point particle path), нових failures немає; ця правка двоточкового sprite не закриває N08 для particles.

A03 загалом PARTIAL. Перевірено CalcDir=0, identity base direction та просту KP0 position; композиції interpolation/orbit не закриті. Helper для raw path повертає segment orientation, але сам по собі не доводить правильність CalcDir у driver/billboard. Pre-start path point/CalcDir, композицію з KP0 Direction та suppression Ctrl100/104 окремо аналізує GLM Flash через OpenCode variant=max (read-only, без повторного аналізу raw geometry/base position). Поточний сценарій не закриває ці підпункти.

Докази: `fix-pass10-before-geometry.json`, `fix-pass10-godot.log`, `fix-pass10-bind-cli.json`, `bind-comparison.json`, `fix-pass10-gfx-tests.json`, `fix-pass10-cpu-cli.json`, `fix-pass10-affectors-cli.json`, `fix-pass10-manifest.json`. Межі попереднього проходу незмінні: GPU Godot проти Unity mesh-state, далека ортографічна камера; сукупні6509 CPU fixtures не включають486 render observations.


GLM orientation-огляд проходу10 **завершено**, read-only, без дублювання raw geometry. Підтверджені code-level напрямки наступного фіксу: pre-start Ctrl110 не має раннього skip у Godot; наявність110 блокує100/104 на тому самому kp-base незалежно від порядку/StartTime; CalcDir має компонуватися з KP0 direction і billboard, а не заміняти direction. GPU-відтворення цих підпунктів ще не виконано.

Поправки основного агента до запропонованих GLM fixtures: quaternion `(0,0.70710678,0,0.70710678)` — yaw90°, а не45°. Для45° потрібен `(0,0.38268343,0,0.92387953)`. `_sprite` задає Width=0.5, але production mesh має повну ширину `2*Width=1`; отже yaw135 дає проєкційну ширину близько0.7071, не0.35355. Поточний oracle порівнює геометричну ширину Unity, тому для CalcDir треба додати коректну проєкцію/рендер Unity і дозволити справді edge-on невидимий quad; числові очікування GLM не приймати як golden без перевірки. Збережений огляд: `glm-path-orientation-max.md/jsonl`.


## Виправлення — прохід11, 2026-11-14

### Pre-start Ctrl110 і suppression Ctrl100/104

Відтворено48 failures на606 observations. У spriteShaderTiming Ctrl110 виконується і до StartTime (progress0), а наявність110 блокує100/104 на однаковій накопиченій базі keypoint незалежно від порядку. База просувається лише за позитивного TimeSpan; порожній110 тепер зберігається builder-ом як marker suppression. Неспрaйтовий CPU режим не змінено. Invalid end<start проходить sprite-path формулу duration/progress, але окремого GPU fixture для цього не було (не оголошено перевіреним).

Після правки **606/606**, NUnit Gfx **223/223**. Додано pre-start+order100/104 та equal-base/empty-path regressions. CPU:480/480 timing,478/483 keypoints,45/45 curves; affectors212/232 — ті самі N02/N08, нових розбіжностей немає. A03 загалом PARTIAL: CalcDir/billboard composition, interpolation/orbit і domain-reload matrix залишаються.

### Обов’язковий реальний GFX — виконано Godot ↔ Unity

Джерело: єдиний GFX із користувацького `GFX Extracted.zip`, corpus000, **10 елементів** (101×3,110×3,123×4),7 унікальних DDS. Unity texture assets побайтно збігаються з вихідними DDS. В Godot всі10 texture bindings завантажені успішно через ResourceLoader; source-hook `gfx_sources.begin` лише задає джерело текстур, builders/parser/header/shaders production без змін.

Захоплено12 кадрів повного ефекту:0,100,250,500,750,1000,1250,1500,1750,1999,2001,2250 **GFX ms**, не wall-clock/video ms (файл має PlaySpeed2). Додатково по10 ізольованих кадрів при1000ms. Unity: fresh parser/build, усі textures з перевіреної reference scene, URP; Godot4.7.2 Compatibility/OpenGL. Кадр354×394, чорний фон, root scale2, camera(0,.15,-3), orthographic vertical span1.9, look-at origin (Unity pitch2.86240554°). Godot camera right/up basis і Unity handedness не вважаються доведеними еквівалентами — дзеркалення/UV/billboard треба ізолювати окремо.

**Реальна візуальна відповідність НЕ закрита.** Середній RGB MAE по12 кадрах = **0.05662078** на шкалі0..1; mean maskIoU = **0.69750293**, threshold maxRGB>0.05. Це описові метрики, не pass/fail і не «69.8% готовності». Спочатку без pitch Godot IoU був0.56218; цей попередній результат замінено узгодженим кадруванням.

Поелементна перевірка при1000ms підтверджує залишкові розбіжності: element9(ID123,smork007) у Godot має ширше рознесені/тьмяніші клуби, Unity — щільніший яскравіший дим. У sprite1/7 відрізняється орієнтація/форма полум’я; у particle6/10 — положення іскор. Це кандидати для наступної ізоляції seed/emission/UV/coordinate/billboard, не доведена єдина причина. Black-frame trail2/3 при одній часовій точці не доводить, що trail працює правильно в усьому циклі.

Виправлення harness:
- Godot Image.load(DDS) повертав error15, білі quads — відкинуто. Використано оригінальні DDS у ізольованому host та load(Texture2D), всі10 loaded=true. Імпорт/renderer попереджали про недоступний user shader cache через sandbox; це не shader compile failure. Фінальний лог має лише cache warning, не відсутні textures.
- Preview-scene camera Unity не доступна через Camera.main. Додано тимчасову HideAndDontSave camera-context із точною позою/render settings та перевіркою Camera.main; чужі MainCamera tags відновлюються, тимчасовий об’єкт знищується. Render залишається в ізольованій preview scene.
-12 кадрів Unity до/після production-фіксу були побайтно однакові у попередньому camera-harness (`fix-pass11-real-unity-regression.json`), але це лише regression evidence старого harness. Фінальне міжрушієве порівняння використовує виправлений camera-context.

Докази й відтворення: `godot_real_capture.gd`, `UnityReferenceCapture.Fresh`, `compare_real_capture.py`; outputs `real-godot-capture.json`, `reference-fresh-capture.json`, `fix-pass11-real-comparison.json`, `fix-pass11-real-contact.png`, `fix-pass11-elements-contact.png`, `fix-pass11-elements-bounds.json`, `fix-pass11-real-godot.log`, `fix-pass11-real-unity-cli.json`, `fix-pass11-manifest.json`. Синтетика: `fix-pass11-before.json`, `bind-comparison.json`, `fix-pass11-gfx-tests.json`. Візуальні486/606 counts не додавати до6509 CPU fixtures.

GLM отримав окреме завдання Godot real-capture setup, але його лог обірвався на відмові доступу до Unity scene; завершеного результату немає. Основний агент завершив harness самостійно з локальним доступом. Подальші production-фікси обов’язково перевіряти цим реальним набором плюс цільовими synthetic fixtures.


## Виправлення — прохід12, 2026-11-14

### Реальний particle seed mismatch — CLOSED для нових imports120–125

У production Godot4.7.2 parser `String.num(element_count)` формує `9.0`, Node санітизує крапку: реальний smoke-node `Element_9_0_ID_123`. Unity planner зберігав `Element_9_ID_123` і хешував його. Контрольний fresh capture із seed від фактичного Godot імені підтвердив зміну розподілу диму.

Виправлено: planner зберігає окреме GodotNodeName; particle factory використовує його для seed, а Unity display-name не змінюється. Ручні плани без GodotNodeName мають попередній fallback на Name. Решта сімейств (lightning/bolt) цим фіксом не змінені. Це паритет із перевіреним Godot4.7.2 runtime naming, а не гарантія для всіх версій Godot/інших конвенцій імен. Старі prefab/material assets не переписувалися; для нового seed потрібен rebuild/reimport.

**Реальний GFX прогнано в обох рушіях**, усі10 елементів і оригінальні7 DDS,12 часових кадрів плюс10 ізольованих елементів. Додатково зіставлено CPU-стан реальних particle builders при1000 GFX ms: index5 seed64244 alive15; index7 seed7478 alive4; index8(smoke) seed44631 alive15; index9 seed30559 alive6. Для всіх40 живих emissions збігаються age,position,size з допуском1e-5+1e-4*abs(reference). Godot CPU evaluation отримує uniforms і cpu_info від справжнього production shader builder; Unity — реальний driver sim. GPU vertex output безпосередньо не readback-нуто. Color/rotation та всі12 часових CPU-станів цим spot-check не перевірені.

Візуально повної відповідності ще немає. MeanIoU **0.68161609**, RGB MAE **0.05735051** проти попередніх0.69750293/0.05662078; фікс seed НЕ оголошується покращенням попіксельної метрики. Він усуває доведене різне випадкове джерело. Частинки/дим лишаються дзеркальними або інакше відображеними, різниться яскравість. Наступний пріоритет — camera/handedness/billboard/UV та blend/color за однакових симуляційних станів. Змінювати симуляцію для штучного покращення кадру без доказів не потрібно.

Докази: `fix-pass12-real-states.json`, `fix-pass12-real-comparison.json`, `fix-pass12-real-contact.png`, `fix-pass12-elements-contact.png`, `fix-pass12-real-cli.json`, `fix-pass12-godot.log`, `fix-pass12-matched-seed-cli.json`, `fix-pass12-tests.json`. Старі кадри перед seed-фіксом збережено у `fix-pass12-before/`. Shared real capture outputs з префіксомpass11 перезаписуються runner-ом; для історичного порівняння використовувати snapshots pass12 та числові результати звіту. Синтетичні606 fixtures цього проходу не перезапускались, поточний фікс стосується лише seed при побудові particles.

Фінальний NUnit Gfx проходу12: **224/224 Passed**, failed=0. Додано regression build тест actual seed44631 зі збереженим Unity display name. Compilation без помилок, diff whitespace check чистий.
