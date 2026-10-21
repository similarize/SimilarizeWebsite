# Four Froggies — world bible (CANON)

Source: `similarize/bot-notes` BOT_NOTES.md, the Four Froggies World entry (2026-10-11), with Bill's corrections.
**Four Froggies World** (Grok bot) owns world, story and characters: its call wins on canon. Games SME owns build and tech.
Change this file only together with a matching BOT_NOTES.md entry.

## The froggies
| Index | Name | Colour |
|---|---|---|
| 0 | **James** | green `#3fbf3f` |
| 1 | **Jimmy** | lime `#a6d832` |
| 2 | **Bubbles** | teal `#2fb3c4` |
| 3 | **Rexy** | orange `#f2582a` |

Kid-friendly, always. Keep it light (it also has to run on Quest / Tesla browsers and phones).

## The fixed roster (playable characters)
Only these ten, each always the same animal. Never invent new names or swap species. (`Assets/Scripts/Frogs/Roster.cs`)

| Id | Name | Animal |
|---|---|---|
| 0 | James | frog |
| 1 | Jimmy | frog |
| 2 | Bubbles | frog |
| 3 | Rexy | frog |
| 4 | Spotty | cat (white with dark spots) |
| 5 | Tigy | cat (orange tabby stripes) |
| 6 | Kitty | cat (lilac grey) |
| 7 | Little White Socks ("Socks") | cat (black with white socks) |
| 8 | Germy | dog (German shepherd) |
| 9 | Daisy | dog (dachshund) |

Dad (James's dad) is a non-playable character: he lives in the house (kitchen) and owns a Cybertruck.
**Dad is a FROGGY DADDY** (Bill 2026-10-20): a bigger adult frog in the same style as the froggies - never a human. He has a
daily life: cooking at the stove, washing dishes, wiping the counter, the fridge, walking round the house, sometimes out on the
ranch. He can do everything froggies do (hop, swim, play, swing, ride, drive his own Cybertruck).
- In game (ffu66, Bill 2026-10-20 "he should be doing stuff that all the froggies can do"): Dad is a FULL FROGGY. Indoors he keeps his
  house life loop; out of the front door he is an AI-driven froggy agent (same froggy AI / verbs / vehicle code as the AI froggies)
  who picks his own fun: hops about the lawn, swims in the pond, swings / naps in a garden hammock, plays with the kids, and drives
  HIS OWN Cybertruck (garage bay 1) round the ranch and parks it back in his bay, then goes back inside. Robot phone PLAY tab + voice:
  "Dad, come play", "Dad, go for a drive", "Dad, swim with me". He is a canon character, NOT a roster seat and not joinable - the
  fixed roster (four froggies, four cats, two dogs) is unchanged. Dad stays on the ranch (he does not tag along to other worlds).
**Robot voices are male + monotone/robotic** (Bill 2026-10-20): every robot (Figure 03 too, who keeps his playful words) speaks with a
male, flat, even voice (a short beep before each line) so a robot is never mistaken for a human or a froggy; froggies sound distinct.

## Vehicles and ownership
- **Cybertrucks:** James and Dad each have their own Cybertruck ("James's Cybertruck" is simply its name). Either can
  use the other's **if they ask first and it isn't already in use**: an ask-to-borrow step plus an in-use check, not a
  hard owner lock. The same sharing rule covers James's and Dad's other ordinary vehicles.
  - In game (ffu34): garage bay 0 = James's Cybertruck, bay 1 = Dad's Cybertruck. A non-owner sees
    "A / E: ask James (or Dad) to borrow the …"; getting in shows "<name> asked … to borrow it – Sure, it's free".
    A truck someone is driving shows "… is driving" and can't be entered (in-use check).
  - **Neither truck ever drives in the house.** They stay in the garage or out front.
- **Mechs stay owner-locked** (`StoryMech.CanEnter`): each froggy pilots only its own mechs.
  - Everyone has a **10-story** and a **100-story** mech.
  - James and Bubbles also have **1000-story** mechs.
  - Only James has the **trillion-story** mech.
- **The VTOL jet is James-only** for now (`Jet.CanEnter`), until Four Froggies World says otherwise.
- **Spotty's Tourer motorcycle + Carrier Camper** (ffu48, Bill 2026-10-15): Spotty (the white cat with dark spots)
  owns an adventure-tourer motorcycle and a solar-roofed camper trailer ("Spotty's Carrier Camper", white with black
  spots and a pink stripe), parked at **Spotty's Spot** on the pond's north shore. Anyone else may use them **if they ask
  Spotty first and they aren't in use** (same ask-to-borrow rule as the Cybertrucks; `ownerName` "Spotty", roster id 4).
  - The motorcycle tows the camper. Ridden into the pond, the rig transforms into **Spotty's Carrier**, a small aircraft
    carrier: the motorcycle tips onto its side and becomes the stern drive pod, the camper is the cabin below the flight
    deck, the helm is up on the island's flying bridge. Driving back to the shore turns it into bike + camper again.
  - James's VTOL jet may land on and take off from the carrier's deck (the jet itself stays James-only).
  - AI froggies don't take Spotty's motorcycle out on their own.
- **The Roadster** (ffu49, Bill 2026-10-15): a white, low two-seater sports car with suction fans and cold-gas thrusters
  (it can stick to walls / ceilings, hop onto roofs and barrel-roll). **Owner TBD** - for now it is the **family car**:
  it lives in James's garage (bay 1, next to Dad's Cybertruck) and anyone may use it **if they ask first and it isn't
  already in use** (same ask-to-borrow + in-use check as the Cybertrucks; in game `ownerName` "the family", no playable
  owner). Four Froggies World may name an owner later.
