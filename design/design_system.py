# Збирає дизайн-систему Кватирки з тих самих токенів і компонентів, що й макети.
import json, os, sys, re, datetime
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mockups as B

DS = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out', 'design-system', 'project')
WALL = '/_blob/b1feb6c221c051aca93cc6e8288afab6'
ICON_IDS = {
 'apps': '7d41c44d5f273546e89ddea606b1fd1e', 'arrow': '3fe3412041605dd572ad63887ecf4bf4', 'bag': 'a37852195e234a9ca35b9e55dd95b52d',
 'calc': 'f153ad5bb36981a43da34d118d6699ff', 'close': '0b9fb71f5b25861185dde2d88dd0c8ab', 'display': '31f6ecf0163d96fd8df19539575316f2',
 'download': '07f470f86b76b1b94433b439c75d0b2c', 'drive': '77e5cf9c03e1df00b8b4e59bd733b725', 'folder': 'eddffe554b82232fcd167aba68e60d12',
 'gamepad': '238e9e71e3f9c3285dca11000c34c6d8', 'globe': '3905b691cc70e497c326c493354c342d', 'home': '8b65fdc9e2691100cb727f71a229b2d7',
 'left': 'a3e48915119ad7973be7d347538cd164', 'max': '6345d29c111241ac14d4818398f3a266', 'min': '8c87fa075c57d222cb9ec040ed31499d',
 'moon': '7545ce2db6f0f1ff4b5dd6d6bf050315', 'music': '13308826d2c9892871264b13fde85ee4', 'note': 'ae66112e288c2f9917f3c841877dac30',
 'palette': 'bb4be2ca5e5408803b31506350f1da40', 'photo': '2c06d450509aef69b50569b6dcddc432', 'power': '009b11203c1c78b3f6cc641b69b4c64f',
 'refresh': 'c3ac0acb79227f9f4b56f7f4ad043dd5', 'right': '4c45714ff183e52473f80f7da6e2c6e7', 'search': 'e87b195c8070f50eb5d6393068b601b0',
 'shield': 'cda2dd6ec8ad2cbf5c52eec03bbf261e', 'shot': '0e346a8e653c6cff4609b64655f0ac5a', 'sliders': 'a10c57022b7c873ce7b2526499a0529a',
 'sun': 'b6cf1c7f491cc39bb9b2151705d306c0', 'terminal': '458821657d8f233d1c85ebf9bef91b21', 'user': '3d6ea8abe6d3347d882ef352de5700ce',
 'video': '5e74e5ebfd51d4534b167c917661613f', 'contrast': '3b1448b40f5fc7ca25f9fb1687175269', 'volume': '938b00074b708a83ad8302af6dd1cf34', 'wifi': '980f8f300ff68fb1703633f8539615a5',
 'taskbar': 'c581cbc6fe6a60bc7e4bc7a8f0304951', 'block': '294b819f1e4892dec3a2e3162a8d8e23', 'trash': '338feb5e63db93c8617535a08cf4caea', 'island': 'e4cc032ba8586a6f1491901659391dcc'}
MARK_ID = '371917942b080c8bee154770436017f6'
WALL_ID = 'b1feb6c221c051aca93cc6e8288afab6'
ICON_UA = {'apps': 'програми', 'arrow': 'увійти, далі', 'bag': 'крамниця', 'calc': 'калькулятор', 'close': 'закрити', 'display': 'система, екран',
 'download': 'завантаження', 'drive': 'диск', 'folder': 'файли, папка', 'gamepad': 'ігри, ігровий режим', 'globe': 'браузер', 'home': 'домівка',
 'left': 'назад', 'max': 'розгорнути', 'min': 'згорнути', 'moon': 'не турбувати', 'music': 'музика', 'note': 'нотатки', 'palette': 'персоналізація',
 'photo': 'фото', 'power': 'вимкнення', 'refresh': 'оновлення', 'right': 'вперед', 'search': 'пошук', 'shield': 'приватність', 'shot': 'знімок екрана',
 'sliders': 'налаштування', 'sun': 'яскравість', 'contrast': 'темна тема', 'terminal': 'термінал', 'user': 'акаунт', 'video': 'відео', 'volume': 'звук', 'wifi': 'мережа',
 'taskbar': 'Пуск і панель задач', 'block': 'реклама й підказки', 'trash': 'прибрати програми', 'island': 'острів'}

# ключ у макетах → назва токена
KEYS = dict(text='text', sec='text-secondary', dis='text-disabled', glass='glass', pop='popover', border='border', divider='divider',
            line='line', active='fill-active', tile='fill-tile', field='fill-field', win='window', panel='panel', sel='selected',
            track='track', knob='knob', sh_win='shadow-window', sh_isl='shadow-island', sh_pop='shadow-popover')
