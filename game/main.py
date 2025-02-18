import pygame
import random
import asyncio

pygame.init()

WIDTH = 800
HEIGHT = 600
screen = pygame.display.set_mode((WIDTH, HEIGHT))
pygame.display.set_caption("RC Rally Bird")

# Load assets with fallbacks
try:
    RC_CAR = pygame.image.load("rc_car.png").convert_alpha()
    RC_CAR = pygame.transform.scale(RC_CAR, (40, 30))
except FileNotFoundError:
    print("Warning: rc_car.png not found, using fallback")
    RC_CAR = pygame.Surface((40, 30), pygame.SRCALPHA)
    RC_CAR.fill((255, 0, 0))

try:
    PIPE = pygame.image.load("pipe.png").convert_alpha()
    PIPE = pygame.transform.scale(PIPE, (60, 300))
except FileNotFoundError:
    print("Warning: pipe.png not found, using fallback")
    PIPE = pygame.Surface((60, 300), pygame.SRCALPHA)
    PIPE.fill((0, 255, 0))

try:
    BACKGROUND = pygame.image.load("background.png").convert_alpha()
    BACKGROUND = pygame.transform.scale(BACKGROUND, (WIDTH, HEIGHT))
except FileNotFoundError:
    print("Warning: background.png not found, using fallback")
    BACKGROUND = pygame.Surface((WIDTH, HEIGHT))
    BACKGROUND.fill((135, 206, 235))

try:
    FLAP_SOUND = pygame.mixer.Sound("flap.wav")
    CRASH_SOUND = pygame.mixer.Sound("crash.wav")
    pygame.mixer.music.load("bgm.ogg")
    pygame.mixer.music.play(-1)
except FileNotFoundError as e:
    print(f"Warning: Sound file missing ({e}), proceeding without sound")
    FLAP_SOUND = CRASH_SOUND = lambda: None  # Dummy sound objects

# Colors
BLACK = (0, 0, 0)
GREEN = (0, 255, 0)

# Player settings
PLAYER_WIDTH = 40
PLAYER_HEIGHT = 30
player_x = 100
player_y = HEIGHT - PLAYER_HEIGHT - 50
player_vel_y = 0
GRAVITY = 0.5
FLAP_POWER = -12
MAX_FALL_SPEED = 10

# Obstacle settings
PIPE_WIDTH = 60
PIPE_GAP = 200
pipe_x = WIDTH
pipe_height = random.randint(100, HEIGHT - PIPE_GAP - 100)
pipe_speed = 3