- **Figure 03's rocket** (ffu50, Bill 2026-10-16): Figure 03 has his **very own rocket** (cream knit-look body, dark grey
  nose / fins / legs, a dark visor band with a glowing ring, a glowing strip, "FIGURE 03" name plates) on its own small pad
  near the robot mission pad. **Owner-locked to Figure 03** (like the mechs, not ask-to-borrow): any froggy, player, pet or
  other robot is told "Only Figure 03 can fly this rocket" and can't board. Figure 03 boards it from the robot phone ("Fly
  your rocket" / any MISSION) or when a player drives him with DRIVE IT!; it flies the usual robot space mission (Mars /
  Callisto) and lands back on his pad.
- **Space materials, arrows and Figure 03's bow** (ffu50, Bill 2026-10-16): robot missions bring back Mars rock, Callisto
  ice and meteorite metal (a shared stash, bins at the pads). Froggies craft arrowheads and arrows at the garden workbench;
  Figure 03 has a bow and uses them (phone "Hunt" or DRIVE IT!). **Kid-friendly and cartoony, no blood:** an arrow makes a
  ranch cow tip over with stars and go POOF into a cartoon drumstick (the cow pops back in its pen later); froggies cook it
  on the garden campfire and eat it with a happy hop.
- **Figure 03's bow is ask-to-borrow for the other robots** (ffu54, Bill 2026-10-16: "the robot should be able to do
  anything a froggy can do"): any robot sent hunting asks Figure 03 first, and he lends it unless he is using it (same
  ask-first idea as the Cybertrucks). His rocket stays owner-locked.
- **Froggy bows** (ffu61, Bill 2026-10-17): a rack of four kid-sized froggy bows (green, lime, teal, orange) stands by the
  garden workbench. They are shared ranch kit: any froggy may take one (free, like the ranch toys); pets can't (paws).
  Arrows come from the shared quiver at the workbench; Figure 03's own bow and the arrows handed to him stay his (robots
  still ask to borrow his bow). Same kid-friendly rule: a hit cow tips over with stars, POOFs into a cartoon drumstick and
  pops back in its pen later - no blood. Missed arrows can be picked up again.
- **Robots everywhere** (ffu54, Bill 2026-10-16): James's robots come when called from the phone in every world (ranch,
  house, Real Room, Mars, Callisto, Moon, space, the pond, the forest), say hi, and do whatever James asks there; left
  alone they keep doing chores. James's request always wins. Vehicle ownership rules apply to robots too.