USAGE = {
 'text': 'Основний текст, іконки й назви на window, panel, glass і popover.',
 'text-secondary': 'Другорядний текст: дата під часом, підказки в полях, шлях, порожні стани. Читається на window, panel, glass і popover.',
 'text-disabled': 'Недоступні кнопки (Вперед без історії) і розділювач «/» у шляху. Не для тексту, який треба прочитати.',
 'glass': 'Острів і вхід на екрані блокування. Завжди разом з blur-island.',
 'popover': 'Меню, швидкі налаштування, сповіщення з календарем. Завжди разом з blur-popover.',
 'border': 'Тонка рамка 1px навколо острова, панелей і вікон.',
 'divider': 'Вертикальні розділювачі 1×28 в острові.',
 'line': 'Горизонтальні лінії між секціями панелей і рядками налаштувань.',
 'fill-active': 'Підкладка відкритої програми, відкритої панелі й вибраного сегмента. Єдиний сигнал стану, без крапок і смужок.',
 'fill-tile': 'Підкладка плиток меню, вимкнених плиток швидких налаштувань і великого поля пошуку.',
 'fill-field': 'Поля в рядку вікна (шлях, пошук), сегментований перемикач, кнопка «Змінити», поле пароля.',
 'window': 'Тло вікон програм.',
 'panel': 'Внутрішня робоча область вікна (вміст праворуч від навігації).',
 'selected': 'Вибраний пункт навігації у вікні.',
 'track': 'Незаповнена частина повзунка.',
 'knob': 'Бігунок повзунка.',
 'accent': 'Єдиний акцент: увімкнені плитки й перемикачі, сьогоднішній день, заповнена частина повзунка, кнопка входу, шибка знака. Текст на ньому лише on-accent.',
 'on-accent': 'Текст і іконки на accent і на аватарі.',
 'solid-glass': 'Острів і панелі, коли прозорість вимкнена в налаштуваннях.',
 'accent-green': 'Акцент на вибір користувача в Налаштування → Персоналізація.',
 'accent-amber': 'Акцент на вибір користувача в Налаштування → Персоналізація.',
 'accent-coral': 'Акцент на вибір користувача в Налаштування → Персоналізація.',
 'accent-lilac': 'Акцент на вибір користувача в Налаштування → Персоналізація.',
 'accent-gray': 'Акцент на вибір користувача в Налаштування → Персоналізація.',
}
SHADOW_USAGE = {'shadow-window': 'Вікна програм.', 'shadow-island': 'Острів і вхід на екрані блокування.', 'shadow-popover': 'Меню й бокові панелі над островом.'}


