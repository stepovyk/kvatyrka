# Збирає всі екрани Кватирки з одних і тих самих компонентів і токенів.
import json, os

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(ROOT, 'out', 'mockups', 'project')
WALL = '/_blob/75096bbd194221a5c7752323110b3444'

DARK = dict(text='#F4F6F8', sec='rgba(244,246,248,0.68)', dis='rgba(244,246,248,0.36)',
            glass='rgba(18,20,24,0.78)', pop='rgba(18,20,24,0.8)', border='rgba(255,255,255,0.09)',
            divider='rgba(255,255,255,0.12)', line='rgba(255,255,255,0.08)', active='rgba(255,255,255,0.1)',
            tile='rgba(255,255,255,0.08)', field='rgba(255,255,255,0.06)', win='#16181C', panel='#1C1F24',
            sel='rgba(91,155,255,0.16)', track='rgba(255,255,255,0.16)', knob='#F4F6F8',
            sh_win='0 24px 64px rgba(0,0,0,0.45)', sh_isl='0 16px 40px rgba(0,0,0,0.3)', sh_pop='0 24px 64px rgba(0,0,0,0.4)')
LIGHT = dict(text='#16181C', sec='rgba(22,24,28,0.62)', dis='rgba(22,24,28,0.34)',
             glass='rgba(250,250,252,0.8)', pop='rgba(250,250,252,0.84)', border='rgba(0,0,0,0.08)',
             divider='rgba(0,0,0,0.12)', line='rgba(0,0,0,0.08)', active='rgba(0,0,0,0.07)',
             tile='rgba(0,0,0,0.06)', field='rgba(0,0,0,0.045)', win='#FFFFFF', panel='#F4F5F7',
             sel='rgba(47,123,245,0.13)', track='rgba(0,0,0,0.14)', knob='#FFFFFF',
             sh_win='0 24px 64px rgba(0,0,0,0.18)', sh_isl='0 16px 40px rgba(0,0,0,0.12)', sh_pop='0 24px 64px rgba(0,0,0,0.16)')
ACCENT = '#5B9BFF'
ON_ACCENT = '#0E1420'

