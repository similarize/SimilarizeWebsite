import pygame
import random
import asyncio
import sys

pygame.init()

# Constants
WIDTH = 800
HEIGHT = 600
PLAYER_WIDTH = 120
PLAYER_HEIGHT = 90
PIPE_WIDTH = 300
PIPE_GAP = 200
GROUND_SEGMENT_WIDTH = 10
GRAVITY = 0.4  # Reduced for smoother jumping
FLAP_ACCEL = -0.9  # Adjusted for smoother lift
MAX_FALL_SPEED = 10
PIPE_SPEED = 3
BATTERY_DRAIN = 0.05
BATTERY_RECHARGE = 2.0
FLAP_SOUND_DURATION = 1000
DRONE_WIDTH = 30
DRONE_HEIGHT = 30
DRONE_SPEED_MIN = 2
DRONE_SPEED_MAX = 5
DEBUG_MODE = False
TREE_WIDTH = 50
TREE_HEIGHT = 100

# Colors
BLACK = (0, 0, 0)
GREEN = (0, 255, 0)
WHITE = (255, 255, 255)
TAN = (245, 222, 179)
BROWN = (139, 69, 19)

# Initialize screen
screen = pygame.display.set_mode((WIDTH, HEIGHT))
pygame.display.set_caption("RC Rally Bird")
clock = pygame.time.Clock()

# Load assets
try:
    RC_CAR = pygame.image.load("rc_car.png").convert_alpha()
    RC_CAR = pygame.transform.scale(RC_CAR, (PLAYER_WIDTH, PLAYER_HEIGHT))
    PIPE = pygame.image.load("pipe.png").convert_alpha()
    PIPE = pygame.transform.scale(PIPE, (PIPE_WIDTH, 300))
    bg_files = ["background.png", "background2.png", "background3.png", "background4.png"]
    BACKGROUND = pygame.image.load(random.choice(bg_files)).convert_alpha()
    BACKGROUND = pygame.transform.scale(BACKGROUND, (WIDTH, HEIGHT))
    FLAP_SOUND = pygame.mixer.Sound("flap.wav")
    CRASH_SOUND = pygame.mixer.Sound("crash.wav")
    FLAP_SOUND.set_volume(0.2)  # 20%
    CRASH_SOUND.set_volume(0.2)  # 20%
    pygame.mixer.music.load("bgm.ogg")
    pygame.mixer.music.set_volume(0.2)
    pygame.mixer.music.play(-1)
except pygame.error as e:
    print(f"Error loading assets: {e}")
    sys.exit(1)