def tokens():
    colors = []
    for k, name in KEYS.items():
        if k.startswith('sh_'):
            continue
        colors.append({'name': name, 'value': {'dark': B.DARK[k], 'light': B.LIGHT[k]}, 'usage': USAGE[name]})
    colors += [{'name': 'accent', 'value': {'dark': '#5b9bff', 'light': '#5b9bff'}, 'usage': USAGE['accent']},
               {'name': 'on-accent', 'value': {'dark': '#0e1420', 'light': '#0e1420'}, 'usage': USAGE['on-accent']},
               {'name': 'solid-glass', 'value': {'dark': '#1b1e23', 'light': '#f2f3f5'}, 'usage': USAGE['solid-glass']}]
    for n, v in [('accent-green', '#3fb68b'), ('accent-amber', '#e5a23b'), ('accent-coral', '#e26d5a'), ('accent-lilac', '#b07cf0'), ('accent-gray', '#8a93a0')]:
        colors.append({'name': n, 'value': {'dark': v, 'light': v}, 'usage': USAGE[n]})
    shadows = [{'name': n, 'value': {'dark': B.DARK[k], 'light': B.LIGHT[k]}, 'usage': SHADOW_USAGE[n]} for k, n in KEYS.items() if k.startswith('sh_')]
    return {
     'name': 'Кватирка', 'version': 1,
     'color': {'themes': [{'id': 'dark', 'name': 'Темна'}, {'id': 'light', 'name': 'Світла'}], 'tokens': colors},
     'type': {'fonts': [], 'families': {'sans': 'Inter, system-ui, sans-serif'},
              'groups': [{'name': 'Інтерфейс', 'family': 'sans', 'styles': [
                  {'name': 'search-large', 'fontSize': '15px', 'lineHeight': 1, 'fontWeight': 400, 'sample': 'Пошук програм, файлів і налаштувань', 'usage': 'Велике поле пошуку в меню.'},
                  {'name': 'title', 'fontSize': '14px', 'lineHeight': 1, 'fontWeight': 600, 'sample': 'Файли', 'usage': 'Назви вікон і секцій, час в острові, плитки швидких налаштувань.'},
                  {'name': 'label', 'fontSize': '14px', 'lineHeight': 1, 'fontWeight': 500, 'sample': 'УКР', 'usage': 'Мова, імʼя користувача, кнопки з текстом.'},
                  {'name': 'body', 'fontSize': '14px', 'lineHeight': 1, 'fontWeight': 400, 'sample': 'Завантаження', 'usage': 'Пункти навігації, рядки списків, підписи плиток, налаштування.'},
                  {'name': 'caption', 'fontSize': '12px', 'lineHeight': 1, 'fontWeight': 400, 'sample': 'пт, 6 листопада', 'usage': 'Дата в острові, дні тижня в календарі.'}]}]},
     'spacing': {'tokens': [
         {'name': 'space-2', 'value': '2px', 'usage': 'Між пунктами навігації й рядками списку.'},
         {'name': 'space-4', 'value': '4px', 'usage': 'Між комірками острова, плитками меню й днями календаря.'},
         {'name': 'space-6', 'value': '6px', 'usage': 'Відступ іконки всередині поля 36 px.'},
         {'name': 'space-8', 'value': '8px', 'usage': 'Внутрішні краї острова (з рамкою), відступ області вмісту від краю вікна, між плитками швидких налаштувань.'},
         {'name': 'space-12', 'value': '12px', 'usage': 'Між іконкою й текстом.'},
         {'name': 'space-16', 'value': '16px', 'usage': 'Відступ острова від краю екрана й панелей від острова; між секціями панелі.'},
         {'name': 'space-20', 'value': '20px', 'usage': 'Між пошуком і сіткою програм у меню.'},
         {'name': 'space-24', 'value': '24px', 'usage': 'Внутрішній відступ меню й бокових панелей (з рамкою); відступ іконок навігації від краю вікна.'}]},
     'radius': {'tokens': [
         {'name': 'radius-8', 'value': '8px', 'usage': 'Область вмісту у вікні й рядки списку (вкладене у вікно з відступом 8: 16 − 8).'},
         {'name': 'radius-10', 'value': '10px', 'usage': 'Елементи 36 px: кнопки й поля в рядку вікна, пункти навігації.'},
         {'name': 'radius-12', 'value': '12px', 'usage': 'Дні календаря, перемикач.'},
         {'name': 'radius-14', 'value': '14px', 'usage': 'Комірки 48 px в острові, плитки меню, велике поле пошуку.'},
         {'name': 'radius-16', 'value': '16px', 'usage': 'Вікна, плитки швидких налаштувань.'},
         {'name': 'radius-22', 'value': '22px', 'usage': 'Острів, меню й бокові панелі.'},
         {'name': 'radius-full', 'value': '9999px', 'usage': 'Аватар і кольори акценту.'}]},
     'shadow': {'tokens': shadows},
     'size': {'note': 'Фіксовані розміри, з яких складається вся оболонка.', 'tokens': [
         {'name': 'size-icon', 'value': '24px', 'usage': 'Єдиний розмір іконки скрізь.'},
         {'name': 'size-control', 'value': '36px', 'usage': 'Кнопки й поля в рядку вікна, кнопки календаря.'},
         {'name': 'size-row', 'value': '40px', 'usage': 'Пункт навігації, день календаря.'},
         {'name': 'size-cell', 'value': '48px', 'usage': 'Комірка острова, плитка програми в меню.'},
         {'name': 'size-island', 'value': '64px', 'usage': 'Висота острова.'},
         {'name': 'size-menu-min', 'value': '640px', 'usage': 'Найменша ширина меню; ширше острова воно не буває.'},
         {'name': 'size-side-panel', 'value': '408px', 'usage': 'Ширина бокових панелей: швидкі налаштування, сповіщення з календарем.'},
         {'name': 'size-title-bar', 'value': '56px', 'usage': 'Верхній рядок вікна.'},
         {'name': 'size-nav', 'value': '216px', 'usage': 'Ширина навігації у вікні разом з рамкою.'}]},
     'blur': {'note': 'Матеріал скла.', 'tokens': [
         {'name': 'blur-island', 'value': 'blur(28px) saturate(140%)', 'usage': 'backdrop-filter острова.'},
         {'name': 'blur-popover', 'value': 'blur(32px) saturate(140%)', 'usage': 'backdrop-filter меню й панелей.'}]},
     'stroke': {'tokens': [{'name': 'stroke-icon', 'value': '1.5px', 'usage': 'Товщина лінії всіх іконок.'}]},
    }


def tokens_css(tk):
    def block(theme):
        out = []
        for t in tk['color']['tokens'] + tk['shadow']['tokens']:
            out.append(f'  --{t["name"]}: {t["value"][theme]};')
        return '\n'.join(out)
    other = []
    for fam in ('spacing', 'radius', 'size', 'blur', 'stroke'):
        for t in tk[fam]['tokens']:
            other.append(f'  --{t["name"]}: {t["value"]};')
    other.append(f'  --font-sans: {tk["type"]["families"]["sans"]};')
    styles = []
    for s in tk['type']['groups'][0]['styles']:
        ls = f' letter-spacing: {s["letterSpacing"]};' if 'letterSpacing' in s else ''
        styles.append(f'.{s["name"]} {{ font-family: var(--font-sans); font-size: {s["fontSize"]}; line-height: {s["lineHeight"]}; font-weight: {s["fontWeight"]};{ls} }}')
    return ('/* Кватирка — generated from tokens.json */\n'
            f':root, [data-theme="dark"] {{\n{block("dark")}\n}}\n[data-theme="light"] {{\n{block("light")}\n}}\n:root {{\n' + '\n'.join(other) + '\n}\n' + '\n'.join(styles) + '\n')