DOT = '<circle cx="{x}" cy="{y}" r="{r}" fill="currentColor" stroke="none"></circle>'
P = {
    'folder': '<path d="M3 7.5A2.5 2.5 0 0 1 5.5 5H9l2 2h7.5A2.5 2.5 0 0 1 21 9.5v7a2.5 2.5 0 0 1-2.5 2.5h-13A2.5 2.5 0 0 1 3 16.5z"></path>',
    'globe': '<circle cx="12" cy="12" r="9"></circle><path d="M3 12h18"></path><path d="M12 3c2.5 2.6 3.8 5.6 3.8 9s-1.3 6.4-3.8 9c-2.5-2.6-3.8-5.6-3.8-9S9.5 5.6 12 3z"></path>',
    'gamepad': '<path d="M7 8h10a4 4 0 0 1 4 4v1.5a3.5 3.5 0 0 1-6.2 2.2L13.5 14h-3l-1.3 1.7A3.5 3.5 0 0 1 3 13.5V12a4 4 0 0 1 4-4z"></path><path d="M8 10.5v3M6.5 12h3"></path>',
    'terminal': '<rect x="3" y="5" width="18" height="14" rx="2.5"></rect><path d="M7 10l3 2-3 2M13 14h4"></path>',
    'bag': '<path d="M5 8h14l-1 11.5a1.5 1.5 0 0 1-1.5 1.5h-9A1.5 1.5 0 0 1 6 19.5z"></path><path d="M9 8V6.5a3 3 0 0 1 6 0V8"></path>',
    'sliders': '<path d="M4 7h10M18 7h2M4 17h4M12 17h8"></path><circle cx="16" cy="7" r="2"></circle><circle cx="10" cy="17" r="2"></circle>',
    'music': '<path d="M9 18V6l11-2v12"></path><circle cx="6.5" cy="18" r="2.5"></circle><circle cx="17.5" cy="16" r="2.5"></circle>',
    'photo': '<rect x="3" y="4.5" width="18" height="15" rx="2.5"></rect><circle cx="9" cy="10" r="1.75"></circle><path d="M21 15.5l-5-5-9.5 9"></path>',
    'video': '<rect x="3" y="5" width="18" height="14" rx="2.5"></rect><path d="M10 9.5v5l4.5-2.5z"></path>',
    'note': '<path d="M7.5 4H14l4 4v10.5a1.5 1.5 0 0 1-1.5 1.5h-9A1.5 1.5 0 0 1 6 18.5v-13A1.5 1.5 0 0 1 7.5 4z"></path><path d="M9.5 12.5h5M9.5 16h5"></path>',
    'calc': '<rect x="5" y="3" width="14" height="18" rx="2.5"></rect><path d="M8.5 7h7"></path>' + ''.join(DOT.format(x=x, y=y, r=0.9) for y in (11.5, 14.5, 17.5) for x in (9, 12, 15)),
    'shot': '<path d="M4 8V6.5A2.5 2.5 0 0 1 6.5 4H8M16 4h1.5A2.5 2.5 0 0 1 20 6.5V8M20 16v1.5a2.5 2.5 0 0 1-2.5 2.5H16M8 20H6.5A2.5 2.5 0 0 1 4 17.5V16"></path>',
    'search': '<circle cx="11" cy="11" r="6.5"></circle><path d="M16 16l4 4"></path>',
    'left': '<path d="M14.5 6l-6 6 6 6"></path>',
    'right': '<path d="M9.5 6l6 6-6 6"></path>',
    'min': '<path d="M6 12h12"></path>',
    'max': '<rect x="6" y="6" width="12" height="12" rx="2.5"></rect>',
    'close': '<path d="M7 7l10 10M17 7L7 17"></path>',
    'home': '<path d="M4 11l8-6.5 8 6.5v7.5a1.5 1.5 0 0 1-1.5 1.5h-13A1.5 1.5 0 0 1 4 18.5z"></path><path d="M10 20v-5h4v5"></path>',
    'drive': '<rect x="3" y="5" width="18" height="14" rx="2.5"></rect><path d="M3 10h18"></path>',
    'download': '<path d="M12 4v11M7.5 10.5L12 15l4.5-4.5"></path><path d="M5 19h14"></path>',
    'apps': '<rect x="4" y="4" width="6.5" height="6.5" rx="1.75"></rect><rect x="13.5" y="4" width="6.5" height="6.5" rx="1.75"></rect><rect x="4" y="13.5" width="6.5" height="6.5" rx="1.75"></rect><rect x="13.5" y="13.5" width="6.5" height="6.5" rx="1.75"></rect>',
    'wifi': '<path d="M4 10.2a11.3 11.3 0 0 1 16 0M7.5 13.5a6.4 6.4 0 0 1 9 0"></path>' + DOT.format(x=12, y=16.9, r=1.2),
    'volume': '<path d="M4 9.5h3l4-3.5v12l-4-3.5H4z"></path><path d="M15 9a4 4 0 0 1 0 6M17.5 6.5a7.5 7.5 0 0 1 0 11"></path>',
    'moon': '<path d="M19.5 14.5A8 8 0 0 1 9.5 4.5a8 8 0 1 0 10 10z"></path>',
    'sun': '<circle cx="12" cy="12" r="4"></circle><path d="M12 3v1.5M12 19.5V21M3 12h1.5M19.5 12H21M5.6 5.6l1.1 1.1M17.3 17.3l1.1 1.1M5.6 18.4l1.1-1.1M17.3 6.7l1.1-1.1"></path>',
    'power': '<path d="M12 3.5v8"></path><path d="M7.2 6.5a7 7 0 1 0 9.6 0"></path>',
    'display': '<rect x="3" y="4.5" width="18" height="12" rx="2.5"></rect><path d="M9 20h6M12 16.5V20"></path>',
    'palette': '<path d="M12 3.5a8.5 8.5 0 0 0 0 17c1.1 0 1.7-.8 1.7-1.7 0-.5-.2-.9-.5-1.2-.3-.3-.5-.7-.5-1.2 0-.9.8-1.7 1.7-1.7h2a4.1 4.1 0 0 0 4.1-4.1C20.5 6.9 16.7 3.5 12 3.5z"></path>' + ''.join(DOT.format(x=x, y=y, r=1.1) for x, y in ((7.5, 12), (9.5, 8), (14.5, 8))),
    'user': '<circle cx="12" cy="8.5" r="3.5"></circle><path d="M5 19.5a7 7 0 0 1 14 0"></path>',
    'shield': '<path d="M12 3.5l7 2.8v5.2c0 4.3-2.9 7.6-7 9-4.1-1.4-7-4.7-7-9V6.3z"></path>',
    'refresh': '<path d="M19.5 12a7.5 7.5 0 1 1-2.2-5.3"></path><path d="M19.5 4.5v4h-4"></path>',
    'arrow': '<path d="M5 12h14M13 6l6 6-6 6"></path>',
    'contrast': '<circle cx="12" cy="12" r="8.5"></circle><path d="M12 3.5a8.5 8.5 0 0 1 0 17z" fill="currentColor" stroke="none"></path>',
    'taskbar': '<rect x="3" y="4.5" width="18" height="15" rx="2.5"></rect><path d="M3 15h18M10 17.25h4"></path>',
    'block': '<circle cx="12" cy="12" r="8.5"></circle><path d="M6 6l12 12"></path>',
    'trash': '<path d="M4.5 7h15M9.5 7V5.5A1.5 1.5 0 0 1 11 4h2a1.5 1.5 0 0 1 1.5 1.5V7"></path><path d="M6.5 7l.8 11.6A1.5 1.5 0 0 0 8.8 20h6.4a1.5 1.5 0 0 0 1.5-1.4L17.5 7"></path><path d="M10 11v5M14 11v5"></path>',
    'island': '<rect x="3" y="4" width="18" height="16" rx="2.5"></rect><rect x="7.5" y="14.5" width="9" height="3" rx="1.5"></rect>',
}


