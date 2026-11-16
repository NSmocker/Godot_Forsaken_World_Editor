# Передача роботи GFX

Оновлено: 2026-11-16. Поточний прохід: узгодження нових правил, без змін реалізації та нових рендерів. Перед початком читати [AGENTS.md](AGENTS.md).

## Поточний стан

- Unity: `A:/Github/Unity Native Reader`, `ProjectSettings/ProjectVersion.txt` перевірено — 6000.0.60f1. Поточний етап лише URP Editor; URP Build/HDRP Editor/HDRP Build ще не підтверджені.
- Реєстр: [GFX_CPP_UNITY_CHECKLIST.md](GFX_CPP_UNITY_CHECKLIST.md), 9018 записів. №1–20 позначені перевіреними, але частина має відкриті розбіжності/недостатньо даних. №21 — перший непозначений. Статуси не скинуті.
- За прямою вказівкою користувача старі MD, включно зі звітами, видалені. Їхні посилання в історичних рядках реєстру можуть не працювати. Це не привід скинути перевірки. Кадри, JSON, логи та код не видалялися. Нові постійні документи: AGENTS.md, цей файл і реєстр.
- Є незакомічені зміни попередніх проходів. Перед правками виконати `git -c core.fsmonitor=false status --short` і вивчити релевантний diff. Видалення старих MD є навмисними; не відновлювати їх автоматично.

## Незавершена задача, яку продовжити першою

**GFX-00002** — `B:/Git/PW146RUUnpackedAssets/gfx/gfx/效果/无定宙水.gfx`.

Останній завершений технічний прохід у цьому чаті виправив GfxModelDriver:

- Емісія матеріалу модулюється текстурою шкіри через emission map; модель перестала бути білою.
- AlphaCmp керує alpha clipping незалежно від blending; прибрані помилкові вирізи.
- Capture harness зберігає DefaultScale, використовує ізольовану сцену, тимчасово вимикає чужі renderer-и й відновлює їх у finally.

**Залишилися:** відмінності освітлення/яскравості, центральної сфери, частинок та кілець. Не вважати №2 виправленим повністю. Спершу перевірити фактичний код і останні кадри; потім локалізувати одну з цих відмінностей за C++.

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

1. Перевірити dirty changes і актуальність наведених артефактів №2, не починати повторний аудит з нуля.
2. Продовжити відкритий фікс №2 за оригінальними алгоритмами, з цільовою регресією. При нерозв'язній перешкоді звернутися до користувача з конкретною причиною.
3. Потім закривати інші відкриті записи за порядком, включно з №1; після цього перейти до неперевірених з №21. Наявним успішним перевіркам довіряти, повторювати їх за впливом спільного фіксу чи конкретними новими доказами.
4. Оновити реєстр, докази й цей файл після кожного суттєвого результату та перед передачею іншому агенту.