# ---------- компоненти з тих самих функцій, що й макети, але на токенах ----------
VT = {k: f'var(--{n})' for k, n in KEYS.items()}
B.ACCENT, B.ON_ACCENT = 'var(--accent)', 'var(--on-accent)'


def unpin(html):
    html = re.sub(r'position: absolute; left: \d+px; (bottom|top): \d+px; ', 'position: relative; ', html)
    html = re.sub(r'(blur\(\d+px\) saturate\(140%\))', lambda m: 'var(--blur-island)' if '28px' in m.group(1) else 'var(--blur-popover)', html)
    return html


FONT = '<link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600&amp;display=swap" rel="stylesheet">'


def preview(group, height, subtitle, body, ground='wall', width=None, css=''):
    w = f' width={width}' if width else ''
    g = {'wall': f'background: url({WALL}) center/cover no-repeat; padding: 24px;', 'window': 'background: var(--window); padding: 24px;', 'none': 'padding: 0;'}[ground]
    return (f'<!-- @dsCard group="{group}" height={height}{w} subtitle="{subtitle}" -->\n<!doctype html>\n<html lang="uk">\n<head>\n<meta charset="utf-8">\n{FONT}\n'
            f'<style>\nbody{{margin:0;font-family:var(--font-sans);font-size:14px;color:var(--text)}}\nbutton{{font:inherit;color:inherit;border:0;background:none;cursor:pointer;padding:0}}\na{{color:inherit}}\n.wall{{background:url({WALL}) center/cover no-repeat}}\n{css}</style>\n</head>\n<body>\n'
            f'<div style="{g} display: flex; gap: 24px; align-items: flex-start; flex-wrap: wrap; min-height: {height - (0 if ground == 'none' else 48)}px;">\n{body}\n</div>\n</body>\n</html>\n')


