# Кватирка

Свіже повітря для Windows. Кватирка складається з двох частин:

- **оболонка** з островом замість панелі завдань, власним меню, швидкими налаштуваннями й календарем;
- **збирач**, який робить з офіційного ISO Windows 11 чистий образ без реклами, нав'язаних програм і стеження.

Ядро й драйвери лишаються рідними, тому працює все, що працює у Windows, зокрема ігри з античитами на рівні ядра.

## Оболонка

`Kvatyrka.exe` – один файл, нічого встановлювати не треба. Запустіть його, і острів замінить панель завдань.

- **Острів:** меню, закріплені й запущені програми, мережа зі звуком, мова, годинник.
- **Win** відкриває меню, а поєднання Win+E, Win+D та інші працюють як завжди.
- **Права кнопка** на програмі в меню чи на острові закріплює або відкріплює її.
- **Налаштування** (остання іконка на острові): тема, акцент, прозорість, порядок програм, заміна панелі Windows, автозапуск.

Усі налаштування лежать в одному файлі `%APPDATA%\Kvatyrka\settings.json`, його можна правити вручну. Журнал помилок пишеться поруч, у `kvatyrka.log`.

### Якщо щось пішло не так

`Kvatyrka.exe --restore` повертає звичну панель завдань Windows. Перезапуск Провідника робить те саме.

### Параметри запуску

| Параметр | Що робить |
| --- | --- |
| `--preview` | Показує острів, але не чіпає панель Windows |
| `--restore` | Повертає панель завдань Windows і завершується |
| `--snapshot <папка>` | Зберігає знімки острова й панелей у PNG |

## Збирач образу

Потрібні Windows 10 або 11, права адміністратора, 25 ГБ вільного місця й офіційний ISO Windows 11 з [сайту Microsoft](https://www.microsoft.com/software-download/windows11).

```powershell
.\builder\Build-Kvatyrka.ps1 -Iso D:\Win11.iso                    # універсальний профіль
.\builder\Build-Kvatyrka.ps1 -Iso D:\Win11.iso -Config gigant     # свій профіль
.\builder\Build-Kvatyrka.ps1 -Iso D:\Win11.iso -Check             # лише показати, що буде зроблено
```

Перед збиранням покладіть `Kvatyrka.exe` у `builder\shell\`. Готовий ISO з'явиться в `builder\out\`, його можна записати на флешку, наприклад, Rufus.

### Профілі

Профіль у `builder\profiles\*.json` визначає все: які програми залишити, що вимкнути, які компоненти поставити при першому запуску. Скопіюйте `universal.json`, змініть під себе й передайте його назву або шлях у `-Config`.

| Поле | Що задає |
| --- | --- |
| `edition` | Редакція: `Home`, `Pro`, `Education`, `Enterprise`, `ProWorkstations` |
| `keepApps` | Вбудовані програми, що лишаються (решта прибирається), можна з `*` |
| `privacy` | Телеметрія, реклама, Copilot, віджети, Bing у пошуку, OneDrive, Teams і Outlook |
| `system` | Локальний акаунт, гібернація, швидкий запуск, драйвери з Windows Update, ізоляція ядра, ігровий режим, відновлення |
| `folders` | Папки, які треба створити |
| `drivers` | Папка з драйверами (`.inf`) для вбудовування, відносно `builder\` |
| `wallpaper` | Шпалери за замовчуванням, відносно `builder\`; порожньо – залишити шпалери Windows |
| `packages` | Компоненти Microsoft для першого запуску: `name`, `url`, `args` |
| `kvatyrka` | Чи ставити оболонку Кватирки |

Компоненти завантажуються лише з офіційних адрес. Перед вбудовуванням збирач перевіряє, що кожен файл підписаний Microsoft, і зупиняється, якщо це не так.

**Локальний акаунт.** Якщо на екрані входу немає варіанта без інтернету, натисніть Shift+F10 і введіть `start ms-cxh:localonly`.

## Будова репозиторію

| Папка | Вміст |
| --- | --- |
| `shell/` | Оболонка (C#, WPF, .NET 10) |
| `builder/` | Збирач образу й профілі |
| `design/` | Джерело дизайну: макети, дизайн-система, іконки для оболонки |
| `tests/` | Перевірка збирача без Windows |

Дизайн має одне джерело: `design/mockups.py`. З нього генеруються макети, дизайн-система й іконки оболонки (`python design/icons_cs.py`), тож вони не розходяться між собою.

## Збірка

```powershell
dotnet build shell/Kvatyrka.Shell -c Release
dotnet publish shell/Kvatyrka.Shell -c Release -o publish -p:SelfContained=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

GitHub збирає оболонку на кожен коміт, робить знімки острова й панелей і перевіряє збирач.

Шпалери за замовчуванням – [фото з Unsplash](https://images.unsplash.com/photo-1500382017468-9049fed747ef) за [ліцензією Unsplash](https://unsplash.com/license).

## Подякувати

Кватирку робить Stepovyk. Підтримати: [Monobank](https://send.monobank.ua/jar/8aeC9dGFrn) · [Приват24](https://www.privat24.ua/send/kkyqm)

---

**Kvatyrka** is a Windows 11 shell (a floating "island" instead of the taskbar, own launcher, quick settings and calendar) and an image builder that turns an official Windows 11 ISO into a clean one without ads, bloatware and telemetry. The Windows kernel and drivers stay intact, so everything that runs on Windows, kernel-level anti-cheat games included, keeps working.