# Ground settings
GROUND_SEGMENT_WIDTH = 20
ground_heights = [HEIGHT - 50] * (WIDTH // GROUND_SEGMENT_WIDTH + 1)
for i in range(1, len(ground_heights)):
    ground_heights[i] = ground_heights[i-1] + random.randint(-10, 10)
    ground_heights[i] = max(HEIGHT - 100, min(HEIGHT - 20, ground_heights[i]))

# Battery and sound settings
battery = 100
BATTERY_DRAIN = 0.5
BATTERY_RECHARGE = 0.2
flap_sound_timer = 0
FLAP_SOUND_DURATION = 1000

def reset_game():
    global player_y, player_vel_y, pipe_x, pipe_height, battery, game_over, ground_heights
    player_y = HEIGHT - PLAYER_HEIGHT - 50
    player_vel_y = 0
    pipe_x = WIDTH
    pipe_height = random.randint(100, HEIGHT - PIPE_GAP - 100)
    battery = 100
    game_over = False
    ground_heights = [HEIGHT - 50] * (WIDTH // GROUND_SEGMENT_WIDTH + 1)
    for i in range(1, len(ground_heights)):
        ground_heights[i] = ground_heights[i-1] + random.randint(-10, 10)
        ground_heights[i] = max(HEIGHT - 100, min(HEIGHT - 20, ground_heights[i]))

def draw_player(x, y, flapping=False):
    if flapping:
        car = pygame.transform.rotate(RC_CAR, -15)
    else:
        car = RC_CAR
    screen.blit(car, (x, y))

def draw_pipe(x, height):
    top_pipe = pygame.transform.flip(PIPE, False, True)
    screen.blit(top_pipe, (x, height - 300))
    screen.blit(PIPE, (x, height + PIPE_GAP))

def draw_ground():
    for i in range(len(ground_heights)):
        pygame.draw.rect(screen, BLACK, (i * GROUND_SEGMENT_WIDTH, ground_heights[i], GROUND_SEGMENT_WIDTH, HEIGHT - ground_heights[i]))

def check_collision(player_rect, pipe_x, pipe_height):
    top_pipe = pygame.Rect(pipe_x, 0, PIPE_WIDTH, pipe_height)
    bottom_pipe = pygame.Rect(pipe_x, pipe_height + PIPE_GAP, PIPE_WIDTH, HEIGHT - pipe_height - PIPE_GAP)
    return (player_rect.colliderect(top_pipe) or 
            player_rect.colliderect(bottom_pipe) or 
            player_rect.top <= 0)

async def main():
    global player_y, player_vel_y, pipe_x, pipe_height, battery, game_over, flap_sound_timer
    clock = pygame.time.Clock()
    reset_game()
    flapping = False

    while True:
        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                return
            if event.type == pygame.KEYDOWN:
                if event.key == pygame.K_SPACE and battery > 0 and not game_over:
                    player_vel_y = FLAP_POWER
                    battery -= 10
                    if flap_sound_timer <= 0:
                        FLAP_SOUND.play()
                        flap_sound_timer = FLAP_SOUND_DURATION
                    flapping = True
                if event.key == pygame.K_r and game_over:
                    reset_game()
            if event.type == pygame.KEYUP:
                if event.key == pygame.K_SPACE:
                    flapping = False

        if not game_over:
            if flap_sound_timer > 0:
                flap_sound_timer -= clock.get_time()
                if flap_sound_timer <= 0:
                    FLAP_SOUND.stop()

            ground_index = min(player_x // GROUND_SEGMENT_WIDTH, len(ground_heights) - 1)
            ground_y = ground_heights[ground_index]
            prev_ground_y = ground_heights[max(0, ground_index - 1)]

            player_vel_y += GRAVITY
            player_y += player_vel_y

            if player_y + PLAYER_HEIGHT >= ground_y and player_vel_y > 0:
                player_y = ground_y - PLAYER_HEIGHT
                player_vel_y = 0
                if battery < 100:
                    battery += BATTERY_RECHARGE
                height_diff = prev_ground_y - ground_y
                if height_diff > 10:
                    player_vel_y = -5

            player_vel_y = min(player_vel_y, MAX_FALL_SPEED)

            pipe_x -= pipe_speed
            if pipe_x < -PIPE_WIDTH:
                pipe_x = WIDTH
                pipe_height = random.randint(100, HEIGHT - PIPE_GAP - 100)
                ground_heights.pop(0)
                last_height = ground_heights[-1]
                new_height = last_height + random.randint(-10, 10)
                ground_heights.append(max(HEIGHT - 100, min(HEIGHT - 20, new_height)))

            player_rect = pygame.Rect(player_x, player_y, PLAYER_WIDTH, PLAYER_HEIGHT)
            if check_collision(player_rect, pipe_x, pipe_height):
                game_over = True
                CRASH_SOUND.play()

        screen.blit(BACKGROUND, (0, 0))
        draw_ground()
        draw_player(player_x, player_y, flapping)
        draw_pipe(pipe_x, pipe_height)

        pygame.draw.rect(screen, GREEN, (10, 10, battery, 10))
        pygame.draw.rect(screen, BLACK, (10, 10, 100, 10), 2)

        if game_over:
            font = pygame.font.SysFont(None, 55)
            text = font.render("Game Over! Press R to Restart", True, BLACK)
            screen.blit(text, (WIDTH // 2 - text.get_width() // 2, HEIGHT // 2 - text.get_height() // 2))

        pygame.display.flip()
        clock.tick(60)
        await asyncio.sleep(0)

asyncio.run(main())