def comps():
    t = VT
    c = {}
    c['Island'] = (preview('Оболонка', 112, 'Єдина панель внизу екрана', unpin(B.island(t, 'Файли')), width=760),
        '''# Island
Острів – єдина панель Кватирки внизу по центру екрана: меню, закріплені програми, швидкі налаштування, мова й годинник в одній капсулі.

## Будова
- Ширина за вмістом, висота `size-island`, відступ від нижнього краю екрана `space-16`, по центру.
- Тло `glass` з `blur-island`, рамка 1 px `border`, заокруглення `radius-22`, тінь `shadow-island`.
- Комірки `size-cell` з заокругленням `radius-14`, між ними `space-4`, іконка `size-icon` по центру.
- Порядок фіксований: знак Кватирки, розділювач `divider`, закріплені програми, запущені незакріплені, розділювач, мережа й звук однією кнопкою на дві комірки, мова, годинник.
- Системні програми – іконками Кватирки; сторонні – своїми справжніми іконками того ж розміру.
- Клік: програма не запущена – запуск; її вікно спереду – згорнути; інакше – показати. Права кнопка – закріпити, відкріпити, закрити вікна.

## Стани
- Відкрита програма або відкрита панель: підкладка `fill-active` під коміркою. Інших позначок (крапок, смужок) немає.
- Розгорнуте вікно зупиняється на `space-16` над островом; у повноекранній грі острів ховається повністю.

## Не можна
- Додавати комірки іншого розміру чи третій розділювач.
- Розтягувати острів на всю ширину екрана.
''')
    c['Menu'] = (preview('Оболонка', 560, 'Відкривається знаком Кватирки', unpin(B.menu(t)), width=760),
        '''# Menu
Меню Кватирки – пошук і всі програми, відкривається знаком на острові або клавішею Win.

## Будова
- Тієї ж ширини, що й острів, але не вужче `size-menu-min`; стоїть на `space-16` над ним по центру.
- Тло `popover` з `blur-popover`, рамка `border`, `radius-22`, тінь `shadow-popover`, внутрішній відступ `space-24`.
- Велике поле пошуку висотою 52 px на `fill-tile`, текст `search-large`.
- Усі встановлені програми за абеткою. Колонок стільки, скільки вміщується плиток від 100 px з кроком `space-4`; сітка стоїть рівно по центру, видно чотири ряди, далі прокрутка.
- Плитка 92 px: іконка `size-icon` у квадраті `size-cell` на `fill-tile`, підпис `body`. Наведення – `fill-field`, вибрана – `fill-active`.
- Пошук фільтрує одразу; Enter запускає першу, стрілка вниз переходить до сітки. Права кнопка – закріпити або відкріпити на острові.
- Під лінією `line` – користувач і вимкнення. Більше ніде в системі цих двох дій немає.

## Не можна
- Додавати заголовки на кшталт «Закріплені» чи «Рекомендовані»: меню показує лише програми.
''')
    c['QuickSettings'] = (preview('Оболонка', 330, 'Відкривається кнопкою мережі й звуку', unpin(B.quick(t)), width=460, css=B.vol_css(t).replace(t['track'], 'var(--track)')),
        '''# QuickSettings
Швидкі налаштування – чотири перемикачі й гучність, відкриваються кнопкою мережі й звуку на острові.

## Будова
- Ширина `size-side-panel`, правий край врівень з правим краєм острова, на `space-16` над ним.
- Плитки 56 px у дві колонки з кроком `space-8`, заокруглення `radius-16`, іконка й текст `title` з відступом `space-16`.
- Увімкнена плитка – `accent` з текстом `on-accent`, вимкнена – `fill-tile`.
- Повзунок гучності: заповнена частина `accent`, решта `track`, бігунок `knob` 20 px.

## Що тут
Мережа (стан і перехід до параметрів мережі), ігровий режим, не турбувати, темна тема – разом із Windows. Усе пишеться туди ж, куди й перемикачі в параметрах Windows. Користувача й вимкнення тут немає: вони живуть у меню.
''')
    c['ClockPanel'] = (preview('Оболонка', 580, 'Відкривається годинником', unpin(B.clock_panel(t)), width=460),
        '''# ClockPanel
Сповіщення й календар – одна панель, що відкривається годинником на острові.

## Будова
- Ширина `size-side-panel`, правий край врівень з островом, як у QuickSettings.
- Зверху рядок «Сповіщення» з кнопкою «Відкрити»: вона відкриває центр сповіщень Windows. Під лінією `line` – календар.
- Дні – комірки `size-row` з `radius-12` у сітці на сім колонок з кроком `space-4`; тиждень починається з понеділка.
- Сьогодні – `accent` з `on-accent`; дні інших місяців не показуються.
''')
    c['Window'] = (preview('Вікна', 720, 'Вікно Кватирки: покращення Windows', unpin(B.kvatyrka_window(t, 'home', True)), width=1040),
        '''# Window
Вікно Кватирки: верхній рядок, навігація ліворуч, область вмісту праворуч. Розділи – «Головне», розділи каталогу покращень, «Програми» й «Острів».

## Будова
- Тло `window`, рамка `border`, `radius-16`, тінь `shadow-window`; розмір 960×640 (на низьких екранах – за висотою робочої області), вікно перетягується за верхній рядок.
- Верхній рядок `size-title-bar`: знак і назва «Кватирка» над навігацією, назва розділу над вмістом, праворуч «Згорнути» й «Закрити».
- Навігація шириною `size-nav`: пункти `size-row`, `radius-10`, вибраний на `selected`.
- Область вмісту – `panel` з відступом `space-8` від країв вікна й заокругленням `radius-8`; прокручується.
- Блок угорі розділу: заголовок 20/24 `SemiBold`, пояснення 12/18 `text-secondary` шириною до 560, під ним кнопки (головна – `accent` з `on-accent`).
- Рядок налаштування: назва `body` 14/20, під нею опис 12/18 `text-secondary`, що переноситься; праворуч за `space-24` елемент керування – Switch, Segmented чи кнопка. Між рядками лінія `line`, після останнього – немає.
- Коли зміна з'явиться лише після перезапуску Провідника, входу чи перезавантаження, знизу області вмісту – рядок з поясненням, «Пізніше» й кнопкою дії.
- Підтвердження – картка `window` шириною 440 з `radius-14` і `shadow-popover` поверх затемненого вікна.

## Вирівнювання
Знак Кватирки стоїть точно над іконками навігації; елементи керування вирівняні по правому краю.
''')
    seg = ''.join(f'<button role="radio" aria-checked="{"true" if on else "false"}" style="height: 32px; padding: 0 14px; border-radius: 8px; line-height: 1;{" background: var(--fill-active); font-weight: 600;" if on else " color: var(--text-secondary);"}">{n}</button>'
                  for n, on in (('Світла', False), ('Темна', True), ('Автоматично', False)))
    c['Segmented'] = (preview('Елементи', 96, 'Вибір одного з кількох', f'<div role="radiogroup" aria-label="Тема" style="height: 36px; display: flex; padding: 2px; box-sizing: border-box; background: var(--fill-field); border-radius: 10px;">{seg}</div>', ground='window'),
        '''# Segmented
Сегментований перемикач – вибір одного з двох-чотирьох варіантів в одному рядку налаштувань.

Висота `size-control`, тло `fill-field`, `radius-10`; вибраний сегмент на `fill-active` з накресленням `title`, решта `text-secondary`.
''')
    sw = ''.join(f'<button role="radio" aria-checked="{"true" if i == 0 else "false"}" aria-label="{n}" style="width: 32px; height: 32px; border-radius: 50%; display: flex; align-items: center; justify-content: center;{" box-shadow: inset 0 0 0 2px var(--" + tok + ");" if i == 0 else ""}"><span style="width: 22px; height: 22px; border-radius: 50%; background: var(--{tok});"></span></button>'
                 for i, (n, tok) in enumerate((('Блакитний', 'accent'), ('Зелений', 'accent-green'), ('Бурштиновий', 'accent-amber'), ('Кораловий', 'accent-coral'), ('Бузковий', 'accent-lilac'), ('Сірий', 'accent-gray'))))
    c['AccentPicker'] = (preview('Елементи', 96, 'Вибір кольору акценту', f'<div role="radiogroup" aria-label="Акцент" style="display: flex; gap: 8px;">{sw}</div>', ground='window'),
        '''# AccentPicker
Вибір акценту – шість кольорів, один з яких стає `accent` для всієї системи.

Кружки 22 px у кнопках 32 px з кроком `space-8`; вибраний обведено кільцем 2 px його ж кольору.
''')
    def switch(on):
        return (f'<button role="switch" aria-checked="{"true" if on else "false"}" aria-label="{"Увімкнено" if on else "Вимкнено"}" style="width: 44px; height: 24px; border-radius: 12px; background: var({"--accent" if on else "--track"}); position: relative;">'
                f'<span style="position: absolute; top: 2px; left: {22 if on else 2}px; width: 20px; height: 20px; border-radius: 50%; background: #ffffff;"></span></button>')
    c['Switch'] = (preview('Елементи', 88, 'Увімкнено або вимкнено', switch(True) + switch(False), ground='window'),
        '''# Switch
Перемикач – одне налаштування, яке або увімкнене, або ні.

44×24 px, `radius-12`; увімкнений – `accent`, вимкнений – `track`; бігунок білий 20 px.
''')
    ib = (f'<button aria-label="Файли" style="{B.CELL}">{B.I("folder")}</button>'
          f'<button aria-label="Файли, відкрито" style="{B.CELL} background: var(--fill-active);">{B.I("folder")}</button>'
          f'<button aria-label="Назад" style="width: 36px; height: 36px; border-radius: 10px; display: flex; align-items: center; justify-content: center;">{B.I("left")}</button>'
          f'<button aria-label="Вперед" disabled style="width: 36px; height: 36px; border-radius: 10px; display: flex; align-items: center; justify-content: center; color: var(--text-disabled);">{B.I("right")}</button>')
    c['IconButton'] = (preview('Елементи', 96, 'Комірка острова й кнопка вікна', ib, ground='window'),
        '''# IconButton
Кнопка-іконка – комірка острова (`size-cell`, `radius-14`) або кнопка вікна (`size-control`, `radius-10`), всередині завжди іконка `size-icon`.

Стани: звичайна без підкладки, відкрита на `fill-active`, недоступна кольором `text-disabled`. Кожна кнопка має `aria-label`.
''')
    return c


