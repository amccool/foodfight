"""FOOD FIGHT -- an homage to the 1983 Atari arcade classic.

Eat the ice cream cone before it melts.  Chefs pour out of holes in the
kitchen floor and chase you, hurling food.  Grab ammo from the food piles
and throw it back.  Don't fall in the holes.

Controls (the one change from the original):
    W A S D : move
    I J K L : throw food (hold for constant fire)  I=up J=left K=down L=right
    P       : pause          ESC : quit to title / exit
"""

import math
import random
import struct
import sys

import pygame

# ----------------------------------------------------------------- constants
SW, SH = 960, 720          # window size
WALL = 26                  # border wall thickness
FPS = 60

PLAYER_SPEED = 200.0       # px / sec
PLAYER_R = 14
FIRE_RATE = 0.11           # seconds between throws while a key is held
PROJ_SPEED = 540.0
CHEF_PROJ_SPEED = 330.0
MAX_CARRY = 30
PILE_CAPACITY = 18
START_LIVES = 3
EXTRA_LIFE_EVERY = 15000

# food types: name, colour, points for hitting a chef with it
FOODS = [
    ("PEAS", (110, 205, 60), 50),
    ("TOMATOES", (225, 60, 40), 75),
    ("BANANAS", (245, 215, 70), 100),
    ("PIES", (246, 240, 214), 150),
    ("WATERMELON", (236, 90, 125), 200),
]

CHEF_ACCENTS = [(70, 110, 230), (180, 70, 200), (60, 170, 90),
                (235, 140, 40), (50, 180, 190), (210, 60, 120)]

FLOOR = (24, 26, 36)
FLOOR_TILE = (30, 33, 46)
WALL_C = (96, 60, 130)
WALL_EDGE = (150, 105, 200)
WHITE = (240, 240, 240)
YELLOW = (255, 230, 90)
RED = (240, 70, 60)

TITLE, LEVEL_START, PLAYING, DEATH, LEVEL_CLEAR, REPLAY, GAME_OVER = range(7)


def clamp(v, lo, hi):
    return lo if v < lo else hi if v > hi else v


def dist(ax, ay, bx, by):
    return math.hypot(ax - bx, ay - by)


# ------------------------------------------------------------------- sounds
class Sounds:
    """Tiny square-wave synth so the game has arcade blips without assets."""

    RATE = 22050

    def __init__(self):
        self.ok = False
        self.bank = {}
        try:
            pygame.mixer.pre_init(self.RATE, -16, 1, 512)
            pygame.mixer.init()
            self.ok = True
        except pygame.error:
            return
        self.bank["throw"] = self._tone([(880, 0.03), (660, 0.03)], 0.18)
        self.bank["splat"] = self._noise(0.12, 0.25)
        self.bank["pickup"] = self._tone([(520, 0.04), (780, 0.05)], 0.18)
        self.bank["eat"] = self._tone(
            [(523, 0.09), (659, 0.09), (784, 0.09), (1047, 0.2)], 0.25)
        self.bank["death"] = self._tone(
            [(400, 0.1), (300, 0.1), (200, 0.12), (120, 0.25)], 0.25)
        self.bank["chefdie"] = self._tone([(200, 0.05), (300, 0.05),
                                           (150, 0.08)], 0.22)
        self.bank["start"] = self._tone(
            [(392, 0.1), (523, 0.1), (659, 0.1), (784, 0.18)], 0.25)
        self.bank["melt"] = self._tone(
            [(700, 0.12), (500, 0.12), (350, 0.2)], 0.22)
        self.bank["bonus"] = self._tone([(990, 0.03)], 0.15)
        self.bank["fall"] = self._tone(
            [(600, 0.06), (450, 0.06), (330, 0.06), (240, 0.06),
             (170, 0.1)], 0.22)

    def _tone(self, seq, vol):
        data = bytearray()
        amp = int(32767 * vol)
        for freq, dur in seq:
            n = int(self.RATE * dur)
            period = self.RATE / max(freq, 1)
            for i in range(n):
                v = amp if (i % period) < period / 2 else -amp
                data += struct.pack("<h", v)
        return pygame.mixer.Sound(buffer=bytes(data))

    def _noise(self, dur, vol):
        data = bytearray()
        amp = int(32767 * vol)
        rng = random.Random(7)
        for i in range(int(self.RATE * dur)):
            fade = 1.0 - i / (self.RATE * dur)
            data += struct.pack("<h", int(rng.uniform(-amp, amp) * fade))
        return pygame.mixer.Sound(buffer=bytes(data))

    def play(self, name):
        if self.ok and name in self.bank:
            self.bank[name].play()


