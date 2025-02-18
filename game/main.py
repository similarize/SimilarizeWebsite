import pygame
import random
import asyncio
import sys

pygame.init()

# Constants
WIDTH = 800
HEIGHT = 600
PLAYER_WIDTH = 40
PLAYER_HEIGHT = 30
PIPE_WIDTH = 60
PIPE_GAP = 200
GROUND_SEGMENT_WIDTH = 20
GRAVITY = 0.5
FLAP_POWER = -12
MAX_FALL_SPEED = 10
PIPE_SPEED = 3
BATTERY_DRAIN = 0.5
BATTERY_RECHARGE = 0.2
FLAP_SOUND_DURATION = 1000  # 1 second in milliseconds
DEBUG_MODE = False  # Toggle for debugging info

# Colors
BLACK = (0, 0, 0)
GREEN = (0, 255, 0)
WHITE = (255, 255, 255)

# Initialize screen
screen = pygame.display.set_mode((WIDTH, HEIGHT))
pygame.display.set_caption("RC Rally Bird")
clock = pygame.time.Clock()

# Load assets with error handling
try:
    RC_CAR = pygame.image.load("rc_car.png").convert_alpha()
    RC_CAR = pygame.transform.scale(RC_CAR, (PLAYER_WIDTH, PLAYER_HEIGHT))
    PIPE = pygame.image.load("pipe.png").convert_alpha()
    PIPE = pygame.transform.scale(PIPE, (PIPE_WIDTH, 300))
    BACKGROUND = pygame.image.load("background.png").convert_alpha()
    BACKGROUND = pygame.transform.scale(BACKGROUND, (WIDTH, HEIGHT))
    FLAP_SOUND = pygame.mixer.Sound("flap.wav")
    CRASH_SOUND = pygame.mixer.Sound("crash.wav")
    pygame.mixer.music.load("bgm.ogg")
    pygame.mixer.music.play(-1)
except pygame.error as e:
    print(f"Error loading assets: {e}")
    sys.exit(1)

# Game state
player_x = 100
player_y = HEIGHT - PLAYER_HEIGHT - 50
player_vel_y = 0
pipe_x = WIDTH
pipe_height = random.randint(100, HEIGHT - PIPE_GAP - 100)
battery = 100
game_over = False
flap_sound_timer = 0
flapping = False
ground_heights = [HEIGHT - 50] * (WIDTH // GROUND_SEGMENT_WIDTH + 1)
ground_offset = 0  # Tracks ground scroll

def reset_game():
    global player_y, player_vel_y, pipe_x, pipe_height, battery, game_over, ground_heights, ground_offset
    player_y = HEIGHT - PLAYER_HEIGHT - 50
    player_vel_y = 0
    pipe_x = WIDTH
    pipe_height = random.randint(100, HEIGHT - PIPE_GAP - 100)
    battery = 100
    game_over = False
    ground_heights = [HEIGHT - 50] * (WIDTH // GROUND_SEGMENT_WIDTH + 1)
    ground_offset = 0

def update_player():
    global player_y, player_vel_y, battery, flap_sound_timer, flapping
    ground_index = min(int((player_x + ground_offset) // GROUND_SEGMENT_WIDTH), len(ground_heights) - 1)
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
            player_vel_y = -5  # Small jump over humps

    player_vel_y = min(player_vel_y, MAX_FALL_SPEED)

    if flap_sound_timer > 0:
        flap_sound_timer -= clock.get_time()
        if flap_sound_timer <= 0:
            FLAP_SOUND.stop()

def update_world():
    global pipe_x, pipe_height, ground_offset, ground_heights
    pipe_x -= PIPE_SPEED
    ground_offset += PIPE_SPEED

    if pipe_x < -PIPE_WIDTH:
        pipe_x = WIDTH
        pipe_height = random.randint(100, HEIGHT - PIPE_GAP - 100)

    # Shift ground
    while ground_offset >= GROUND_SEGMENT_WIDTH:
        ground_offset -= GROUND_SEGMENT_WIDTH
        ground_heights.pop(0)
        last_height = ground_heights[-1]
        new_height = last_height + random.randint(-10, 10)
        ground_heights.append(max(HEIGHT - 100, min(HEIGHT - 20, new_height)))

def draw_player(x, y, flapping):
    car = pygame.transform.rotate(RC_CAR, -15) if flapping else RC_CAR
    screen.blit(car, (x, y))

def draw_pipe(x, height):
    top_pipe = pygame.transform.flip(PIPE, False, True)
    screen.blit(top_pipe, (x, height - 300))
    screen.blit(PIPE, (x, height + PIPE_GAP))

def draw_ground():
    for i, height in enumerate(ground_heights):
        x = i * GROUND_SEGMENT_WIDTH - ground_offset
        if x < WIDTH:
            pygame.draw.rect(screen, BLACK, (x, height, GROUND_SEGMENT_WIDTH, HEIGHT - height))

def draw_debug():
    if DEBUG_MODE:
        font = pygame.font.SysFont(None, 24)
        debug_info = [
            f"Player Y: {player_y:.1f}",
            f"Velocity: {player_vel_y:.1f}",
            f"Battery: {battery:.1f}",
            f"Pipe X: {pipe_x:.1f}",
            f"Ground Offset: {ground_offset:.1f}"
        ]
        for i, line in enumerate(debug_info):
            text = font.render(line, True, WHITE)
            screen.blit(text, (10, 50 + i * 20))

def check_collision():
    player_rect = pygame.Rect(player_x, player_y, PLAYER_WIDTH, PLAYER_HEIGHT)
    top_pipe = pygame.Rect(pipe_x, 0, PIPE_WIDTH, pipe_height)
    bottom_pipe = pygame.Rect(pipe_x, pipe_height + PIPE_GAP, PIPE_WIDTH, HEIGHT - pipe_height - PIPE_GAP)
    return player_rect.colliderect(top_pipe) or player_rect.colliderect(bottom_pipe) or player_rect.top <= 0

def draw_scene():
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
    draw_debug()

async def main():
    global player_vel_y, battery, game_over, flap_sound_timer, flapping
    reset_game()

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
                if event.key == pygame.K_d:  # Toggle debug mode
                    global DEBUG_MODE
                    DEBUG_MODE = not DEBUG_MODE
            if event.type == pygame.KEYUP:
                if event.key == pygame.K_SPACE:
                    flapping = False

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