README = '''Кватирка доводить Windows до ладу: вікно з покращеннями поверх рідних Пуску, панелі задач і Провідника, а за бажанням – острів замість панелі. Одна мова на все, нічого зайвого, кожна річ в одному місці.

## Принципи
- **Рідне лишається.** Кватирка не підробляє вигляд Windows і не замінює його частин без прохання: вікно Кватирки – окреме, острів вмикає сама людина.
- **Одне ціле.** Острів, панелі й вікна збудовані з тих самих розмірів, заокруглень і кольорів. Нова частина бере наявні токени, а не вигадує свої.
- **Кожна річ один раз.** Користувач і вимкнення – лише в меню; мережа й звук – одна кнопка; сповіщення – у годиннику. Не дублюй дію в другому місці.
- **Стан – підкладкою.** Відкрита програма, відкрита панель і вибраний сегмент позначаються лише `fill-active`. Крапок, смужок і бейджів немає.
- **Рівно до пікселя.** Усі розміри цілі; внутрішнє заокруглення = зовнішнє мінус відступ (`radius-16` − `space-8` = `radius-8`); краї панелей збігаються з краями острова.
- **Без моди.** Жодних градієнтів, неонових відблисків чи декоративних ілюстрацій. Виразність дають шпалери й один акцент.

## Мова інтерфейсу
- Українська, звичайний регістр: «Швидкі налаштування», а не «ШВИДКІ НАЛАШТУВАННЯ» чи «Швидкі Налаштування».
- Підписи – іменниками або короткими дієсловами: «Файли», «Змінити», «Увійти». Без крапок у кінці й без емодзі. Описи під налаштуваннями – повними реченнями з крапкою: що зміниться для людини, а не що робиться в реєстрі.
- Порожні стани кажуть як є: «Нічого не знайдено».
- Час – 24-годинний (`03:01`); дата в острові – «пт, 6 листопада», на екрані блокування – «пʼятниця, 6 листопада»; тиждень починається з понеділка.

## Колір
- Дві теми, `dark` і `light`, з однаковими назвами токенів; за замовчуванням – темна.
- Текст – `text`, другорядне – `text-secondary`, недоступне – `text-disabled`; усе читається на `window`, `panel`, `glass` і `popover`.
- Скло: острів – `glass` + `blur-island`, панелі – `popover` + `blur-popover`. Рамка завжди `border`.
- Один акцент `accent` (користувач може замінити на `accent-green`, `accent-amber`, `accent-coral`, `accent-lilac` чи `accent-gray`). На ньому – лише `on-accent`.
- `solid-glass` замінює скло, коли прозорість вимкнена.

## Шрифт
Inter, три накреслення: 400, 500, 600 (в оболонці – вбудовані файли шрифту). Основний розмір 14 px; `caption` 12 px – лише для дати в острові, днів тижня й підказок у налаштуваннях. В елементах керування `line-height` дорівнює 1, щоб текст стояв точно по центру.

## Сітка й розміри
- Відступи кратні 2: `space-2` … `space-24`. Між іконкою й текстом завжди `space-12`.
- Керування трьох висот: `size-control` 36, `size-row` 40, `size-cell` 48. Острів – `size-island` 64.
- Острів стоїть на `space-16` від низу екрана; панелі – на `space-16` над островом; розгорнуте вікно – на `space-16` над островом.

## Форми й тіні
- Заокруглення за розміром елемента: 36 px → `radius-10`, 48 px → `radius-14`, вікно → `radius-16`, острів і панелі → `radius-22`.
- Тіні лише три: `shadow-window`, `shadow-island`, `shadow-popover`.

## Іконки
- Власний набір на сітці 24×24 (група Icons): лінія `stroke-icon` 1,5 px, заокруглені кінці й з'єднання, без заливки (крім знаків-крапок).
- Розмір лише `size-icon` 24 px – у кнопках, навігації, плитках і полях. Інших розмірів немає.
- У коді іконки вставляються вбудованим SVG з `stroke="currentColor"`, щоб брати колір тексту. Файли в групі Icons намальовані чорнилом `#16181C` для перегляду.
- Знак Кватирки (група Logos) – вікно з відчиненою шибкою кольору `accent`.
- Набір в оболонці генерується з того самого джерела (`design/icons_cs.py`), тож іконки на макетах і в Кватирці однакові.

## Шпалери
За замовчуванням – справжнє фото пшеничного поля на заході сонця (група Wallpapers, ліцензія Unsplash). Скло острова й панелей розраховане на будь-які шпалери.
'''