# ------------------------------------------------------------------ entities
class Projectile:
    __slots__ = ("x", "y", "vx", "vy", "color", "from_player", "points", "t")

    def __init__(self, x, y, vx, vy, color, from_player, points=0):
        self.x, self.y, self.vx, self.vy = x, y, vx, vy
        self.color = color
        self.from_player = from_player
        self.points = points
        self.t = 0.0


class Pile:
    def __init__(self, x, y, food_idx):
        self.x, self.y = x, y
        self.food_idx = food_idx
        self.amount = PILE_CAPACITY


class Hole:
    def __init__(self, x, y):
        self.x, self.y = x, y
        self.r = 30


class Chef:
    RISE_T = 0.7
    SPLAT_T = 0.6

    def __init__(self, accent, speed):
        self.accent = accent
        self.speed = speed
        self.x = self.y = 0.0
        self.state = "waiting"      # waiting -> rising -> active -> dead
        self.timer = random.uniform(0.5, 2.0)
        self.throw_cd = random.uniform(1.5, 3.0)
        self.phase = random.uniform(0, math.tau)

    def spawn_from(self, hole):
        self.x, self.y = hole.x, hole.y
        self.state = "rising"
        self.timer = self.RISE_T


# ---------------------------------------------------------------------- game
class Game:
    def __init__(self, headless=False):
        self.headless = headless
        flags = 0
        self.screen = pygame.display.set_mode((SW, SH), flags)
        pygame.display.set_caption("FOOD FIGHT")
        self.clock = pygame.time.Clock()
        self.snd = Sounds()
        self.font_big = self._font(72, bold=True)
        self.font_mid = self._font(36, bold=True)
        self.font_sml = self._font(24, bold=True)
        self.high_score = 0
        self.state = TITLE
        self.state_t = 0.0
        self.blink = 0.0
        self.reset_game()

    @staticmethod
    def _font(size, bold=False):
        try:
            return pygame.font.SysFont("consolas,couriernew,arial", size,
                                       bold=bold)
        except Exception:
            return pygame.font.Font(None, size)

    # ------------------------------------------------------------ game setup
    def reset_game(self):
        self.score = 0
        self.lives = START_LIVES
        self.level = 1
        self.next_extra = EXTRA_LIFE_EVERY

    def new_level(self):
        rng = random.Random()
        self.melt_time = max(18.0, 40.0 - 2.0 * (self.level - 1))
        self.melt_frac = 1.0
        n_chefs = min(3 + (self.level - 1) // 2, 6)
        chef_speed = min(115.0 + 12.0 * (self.level - 1), 195.0)

        self.cone_x, self.cone_y = WALL + 70, SH / 2
        self.player_start = (SW - WALL - 70, SH / 2)

        # holes: keep them away from the cone, the player start and each other
        self.holes = []
        tries = 0
        want = min(4 + self.level // 3, 6)
        while len(self.holes) < want and tries < 400:
            tries += 1
            x = rng.uniform(WALL + 120, SW - WALL - 120)
            y = rng.uniform(WALL + 90, SH - WALL - 90)
            if dist(x, y, self.cone_x, self.cone_y) < 150:
                continue
            if dist(x, y, *self.player_start) < 150:
                continue
            if any(dist(x, y, h.x, h.y) < 140 for h in self.holes):
                continue
            self.holes.append(Hole(x, y))

        # food piles
        self.piles = []
        tries = 0
        while len(self.piles) < 8 and tries < 500:
            tries += 1
            x = rng.uniform(WALL + 60, SW - WALL - 60)
            y = rng.uniform(WALL + 60, SH - WALL - 60)
            if dist(x, y, self.cone_x, self.cone_y) < 110:
                continue
            if any(dist(x, y, h.x, h.y) < 95 for h in self.holes):
                continue
            if any(dist(x, y, p.x, p.y) < 95 for p in self.piles):
                continue
            self.piles.append(Pile(x, y, rng.randrange(len(FOODS))))

        self.chefs = [Chef(CHEF_ACCENTS[i % len(CHEF_ACCENTS)],
                           chef_speed * rng.uniform(0.85, 1.1))
                      for i in range(n_chefs)]
        self.projectiles = []
        self.splats = []           # (x, y, color, ttl)
        self.replay_frames = []
        self.close_call = False
        self.reset_player()

    def reset_player(self):
        self.px, self.py = self.player_start
        self.pvx = self.pvy = 0.0
        self.face = (-1.0, 0.0)
        self.ammo = 0
        self.ammo_food = 1
        self.fire_cd = 0.0
        self.pickup_cd = 0.0
        self.projectiles = []
        for c in self.chefs:
            c.state = "waiting"
            c.timer = random.uniform(0.4, 1.8)

    # --------------------------------------------------------------- helpers
    def add_score(self, pts):
        self.score += pts
        if self.score >= self.next_extra:
            self.next_extra += EXTRA_LIFE_EVERY
            self.lives += 1
            self.snd.play("eat")
        self.high_score = max(self.high_score, self.score)

    def set_state(self, st):
        self.state = st
        self.state_t = 0.0

    def kill_player(self, kind):
        self.death_kind = kind
        self.snd.play("fall" if kind == "hole" else
                      "melt" if kind == "melt" else "death")
        self.set_state(DEATH)

    # ---------------------------------------------------------------- update
    def update(self, dt, keys, events):
        self.state_t += dt
        self.blink += dt
        for ev in events:
            if ev.type == pygame.KEYDOWN:
                if self.state == TITLE and ev.key in (pygame.K_SPACE,
                                                      pygame.K_RETURN):
                    self.reset_game()
                    self.new_level()
                    self.snd.play("start")
                    self.set_state(LEVEL_START)
                elif self.state == GAME_OVER and ev.key in (pygame.K_SPACE,
                                                            pygame.K_RETURN):
                    self.set_state(TITLE)
                elif self.state == REPLAY:
                    self.finish_replay()
                elif ev.key == pygame.K_ESCAPE:
                    if self.state == TITLE:
                        return False
                    self.set_state(TITLE)

        if self.state == LEVEL_START and self.state_t > 1.6:
            self.set_state(PLAYING)
        elif self.state == PLAYING:
            self.update_playing(dt, keys)
        elif self.state == DEATH and self.state_t > 1.4:
            self.lives -= 1
            if self.lives <= 0:
                self.set_state(GAME_OVER)
            else:
                if self.death_kind == "melt":
                    self.melt_frac = 1.0
                self.reset_player()
                self.set_state(LEVEL_START)
        elif self.state == LEVEL_CLEAR:
            if self.state_t > 2.4:
                if self.close_call and self.replay_frames:
                    self.replay_i = 0
                    self.set_state(REPLAY)
                else:
                    self.finish_replay()
        elif self.state == REPLAY:
            self.replay_i += 2          # play back slightly fast
            if self.replay_i >= len(self.replay_frames):
                self.finish_replay()
        return True

    def finish_replay(self):
        self.level += 1
        self.new_level()
        self.snd.play("start")
        self.set_state(LEVEL_START)

    def update_playing(self, dt, keys):
        # ---- player movement (WASD)
        mx = (keys[pygame.K_d] - keys[pygame.K_a])
        my = (keys[pygame.K_s] - keys[pygame.K_w])
        if mx or my:
            n = math.hypot(mx, my)
            self.pvx, self.pvy = mx / n * PLAYER_SPEED, my / n * PLAYER_SPEED
            self.face = (mx / n, my / n)
        else:
            self.pvx = self.pvy = 0.0
        self.px = clamp(self.px + self.pvx * dt, WALL + PLAYER_R,
                        SW - WALL - PLAYER_R)
        self.py = clamp(self.py + self.pvy * dt, WALL + PLAYER_R,
                        SH - WALL - PLAYER_R)

        # ---- firing (IJKL, hold for constant fire)
        self.fire_cd -= dt
        fx = (keys[pygame.K_l] - keys[pygame.K_j])
        fy = (keys[pygame.K_k] - keys[pygame.K_i])
        if (fx or fy) and self.fire_cd <= 0 and self.ammo > 0:
            n = math.hypot(fx, fy)
            food = FOODS[self.ammo_food]
            self.projectiles.append(Projectile(
                self.px, self.py, fx / n * PROJ_SPEED, fy / n * PROJ_SPEED,
                food[1], True, food[2]))
            self.ammo -= 1
            self.fire_cd = FIRE_RATE
            self.snd.play("throw")

        # ---- food pickup
        self.pickup_cd -= dt
        for p in self.piles:
            if p.amount > 0 and dist(self.px, self.py, p.x, p.y) < 34:
                if self.ammo < MAX_CARRY and self.pickup_cd <= 0:
                    grab = min(3, p.amount, MAX_CARRY - self.ammo)
                    p.amount -= grab
                    self.ammo += grab
                    self.ammo_food = p.food_idx
                    self.pickup_cd = 0.12
                    self.snd.play("pickup")

        # ---- melting cone
        self.melt_frac -= dt / self.melt_time
        if self.melt_frac < 0.15:
            self.close_call = True
        if self.melt_frac <= 0:
            self.melt_frac = 0
            self.kill_player("melt")
            return

        # ---- eat the cone -> level clear
        if dist(self.px, self.py, self.cone_x, self.cone_y) < 42:
            bonus = int(self.melt_frac * self.melt_time) * 100
            self.clear_bonus = 500 + bonus
            self.add_score(self.clear_bonus)
            self.snd.play("eat")
            self.set_state(LEVEL_CLEAR)
            return

        # ---- holes are deadly
        for h in self.holes:
            if dist(self.px, self.py, h.x, h.y) < h.r - 8:
                self.kill_player("hole")
                return

        # ---- chefs
        for c in self.chefs:
            if c.state == "waiting":
                c.timer -= dt
                if c.timer <= 0 and self.holes:
                    c.spawn_from(random.choice(self.holes))
            elif c.state == "rising":
                c.timer -= dt
                if c.timer <= 0:
                    c.state = "active"
            elif c.state == "dead":
                c.timer -= dt
                if c.timer <= 0:
                    c.state = "waiting"
                    c.timer = random.uniform(1.0, 2.5)
            elif c.state == "active":
                ang = math.atan2(self.py - c.y, self.px - c.x)
                ang += math.sin(self.state_t * 2.2 + c.phase) * 0.45
                c.x += math.cos(ang) * c.speed * dt
                c.y += math.sin(ang) * c.speed * dt
                c.x = clamp(c.x, WALL + 14, SW - WALL - 14)
                c.y = clamp(c.y, WALL + 14, SH - WALL - 14)
                d = dist(c.x, c.y, self.px, self.py)
                if d < 40:
                    self.close_call = True
                if d < PLAYER_R + 13:
                    self.kill_player("chef")
                    return
                c.throw_cd -= dt
                if c.throw_cd <= 0 and 110 < d < 520:
                    lead = random.uniform(0.0, 0.5)
                    tx = self.px + self.pvx * lead
                    ty = self.py + self.pvy * lead
                    a = math.atan2(ty - c.y, tx - c.x)
                    a += random.uniform(-0.12, 0.12)
                    self.projectiles.append(Projectile(
                        c.x, c.y, math.cos(a) * CHEF_PROJ_SPEED,
                        math.sin(a) * CHEF_PROJ_SPEED, (225, 60, 40), False))
                    c.throw_cd = random.uniform(
                        1.2, 2.8) * max(0.55, 1.0 - self.level * 0.04)
                    self.snd.play("throw")

        # ---- projectiles
        alive = []
        for pr in self.projectiles:
            pr.x += pr.vx * dt
            pr.y += pr.vy * dt
            pr.t += dt
            hit = False
            if not (WALL < pr.x < SW - WALL and WALL < pr.y < SH - WALL):
                self.splats.append([clamp(pr.x, WALL, SW - WALL),
                                    clamp(pr.y, WALL, SH - WALL),
                                    pr.color, 2.5])
                hit = True
            elif pr.from_player:
                for c in self.chefs:
                    if c.state == "active" and dist(pr.x, pr.y, c.x,
                                                    c.y) < 24:
                        c.state = "dead"
                        c.timer = Chef.SPLAT_T
                        self.add_score(pr.points * max(1, self.level // 2))
                        self.splats.append([c.x, c.y, pr.color, 2.5])
                        self.snd.play("chefdie")
                        hit = True
                        break
            else:
                d = dist(pr.x, pr.y, self.px, self.py)
                if d < 30:
                    self.close_call = True
                if d < PLAYER_R + 6:
                    self.splats.append([self.px, self.py, pr.color, 2.5])
                    self.snd.play("splat")
                    self.kill_player("hit")
                    return
            if not hit:
                alive.append(pr)
            else:
                self.snd.play("splat")
        self.projectiles = alive

        for s in self.splats:
            s[3] -= dt
        self.splats = [s for s in self.splats if s[3] > 0]

        # ---- record for instant replay (keep the last ~25 seconds)
        self.replay_frames.append((
            (self.px, self.py),
            tuple((c.x, c.y, c.state) for c in self.chefs),
            tuple((pr.x, pr.y, pr.color) for pr in self.projectiles),
            self.melt_frac,
        ))
        if len(self.replay_frames) > FPS * 25:
            self.replay_frames.pop(0)

    # ------------------------------------------------------------------ draw
    def draw(self):
        scr = self.screen
        if self.state == TITLE:
            self.draw_title()
        elif self.state == GAME_OVER:
            self.draw_world()
            self.dim()
            self.center_text("GAME OVER", self.font_big, RED, -40)
            self.center_text(f"SCORE  {self.score}", self.font_mid, WHITE, 30)
            if self.blink % 1.0 < 0.6:
                self.center_text("PRESS SPACE", self.font_sml, YELLOW, 90)
        elif self.state == REPLAY:
            f = self.replay_frames[min(self.replay_i,
                                       len(self.replay_frames) - 1)]
            self.draw_world(snapshot=f)
            if self.blink % 0.5 < 0.3:
                self.center_text("INSTANT REPLAY", self.font_big, YELLOW, -SH
                                 // 2 + 90)
        else:
            self.draw_world()
            if self.state == LEVEL_START:
                self.center_text(f"LEVEL {self.level}", self.font_big,
                                 YELLOW, -20)
                self.center_text("EAT THE CONE BEFORE IT MELTS!",
                                 self.font_sml, WHITE, 50)
            elif self.state == DEATH:
                msg = {"melt": "THE CONE MELTED!",
                       "hole": "DOWN THE HOLE!",
                       "chef": "CAUGHT BY THE CHEF!",
                       "hit": "SPLATTED!"}[self.death_kind]
                self.center_text(msg, self.font_mid, RED, -10)
            elif self.state == LEVEL_CLEAR:
                self.center_text("DELICIOUS!", self.font_big, YELLOW, -30)
                self.center_text(f"BONUS  {self.clear_bonus}", self.font_mid,
                                 WHITE, 40)
        pygame.display.flip()

    def dim(self):
        ov = pygame.Surface((SW, SH), pygame.SRCALPHA)
        ov.fill((0, 0, 0, 150))
        self.screen.blit(ov, (0, 0))

    def center_text(self, txt, font, color, dy=0):
        img = font.render(txt, True, color)
        sh = pygame.Surface(img.get_size(), pygame.SRCALPHA)
        self.screen.blit(font.render(txt, True, (0, 0, 0)),
                         img.get_rect(center=(SW // 2 + 3, SH // 2 + dy + 3)))
        self.screen.blit(img, img.get_rect(center=(SW // 2, SH // 2 + dy)))

    def draw_title(self):
        scr = self.screen
        scr.fill(FLOOR)
        for i in range(0, SW, 48):
            pygame.draw.line(scr, FLOOR_TILE, (i, 0), (i, SH))
        for i in range(0, SH, 48):
            pygame.draw.line(scr, FLOOR_TILE, (0, i), (SW, i))
        t = self.blink
        # bouncing food behind the title
        rng = random.Random(3)
        for k in range(14):
            fx = (rng.random() * SW + t * (40 + 30 * rng.random())) % SW
            fy = (rng.random() * SH + t * (25 + 40 * rng.random())) % SH
            food = FOODS[k % len(FOODS)]
            pygame.draw.circle(scr, food[1], (int(fx), int(fy)), 9)
        self.center_text("FOOD FIGHT", self.font_big, YELLOW, -190)
        self.center_text("a fast-food arcade homage", self.font_sml,
                         (180, 180, 200), -130)
        lines = [
            ("W A S D", "move"),
            ("I J K L", "throw food (hold = rapid fire)"),
            ("", ""),
            ("EAT THE ICE CREAM CONE BEFORE IT MELTS", ""),
            ("GRAB FOOD PILES FOR AMMO -- SPLAT THE CHEFS", ""),
            ("DON'T FALL IN THE HOLES", ""),
        ]
        y = -60
        for key, what in lines:
            if key and what:
                self.center_text(f"{key}  :  {what}", self.font_sml, WHITE, y)
            elif key:
                self.center_text(key, self.font_sml, (255, 170, 90), y)
            y += 34
        self.center_text(f"HIGH SCORE  {self.high_score}", self.font_sml,
                         (160, 220, 160), y + 20)
        if self.blink % 1.0 < 0.6:
            self.center_text("PRESS SPACE TO START", self.font_mid, YELLOW,
                             y + 80)

    def draw_world(self, snapshot=None):
        scr = self.screen
        scr.fill(FLOOR)
        for i in range(WALL, SW - WALL, 48):
            pygame.draw.line(scr, FLOOR_TILE, (i, WALL), (i, SH - WALL))
        for i in range(WALL, SH - WALL, 48):
            pygame.draw.line(scr, FLOOR_TILE, (WALL, i), (SW - WALL, i))
        # walls
        pygame.draw.rect(scr, WALL_C, (0, 0, SW, WALL))
        pygame.draw.rect(scr, WALL_C, (0, SH - WALL, SW, WALL))
        pygame.draw.rect(scr, WALL_C, (0, 0, WALL, SH))
        pygame.draw.rect(scr, WALL_C, (SW - WALL, 0, WALL, SH))
        pygame.draw.rect(scr, WALL_EDGE, (WALL - 3, WALL - 3,
                                          SW - 2 * WALL + 6,
                                          SH - 2 * WALL + 6), 3)

        for h in self.holes:
            pygame.draw.ellipse(scr, (8, 8, 12),
                                (h.x - h.r, h.y - h.r * 0.7, h.r * 2,
                                 h.r * 1.4))
            pygame.draw.ellipse(scr, (60, 60, 75),
                                (h.x - h.r, h.y - h.r * 0.7, h.r * 2,
                                 h.r * 1.4), 3)

        for s in self.splats:
            a = clamp(s[3] / 2.5, 0, 1)
            col = tuple(int(c * a + FLOOR[i] * (1 - a))
                        for i, c in enumerate(s[2]))
            pygame.draw.ellipse(scr, col, (s[0] - 14, s[1] - 9, 28, 18))

        for p in self.piles:
            if p.amount <= 0:
                continue
            food = FOODS[p.food_idx]
            size = 8 + 14 * p.amount / PILE_CAPACITY
            for ox, oy, sc in ((-8, 2, 0.8), (8, 2, 0.8), (0, -4, 1.0),
                               (0, 6, 0.9)):
                pygame.draw.circle(scr, food[1],
                                   (int(p.x + ox * size / 14),
                                    int(p.y + oy * size / 14)),
                                   int(size * sc * 0.55))
            dark = tuple(int(c * 0.6) for c in food[1])
            pygame.draw.circle(scr, dark, (int(p.x), int(p.y)),
                               int(size * 0.55), 2)

        melt = snapshot[3] if snapshot else self.melt_frac
        self.draw_cone(self.cone_x, self.cone_y, melt)

        if snapshot:
            chefs = snapshot[1]
            for (cx, cy, cstate), c in zip(chefs, self.chefs):
                self.draw_chef(cx, cy, cstate, c.accent)
            for (x, y, col) in snapshot[2]:
                self.draw_food(x, y, col)
            self.draw_player(*snapshot[0])
        else:
            for c in self.chefs:
                self.draw_chef(c.x, c.y, c.state, c.accent)
            for pr in self.projectiles:
                self.draw_food(pr.x, pr.y, pr.color)
            if not (self.state == DEATH and self.death_kind == "hole"
                    and self.state_t > 0.5):
                self.draw_player(self.px, self.py)

        self.draw_hud(melt)

    def draw_cone(self, x, y, frac):
        scr = self.screen
        # melt puddle
        pud = int(34 * (1 - frac))
        if pud > 4:
            pygame.draw.ellipse(scr, (250, 200, 215),
                                (x - pud, y + 26 - pud // 3, pud * 2,
                                 pud * 0.7))
        # cone
        pygame.draw.polygon(scr, (210, 160, 90),
                            [(x - 16, y + 2), (x + 16, y + 2), (x, y + 34)])
        pygame.draw.polygon(scr, (160, 115, 60),
                            [(x - 16, y + 2), (x + 16, y + 2), (x, y + 34)], 2)
        # scoops shrink as it melts
        scoops = [((250, 190, 205), 0), ((250, 245, 235), 1),
                  ((150, 95, 60), 2)]
        for col, i in scoops:
            keep = clamp(frac * 3 - (2 - i), 0, 1)
            if keep <= 0.05:
                continue
            r = int(15 * keep) + 2
            pygame.draw.circle(scr, col, (int(x), int(y - 4 - i * 16 * keep)),
                               r)

    def draw_player(self, x, y):
        scr = self.screen
        x, y = int(x), int(y)
        # body
        pygame.draw.ellipse(scr, (90, 160, 235), (x - 11, y - 4, 22, 20))
        # head
        pygame.draw.circle(scr, (250, 215, 180), (x, y - 10), 10)
        # cap
        pygame.draw.arc(scr, (230, 60, 60), (x - 11, y - 24, 22, 20),
                        0.2, math.pi - 0.2, 6)
        # eyes follow facing direction
        ex = int(self.face[0] * 3)
        ey = int(self.face[1] * 2)
        pygame.draw.circle(scr, (20, 20, 30), (x - 4 + ex, y - 11 + ey), 2)
        pygame.draw.circle(scr, (20, 20, 30), (x + 4 + ex, y - 11 + ey), 2)
        # feet
        pygame.draw.circle(scr, (60, 60, 80), (x - 6, y + 15), 4)
        pygame.draw.circle(scr, (60, 60, 80), (x + 6, y + 15), 4)

    def draw_chef(self, x, y, state, accent):
        scr = self.screen
        x, y = int(x), int(y)
        if state in ("waiting",):
            return
        if state == "dead":
            pygame.draw.ellipse(scr, (200, 200, 210), (x - 16, y - 8, 32, 16))
            return
        rise = 1.0
        if state == "rising":
            # peeking out of the hole
            rise = 0.5
        h = int(26 * rise)
        # body / apron
        pygame.draw.ellipse(scr, (235, 235, 240), (x - 12, y - h // 2, 24, h))
        pygame.draw.rect(scr, accent, (x - 9, y + 2 - (1 - rise) * 8, 18, 7))
        # head
        hy = y - h // 2 - 6
        pygame.draw.circle(scr, (250, 220, 190), (x, hy), 9)
        # chef hat
        pygame.draw.rect(scr, WHITE, (x - 8, hy - 16, 16, 9))
        pygame.draw.circle(scr, WHITE, (x - 6, hy - 16), 5)
        pygame.draw.circle(scr, WHITE, (x, hy - 18), 6)
        pygame.draw.circle(scr, WHITE, (x + 6, hy - 16), 5)
        # angry eyes + moustache
        pygame.draw.circle(scr, (30, 30, 40), (x - 3, hy - 2), 2)
        pygame.draw.circle(scr, (30, 30, 40), (x + 3, hy - 2), 2)
        pygame.draw.rect(scr, accent, (x - 5, hy + 2, 10, 3))

    def draw_food(self, x, y, color):
        pygame.draw.circle(self.screen, color, (int(x), int(y)), 6)
        dark = tuple(int(c * 0.6) for c in color)
        pygame.draw.circle(self.screen, dark, (int(x), int(y)), 6, 1)

    def draw_hud(self, melt):
        scr = self.screen
        s = self.font_sml.render(f"SCORE {self.score}", True, WHITE)
        scr.blit(s, (WALL + 8, 2))
        s = self.font_sml.render(f"HI {self.high_score}", True,
                                 (180, 180, 200))
        scr.blit(s, (WALL + 230, 2))
        s = self.font_sml.render(f"LEVEL {self.level}", True, WHITE)
        scr.blit(s, (SW // 2 - 40, 2))
        # lives
        for i in range(self.lives - 1):
            pygame.draw.circle(scr, (250, 215, 180),
                               (SW - WALL - 20 - i * 26, 13), 8)
            pygame.draw.arc(scr, (230, 60, 60),
                            (SW - WALL - 29 - i * 26, 2, 18, 16), 0.2,
                            math.pi - 0.2, 4)
        # ammo readout (bottom left)
        food = FOODS[self.ammo_food]
        pygame.draw.circle(scr, food[1], (WALL + 14, SH - 13), 7)
        s = self.font_sml.render(f"x {self.ammo}", True, WHITE)
        scr.blit(s, (WALL + 28, SH - 25))
        # melt bar (bottom)
        bw = 300
        pygame.draw.rect(scr, (60, 60, 75),
                         (SW // 2 - bw // 2, SH - 19, bw, 13))
        col = (110, 205, 60) if melt > 0.4 else \
            (245, 215, 70) if melt > 0.18 else RED
        pygame.draw.rect(scr, col, (SW // 2 - bw // 2 + 2, SH - 17,
                                    int((bw - 4) * melt), 9))
        s = self.font_sml.render("MELT", True, WHITE)
        scr.blit(s, (SW // 2 - bw // 2 - 70, SH - 25))

    # ------------------------------------------------------------- main loop
    def run(self):
        paused = False
        while True:
            dt = self.clock.tick(FPS) / 1000.0
            dt = min(dt, 1 / 20.0)
            events = pygame.event.get()
            for ev in events:
                if ev.type == pygame.QUIT:
                    return
                if ev.type == pygame.KEYDOWN and ev.key == pygame.K_p \
                        and self.state == PLAYING:
                    paused = not paused
            if paused:
                self.draw_world()
                self.dim()
                self.center_text("PAUSED", self.font_big, WHITE)
                pygame.display.flip()
                continue
            keys = pygame.key.get_pressed()
            if not self.update(dt, keys, events):
                return
            self.draw()


# ------------------------------------------------------------------ selftest
class _FakeKeys:
    def __init__(self):
        self.down = set()

    def __getitem__(self, k):
        return 1 if k in self.down else 0


def selftest():
    """Headless smoke test: run a few hundred frames of game logic."""
    import os
    os.environ["SDL_VIDEODRIVER"] = "dummy"
    os.environ["SDL_AUDIODRIVER"] = "dummy"
    pygame.init()
    g = Game(headless=True)
    g.reset_game()
    g.new_level()
    g.set_state(PLAYING)
    keys = _FakeKeys()
    rng = random.Random(42)
    for frame in range(1200):
        keys.down = set()
        for k, ch in ((pygame.K_w, .3), (pygame.K_a, .4), (pygame.K_s, .3),
                      (pygame.K_d, .3), (pygame.K_i, .3), (pygame.K_j, .3),
                      (pygame.K_k, .3), (pygame.K_l, .3)):
            if rng.random() < ch:
                keys.down.add(k)
        g.ammo = max(g.ammo, 5)        # keep firing exercised
        g.update(1 / 60.0, keys, [])
        g.draw()
        if g.state == DEATH and g.state_t == 0:
            pass
        if g.state == GAME_OVER:
            g.reset_game()
            g.new_level()
            g.set_state(PLAYING)
    # force the level-clear / replay paths
    g.set_state(PLAYING)
    g.close_call = True
    g.px, g.py = g.cone_x, g.cone_y
    g.update(1 / 60.0, keys, [])
    assert g.state == LEVEL_CLEAR
    for _ in range(400):
        g.update(1 / 60.0, keys, [])
        g.draw()
    print("SELFTEST OK  score=%d level=%d state=%d"
          % (g.score, g.level, g.state))


def main():
    if "--selftest" in sys.argv:
        selftest()
        return
    pygame.init()
    Game().run()
    pygame.quit()


if __name__ == "__main__":
    main()
