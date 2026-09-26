# Four Froggies 3D entry

- Launcher: `games/four-froggies-3d/` sets `ff-engine=three` and opens hub with `?engine=three` (**no `go=1`**).
- Lobby stays visible so couch pads can claim froggy seats before GO.
- If Three fails to boot, UI hard-fails with toast and **keeps Three selected** (never silent Canvas fallback).
- GO with Three selected starts `three-hub` with the multi-local seat map.