ASSET_READMES = {
 'Icons': '# Icons\nІконки Кватирки, сітка 24×24, лінія 1,5 px. Файли намальовані чорнилом `#16181C`; в інтерфейсі вставляй вбудованим SVG зі `stroke="currentColor"`.\n\n' + '\n'.join(f'- `{k}.svg` – {v}' for k, v in sorted(ICON_UA.items())) + '\n',
 'Logos': '# Logos\n`kvatyrka-mark.svg` – знак Кватирки: рамка й хрестовина чорнилом `#16181C` (на темному тлі – `text` темної теми), відчинена шибка `accent`.\n',
 'Wallpapers': '# Wallpapers\n`wheat-field-sunset.jpg` – шпалери за замовчуванням, фото з Unsplash (images.unsplash.com/photo-1500382017468-9049fed747ef) за ліцензією Unsplash. У збирачі – версія 3840×2160.\n',
}


def cover():
    return '''<!-- @dsCard height=320 -->
<!doctype html>
<html lang="uk">
<head>
<meta charset="utf-8">
<link href="https://fonts.googleapis.com/css2?family=Inter:wght@600&amp;display=swap" rel="stylesheet">
<style>
body{margin:0;background:var(--window)}
.cv{position:relative;width:960px;height:320px;background:var(--window);overflow:hidden;font-family:var(--font-sans)}
.cv svg{position:absolute;left:0;top:0}
.ink{fill:var(--text)}.acc{fill:var(--accent)}.amb{fill:var(--accent-amber)}.sec{fill:var(--text-secondary)}
.isl{fill:var(--panel);stroke:var(--border);stroke-width:1}
.cell{fill:var(--fill-tile)}
.name{position:absolute;left:48px;bottom:72px;margin:0;font-size:88px;line-height:.95;font-weight:600;letter-spacing:-0.02em;color:var(--text)}
.tag{position:absolute;left:48px;bottom:40px;margin:0;font-size:14px;line-height:1;color:var(--text-secondary);max-width:440px}
</style>
</head>
<body>
<div class="cv">
<svg width="960" height="320" viewBox="0 0 960 320" aria-hidden="true">
<!-- блоки: text, accent, accent-amber, text-secondary – шибки 104×104 (space-8 × 13), radius-22
     розстановка: вікно 2×2 з кроком space-8, праворуч від x=480; шибка accent – відчинена кватирка, виставлена на space-16 вгору-вправо
     візерунок: острів – капсула radius-22 з комірками radius-14 на кроці space-4; рядок «геометричний, модульний» – система складена з комірок
     кроки й заокруглення: space-8, space-16, space-4, radius-22, radius-14 -->
<rect class="ink" x="600" y="32" width="104" height="104" rx="22"/>
<rect class="acc" x="728" y="16" width="104" height="104" rx="22"/>
<rect class="amb" x="600" y="144" width="104" height="104" rx="22"/>
<rect class="sec" x="712" y="144" width="104" height="104" rx="22"/>
<rect class="isl" x="560.5" y="268.5" width="335" height="39" rx="19.5"/>
<rect class="cell" x="568" y="274" width="28" height="28" rx="10"/>
<rect class="cell" x="608" y="274" width="28" height="28" rx="10"/>
<rect class="cell" x="640" y="274" width="28" height="28" rx="10"/>
<rect class="cell" x="672" y="274" width="28" height="28" rx="10"/>
<rect class="cell" x="704" y="274" width="28" height="28" rx="10"/>
<rect class="cell" x="736" y="274" width="28" height="28" rx="10"/>
<rect class="cell" x="768" y="274" width="28" height="28" rx="10"/>
<rect class="cell" x="808" y="274" width="44" height="28" rx="10"/>
<rect class="cell" x="856" y="274" width="32" height="28" rx="10"/>
</svg>
<h1 class="name">Кватирка</h1>
<p class="tag">Свіже повітря для Windows</p>
</div>
</body>
</html>
'''


