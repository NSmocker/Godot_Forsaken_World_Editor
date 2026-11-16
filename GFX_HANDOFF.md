# Передача роботи GFX

Оновлено: 2026-11-16. За вказівкою користувача №2 залишено відкритим, поточний ефект — GFX-00021. Перед початком читати [AGENTS.md](AGENTS.md).

## Поточний стан

- Unity: `A:/Github/Unity Native Reader`, `ProjectSettings/ProjectVersion.txt` перевірено — 6000.0.60f1. Поточний етап лише URP Editor; URP Build/HDRP Editor/HDRP Build ще не підтверджені.
- Реєстр: [GFX_CPP_UNITY_CHECKLIST.md](GFX_CPP_UNITY_CHECKLIST.md), 9018 записів. №1–20 позначені перевіреними, але частина має відкриті розбіжності/недостатньо даних. №21 — перший непозначений. Статуси не скинуті.
- За прямою вказівкою користувача старі MD, включно зі звітами, видалені. Їхні посилання в історичних рядках реєстру можуть не працювати. Це не привід скинути перевірки. Кадри, JSON, логи та код не видалялися. Нові постійні документи: AGENTS.md, цей файл і реєстр.
- Є незакомічені зміни попередніх проходів. Перед правками виконати `git -c core.fsmonitor=false status --short` і вивчити релевантний diff. Видалення старих MD є навмисними; не відновлювати їх автоматично.

## Пауза знята

Користувач зняв паузу. Перевірено новий runtime-меш частинок на №2, не оголошувати ефект виправленим.

- Unity Native Reader відкрито (порт 7801). Окремо запущений Medal Of Honor VR; GFX-перевірка йде лише в Native Reader.
- Capture: `Tools/Validation/GfxParity/Gfx00002SpotCapture.cs` → `.validation/checklist-first10/GFX-00002/runtime-particles/`.
- 800 мс: дракон близький до `model-recheck/cpp.frame-20.png`. Частинок 0, як і має бути до старту 875.
- 1800 мс: 77 частинок, текстури є (`particle004` 38, `ligbolt11` 6, 3D 水珠 6, `shuizhu` 27). Ядро ціан є, але зіркові квади й краплі більші/вище, ніж C++ `cpp.frame-45.png`.
- 2400 мс: `particle004` уже 0 (quota 38 — загальна кількість емісій, як `m_nEmissionCount` у C++, не одночасний ліміт). Сфера лишається скляною, краплі `shuizhu` вище й більші, кільця темніші за `cpp.frame-60.png`.
- Перший capture без GameDataIndex був невалідний (суцільні квади без текстур). Не використовувати його.

## Поточний ефект

**GFX-00021** — `npc/仙女伸手交谈.gfx`. Три моделі ID 160 (`飘带.SMD`). Колір виправлено загальним правилом у `GfxModelDriver.ApplyBlendState`: `WriteZ` більше не скасовує SrcBlend/DestBlend. На 50 мс колір теплий, як у C++. На 1750 мс поза Unity вужча за C++ — кліп знайдено, seek ще не звірено. Кадри: `.validation/gfx-cpp-unity-check/GFX-00021-blendfix/`.

Наступний крок: або поза №21 (час кліпу / skinning), або неперевірений №22, якщо користувач знову просить йти далі. №2 лишається відкритим.

## Незавершена задача №2

**GFX-00002** — `B:/Git/PW146RUUnpackedAssets/gfx/gfx/效果/无定宙水.gfx`.

Останній завершений технічний прохід у цьому чаті виправив GfxModelDriver:

- Емісія матеріалу модулюється текстурою шкіри через emission map; модель перестала бути білою.
- AlphaCmp керує alpha clipping незалежно від blending; прибрані помилкові вирізи.
- Capture harness зберігає DefaultScale, використовує ізольовану сцену, тимчасово вимикає чужі renderer-и й відновлює їх у finally.

**Залишилися:** відмінності освітлення/яскравості, центральної сфери, частинок та кілець. Не вважати №2 виправленим повністю.

Прохід 2026-11-16 (читання кадрів + алгоритм, без нового capture):

- `git status`: лише `D unity-package/DEPENDENCIES.md.meta` і `D unity-package/README.md.meta`. Це не GFX-код; не чіпати. Код попередніх фіксів уже в `2375e11`.
- Кадри `model-recheck/comparison-final.png`, `pair-final-800ms.png`, `GFX-00002/unity-frame-60.png` і `cpp.frame-60.png` існують. Дракон на ~800 мс близький. На ~2400 мс C++ має залитий ціан-шар, дрібні крапки й яскравіші кільця; Unity — скляну сферу, блискавки всередині, великі бульбашки `shuizhu` і темніші кільця.
- GFX текст, 13 елементів: 100×5 кільця/декалі (`ringblue`, `蓝色图案`, `毁天灭地释放`), 101 `toumingqiublue` (старт 1550, 0.4×0.4, SrcAlpha+One), 123×4 (`particle004` quota 38 rate 300 size 0.25; `ligbolt11` rate 55 size 1 TTL 0.1; `水珠` 3DParticle size 1.5; `shuizhu` rate 30 size 1 speed 4 acc 4 IsBind), 160 модель, 170 звук, 130 point light Range 10 Diffuse `-16759062` (A255 R0 G70 B234).
- Текстури DXT1 без punch-through alpha. `toumingqiublue` — темна скляна куля (адитив дає обідок, не ціан-заливку). `shuizhu` — багато дрібних крапель на чорному. `particle004` — ціан-радіальний градієнт. `ringblue` — тонке синє кільце.
- Світло ID 130 у C++ не малює себе (`A3DGFXLight::Render` порожній поза editor wire). Checker `AfxSetA3DSkinModelSceneLightInfo` світить лише skin model (`colPtDiff = Diffuse * a * 2`, atten 0, 0.1, 0). Декалі/частинки unlit. Яскравість сфери не пояснюється лише Unity Light.
- `A3DGFXCtrlColorChange` теж клампить 0..255, тож overbright ColorDelta не є причиною.
- Наступний крок — ізольований capture підмножин елементів (лише 101, лише 123 `particle004`, лише `shuizhu`, лише 100-кільця, лише 130+160) у новій папці `.validation/checklist-first10/GFX-00002/isolate/`, не перезаписуючи `model-recheck`. Не підганяти коефіцієнти.

