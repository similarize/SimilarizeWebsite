# Four Froggies 3D

This folder is the standalone three.js ranch. Do not replace index.html with a redirect to /games/four-froggies/.

## Controllers (same map as the other cabinets)

Uses `/games/shared/gamepad.js` (`window.SimilarizeGamepad`). Do not fork that file.

Xbox / Cybertruck pads, up to 4:

- Title: A or Start claims the next open frog for that pad. B releases it. Y starts.
- In game: left stick and d-pad move that frog. A gets into the one Cybertruck or gets out. B or X hops. LB / RB change tire size, driver only.
- One truck. The keyboard / on-screen frog starts driving. Other claimed frogs walk. Unclaimed frogs are AI and wander.
- Camera follows the truck while someone is driving, otherwise whoever is walking.
