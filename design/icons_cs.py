# Перетворює іконки дизайну на геометрію WPF для оболонки (один набір на все).
import os, re, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mockups

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'shell', 'Kvatyrka.Shell', 'Ui', 'Icons.g.cs')
ATTR = re.compile(r'([\w-]+)="([^"]*)"')


def n(v):
    return f'{float(v):g}'


def to_path(tag, a):
    if tag == 'path':
        return a['d']
    if tag == 'circle':
        cx, cy, r = float(a['cx']), float(a['cy']), float(a['r'])
        return f'M{n(cx - r)},{n(cy)} A{n(r)},{n(r)} 0 1 0 {n(cx + r)},{n(cy)} A{n(r)},{n(r)} 0 1 0 {n(cx - r)},{n(cy)} Z'
    if tag == 'rect':
        x, y, w, h = (float(a[k]) for k in ('x', 'y', 'width', 'height'))
        r = float(a.get('rx', 0))
        if not r:
            return f'M{n(x)},{n(y)} H{n(x + w)} V{n(y + h)} H{n(x)} Z'
        return (f'M{n(x + r)},{n(y)} H{n(x + w - r)} A{n(r)},{n(r)} 0 0 1 {n(x + w)},{n(y + r)} V{n(y + h - r)} '
                f'A{n(r)},{n(r)} 0 0 1 {n(x + w - r)},{n(y + h)} H{n(x + r)} A{n(r)},{n(r)} 0 0 1 {n(x)},{n(y + h - r)} '
                f'V{n(y + r)} A{n(r)},{n(r)} 0 0 1 {n(x + r)},{n(y)} Z')
    raise ValueError(tag)


def main():
    lines = []
    for name, svg in mockups.P.items():
        stroke, fill = [], []
        for tag, attrs in re.findall(r'<(path|circle|rect)\s([^>]*)>', svg):
            a = dict(ATTR.findall(attrs))
            (fill if a.get('fill') == 'currentColor' else stroke).append(to_path(tag, a))
        key = ''.join(p.capitalize() for p in name.split('-'))
        lines.append(f'        [IconKind.{key}] = new("{" ".join(stroke)}", "{" ".join(fill)}"),')
    kinds = ', '.join(''.join(p.capitalize() for p in k.split('-')) for k in mockups.P)
    code = f'''// Згенеровано з design/mockups.py скриптом design/icons_cs.py. Не редагувати вручну.
namespace Kvatyrka.Shell.Ui;

public enum IconKind {{ None, {kinds} }}

public static partial class Icons
{{
    private static readonly Dictionary<IconKind, (string Stroke, string Fill)> Data = new()
    {{
{chr(10).join(lines)}
    }};
}}
'''
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, 'w', encoding='utf-8', newline='\n') as f:
        f.write(code)
    print(OUT, len(lines))


if __name__ == '__main__':
    main()
