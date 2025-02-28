import pygame
import random
import asyncio

pygame.init()

WIDTH = 800
HEIGHT = 600
screen = pygame.display.set_mode((WIDTH, HEIGHT))
pygame.display.set_caption("RC Rally Bird")

# Load assets
RC_CAR = pygame.image.load("rc_car.png").convert_alpha()
RC_CAR = pygame.transform.scale(RC_CAR, (40, 30))
PIPE = pygame.image.load("pipe.png").convert_alpha()
PIPE = pygame.transform.scale(PIPE, (60, 300))
BACKGROUND = pygame.image.load("background.png").convert_alpha()
BACKGROUND = pygame.transform.scale(BACKGROUND, (WIDTH, HEIGHT))

# Load sounds
FLAP_SOUND = pygame.mixer.Sound("flap.ogg")
CRASH_SOUND = pygame.mixer.Sound("crash.ogg")
pygame.mixer.music.load("bgm.ogg")
pygame.mixer.music.play(-1)  # Loop background music

# Colors (for fallback)
BLACK = (0, 0, 0)
GREEN = (0, 255, 0)

# Player settings
PLAYER_WIDTH = 40
PLAYER_HEIGHT = 30
player_x = 100
player_y = HEIGHT - PLAYER_HEIGHT - 50
player_vel_y = 0
GRAVITY = 0.5
FLAP_POWER = -10
MAX_FALL_SPEED = 10

# Obstacle settings
PIPE_WIDTH = 60
PIPE_GAP = 200
pipe_x = WIDTH
pipe_height = random.randint(100, HEIGHT - PIPE_GAP - 100)
pipe_speed = 3

# Ground settings
GROUND_HEIGHT = 50

# Battery settings
battery = 100
BATTERY_DRAIN = 0.5
BATTERY_RECHARGE = 0.2

def reset_game():
    global player_y, player_vel_y, pipe_x, pipe_height, battery, game_over
    player_y = HEIGHT - PLAYER_HEIGHT - 50
    player_vel_y = 0
    pipe_x = WIDTH
    pipe_height = random.randint(100, HEIGHT - PIPE_GAP - 100)
    battery = 100
    game_over = False

def draw_player(x, y, flapping=False):
    # Rotate slightly when flapping for realism
    if flapping:
        car = pygame.transform.rotate(RC_CAR, -15)  # Tilt up
    else:
        car = RC_CAR
    screen.blit(car, (x, y))

def draw_pipe(x, height):
    # Top pipe (flipped)
    top_pipe = pygame.transform.flip(PIPE, False, True)
    screen.blit(top_pipe, (x, height - 300))
    # Bottom pipe
    screen.blit(PIPE, (x, height + PIPE_GAP))

def draw_ground():
    pygame.draw.rect(screen, BLACK, (0, HEIGHT - GROUND_HEIGHT, WIDTH, GROUND_HEIGHT))  # Fallback

def check_collision(player_rect, pipe_x, pipe_height):
    top_pipe = pygame.Rect(pipe_x, 0, PIPE_WIDTH, pipe_height)
    bottom_pipe = pygame.Rect(pipe_x, pipe_height + PIPE_GAP, PIPE_WIDTH, HEIGHT - pipe_height - PIPE_GAP)
    return (player_rect.colliderect(top_pipe) or 
            player_rect.colliderect(bottom_pipe) or 
            player_rect.top <= 0)

async def main():
    global player_y, player_vel_y, pipe_x, pipe_height, battery, game_over
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
                    FLAP_SOUND.play()
                    flapping = True
                if event.key == pygame.K_r and game_over:
                    reset_game()
            if event.type == pygame.KEYUP:
                if event.key == pygame.K_SPACE:
                    flapping = False

        if not game_over:
            player_vel_y += GRAVITY
            if player_y >= HEIGHT - PLAYER_HEIGHT - GROUND_HEIGHT:
                player_y = HEIGHT - PLAYER_HEIGHT - GROUND_HEIGHT
                player_vel_y = 0
                if battery < 100:
                    battery += BATTERY_RECHARGE
            else:
                player_vel_y = min(player_vel_y, MAX_FALL_SPEED)
                player_y += player_vel_y

            pipe_x -= pipe_speed
            if pipe_x < -PIPE_WIDTH:
                pipe_x = WIDTH
                pipe_height = random.randint(100, HEIGHT - PIPE_GAP - 100)

            player_rect = pygame.Rect(player_x, player_y, PLAYER_WIDTH, PLAYER_HEIGHT)
            if check_collision(player_rect, pipe_x, pipe_height):
                game_over = True
                CRASH_SOUND.play()

        screen.blit(BACKGROUND, (0, 0))
        draw_ground()  # Only if background doesn’t cover ground
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