def I(name, color=None):
    style = f' style="color: {color};"' if color else ''
    return (f'<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" '
            f'stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"{style}>{P[name]}</svg>')


def logo(t):
    return ('<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke-width="1.5" stroke-linejoin="round" aria-hidden="true">'
            f'<rect x="3.5" y="3.5" width="17" height="17" rx="4" stroke="{t["text"]}"></rect>'
            f'<path d="M12 3.5v17M3.5 12h17" stroke="{t["text"]}"></path>'
            f'<path d="M13.5 5h3a2.5 2.5 0 0 1 2.5 2.5v3h-5.5z" fill="{ACCENT}" stroke="none"></path></svg>')


def page(title, body, t, extra_css=''):
    return f'''<!doctype html>
<html lang="uk">
<head>
<meta charset="utf-8">
<title>{title}</title>
<script src="./support.js"></script>
</head>
<body>
<x-dc>
<helmet>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600&amp;display=swap" rel="stylesheet">
<style>
body{{margin:0}}
.wall{{background:url({WALL}) center/cover no-repeat}}
button{{font:inherit;color:inherit;border:0;background:none;cursor:pointer;padding:0}}
a{{color:inherit}}a:hover{{color:inherit}}
{extra_css}</style>
</helmet>
<div class="wall" style="width: 1440px; height: 810px; position: relative; overflow: hidden; font-family: Inter, system-ui, sans-serif; color: {t["text"]}; font-size: 14px;">
{body}
</div>
</x-dc>
<script type="text/x-dc" data-dc-script data-props='{{"$preview":{{"width":1440,"height":810}}}}'>
class Component extends DCLogic {{
  renderVals() {{ return {{}}; }}
}}
</script>
</body>
</html>
'''


def vol_css(t):
    return (f'.vol{{-webkit-appearance:none;appearance:none;flex-grow:1;margin:0;height:6px;border-radius:3px;'
            f'background:linear-gradient(to right,{ACCENT} 0 62%,{t["track"]} 62% 100%);outline:none}}\n'
            f'.vol::-webkit-slider-thumb{{-webkit-appearance:none;width:20px;height:20px;border-radius:50%;background:{t["knob"]};border:0;box-shadow:0 1px 3px rgba(0,0,0,0.3)}}\n'
            f'.vol::-moz-range-thumb{{width:20px;height:20px;border-radius:50%;background:{t["knob"]};border:0;box-shadow:0 1px 3px rgba(0,0,0,0.3)}}\n')


