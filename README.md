# FOOD FIGHT

[![build](https://github.com/amccool/foodfight/actions/workflows/build.yml/badge.svg)](https://github.com/amccool/foodfight/actions/workflows/build.yml)

A single-file pygame homage to the 1983 Atari arcade game *Food Fight*:
eat the ice cream cone before it melts, while chefs pour out of holes in
the floor, chase you, and pelt you with food. Grab ammo from the food
piles, splat the chefs, don't fall in the holes. Close calls earn an
instant replay.

## Controls

| Keys | Action |
|------|--------|
| `W` `A` `S` `D` | Move |
| `I` `J` `K` `L` | Throw food — hold for constant fire (I=up, J=left, K=down, L=right; combine for diagonals) |
| `P` | Pause |
| `Esc` | Quit to title / exit |

## Download

Grab `FoodFight.exe` from the latest
[release](https://github.com/amccool/foodfight/releases) — standalone,
no install required.

## Build from source

```
pip install pygame pyinstaller
python main.py                # run directly
python main.py --selftest     # headless logic smoke test
pyinstaller --onefile --windowed --name FoodFight main.py   # build dist/FoodFight.exe
```

## Releasing

CI (GitHub Actions, `.github/workflows/build.yml`) tests and builds the
exe on every push. Pushing a `v*` tag publishes a GitHub Release with the
exe attached and release notes generated from the commit log:

```
git tag v1.0.1
git push origin v1.0.1
```