# Game state
player_x = 100
player_y = HEIGHT - PLAYER_HEIGHT - 50
player_vel_y = 0
player_accel_y = 0
player_rotation = 0
pipe_x = WIDTH
pipe_height = random.randint(100, HEIGHT - PIPE_GAP - 100)
battery = 1000
game_over = False
flap_sound_timer = 0
flapping = False
ground_heights = [HEIGHT - 50] * (WIDTH // GROUND_SEGMENT_WIDTH + 1)
ground_offset = 0
drones = []  # (x, y, speed, rotation, color, color_timer)
trees = [(WIDTH + i * 200, HEIGHT - 150, random.randint(80, 120)) for i in range(4)]  # (x, y, height)
volume_slider = pygame.Rect(WIDTH - 110, 20, 100, 10)
effects_slider = pygame.Rect(WIDTH - 110, 50, 100, 10)

def reset_game():
    global player_y, player_vel_y, player_accel_y, player_rotation, pipe_x, pipe_height, battery, game_over, ground_heights, ground_offset, drones, trees
    player_y = HEIGHT - PLAYER_HEIGHT - 50
    player_vel_y = 0
    player_accel_y = 0
    player_rotation = 0
    pipe_x = WIDTH
    pipe_height = random.randint(100, HEIGHT - PIPE_GAP - 100)
    battery = 1000
    game_over = False
    ground_heights = [HEIGHT - 50] * (WIDTH // GROUND_SEGMENT_WIDTH + 1)
    ground_offset = 0
    drones = []
    trees = [(WIDTH + i * 200, HEIGHT - 150, random.randint(80, 120)) for i in range(4)]

def spawn_drone():
    if random.random() < 0.01 and not game_over:
        y = random.randint(50, HEIGHT - PIPE_GAP - 50)
        speed = random.uniform(DRONE_SPEED_MIN, DRONE_SPEED_MAX)
        color = (random.randint(100, 255), random.randint(100, 255), random.randint(100, 255))
        drones.append((WIDTH, y, speed, 0, color, 1000))

def update_player():
    global player_y, player_vel_y, player_accel_y, player_rotation, battery, flap_sound_timer, flapping
    ground_index = min(int((player_x + ground_offset) // GROUND_SEGMENT_WIDTH), len(ground_heights) - 1)
    ground_y = ground_heights[ground_index]
    prev_ground_y = ground_heights[max(0, ground_index - 1)]

    player_vel_y += player_accel_y + GRAVITY
    player_y += player_vel_y
    player_accel_y *= 0.85  # Slightly more damping for smoother fall

    if player_y + PLAYER_HEIGHT >= ground_y and player_vel_y > 0:
        player_y = ground_y - PLAYER_HEIGHT
        player_vel_y = 0
        player_accel_y = 0
        if battery < 1000:
            battery += BATTERY_RECHARGE
        slope = prev_ground_y - ground_y
        if not (pygame.key.get_pressed()[pygame.K_LEFT] or pygame.key.get_pressed()[pygame.K_RIGHT]):
            player_rotation = min(max(slope * 0.5, -15), 15)
        if slope > 5:
            player_vel_y = -5
    else:
        if not (pygame.key.get_pressed()[pygame.K_LEFT] or pygame.key.get_pressed()[pygame.K_RIGHT]):
            player_rotation *= 0.95

    player_vel_y = min(player_vel_y, MAX_FALL_SPEED)
    if flap_sound_timer > 0:
        flap_sound_timer -= clock.get_time()
        if flap_sound_timer <= 0:
            FLAP_SOUND.stop()

def update_world():
    global pipe_x, pipe_height, ground_offset, ground_heights, drones, trees
    pipe_x -= PIPE_SPEED
    ground_offset += PIPE_SPEED

    if pipe_x < -PIPE_WIDTH:
        pipe_x = WIDTH
        pipe_height = random.randint(100, HEIGHT - PIPE_GAP - 100)

    while ground_offset >= GROUND_SEGMENT_WIDTH:
        ground_offset -= GROUND_SEGMENT_WIDTH
        ground_heights.pop(0)
        last_height = ground_heights[-1]
        new_height = last_height + random.uniform(-10, 10)
        ground_heights.append(max(HEIGHT - 200, min(HEIGHT - 20, new_height)))

    spawn_drone()
    drones[:] = [(x - speed, y, speed, (rot + 5) % 360, 
                  (random.randint(100, 255), random.randint(100, 255), random.randint(100, 255)) if timer <= 0 else color, 
                  1000 if timer <= 0 else timer - clock.get_time()) 
                 for x, y, speed, rot, color, timer in drones if x > -DRONE_WIDTH and random.random() > 0.01]
    
    trees[:] = [(x - PIPE_SPEED * 0.5, y, h) for x, y, h in trees]  # Slower than pipes
    if trees and trees[0][0] < -TREE_WIDTH:
        trees.pop(0)
        trees.append((WIDTH, HEIGHT - 150, random.randint(80, 120)))

def draw_player(x, y, flapping):
    car = pygame.transform.rotate(RC_CAR, player_rotation if not flapping else -15)
    screen.blit(car, (x, y))

def draw_pipe(x, height):
    top_pipe = pygame.transform.flip(PIPE, False, True)
    screen.blit(top_pipe, (x, height - 300))
    screen.blit(PIPE, (x, height + PIPE_GAP))

def draw_ground():
    for i, height in enumerate(ground_heights):
        x = i * GROUND_SEGMENT_WIDTH - ground_offset
        if x < WIDTH:
            shade = max(100, min(255, 245 - (HEIGHT - height) // 4 + PIPE_SPEED * 20))
            pygame.draw.rect(screen, (shade, shade - 43, shade - 66), (x, height, GROUND_SEGMENT_WIDTH, HEIGHT - height))

def draw_drones():
    for x, y, _, rot, color, _ in drones:
        body = [(x - 10, y - 10), (x + 10, y - 10), (x + 10, y + 10), (x - 10, y + 10)]
        rotors = [
            [(x - 15, y - 15), (x - 5, y - 15), (x - 5, y - 5), (x - 15, y - 5)],
            [(x + 5, y - 15), (x + 15, y - 15), (x + 15, y - 5), (x + 5, y - 5)],
            [(x - 15, y + 5), (x - 5, y + 5), (x - 5, y + 15), (x - 15, y + 15)],
            [(x + 5, y + 5), (x + 15, y + 5), (x + 15, y + 15), (x + 5, y + 15)]
        ]
        rotated_body = [(x + (px - x) * pygame.math.Vector2(1, 0).rotate(rot).x + (py - y) * pygame.math.Vector2(1, 0).rotate(rot).y,
                         y + (px - x) * pygame.math.Vector2(0, 1).rotate(rot).x + (py - y) * pygame.math.Vector2(0, 1).rotate(rot).y)
                        for px, py in body]
        pygame.draw.polygon(screen, color, rotated_body)
        for rotor in rotors:
            rotated_rotor = [(x + (px - x) * pygame.math.Vector2(1, 0).rotate(rot).x + (py - y) * pygame.math.Vector2(1, 0).rotate(rot).y,
                              y + (px - x) * pygame.math.Vector2(0, 1).rotate(rot).x + (py - y) * pygame.math.Vector2(0, 1).rotate(rot).y)
                             for px, py in rotor]
            pygame.draw.polygon(screen, (200, 200, 200), rotated_rotor)

def draw_trees():
    for x, y, h in trees:
        pygame.draw.rect(screen, BROWN, (x - TREE_WIDTH // 4, y + h - TREE_HEIGHT, TREE_WIDTH // 2, TREE_HEIGHT // 2))  # Trunk
        pygame.draw.polygon(screen, (0, 100, 0), [(x, y - h), (x - TREE_WIDTH // 2, y), (x + TREE_WIDTH // 2, y)])  # Foliage

def draw_debug():
    if DEBUG_MODE:
        font = pygame.font.SysFont(None, 24)
        debug_info = [
            f"Player Y: {player_y:.1f}",
            f"Velocity: {player_vel_y:.1f}",
            f"Accel: {player_accel_y:.1f}",
            f"Battery: {battery:.1f}",
            f"Drones: {len(drones)}"
        ]
        for i, line in enumerate(debug_info):
            text = font.render(line, True, WHITE)
            screen.blit(text, (10, 80 + i * 20))

def check_collision():
    player_rect = pygame.Rect(player_x, player_y, PLAYER_WIDTH, PLAYER_HEIGHT)
    top_pipe = pygame.Rect(pipe_x, 0, PIPE_WIDTH, pipe_height - 300)  # Adjusted to match sprite height
    bottom_pipe = pygame.Rect(pipe_x, pipe_height + PIPE_GAP, PIPE_WIDTH, HEIGHT - (pipe_height + PIPE_GAP))
    drone_rects = [pygame.Rect(x - DRONE_WIDTH // 2, y - DRONE_HEIGHT // 2, DRONE_WIDTH, DRONE_HEIGHT) for x, y, _, _, _, _ in drones]
    return (player_rect.colliderect(top_pipe) or 
            player_rect.colliderect(bottom_pipe) or 
            any(player_rect.colliderect(dr) for dr in drone_rects) or 
            player_rect.top <= 0)

def draw_splash_screen():
    screen.fill((50, 50, 100))  # Dark blue background
    font = pygame.font.SysFont(None, 80)
    title = font.render("RC Rally Bird", True, (255, 200, 0))
    font = pygame.font.SysFont(None, 40)
    subtitle = font.render("Get ready to race and soar!", True, WHITE)
    start_prompt = font.render("Press SPACE to Start", True, (0, 255, 0))
    
    title_y = HEIGHT // 4 + (pygame.time.get_ticks() // 500 % 2) * 10  # Bounce effect
    screen.blit(title, (WIDTH // 2 - title.get_width() // 2, title_y))
    screen.blit(subtitle, (WIDTH // 2 - subtitle.get_width() // 2, HEIGHT // 2))
    screen.blit(start_prompt, (WIDTH // 2 - start_prompt.get_width() // 2, HEIGHT * 3 // 4))
    pygame.display.flip()

async def splash_screen():
    while True:
        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                return False
            if event.type == pygame.KEYDOWN and event.key == pygame.K_SPACE:
                return True
        draw_splash_screen()
        await asyncio.sleep(0)
        clock.tick(60)

def draw_scene():
    screen.blit(BACKGROUND, (0, 0))
    draw_trees()
    draw_ground()
    draw_player(player_x, player_y, flapping)
    draw_pipe(pipe_x, pipe_height)
    draw_drones()
    pygame.draw.rect(screen, GREEN, (10, 10, battery / 10, 10))
    pygame.draw.rect(screen, BLACK, (10, 10, 100, 10), 2)
    font = pygame.font.SysFont(None, 20)
    volume_label = font.render("Music Volume", True, BLACK)
    screen.blit(volume_label, (WIDTH - 110, 5))
    pygame.draw.rect(screen, WHITE, volume_slider)
    volume_pos = volume_slider.x + int(pygame.mixer.music.get_volume() * volume_slider.width)
    pygame.draw.rect(screen, BLACK, (volume_pos - 2, volume_slider.y - 2, 4, 14))
    effects_label = font.render("Effects Volume", True, BLACK)
    screen.blit(effects_label, (WIDTH - 110, 35))
    pygame.draw.rect(screen, WHITE, effects_slider)
    effects_pos = effects_slider.x + int(FLAP_SOUND.get_volume() * effects_slider.width)
    pygame.draw.rect(screen, BLACK, (effects_pos - 2, effects_slider.y - 2, 4, 14))
    if game_over:
        font = pygame.font.SysFont(None, 55)
        text = font.render("Game Over! Press R to Restart", True, BLACK)
        screen.blit(text, (WIDTH // 2 - text.get_width() // 2, HEIGHT // 2 - text.get_height() // 2))
    draw_debug()

async def main():
    global player_vel_y, player_accel_y, player_rotation, battery, game_over, flap_sound_timer, flapping
    if not await splash_screen():
        return
    reset_game()

    while True:
        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                return
            if event.type == pygame.KEYDOWN:
                if event.key == pygame.K_SPACE and battery > 0 and not game_over:
                    player_accel_y = FLAP_ACCEL
                    battery -= 10
                    if flap_sound_timer <= 0:
                        FLAP_SOUND.play()
                        flap_sound_timer = FLAP_SOUND_DURATION
                    flapping = True
                if event.key == pygame.K_r and game_over:
                    reset_game()
                if event.key == pygame.K_d:
                    global DEBUG_MODE
                    DEBUG_MODE = not DEBUG_MODE
                if event.key == pygame.K_LEFT and not game_over:
                    player_rotation = min(player_rotation + 5, 15)
                if event.key == pygame.K_RIGHT and not game_over:
                    player_rotation = max(player_rotation - 5, -15)
            if event.type == pygame.KEYUP:
                if event.key == pygame.K_SPACE:
                    flapping = False
            if event.type == pygame.MOUSEBUTTONDOWN:
                if volume_slider.collidepoint(event.pos):
                    new_volume = (event.pos[0] - volume_slider.x) / volume_slider.width
                    pygame.mixer.music.set_volume(max(0, min(1, new_volume)))
                if effects_slider.collidepoint(event.pos):
                    new_volume = (event.pos[0] - effects_slider.x) / effects_slider.width
                    FLAP_SOUND.set_volume(max(0, min(1, new_volume)))
                    CRASH_SOUND.set_volume(max(0, min(1, new_volume)))

        if not game_over:
            update_player()
            update_world()
            if check_collision():
                game_over = True
                CRASH_SOUND.play()

        draw_scene()
        pygame.display.flip()
        clock.tick(60)
        await asyncio.sleep(0)

if __name__ == "__main__":
    asyncio.run(main())