# ---------- Острів ----------
# Типові закріплені програми – ті самі, що ставить оболонка (shell/…/Services/Defaults.cs).
APPS = [('Файли', 'folder'), ('Браузер', 'globe'), ('Термінал', 'terminal'), ('Крамниця', 'bag'), ('Налаштування', 'sliders')]
CELL = 'width: 48px; height: 48px; border-radius: 14px; display: flex; align-items: center; justify-content: center;'
SCREEN_W = 1440
# Ширина острова з його вмісту: рамка 2 + поля 14 + знак 48 + 2 розділювачі по 13 + програми + 96 + 56 + 126 + проміжки по 4.
ISL_W = 2 + 14 + 48 + 13 + 48 * len(APPS) + 13 + 96 + 56 + 126 + 4 * (len(APPS) + 5)
ISL_LEFT = (SCREEN_W - ISL_W) // 2
ISL_RIGHT = ISL_LEFT + ISL_W
SIDE_W = 408
SIDE_LEFT = ISL_RIGHT - SIDE_W
MENU_W = max(ISL_W, 640)
# Як в оболонці: поля 24 з боків і ще по 8 під смугу прокрутки, плитки від 100 px з кроком 4.
MENU_COLS = max(1, (MENU_W - 48 - 16 + 4) // 104)
MENU_TILE = (MENU_W - 48 - 16 - 4 * (MENU_COLS - 1)) // MENU_COLS


def island(t, active=None):
    def bg(key):
        return f' background: {t["active"]};' if active == key else ''
    def exp(key):
        return ' aria-expanded="true"' if active == key else ''
    div = f'<span style="width: 1px; height: 28px; background: {t["divider"]}; margin: 0 6px;"></span>'
    cells = [f'<button aria-label="Меню Кватирки"{exp("menu")} style="{CELL}{bg("menu")}">{logo(t)}</button>', div]
    for name, icon in APPS:
        cells.append(f'<button aria-label="{name}" style="{CELL}{bg(name)}">{I(icon)}</button>')
    cells += [div,
              f'<button aria-label="Швидкі налаштування: мережа і звук"{exp("quick")} style="width: 96px; height: 48px; border-radius: 14px; display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); place-items: center;{bg("quick")}">{I("wifi")}{I("volume")}</button>',
              f'<button aria-label="Мова: українська" style="width: 56px; height: 48px; border-radius: 14px; display: flex; align-items: center; justify-content: center; line-height: 1; font-weight: 500;">УКР</button>',
              f'<button aria-label="Годинник, календар і сповіщення"{exp("clock")} style="width: 126px; height: 48px; box-sizing: border-box; display: flex; flex-direction: column; align-items: flex-end; justify-content: center; gap: 4px; padding: 0 12px; border-radius: 14px; line-height: 1;{bg("clock")}"><span style="font-weight: 600;">03:01</span><span style="font-size: 12px; color: {t["sec"]};">пт, 6 листопада</span></button>']
    inner = '\n  '.join(cells)
    return (f'<div style="position: absolute; left: {ISL_LEFT}px; bottom: 16px; width: {ISL_W}px; height: 64px; box-sizing: border-box; display: flex; align-items: center; gap: 4px; padding: 0 7px; '
            f'background: {t["glass"]}; backdrop-filter: blur(28px) saturate(140%); -webkit-backdrop-filter: blur(28px) saturate(140%); border: 1px solid {t["border"]}; border-radius: 22px; box-shadow: {t["sh_isl"]};">\n  {inner}\n</div>')


def popover(t, label, left, width, inner, padded=True):
    pad = 'padding: 23px; gap: 16px;' if padded else ''
    return (f'<div role="dialog" aria-label="{label}" style="position: absolute; left: {left}px; bottom: 96px; width: {width}px; box-sizing: border-box; display: flex; flex-direction: column; {pad} '
            f'background: {t["pop"]}; backdrop-filter: blur(32px) saturate(140%); -webkit-backdrop-filter: blur(32px) saturate(140%); border: 1px solid {t["border"]}; border-radius: 22px; box-shadow: {t["sh_pop"]};">\n{inner}\n</div>\n')


def text_button(t, text):
    return f'<button style="height: 36px; padding: 0 14px; border-radius: 10px; background: {t["field"]}; line-height: 1; font-weight: 500; flex-shrink: 0;">{text}</button>'


# ---------- Меню ----------
# Меню показує всі встановлені програми за абеткою; тут – системні з набору іконок.
LAUNCHER = sorted([('Файли', 'folder'), ('Браузер', 'globe'), ('Термінал', 'terminal'), ('Крамниця', 'bag'), ('Параметри', 'sliders'),
                   ('Музика', 'music'), ('Фото', 'photo'), ('Відео', 'video'), ('Блокнот', 'note'), ('Калькулятор', 'calc')])


def menu(t):
    tiles = '\n      '.join(
        f'<button style="height: 92px; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 10px; border-radius: 14px; line-height: 1;">'
        f'<span style="width: 48px; height: 48px; display: flex; align-items: center; justify-content: center;">{I(ic)}</span>{n}</button>'
        for n, ic in LAUNCHER)
    inner = f'''  <div style="padding: 23px 23px 19px; display: flex; flex-direction: column; gap: 20px;">
    <label style="height: 52px; display: flex; align-items: center; gap: 12px; padding: 0 14px; box-sizing: border-box; background: {t["tile"]}; border-radius: 14px; color: {t["sec"]}; font-size: 15px;">{I("search")}<input type="search" placeholder="Пошук програм" aria-label="Пошук програм" style="all: unset; width: 100%; color: {t["text"]}; line-height: 1;"></label>
    <div style="display: grid; grid-template-columns: repeat({MENU_COLS}, {MENU_TILE}px); justify-content: center; gap: 4px;">
      {tiles}
    </div>
  </div>
  <div style="height: 1px; background: {t["line"]};"></div>
  <div style="padding: 11px 23px; display: flex; align-items: center; justify-content: space-between;">
    <button style="height: 48px; display: flex; align-items: center; gap: 12px; padding: 0 14px 0 8px; border-radius: 14px; line-height: 1;"><span style="width: 32px; height: 32px; border-radius: 50%; background: {ACCENT}; color: {ON_ACCENT}; font-weight: 600; display: flex; align-items: center; justify-content: center;">D</span><span style="font-weight: 500;">DEO</span></button>
    <button aria-label="Вимкнення" style="{CELL}">{I("power")}</button>
  </div>'''
    return popover(t, 'Меню Кватирки', ISL_LEFT + (ISL_W - MENU_W) // 2, MENU_W, inner, padded=False)


# ---------- Швидкі налаштування ----------
def quick(t):
    def tile(icon, text, on):
        st = f'background: {ACCENT}; color: {ON_ACCENT};' if on else f'background: {t["tile"]};'
        return (f'<button aria-pressed="{"true" if on else "false"}" style="height: 56px; display: flex; align-items: center; gap: 12px; padding: 0 16px; border-radius: 16px; {st} text-align: left; line-height: 1; font-weight: 600; white-space: nowrap;">{I(icon)}{text}</button>')
    inner = f'''  <div style="display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px;">
    {tile("wifi", "Мережа", True)}
    {tile("gamepad", "Ігровий режим", True)}
    {tile("moon", "Не турбувати", False)}
    {tile("contrast", "Темна тема", t is DARK)}
  </div>
  <label style="height: 44px; display: flex; align-items: center; gap: 12px; padding: 0 16px;">{I("volume")}<input class="vol" type="range" min="0" max="100" value="62" aria-label="Гучність"></label>'''
    return popover(t, 'Швидкі налаштування', SIDE_LEFT, SIDE_W, inner)


# ---------- Сповіщення й календар ----------
def clock_panel(t):
    import calendar
    first, days = calendar.monthrange(2026, 11)
    cells = ['<span></span>'] * first
    for d in range(1, days + 1):
        if d == 6:
            cells.append(f'<button aria-current="date" style="height: 40px; border-radius: 12px; background: {ACCENT}; color: {ON_ACCENT}; font-weight: 600; line-height: 1;">{d}</button>')
        else:
            cells.append(f'<button style="height: 40px; border-radius: 12px; line-height: 1;">{d}</button>')
    week = ''.join(f'<span style="height: 32px; display: flex; align-items: center; justify-content: center; font-size: 12px; color: {t["sec"]}; line-height: 1;">{w}</span>' for w in ['Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб', 'Нд'])
    grid = '\n      '.join(cells)
    inner = f'''  <div style="padding: 15px 23px; height: 36px; display: flex; align-items: center; justify-content: space-between;">
    <span style="font-weight: 600; line-height: 1;">Сповіщення</span>
    {text_button(t, "Відкрити")}
  </div>
  <div style="height: 1px; background: {t["line"]};"></div>
  <div style="padding: 15px 23px 23px; display: flex; flex-direction: column; gap: 8px;">
    <div style="height: 36px; display: flex; align-items: center; justify-content: space-between;">
      <span style="font-weight: 600; line-height: 1;">Листопад 2026</span>
      <div style="display: flex; gap: 4px;">
        <button aria-label="Попередній місяць" style="width: 36px; height: 36px; border-radius: 10px; display: flex; align-items: center; justify-content: center;">{I("left")}</button>
        <button aria-label="Наступний місяць" style="width: 36px; height: 36px; border-radius: 10px; display: flex; align-items: center; justify-content: center;">{I("right")}</button>
      </div>
    </div>
    <div style="display: grid; grid-template-columns: repeat(7, minmax(0, 1fr)); gap: 0 4px;">
      {week}
      {grid}
    </div>
  </div>'''
    return popover(t, 'Сповіщення й календар', SIDE_LEFT, SIDE_W, inner, padded=False)


# ---------- Вікно налаштувань Кватирки ----------
CATALOG = json.load(open(os.path.join(ROOT, '..', 'catalog', 'catalog.json'), encoding='utf-8'))
ICON_OF = {'Taskbar': 'taskbar', 'Folder': 'folder', 'Block': 'block', 'Shield': 'shield', 'Globe': 'globe', 'Gamepad': 'gamepad', 'Display': 'display'}
WIN_NAV = ([('home', 'Головне', 'home')] + [(c['id'], c['name'], ICON_OF[c['icon']]) for c in CATALOG['categories']]
           + [('apps', 'Програми', 'trash'), ('island', 'Острів', 'island')])
RECOMMENDED = [x for x in CATALOG['tweaks'] if (x['default'] is True) or (isinstance(x['default'], str) and x['default'] != x['choice']['windows'])]


def switch(t, on, label):
    knob = 22 if on else 2
    bg = ACCENT if on else t['track']
    return (f'<button role="switch" aria-checked="{"true" if on else "false"}" aria-label="{label}" style="width: 44px; height: 24px; border-radius: 12px; background: {bg}; position: relative; flex-shrink: 0;">'
            f'<span style="position: absolute; top: 2px; left: {knob}px; width: 20px; height: 20px; border-radius: 50%; background: #FFFFFF;"></span></button>')


def segmented(t, label, items):
    return (f'<div role="radiogroup" aria-label="{label}" style="height: 36px; display: flex; padding: 2px; box-sizing: border-box; background: {t["field"]}; border-radius: 10px; flex-shrink: 0;">'
            + ''.join(f'<button role="radio" aria-checked="{"true" if on else "false"}" style="height: 32px; padding: 0 14px; border-radius: 8px; line-height: 1; white-space: nowrap;{" background: " + t["active"] + "; font-weight: 600;" if on else " color: " + t["sec"] + ";"}">{n}</button>' for n, on in items)
            + '</div>')


def accent_button(text):
    return f'<button style="height: 36px; padding: 0 16px; border-radius: 10px; background: {ACCENT}; color: {ON_ACCENT}; line-height: 1; font-weight: 500; flex-shrink: 0;">{text}</button>'


def win_row(t, label, control, hint=None, last=False):
    line = '' if last else f' border-bottom: 1px solid {t["line"]};'
    h = f'<span style="font-size: 12px; line-height: 18px; color: {t["sec"]}; margin-top: 4px;">{hint}</span>' if hint else ''
    return (f'      <div style="min-height: 64px; display: flex; align-items: center; justify-content: space-between; gap: 24px; padding: 14px 8px; box-sizing: border-box; flex-shrink: 0;{line}">'
            f'<span style="display: flex; flex-direction: column; min-width: 0;"><span style="line-height: 20px;">{label}</span>{h}</span>{control}</div>')


def win_hero(t, heading, text, actions=''):
    acts = f'<div style="display: flex; gap: 8px; margin-top: 16px;">{actions}</div>' if actions else ''
    return (f'      <div style="padding: 16px 8px 20px; border-bottom: 1px solid {t["line"]}; flex-shrink: 0;">'
            f'<div style="font-size: 20px; line-height: 24px; font-weight: 600;">{heading}</div>'
            f'<div style="font-size: 12px; line-height: 18px; color: {t["sec"]}; margin-top: 8px; max-width: 560px;">{text}</div>{acts}</div>')


def win_section(t, section, applied):
    rows = []
    if section == 'home':
        done = len(RECOMMENDED) if applied else 0
        left = len(RECOMMENDED) - done
        first = (accent_button(f'Увімкнути рекомендоване ({left})') if left else
                 f'<button style="height: 36px; padding: 0 16px; border-radius: 10px; background: {t["field"]}; color: {t["dis"]}; line-height: 1; font-weight: 500;">Усе рекомендоване увімкнено</button>')
        rows.append(win_hero(t, 'Windows без зайвого',
                             f'Увімкнено {done} з {len(RECOMMENDED)} рекомендованих покращень. Кожне вимикається окремим перемикачем, і тоді Windows знову такий, як був.',
                             first + text_button(t, 'Повернути як у Windows')))
        rows.append(win_row(t, 'Охоронець налаштувань', switch(t, applied, 'Охоронець налаштувань'),
                            'Після кожного оновлення Windows Кватирка повертає ваш вибір, якщо оновлення його скинуло. Що ви змінили самі в Параметрах, вона не чіпає. Для цього Кватирка встановлюється в Program Files.'))
        rows.append(win_row(t, 'Тема Кватирки', segmented(t, 'Тема Кватирки', [('Світла', False), ('Темна', False), ('Як у Windows', True)])))
        sw = ('<div role="radiogroup" aria-label="Акцент" style="display: flex; gap: 8px;">'
              + ''.join(f'<button role="radio" aria-checked="{"true" if i == 0 else "false"}" style="width: 32px; height: 32px; border-radius: 50%; display: flex; align-items: center; justify-content: center;{" box-shadow: inset 0 0 0 2px " + c + ";" if i == 0 else ""}"><span style="width: 22px; height: 22px; border-radius: 50%; background: {c};"></span></button>'
                        for i, c in enumerate(['#5B9BFF', '#3FB68B', '#E5A23B', '#E26D5A', '#B07CF0', '#8A93A0']))
              + '</div>')
        rows.append(win_row(t, 'Акцент', sw))
        rows.append(win_row(t, 'Параметри Windows', text_button(t, 'Відкрити'), 'Усе інше – там, де звикли.', last=True))
    elif section == 'apps':
        rows.append(win_hero(t, 'Нав\'язані програми',
                             'Знайдено 5. Прибрані програми зникають для всіх користувачів і не з\'являються в нових профілях. Повернути будь-яку можна з Microsoft Store.',
                             accent_button('Прибрати рекомендовані (4)')))
        apps = [('Clipchamp', True), ('Copilot', True), ('Microsoft 365 Copilot', True), ('Новини', True), ('Погода', False)]
        for k, (name, rec) in enumerate(apps):
            rows.append(win_row(t, name, text_button(t, 'Прибрати'), 'Рекомендовано прибрати.' if rec else 'Комусь потрібна, тож лишається, доки не приберете самі.', last=k == len(apps) - 1))
    else:
        tw = [x for x in CATALOG['tweaks'] if x['category'] == section]
        for k, x in enumerate(tw):
            if 'choice' in x:
                cur = x['default'] if applied else x['choice']['windows']
                ctl = segmented(t, x['name'], [(o['name'], o['id'] == cur) for o in x['choice']['options']])
            else:
                ctl = switch(t, applied and x['default'] is True, x['name'])
            rows.append(win_row(t, x['name'], ctl, x['description'], last=k == len(tw) - 1))
    return '\n'.join(rows)


# Вікно Кватирки 960×640 – так само, як shell/Kvatyrka.Shell/Windows/SettingsWindow.cs.
def kvatyrka_window(t, section, applied):
    title = next(n for i, n, _ in WIN_NAV if i == section)
    cur = ' aria-current="page"'
    navs = '\n      '.join(
        f'<a href="#"{cur if i == section else ""} style="display: flex; align-items: center; gap: 12px; height: 40px; flex-shrink: 0; padding: 0 10px; border-radius: 10px; text-decoration: none; line-height: 1;{" background: " + t["sel"] + ";" if i == section else ""}">{I(ic)}{n}</a>'
        for i, n, ic in WIN_NAV)
    ctl = ''.join(f'<button aria-label="{lab}" style="width: 40px; height: 36px; border-radius: 10px; display: flex; align-items: center; justify-content: center; color: {t["sec"]};">{I(ic)}</button>'
                  for lab, ic in (('Згорнути', 'min'), ('Закрити', 'close')))
    return f'''<div style="position: absolute; left: 240px; top: 85px; width: 960px; height: 640px; box-sizing: border-box; display: flex; flex-direction: column; background: {t["win"]}; border: 1px solid {t["border"]}; border-radius: 16px; box-shadow: {t["sh_win"]}; overflow: hidden;">
  <div style="height: 56px; display: flex; align-items: center; flex-shrink: 0;">
    <div style="width: 216px; height: 56px; box-sizing: border-box; padding-left: 21px; display: flex; align-items: center; gap: 12px; flex-shrink: 0;">{logo(t)}<span style="font-weight: 600; line-height: 1;">Кватирка</span></div>
    <span style="flex-grow: 1; padding-left: 16px; font-weight: 600; line-height: 1;">{title}</span>
    <div style="display: flex; align-items: center; padding: 0 7px 0 12px; flex-shrink: 0;">{ctl}</div>
  </div>
  <div style="flex-grow: 1; display: flex; min-height: 0;">
    <nav aria-label="Розділи" style="width: 216px; flex-shrink: 0; padding: 0 11px; box-sizing: border-box; display: flex; flex-direction: column; gap: 2px;">
      {navs}
    </nav>
    <div style="flex-grow: 1; min-width: 0; margin: 0 7px 7px 0; padding: 8px; box-sizing: border-box; display: flex; flex-direction: column; background: {t["panel"]}; border-radius: 8px; overflow: hidden;">
{win_section(t, section, applied)}
    </div>
  </div>
</div>
'''


BOARDS = [
    ('Kvatyrka.dc.html', 'Кватирка · головне', 0, 0, lambda: page('Кватирка – головне', kvatyrka_window(DARK, 'home', False), DARK)),
    ('Tweaks.dc.html', 'Кватирка · Пуск і панель задач', 1520, 0, lambda: page('Кватирка – Пуск і панель задач', kvatyrka_window(DARK, 'start', True), DARK)),
    ('Apps.dc.html', 'Кватирка · програми', 3040, 0, lambda: page('Кватирка – програми', kvatyrka_window(DARK, 'apps', True), DARK)),
    ('LightKvatyrka.dc.html', 'Світла тема · головне', 0, 930, lambda: page('Кватирка – світла тема, головне', kvatyrka_window(LIGHT, 'home', True), LIGHT)),
    ('LightTweaks.dc.html', 'Світла тема · приватність', 1520, 930, lambda: page('Кватирка – світла тема, приватність', kvatyrka_window(LIGHT, 'privacy', True), LIGHT)),
    ('Main.dc.html', 'Острів (за бажанням)', 3040, 930, lambda: page('Кватирка – острів', island(DARK), DARK)),
    ('Island.dc.html', 'Острів · меню', 0, 1860, lambda: page('Кватирка – меню', menu(DARK) + island(DARK, 'menu'), DARK)),
    ('Quick.dc.html', 'Острів · швидкі налаштування', 1520, 1860, lambda: page('Кватирка – швидкі налаштування', quick(DARK) + island(DARK, 'quick'), DARK, vol_css(DARK))),
    ('Clock.dc.html', 'Острів · сповіщення й календар', 3040, 1860, lambda: page('Кватирка – сповіщення й календар', clock_panel(DARK) + island(DARK, 'clock'), DARK)),
]

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, 'canvas.json')
    idx = json.load(open(path, encoding='utf-8')) if os.path.exists(path) else {
        'v': 3, 'createdOnFiles': {'v': 1, 'at': '2026-11-06T01:05:00Z'}, 'title': 'Кватирка – концепції інтерфейсу', 'launch': {'view': 'canvas'},
        'pages': [], 'notes': {'heading': {'x': 0, 'y': -300, 'text': 'Кватирка – Windows без зайвого', 'kind': 'title1', 'maxW': 4480}}, 'designSystems': []}
    idx['boards'] = {f: {'x': x, 'y': y, 'w': 1440, 'h': 810, 'title': ti} for f, ti, x, y, _ in BOARDS}
    idx['order'] = [b[0] for b in BOARDS]
    json.dump(idx, open(path, 'w', encoding='utf-8'), ensure_ascii=False)
    for f, _, _, _, fn in BOARDS:
        open(os.path.join(OUT, f), 'w', encoding='utf-8').write(fn())
    print('ok', len(BOARDS), 'острів', ISL_W, 'меню', MENU_W, MENU_COLS, MENU_TILE)