if __name__ == '__main__':
    os.makedirs(DS, exist_ok=True)
    tk = tokens()
    def w(path, text):
        p = os.path.join(DS, path); os.makedirs(os.path.dirname(p), exist_ok=True); open(p, 'w').write(text)
    w('tokens.json', json.dumps(tk, ensure_ascii=False, indent=1))
    w('tokens.css', tokens_css(tk))
    w('README.md', README)
    for name, (prev, readme) in comps().items():
        w(f'components/{name}/preview.html', prev)
        w(f'components/{name}/README.md', readme)
    w('components/Cover/preview.html', cover())
    for g, txt in ASSET_READMES.items():
        w(f'assets/{g}/README.md', txt)
    now = datetime.datetime.now(datetime.timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ')
    svg_dir = os.path.join(os.path.dirname(DS), 'svg')
    os.makedirs(svg_dir, exist_ok=True)
    for k, v in B.P.items():
        open(os.path.join(svg_dir, f'{k}.svg'), 'w').write(f'<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#16181C" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" color="#16181C">{v.replace("currentColor", "#16181C")}</svg>\n')
    open(os.path.join(svg_dir, 'kvatyrka-mark.svg'), 'w').write('<svg xmlns="http://www.w3.org/2000/svg" width="96" height="96" viewBox="0 0 24 24" fill="none" stroke-width="1.5" stroke-linejoin="round"><rect x="3.5" y="3.5" width="17" height="17" rx="4" stroke="#16181C"/><path d="M12 3.5v17M3.5 12h17" stroke="#16181C"/><path d="M13.5 5h3a2.5 2.5 0 0 1 2.5 2.5v3h-5.5z" fill="#5B9BFF"/></svg>\n')
    sizes = {k: os.path.getsize(os.path.join(svg_dir, f'{k}.svg')) for k in ICON_IDS}
    icons = {f'{k}.svg': {'name': f'{k}.svg', 'blob': v, 'size': sizes[k], 'type': 'image/svg+xml'} for k, v in sorted(ICON_IDS.items())}
    idx = {'v': 3, 'layout': 'files', 'createdOnFiles': {'v': 1, 'at': '2026-11-06T02:16:38Z'}, 'title': 'Кватирка', 'namespace': 'Kvatyrka', 'libraries': [],
           'sections': {}, 'groups': ['Logos', 'Icons', 'Wallpapers'],
           'assetGroups': {
               'Logos': {'name': 'Logos', 'tile': 'l', 'order': ['kvatyrka-mark.svg'], 'files': {'kvatyrka-mark.svg': {'name': 'kvatyrka-mark.svg', 'blob': MARK_ID, 'size': os.path.getsize(os.path.join(os.path.dirname(DS), 'svg', 'kvatyrka-mark.svg')), 'type': 'image/svg+xml'}}},
               'Icons': {'name': 'Icons', 'tile': 'xs', 'order': sorted(icons), 'files': icons},
               'Wallpapers': {'name': 'Wallpapers', 'tile': 'l', 'order': ['wheat-field-sunset.jpg'], 'files': {'wheat-field-sunset.jpg': {'name': 'wheat-field-sunset.jpg', 'blob': WALL_ID, 'size': 837559, 'type': 'image/jpeg'}}}},
           'blobs': {}, 'docs': {'readme': 'project/README.md', 'sections': []},
           'lastChange': {'by': 'Денис', 'at': now, 'via': 'Claude', 'note': 'Узгоджено з оболонкою: іконка теми, рамка власних вікон, без екрана блокування'}}
    w('design-system.json', json.dumps(idx, ensure_ascii=False, indent=1))
    print('\n'.join(sorted(os.path.relpath(os.path.join(r, f), DS) for r, _, fs in os.walk(DS) for f in fs)))
