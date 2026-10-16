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
- **Story episode 3 "Spotty's Surprise"** (ffu48): Spotty surprises the froggies with the carrier at the pond; James is
  the most surprised of all. In episode 3 Spotty is played by the story itself (a player who picked Spotty plays another
  free roster character for the episode).

## Rules of play
- **One controller = exactly one froggy.**
- Froggies can **jump rails** (and obstacles) to reach the roof or other rooms.
- **Everyone boards the Starship together**: it has a blast-off sequence and everyone aboard goes as one.
- **No pillars in the middle of the garage door openings.**
