# Карта подключения FMOD — LD-60

FMOD Studio: `Audio_Fmod/LD-60/` · FMOD Engine 2.03.11 · Unity 6
Игра 2D, композиционная: **все ивенты 2D, позиция в коде не ставится** — панорама сделана внутри ивентов.
Форматы вывода (mono / stereo / surround) заданы в ивентах осознанно, не менять под стерео.

## 1. Банки

| Банк | Тип | Что внутри |
|---|---|---|
| `Master` | мастер | все ивенты, кроме меню |
| `Menu` | обычный | `start or menu`, `UI menu` (`UI menu` лежит в обоих) |

Грузит и следит за готовностью `FmodWebglPreloader` (WebGL: скачивание корутиной плагина, из кода только поллинг).

## 2. Микшер

```
Master
├── sfx_gr      ← SFX, type*, score, door, tompson, broke_window, protest,
│                  convert, paper_handing, roometone
├── dialog_gr   ← dialog
├── ui          ← UI menu
└── all_music
    ├── music   ← громофон
    └── start   ← start or menu, Final
```

`*` — вложен в `SFX`, играет внутри его микшерной группы.

## 3. Параметры

Runtime-максимум = индекс последней метки (банк обрезает XML `maximum`).
Имена параметров в Studio — без пробелов.

| Параметр | Тип | Значения | Initial | Кто пишет | Смысл в игре |
|---|---|---|---|---|---|
| `персонажи` | labeled | бабка 0 · чиновница 1 · чикист 2 · чиновник толстенький 3 · анонимус маска 4 · анонимус без маски 5 · РЫБОЧЕЛ 6 | 0 | код (`dialog`) | ID персонажа из `AudioManager` → кто говорит |
| `набор слов` | continuous | 0 = молчит · 1 = говорит | 0 | код (`dialog`) | вкл/выкл реплики: на 0 ивент стопается, поэтому инстанс создаётся заново на каждое слово |
| `score` | continuous | 0…1 | 0 | код (`score`) | очки за собранную газету (0.10, 0.20 …), параметр следует за очками. Передавать как есть или нормализовать — **решает Влад** |
| `write_final` | labeled | writing 0 · spacebar 1 · new_string 2 · char 3 · none 4 | 4 (none) | код (`Final`) | каким способом игрок дописал финал → какой вариант концовки играет |
| `cutscene or menu` | labeled | cut 0 · menu 1 · start 2 | 0 | код (`start or menu`) + `Final` пишет 1 | запуск игры (катсцена) / лобби / нажата Start |
| `on or off` | labeled | on 0 · off 1 | 0 | код (`LoudspeakerAudio`) | тумблер громофона |
| `track` | labeled | track1 0 · track2 1 · track3 2 | 0 | код (`LoudspeakerAudio`) | выбор пластинки |
| `newspaper` | labeled | typing 0 · type 1 · convert 2 · none 3 | 3 (none) | код (`SfxAudio`) | взяли бумажку / печать / открыли конверт / покой |
| `final` | labeled, **GLOBAL** | final 0 · game 1 | 1 | **пишет `Final`**, читает `громофон` | наступила концовка или ещё игра. Вся логика внутри FMOD, код не трогает |

## 4. Ивенты

### 4.1 Подключено из кода

| Ивент | Банк / группа | Параметры | Из игры | Код |
|---|---|---|---|---|
| `event:/громофон` | Master / music | `on or off`, `track` | игрок включает/выключает громофон и выбирает пластинку | `LoudspeakerAudio.cs`: инстанс живёт **только когда включён** (после самостопа — `start()` заново + пере-применение параметров, опрос `EnsurePlaying()` 0.1 с) |
| `event:/Music` | Master / music | — | фоновая музыка | `LoudspeakerAudio.cs`: один вечный инстанс |
| `event:/SFX` | Master / sfx_gr | `newspaper` | взяли бумажку → напечатали → открыли конверт | `SfxAudio.cs`: вечный инстанс, «клик параметром» (значение подняли → вложенный звук доиграл → вернули `none`) |

Вложенные в `SFX` (кодом **не** запускаются, живут на значениях `newspaper`):

| Значение | Вложенный ивент | Звук |
|---|---|---|
| typing 0 | `paper_handing` | взяли бумажку (14 рандомных шумов, кулдаун 120 мс) |
| type 1 | `type` | печать (2 контейнера: 7 + 5 вариантов) |
| convert 2 | `convert` | открыли конверт |

### 4.2 Ждут подключения