- **Figure 03 is a free-willed playmate, not just a helper** (Bill 2026-10-19 16:41-16:43: "Figure 03 is the only robot that, if he
  doesn't wanna do chores, he doesn't have to when we tell him to." / "He can play and do everything that the froggies can do,
  and not just do chores." / "He can drive cars, get on a Starship and go, and do everything a froggy can."): told to do a chore
  he sometimes says no (mood; more likely right after one, less likely when asked nicely - "please") with a cheeky line and a fun
  idea instead; "yes" takes his idea. Left to himself he picks his own fun with the froggies - tag, catch, races, swings,
  hammocks, swimming, hopping, building, bug hunts, pets, cookouts - takes cars out (ownership / ask-to-share rules apply), tags
  along to other worlds and flies his own rocket. He keeps his playful froggy words ("Ribbit! Let's go!") but, like every robot, in a male monotone robot voice (ffu65). Every other robot still does
  what it is told, and James's orders still override their chores.
- **Robots sit with you on the porch swing / hammock** (Bill 2026-10-19 16:31): "Swing with me" / "Hammock with me" (phone ORDERS,
  or say "sit with me on the swing", "come to the hammock", "swing on the hammock"): the robot takes the swing / hammock next to
  yours and swings or rocks in step with you; if you are not in one yet, it sits and you can join on the next one.
- **Talk to the robots by voice** (ffu6q, Four Froggies 6.6 only, Bill 2026-10-19: "We wanna be able to talk to the robots
  when they're on missions. And have them talk back."): every player's robot phone has push-to-talk (TALK button, pad RT
  held, key T held), walkie-talkie style, and works wherever the robot is - even on Mars mid-mission. A spoken order does
  exactly what the same phone button does; the robot answers out loud in its own voice with what it is really doing
  (mission progress, where it is, what it sees, battery) and radios in by itself at mission milestones. Kid-friendly
  replies, no online AI. Browsers without speech recognition get tap-to-say quick replies instead.
- **Story episode 3 "Spotty's Surprise"** (ffu48): Spotty surprises the froggies with the carrier at the pond; James is
  the most surprised of all. In episode 3 Spotty is played by the story itself (a player who picked Spotty plays another
  free roster character for the episode).

## Places
- **House + garage layout** (ffu67b, Bill 2026-10-20 13:19: "garage and house stay separate spaces, joined by an inside hallway /
  mudroom with two doors"). The same layout inside and out. James's house (front door + porch on its north side, facing the garage
  side of the ranch) and James's 6-bay garage (big doors open north to the drive / track; back wall on the south) are separate
  buildings. A **mudroom** wing joins them: on the ranch it stands against the house's east wall and meets the garage's back wall;
  inside it has exactly two doors - one into the **kitchen** (kitchen east wall) and one into the **garage** (garage back wall, inside
  the garage by Dad's bay, sign "MUDROOM - HOUSE"). The garage's **outside back door** is a real door in the back wall facing the open
  lawn east of the mudroom: from outside it leads **into the garage** (never into the house); from inside it leads back out. Ways in:
  house front door -> foyer; garage big bay doors -> garage; garage back door -> garage; garage mudroom door <-> mudroom <-> kitchen;
  upstairs windows from the porch roof -> 2nd floor; phone "Me: go upstairs" -> 2nd-floor landing.
- **Apollo 11 landing site** (ffu61, Bill 2026-10-17): on the Moon world, what the real July 1969 mission left behind - the
  Lunar Module descent stage (gold foil, four legs, ladder + the plaque "Here men from the planet Earth first set foot upon
  the Moon, July 1969 A.D. We came in peace for all mankind"), the American flag on its crossbar, the laser reflector,
  the seismometer and bootprints. Real history, shown with respect; the froggies' own flag at their lander is separate.

## Rules of play
- **One controller = exactly one froggy.**
- Froggies can **jump rails** (and obstacles) to reach the roof or other rooms.
- **Everyone boards the Starship together**: it has a blast-off sequence and everyone aboard goes as one.
- **No pillars in the middle of the garage door openings.**
- **Starship flight** (ffu62, Bill 2026-10-19): the nose always points where the ship is going (auto-transfer, free flight,
  warp, orbit). RB / C = warp toward the selected target along the nose (also from orbit), LB / Z = slower, the brake drops warp;
  any body with an orbit (Earth, Moon, Mars, Jupiter, Callisto, ...) can be entered by hand: fly close and brake (LT / S / BRAKE).
- **Esc** never resets the game from Help: it only closes Help (ffu62).
- **Robots are solid** (ffu63a, Bill 2026-10-19 16:14): robots never walk through walls, fences, buildings, furniture, vehicles
  or trees - they go around (or through a door) - and stay inside the same perimeter as the froggies in every world;
  a robot that arrives by jet pack / pod / door lands inside it, never inside a wall.
- **Play catch with the robots** (ffu63b, Bill 2026-10-19 16:27): a robot's plushie throw can be caught by the froggy (it lands
  within reach, or walk onto it / E / A); while held an arc preview aims at the robot (LT / right mouse aims), RT / click / F
  throws it back; the robot runs under it, catches it and throws again (a miss = it fetches it). Kid-friendly, a soft plushie.
- **Camper trailers are walk-in** (ffu64, Bill 2026-10-19 15:58): every camper trailer's door opens (E / A) into its own
  furnished inside - bed, table + lamp, tiny kitchen - true to the trailer's size; you come back out next to the trailer
  wherever it has been towed.
- **Aircraft land on giant mechs** (ffu64, Bill 2026-10-19 17:08): drones and helicopters bump into the big mechs / giant
  robots instead of flying through them, can land on a shoulder or the head, and ride along while the mech walks.
- **James's house has a real upstairs** (ffu64, Bill 2026-10-19 16:47 / 17:02 / 17:06): stairs in the living room to a 2nd
  floor (James's room, guest room, bathroom, landing, bunk room, craft room); froggies can climb onto the porch roof and in
  through an upstairs window (never kicked back outside). (ffu64's "garage back door -> house hallway" was replaced in ffu67b: see House + garage layout.)
- **Stairs are easy to find + you can look up indoors** (ffu65, Bill 2026-10-20 Pixel 9): signs, yellow floor arrows and a first-visit
  hint lead from the front door to the stairs; the robot phone PLAY tab has "Me: go upstairs (house)"; inside the house, the 2nd
  floor and the campers the view tilts up to 65 deg (touch drag, right stick, mouse) to see the ceiling and lamps.
- **Wheel size is per player, per vehicle** (ffu67, Bill 2026-10-20: "as a user, not as a whole game"): each player changes only
  the vehicle they are DRIVING (pads in split-screen never change another player's truck). Pad while driving: D-pad UP = bigger
  wheels, D-pad DOWN = smaller (hold repeats; toast "Wheels: 37 in"), View + D-pad up / down = camera zoom while driving. Keys [ / ],
  robot phone WHEELS tab (touch / mouse), help menu WHEEL SIZE row = YOUR vehicle (each pad's D-pad < > its own). 1x stock .. 4x,
  tyre radius capped at 2.4 m; stock size inside buildings. Each player's pick is remembered per vehicle; the vehicle keeps the last
  size it was driven at for robots / AI / Dad. Applies to every wheeled vehicle (both Cybertrucks, Monster Truck, bikes, RC cars ...).
- **Rumble is per player** (ffu67c, Bill 2026-10-20): only the pad of the player who caused it rumbles (a mech pilot feels each
  footfall; players on the ground feel a giant mech's steps by distance), short pulses with a 0.4 s cap, and every pad goes to
  zero on exit / bail-out / death / world change / pause / focus loss. Never all pads, never Gamepad.current.
- **The paraglider always shows**, in shared view (the shared camera frames everyone even when one froggy glides from a
  trillion-story mech's head) and in split-screen. Froggies and their gear are never distance-culled.
- **Lobby picks the mode explicitly** (ffu67c): STORY and FREE PLAY are both visible options; LB / RB, D-pad / stick, keys S / F
  or a tap move the highlight, LT / Enter (or a second tap) starts the highlighted mode. No bumper starts a mode.
- **BACK TO LOBBY** is in the pause (Start / Help) menu for pad, touch and mouse, with a confirm (View asks, A confirms;
  Backspace + Enter; tap twice). It cleanly resets rumble, phones, vehicles, gliders, cameras and split-screen; seated players stay seated.
