"""Draws Experiments/populations.svg from the CSV written by PopulationExperiment and prints a summary.

Standard library only:  python3 Experiments/plot_regrowth.py [regrowth.csv] [populations.svg]
"""
import csv
import os
import sys
from collections import defaultdict
from statistics import mean, median

HERE = os.path.dirname(os.path.abspath(__file__))
CSV_PATH = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "regrowth.csv")
SVG_PATH = sys.argv[2] if len(sys.argv) > 2 else os.path.join(HERE, "populations.svg")

PANELS = [("sheep", "Moutons"), ("wolves", "Loups"), ("edible_grass", "Touffes comestibles")]
STYLES = ["#15786b", "#c0573e", "#5b5fc7", "#a07a12"]  # one colour per regrowth time
W, H = 1280, 420
PANEL_W, PANEL_H, LEFT, TOP = 330, 270, 70, 90


def load(path):
    runs = defaultdict(list)  # (regrowth, seed) -> rows
    with open(path, newline="") as f:
        for row in csv.DictReader(f):
            row = {k: float(v) for k, v in row.items()}
            runs[(row["regrowth_s"], int(row["seed"]))].append(row)
    return runs


def nice_max(value):
    for step in (5, 10, 20, 25, 50, 100):
        if value <= step * 6:
            return max(step, step * int((value + step - 1) // step))
    return value


def polyline(points, colour, width, opacity, dash=""):
    pts = " ".join(f"{x:.1f},{y:.1f}" for x, y in points)
    dash_attr = f' stroke-dasharray="{dash}"' if dash else ""
    return (f'<polyline points="{pts}" fill="none" stroke="{colour}" stroke-width="{width}" '
            f'stroke-opacity="{opacity}" stroke-linejoin="round"{dash_attr}/>')


def main():
    runs = load(CSV_PATH)
    regrowths = sorted({r for r, _ in runs})
    t_max = max(row["time_s"] for rows in runs.values() for row in rows)
    out = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" '
           f'font-family="Helvetica, Arial, sans-serif">',
           '<rect width="100%" height="100%" fill="white"/>',
           '<text x="30" y="38" font-size="22" font-weight="bold" fill="#1d2b2a">'
           'Repousse de l’herbe et évolution des populations</text>',
           f'<text x="30" y="{H - 14}" font-size="13" fill="#4a5a58">Une ligne fine par graine, '
           'ligne épaisse = moyenne des graines. Temps simulé en secondes.</text>']

    for i, (column, title) in enumerate(PANELS):
        x0 = LEFT + i * (PANEL_W + 70)
        y_max = nice_max(max(row[column] for rows in runs.values() for row in rows))
        sx = lambda t: x0 + t / t_max * PANEL_W
        sy = lambda v: TOP + PANEL_H - v / y_max * PANEL_H
        out.append(f'<text x="{x0}" y="{TOP - 16}" font-size="15" font-weight="bold" fill="#1d2b2a">{title}</text>')
        for k in range(6):  # horizontal grid and y labels
            v = y_max * k / 5
            out.append(f'<line x1="{x0}" x2="{x0 + PANEL_W}" y1="{sy(v):.1f}" y2="{sy(v):.1f}" stroke="#e3e8e7"/>')
            out.append(f'<text x="{x0 - 8}" y="{sy(v) + 4:.1f}" font-size="11" fill="#6b7a78" text-anchor="end">{v:g}</text>')
        for k in range(5):  # x labels
            t = t_max * k / 4
            out.append(f'<text x="{sx(t):.1f}" y="{TOP + PANEL_H + 18}" font-size="11" fill="#6b7a78" '
                       f'text-anchor="middle">{t:g}</text>')
        for j, regrowth in enumerate(regrowths):
            colour, dash = STYLES[j % len(STYLES)], ("7 5" if j % 2 else "")
            series = [rows for (r, _), rows in runs.items() if r == regrowth]
            for rows in series:
                out.append(polyline([(sx(r["time_s"]), sy(r[column])) for r in rows], colour, 1.2, 0.28))
            times = sorted({r["time_s"] for rows in series for r in rows})
            means = [(sx(t), sy(mean(r[column] for rows in series for r in rows if r["time_s"] == t))) for t in times]
            out.append(polyline(means, colour, 3, 1, dash))

    for j, regrowth in enumerate(regrowths):  # legend
        colour, dash = STYLES[j % len(STYLES)], ("7 5" if j % 2 else "")
        lx = W - 30 - len(regrowths) * 150 + j * 150
        out.append(polyline([(lx, 34), (lx + 34, 34)], colour, 3, 1, dash))
        out.append(f'<text x="{lx + 42}" y="39" font-size="13" fill="#1d2b2a">repousse {regrowth:g} s</text>')
    out.append("</svg>")
    with open(SVG_PATH, "w", encoding="utf-8") as f:
        f.write("\n".join(out))

    # Same rows as the table of the README. A population counts as gone with 0 or 1 animal left.
    print(f"{os.path.relpath(SVG_PATH)} written. Summary at t = {t_max:g} s:")
    table = defaultdict(list)
    for regrowth in regrowths:
        series = [rows for (r, _), rows in sorted(runs.items()) if r == regrowth]
        finals = [rows[-1] for rows in series]
        peaks = [max(rows, key=lambda row: row["wolves"]) for rows in series]
        n = len(series)
        table["Grazings (mean total)"].append(f"{mean(f['grazings'] for f in finals):.0f}")
        table["Sheep births (mean total)"].append(f"{mean(f['sheep_births'] for f in finals):.0f}")
        table["Sheep caught (mean total)"].append(f"{mean(f['kills'] for f in finals):.0f}")
        table["Wolf peak (mean of maxima)"].append(f"{mean(p['wolves'] for p in peaks):.1f}")
        table["Wolf peak time (median)"].append(f"{median(p['time_s'] for p in peaks):.0f} s")
        table["Sheep alive (mean)"].append(f"{mean(f['sheep'] for f in finals):.1f}")
        table["Runs where the wolves are gone"].append(f"{sum(f['wolves'] <= 1 for f in finals)}/{n}")
        table["Runs where the sheep are gone"].append(f"{sum(f['sheep'] <= 1 for f in finals)}/{n}")
        table["Runs where both species remain"].append(f"{sum(f['wolves'] >= 2 and f['sheep'] >= 2 for f in finals)}/{n}")
    print(f"{'':32}" + "".join(f"{f'regrowth {r:g} s':>16}" for r in regrowths))
    for name, values in table.items():
        print(f"{name:32}" + "".join(f"{v:>16}" for v in values))


if __name__ == "__main__":
    main()