## Артефакти останнього технічного проходу

Папка: `.validation/checklist-first10/model-recheck/` (дані попереднього проходу, повторно не рендерилися під час створення нових правил).

- `final-capture5.json`: останній успішний capture, №2 — 160 кадрів, регресія №19 — 10 кадрів.
- `GFX-00002/unity-frame-*.png`, `cpp.frame-*.png`, `comparison-final.png`, `pair-final-800ms.png`.
- `GFX-00019/unity-frame-*.png`, `regression-19-500ms.png`; native еталон №19 — `.validation/gfx-cpp-unity-check/GFX-00019/frame-*.png`.
- `cpp.log`, `provenance.json`, `tests-final-result.json` — раніше отримано 244/244 Gfx tests Passed. Це історичний результат, не новий запуск поточного проходу.
- Камера №2: (0,12,-25)→(0,2,0), FOV65, 512², scale3.5, GFX40..6400ms крок40, native wall tick50ms та PlaySpeed0.8. Ранні пози частково виходять за кадр; за потреби додати узгоджений ракурс.
- `before-emission/`, `after-emission/` — проміжні кадри. `comparison.png` містить неправильний масштаб1. FinalCapture2/3/4 мали проблеми ізоляції/сторонній Reference Fire; не використовувати їх як остаточні докази.

Релевантні файли: `unity-package/Runtime/GfxModelDriver.cs`, `unity-package/Tests/Editor/GfxModelTests.cs`, `Tools/Validation/GfxParity/GfxCppUnityBatchCapture.cs`. Є також зміни інших агентових проходів у particle/trail/keypoint/model importer — не перезаписувати.

## Відтворення й відомі проблеми інструментів

- Поточний capture: `Tools/Validation/GfxParity/GfxCppUnityBatchCapture.cs`; runner `Tools/Validation/GfxParity/run_unity_frame_captures.py`. Використовувати абсолютний out-root та `PYTHONIOENCODING=utf-8`. Camera/effect/run JSON лежать у outRoot/ID.
- Приклад одного ID: `python Tools/Validation/GfxParity/run_unity_frame_captures.py --out-root <ABSOLUTE_DIRECTORY> --from 1 --to 2 --batch 1 --force`. Перед --force зберегти потрібні попередні кадри. Індекси діапазону нульові.
- Самодостатній попередній capture: `.validation/checklist-first10/model-recheck/FinalCapture5.cs`, entry `GfxModelRecheckEntry5.Run`; послідовно знімає №2 та №19 і перезаписує їхні кадри. Перед повтором перевірити його відповідність поточному основному скрипту.
- Native копія harness: `.validation/angelica-unity-ten/checker/bin/x64/Release/GFXAngelica2.2Checker.exe`. Для №2: env `GFX_AUDIT_CAMERA=0,12,-25,0,2,0`; параметри `--preview --root B:/Git/PW146RUUnpackedAssets --gfx gfx/效果/无定宙水.gfx --output <ABSOLUTE_PREFIX> --width 512 --height 512 --frames 160`. Одразу зберігати checker.log.
- CLI: `unity recompile --project-path <PROJECT> --format json`; встановлена версія цієї команди не приймала --caller. Script/test команди використовували `--caller plugin --skill unity-cli`. Дочекатися результату компіляції перед capture.
- Звичайна тимчасова сцена має закриватися з видаленням запису (`CloseScene(scene,true)`), інакше лишається порожня вивантажена сцена, що заважає наступному capture. Не закривати невідомі незбережені сцени користувача. Scene culling mask сама по собі не ізолювала сторонній Reference Fire.
- Попередня делегація lighting review до `opencode/glm-5.3-flash-free --variant max` зависла без відповіді; власний процес був зупинений. Результату аналізу GLM немає. За новими правилами можна вибрати іншу модель. Цей документаційний прохід не запускав субагентів або довготривалих процесів; стан інших процесів перевірити перед роботою.

## Наступні дії

1. Зробити ізольований capture підмножин елементів №2 (див. вище), C++ ліворуч / Unity праворуч, той самий час ~2400 мс і камера (0,12,-25)→(0,2,0).
2. За результатом виправити одну загальну причину (частинки `shuizhu`/`particle004` або адитивні декалі), з регресією №19. Не оголошувати №2 виправленим, доки сфера, кільця й частинки не збігаються.
3. Потім закривати інші відкриті записи за порядком, включно з №1; після цього перейти до неперевірених з №21. Наявним успішним перевіркам довіряти, повторювати їх за впливом спільного фіксу чи конкретними новими доказами.
4. Оновити реєстр, докази й цей файл після кожного суттєвого результату та перед передачею іншому агенту.
