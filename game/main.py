import pygame
import random
import asyncio

# Initialize Pygame
pygame.init()

# Screen settings
WIDTH = 800
HEIGHT = 600
screen = pygame.display.set_mode((WIDTH, HEIGHT))
pygame.display.set_caption("RC Rally Bird")

# Colors
WHITE = (255, 255, 255)
BLACK = (0, 0, 0)
GREEN = (0, 255, 0)
RED = (255, 0, 0)

# Player (RC Car) settings
PLAYER_WIDTH = 40
PLAYER_HEIGHT = 30
player_x = 100
player_y = HEIGHT - PLAYER_HEIGHT - 50
player_vel_y = 0
GRAVITY = 0.5
FLAP_POWER = -10
MAX_FALL_SPEED = 10

# Obstacle (Pipe) settings
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

# Drawing functions
def draw_player(x, y):
    pygame.draw.rect(screen, RED, (x, y, PLAYER_WIDTH, PLAYER_HEIGHT))
    pygame.draw.circle(screen, BLACK, (int(x + 10), int(y + PLAYER_HEIGHT)), 5)
    pygame.draw.circle(screen, BLACK, (int(x + 30), int(y + PLAYER_HEIGHT)), 5)

def draw_pipe(x, height):
    pygame.draw.rect(screen, GREEN, (x, 0, PIPE_WIDTH, height))
    pygame.draw.rect(screen, GREEN, (x, height + PIPE_GAP, PIPE_WIDTH, HEIGHT - height - PIPE_GAP))

def draw_ground():
    pygame.draw.rect(screen, BLACK, (0, HEIGHT - GROUND_HEIGHT, WIDTH, GROUND_HEIGHT))

def check_collision(player_rect, pipe_x, pipe_height):
    top_pipe = pygame.Rect(pipe_x, 0, PIPE_WIDTH, pipe_height)
    bottom_pipe = pygame.Rect(pipe_x, pipe_height + PIPE_GAP, PIPE_WIDTH, HEIGHT - pipe_height - PIPE_GAP)
    return (player_rect.colliderect(top_pipe) or 
            player_rect.colliderect(bottom_pipe) or 
            player_rect.top <= 0)

# Main async game loop
async def main():
    global player_y, player_vel_y, pipe_x, pipe_height, battery, game_over
    clock = pygame.time.Clock()
    game_over = False

    while True:
        if not game_over:
            # Handle events
            for event in pygame.event.get():
                if event.type == pygame.QUIT:
                    return
                if event.type == pygame.KEYDOWN:
                    if event.key == pygame.K_SPACE and battery > 0:
                        player_vel_y = FLAP_POWER
                        battery -= 10
                    if event.key == pygame.K_r and game_over:
                        player_y = HEIGHT - PLAYER_HEIGHT - 50
                        player_vel_y = 0
                        pipe_x = WIDTH
                        pipe_height = random.randint(100, HEIGHT - PIPE_GAP - 100)
                        battery = 100
                        game_over = False

            # Player movement
            keys = pygame.key.get_pressed()
            if keys[pygame.K_SPACE] and battery > 0:
                battery -= BATTERY_DRAIN
            else:
                player_vel_y += GRAVITY
                if player_y >= HEIGHT - PLAYER_HEIGHT - GROUND_HEIGHT:
                    player_y = HEIGHT - PLAYER_HEIGHT - GROUND_HEIGHT
                    player_vel_y = 0
                    if battery < 100:
                        battery += BATTERY_RECHARGE
            
            player_vel_y = min(player_vel_y, MAX_FALL_SPEED)
            player_y += player_vel_y

            # Move pipe
            pipe_x -= pipe_speed
            if pipe_x < -PIPE_WIDTH:
                pipe_x = WIDTH
                pipe_height = random.randint(100, HEIGHT - PIPE_GAP - 100)

            # Collision detection
            player_rect = pygame.Rect(player_x, player_y, PLAYER_WIDTH, PLAYER_HEIGHT)
            if check_collision(player_rect, pipe_x, pipe_height):
                game_over = True

        # Drawing
        screen.fill(WHITE)
        draw_ground()
        draw_player(player_x, player_y)
        draw_pipe(pipe_x, pipe_height)

        # Battery bar
        pygame.draw.rect(screen, GREEN, (10, 10, battery, 10))
        pygame.draw.rect(screen, BLACK, (10, 10, 100, 10), 2)

        # Game over text
        if game_over:
            font = pygame.font.SysFont(None, 55)
            text = font.render("Game Over! Press R to Restart", True, BLACK)
            screen.blit(text, (WIDTH // 2 - text.get_width() // 2, HEIGHT // 2 - text.get_height() // 2))

        pygame.display.flip()
        clock.tick(60)
        await asyncio.sleep(0)  # Yield control to the browser

# Run the game
asyncio.run(main())