| Ивент | Банк / группа | Параметры | Из игры | Как звать из кода |
|---|---|---|---|---|
| `event:/dialog` | Master / dialog_gr | `персонажи`, `набор слов` | AudioManager получил ID персонажа → реплика на каждое слово | **инстанс на каждое слово** (сам себя стопает): создать → `setParameterByName("персонажи", id)` → `setParameterByName("набор слов", 1)` → `start()` → по окончании `stop() + release()` |
| `event:/score` | Master / sfx_gr | `score` | начислили очки за газету | инстанс на каждое начисление: создать → `setParameterByName("score", очки)` → `start()`, дальше не трогать (стопы на 3.5 / 9 / 12.77 с) |
| `event:/start or menu` | Menu / start | `cutscene or menu` | запуск игры = `cut`; лобби без катсцены = `menu`; нажатие Start = `start` | один инстанс на буте, `start()`; дальше только менять параметр; на `start` ивент **сам себя уничтожает** Command-инструментом — код обязан отпустить handle (`stop() + release()`) |
| `event:/Final` | Master / all_music | `write_final` | финал игры + титры | создать → `setParameterByName("write_final", способ)` → `start()`. Внутри сам ставит глобальный `final` и `cutscene or menu = menu`, сам зовёт `start or menu` |
| `event:/roometone` | Master / sfx_gr | — | фон локации: играет, **пока громофон выключен**; громофон включился → roometone уничтожить, выключился → вернуть | `CreateInstance + start()`; при выключении `stop() + release()` и создавать заново (луп 81.5 с, attack 4.6 с) |
| `event:/UI menu` | Menu+Master / ui | — | hover на кнопках | `RuntimeManager.PlayOneShot("event:/UI menu")` (кулдаун 380 мс и maxVoices 1 уже внутри) |
| `event:/door` | Master / sfx_gr | — | дверь | `PlayOneShot` (рандом питча ±23.5 %, панорама внутри) |
| `event:/tompson` | Master / sfx_gr | — | выстрел | `PlayOneShot` |
| `event:/broke_window` | Master / sfx_gr | — | разбитое окно | `PlayOneShot` |
| `event:/protest` | Master / sfx_gr | — | протест на улице | `PlayOneShot` (рандом из 3 ActionSheet, паузы 13.5 / 12.5 с) |

## 5. Граф «кто что включает и глушит»

- `громофон` ⇄ `roometone` — взаимоисключение. **Реализует код** (у `roometone` нет ни параметров, ни команд на соседа).
- `Final` → глобальный `final` = 0 (концовка) и `cutscene or menu` = menu → `start or menu`. Вся логика внутри FMOD.
- `start or menu` → при `start` сам себя останавливает Command-инструментом.
- `SFX` → `paper_handing` / `type` / `convert` — только параметром `newspaper`.
- `LoudspeakerAudio` → `громофон` → внутри выбор дорожки, темп, лупы; при глобальном `final` = 0 ведёт себя по своей логике.

## 6. Точки в коде

| Файл | Ответственность |
|---|---|
| `LD60.Main/Startup.cs` | `FmodWebglPreloader.PrepareAsync()` → `LoudspeakerAudio.EnsureCreated()` → `SfxAudio.EnsureCreated()` |
| `Audio/FmodWebglPreloader.cs` | банки → сэмплы → первый клик (WebGL), `IsReady` / `BecameReady` |
| `Audio/LoudspeakerAudio.cs` | `event:/Music`, `event:/громофон` (`SetOn`, `SetTrack`) |
| `Audio/SfxAudio.cs` | `event:/SFX` (`newspaper`, доигрывание вложенного) |
| `Audio/AudioManager.cs` | старый Unity-AudioSource путь, в WebGL заглушён; источник ID персонажа для `dialog` |

## 7. Правила вызова (обязательно)

- `EventInstance` — структура: проверять `isValid()`, сбрасывать `default`, не `null`.
- `setParameterByName(..., ignoreseekspeed: true)` — иначе значение «ползёт».
- `stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT)` — полный путь, `STOP_MODE` без префикса конфликтует.
- Имена параметров сверять с собранным банком (readback `getParameterByName`), не с XML.
- Ивент, который сам себя стопает, после `stop` параметрами не оживает — только новый `start()` / новый инстанс.
- Вложенный ивент: путь `event:/type` или `event:/SFX/type`.
- Банки после правок в Studio пересобирать (Ctrl+B → клик в Unity), иначе параметров нет в банке и set молча не работает.
