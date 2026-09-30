/* Four Froggies — three.js hub (CDN). Fixed-angle 2.5D-ish (orbit locked / isometric-ish).
   view3: start cam frames froggies+trucks toward garage; ranch forest = yard-scale trees (no flat berm).
   view1: outdoor spawn camera toward garage/yard + trucks; track support pillars under elev.
   view2: hard leave-ranch space (fresh scene + starfield plane); perimeter forest matches yard trees.
   NOT free-fly FPS. Canvas-parity landmarks · solo-first.
   Big map · compound · squiggle track · pond whales · 4 trucks+shared ·
   on-water/under · Starship → Escape/hard thruster.
   polish4: compound presence + hills + inviting hotspots + truck bob/spray.
   garage1: garage door spans full south bay wall-to-wall; polish5: ambient pollen/fireflies; pond ripples; track race dust; garage door open-near;
   shared ALL ABOARD; land shake; hotspot sparkle; orbit pull rings + Escape banner.
   polish6: depth shadows + parallax-lite hills + Mars invader silhouette tease + mech wow tip.
   polish7: zone signs + mini-map lite + companion idle bounce / follow lag. Hollow house + frogs kept.
   polish8: truck silhouette + house porch + whale breach + destination beacon. Hollow house + frogs kept.
   polish9: color nameplates; aboard icons; track gate; Optimus punch lite. Hollow house + frogs kept.
   polish10: quieter UI; exit truck anytime; friction/cam; particle caps; dusk sky. Hollow house + frogs + WASD kept.
   solid1: floor z-fight fix; solid walls/mechs/trucks; cast names only on plates.
   tapsteer1: mech pad z-fight fix; faster walk/drive; hold-to-aim tap/click steer + marker.
   eyes1: yaw frog (eyes on +Z) toward walk dir; idle keeps last; AI companions too.
   truck1: kid-toy truck scale; smooth yaw drive toward aim; track elev / crest / land bounce.
   truck2: EXIT anytime (HUD); full elev contact (no zLift damp); ribbon/ramp ride-up; crest launch.
   polish11: truck yaw follows travel; brief EXIT tip; shared ZOOM ability.
   hop1: HOP ability (Y arc + squash); shove toys/animals/pollen.
   hop2: ranch foot ALWAYS hops (continuous arc); ability HOP = bigger jump.
   yard1: backyard creek/trees + outdoor trillion mech; park1: EXIT parks at exit pos.
   mech1: mech ownership locks + shared Ripsaw/Tank (garage).
   mech3: Tank big missiles (Space / X / button) blow up toys/animals/props.
   mech5: tank destroys ONLY Rexy 1000-mech; distinct vehicle speeds; expand drive ground/forest.
   pond1: swim in pond + docked submarine at south rim.
   mech6: tank blast props + Rexy 1000-mech respawn ~7s.
   mech7: swim POSE (stroke + flat body, no hop); board existing docked sub (no clone hull).
   mech8: after tank destroys Rexy 1000-mech, respawn restores full mech mesh (not haze-ball).
   interact2: interact/exit + HOP strictly per pad/player; shared HUD = primary only.
   hop3: faster loco + spam HOP + stack; articulated mechs.
   track3: banks + rocks + live monster wheels (preserved).
   hop4: snappier always-hop; humanoid frogs (torso+head, spring legs).
   joy2: shared virtual joystick via engine-boot setSteer; touch playfield aim disabled.
   WASD camera-relative — read from the live camera, do not hardcode +X+Z.
   qa1: feet/mechs/followers ride trackElevAt; the apron mesh and lane use that same height.
   mechwalk1: boarded mech lumber walk (steer+solid ignore+mesh sync); pad pilot drives.
   boardall1: each couch pad/companion can board a DIFFERENT free mech at once.
   heliyaw1: 2.5D heli nose faces travel (-faceAngle; drone yaw unchanged).
   subyaw1: 2.5D sub nose faces travel (faceYaw - PI/2; mesh nose +X like truck).
   air1: HeliPad + DronePad · low-poly heli (4) + passenger drone (1–2) · fly over ranch.
   mechgun1: story-mech omnigun FIRE — permanent session kill (Canvas parity).
   mechgun2: omnigun also permanently wrecks house/garage/trees/rocks/fish/fences (session).
   spear1: Rexy 1000 SPEAR (B/RB) knocks trillion ~2s; mash get-up; tipped mesh.
   speartank1: thousand-story SPEAR also wrecks tank (parked/driven); eject pilot; perma-gone.
   spearvis1: SPEAR tip/thrust mesh scaled to 1000-mech (was frog-scale @ y=1.4 — invisible).
   goldsteam1: James trillion GOLD armor + steam pipe billows; omnigun FIRE kept.
   airgun1: heli + passenger-drone FIRE (Space/X/ability) pilot only; climb R/C/RT.
   storymuzzle1: story-mech omnigun muzzle = glowing chest plate (not ankles).
   drivefix1: companions board/drive free Cybertruck(s)+Ripsaw+Tank like primary; mech locks stay.
   drivefix2: one INTERACT edge boards and STAYS (no same-press exit). Mech locks stay.
   drivefix3: companions board sub/heli/drone too; own-mech prefer; garage any local; wider reach.
   earth1: space shows procedural Earth (home) — not ranch grounds in vacuum.
   solarsys1: Solar System layout — Sun center; Moon+station orbit Earth; planet gravity wells;
   spacefix1: dark ground plane; orbit cam locks on planet; ranch pad on Earth surface;
   asteroid belt; Pluto included; scale compressed (labeled). */
(function (global) {
  "use strict";

  var C = global.FroggiesCanon;
  var active = false;
  var hooks = {};
  var keySteer = { x: 0, y: 0 };
  var tapSteer = { x: 0, y: 0 };
  var tapHeld = false;
  var tapMarker = null; /* { sx, sy, life, ang } screen px in canvas */
  var wantInteract = false;
  var wantAbility = false;
  var wantSpear = false; /* spear1 */
  var userZoom = 1; /* ctrl1: pinch/wheel cam zoom (1 = default) */
  var interactOrigin = null; /* world {x,y} from pad that pressed A (multi-local) */
  var interactPadIndex = null; /* pad that pressed A (null = keyboard/HUD/primary) */
  var abilityPadIndex = null; /* pad that pressed B/X (null = keyboard/HUD/primary) */
  var interactConsumed = false;
  /* drivefix2: same cached A-edge must not board and then EXIT.
     Gamepad frame-cache reuses one buttonsPressed object; engine-boot polls it
     again after this tick already consumed the edge. */
  var vehEdgeSeen = {};
  var kbInteractHeld = false;

  function claimHeldEdge(padKey, snap) {
    if (!snap || !snap.buttonsPressed) return true;
    if (vehEdgeSeen[padKey] === snap.buttonsPressed) return false;
    vehEdgeSeen[padKey] = snap.buttonsPressed;
    return true;
  }
  function armVehicleLatch(actor) {
    if (!actor) return;
    actor._vehLatch = 1;
    if (interactPadIndex != null) actor._vehLatchKind = "pad:" + (interactPadIndex | 0);
    else if (kbInteractHeld) actor._vehLatchKind = "kb";
    else actor._vehLatchKind = "tap";
  }
  function vehicleLatched(actor) {
    return !!(actor && actor._vehLatch);
  }
  function releaseOneLatch(actor) {
    if (!actor || !actor._vehLatch) return;
    var k = actor._vehLatchKind || "tap";
    var up = false;
    if (k === "kb") up = !kbInteractHeld;
    else if (k.indexOf("pad:") === 0) {
      var pi = parseInt(k.slice(4), 10);
      var gp = global.SimilarizeGamepad && global.SimilarizeGamepad.pollPad(pi);
      up = !(gp && gp.a);
    } else up = true;
    if (up) actor._vehLatch = 0;
  }
  function releaseVehicleLatches() {
    if (!state) return;
    releaseOneLatch(state);
    if (state.companions) {
      for (var i = 0; i < state.companions.length; i++) releaseOneLatch(state.companions[i].userData);
    }
  }
  function setInteractHeld(on) { kbInteractHeld = !!on; }

  function refreshNearFromLocals() {
    if (!state || !C || state.mode !== "ranch") return;
    var companion = (interactPadIndex != null) ? companionForPad(interactPadIndex) : null;
    /* drivefix3: EXIT mode when THIS input is already boarded (truck/mech/sub/air) */
    function companionBoarded(c) {
      if (!c || !c.userData) return false;
      var u = c.userData;
      return !!(u.inMech || u.inTruck || u.inSub || u.inHeli || u.inDrone);
    }
    if (companion && companionBoarded(companion)) {
      state.near = null;
      return;
    }
    if (!companion && (state.inTruck || state.inMech || state.inSub || state.inHeli || state.inDrone) && inputOwnsBoarded()) {
      state.near = null;
      return;
    }
    var actingFrogId = companion ? companion.userData.frogId : state.frogId;
    var best = null;
    var bestScore = Infinity;
    function consider(wx, wy) {
      /* drivefix3: wider reach so owners board at solid rim */
      var h = C.nearestHotspot(wx, wy, 110, { frogId: actingFrogId });
      if (!h) return;
      /* Prefer free mechs when occupied */
      if (C.isMechHotspot && C.isMechHotspot(h) && whoPilotsMechId(h.id)) {
        var alts = C.HOTSPOTS || [];
        var freeBest = null; var freeD = 1e9;
        for (var ai = 0; ai < alts.length; ai++) {
          var ah = alts[ai];
          if (!(C.isMechHotspot && C.isMechHotspot(ah))) continue;
          if (whoPilotsMechId(ah.id)) continue;
          if (C.canBoardMech && !C.canBoardMech(actingFrogId, ah)) continue;
          var ad = Math.hypot(wx - ah.x, wy - ah.y);
          var aReach = C.boardReachFor ? C.boardReachFor(ah, 110) : ((ah.r || 70) + 36);
          if (ad < aReach && ad < freeD) { freeD = ad; freeBest = ah; }
        }
        if (freeBest) h = freeBest;
        else return;
      }
      if (C.isTruckHotspot && C.isTruckHotspot(h) && h.mode !== "shared" && whoPilotsTruckId(h.id)) {
        var talts = C.HOTSPOTS || [];
        var tFree = null; var tD = 1e9;
        for (var ti = 0; ti < talts.length; ti++) {
          var th = talts[ti];
          if (!(C.isTruckHotspot && C.isTruckHotspot(th))) continue;
          if (th.mode === "shared") continue;
          if (whoPilotsTruckId(th.id)) continue;
          var td = Math.hypot(wx - th.x, wy - th.y);
          var tReach = C.boardReachFor ? C.boardReachFor(th, 110) : ((th.r || 70) + 36);
          if (td < tReach && td < tD) { tD = td; tFree = th; }
        }
        if (tFree) h = tFree;
        else return;
      }
      var d = Math.hypot(wx - h.x, wy - h.y);
      var board = (C.isTruckHotspot && C.isTruckHotspot(h)) || (C.isMechHotspot && C.isMechHotspot(h))
        || (C.isSubHotspot && C.isSubHotspot(h)) || (C.isAirHotspot && C.isAirHotspot(h));
      var score = d - (board ? 8 : 0);
      if (score < bestScore) { bestScore = score; best = h; }
    }
    /* interact2: pad-sourced interact only uses that frog's origin — never another local */
    if (interactPadIndex != null && interactOrigin) {
      consider(interactOrigin.x, interactOrigin.y);
      state.near = best;
      return;
    }
    if (interactOrigin) {
      consider(interactOrigin.x, interactOrigin.y);
    }
    var wp = threeToWorld(state.player.position.x, state.player.position.z);
    consider(wp.x, wp.y);
    /* HUD/keyboard near: primary only (not other pad frogs) for shared button label */
    state.near = best;
  }


  /** interact2: does current interactPadIndex own the boarded vehicle? */
  function inputOwnsBoarded() {
    if (!state) return false;
    if (!state.inMech && !state.inTruck && !state.inSub && !state.inHeli && !state.inDrone) return false;
    var pilot = null;
    if (state.inMech) pilot = state.mechPilotPadIndex;
    else if (state.inTruck) pilot = state.truckPilotPadIndex;
    else if (state.inSub) pilot = state.truckPilotPadIndex;
    else if (state.inHeli || state.inDrone) pilot = state.truckPilotPadIndex; /* reuse pilot pad field */
    /* null interactPad = keyboard/HUD — owns if pilot is null (keyboard boarded) or primary pad */
    if (interactPadIndex == null) {
      return pilot == null || pilot === state.primaryPadIndex;
    }
    return pilot != null && (interactPadIndex | 0) === (pilot | 0);
  }

  function companionForPad(padIndex) {
    if (padIndex == null || !state || !state.companions) return null;
    for (var i = 0; i < state.companions.length; i++) {
      var c = state.companions[i];
      if (c.userData.local && c.userData.padIndex != null &&
          (c.userData.padIndex | 0) === (padIndex | 0)) return c;
    }
    return null;
  }

  /** boardall1: solidId for a mech hotspot id */
  function mechSidOf(id) {
    if (!id) return null;
    if (C && C.mechSolidId) return C.mechSolidId(id);
    return String(id).replace(/^mech-/, "mech");
  }

  /** Who already pilots this mech? Returns "primary" | companion mesh | null */
  function whoPilotsMechId(hotId) {
    if (!state || !hotId) return null;
    var sid = mechSidOf(hotId);
    if (state.inMech && mechSidOf(state.mechId) === sid) return "primary";
    if (state.companions) {
      for (var i = 0; i < state.companions.length; i++) {
        var c = state.companions[i];
        if (c.userData.inMech && mechSidOf(c.userData.mechId) === sid) return c;
      }
    }
    return null;
  }

  function exitCompanionMech(c) {
    if (!c || !c.userData.inMech) return;
    if (vehicleLatched(c.userData)) return; /* drivefix2: same press must not hop out */
    var parkW = threeToWorld(c.position.x, c.position.z);
    var parkMid = c.userData.mechId || "mech";
    if (C.setVehiclePark) C.setVehiclePark(parkMid, parkW.x, parkW.y);
    c.userData.inMech = false;
    c.userData.mechId = null;
    c.userData.mechStories = 0;
    c.visible = true;
    state.toast = "Mech parked · walking"; state.toastT = 1.8; state.exitTipT = 0;
    if (hooks.onToast) hooks.onToast(state.toast);
  }

  function boardCompanionMech(c, hot) {
    if (!c || !hot) return false;
    var id = hot.id;
    var cid = c.userData.frogId;
    if (C.canBoardMech && !C.canBoardMech(cid, hot)) {
      state.toast = C.mechDeniedTip ? C.mechDeniedTip(cid, hot) : "Wrong froggy for this mech";
      state.toastT = 2.2;
      if (hooks.onToast) hooks.onToast(state.toast);
      return false;
    }
    if (whoPilotsMechId(id)) {
      state.toast = "Already boarded · pick another"; state.toastT = 1.6;
      if (hooks.onToast) hooks.onToast(state.toast);
      return false;
    }
    var mp = worldToThree(hot.x, hot.y);
    c.position.x = mp.x; c.position.z = mp.z;
    c.userData.inMech = true;
    c.userData.mechId = id;
    c.userData.mechStories = hot.stories || 10;
    c.userData.vx = 0; c.userData.vz = 0;
    c.userData.zLift = 0; c.userData.zVel = 0;
    c.userData.walkPhase = 0;
    c.visible = false;
    state.scrap += 1;
    state.toast = "Boarding " + (C.mechStoriesLabel ? C.mechStoriesLabel(c.userData.mechStories) : (c.userData.mechStories + "-story mech")) + " · FIRE (Space / X / button)!";
    state.exitTipT = 2.4; state.toastT = 2.5;
    if (hooks.onToast) hooks.onToast(state.toast);
    armVehicleLatch(c.userData);
    return true;
  }

  /** drivefix1: who already drives this solo truck/ripsaw/tank? */
  function whoPilotsTruckId(hotId) {
    if (!state || !hotId) return null;
    var hid = String(hotId);
    if (state.inTruck && state.truckMode !== "shared" && state.truckId === hid) return "primary";
    if (state.companions) {
      for (var i = 0; i < state.companions.length; i++) {
        var c = state.companions[i];
        if (c.userData.inTruck && c.userData.truckMode !== "shared" && c.userData.truckId === hid) return c;
      }
    }
    return null;
  }

  function exitCompanionTruck(c) {
    if (!c || !c.userData.inTruck) return;
    if (vehicleLatched(c.userData)) return; /* drivefix2 */
    var parkW = threeToWorld(c.position.x, c.position.z);
    var parkTid = c.userData.truckId || "truck";
    if (C.setVehiclePark) C.setVehiclePark(parkTid, parkW.x, parkW.y);
    c.userData.inTruck = false;
    c.userData.truckId = null;
    c.userData.truckMode = null;
    c.userData.vehicleStyle = null;
    c.userData.zLift = 0; c.userData.zVel = 0;
    c.visible = true;
    state.toast = "Parked · walking"; state.toastT = 1.8; state.exitTipT = 0;
    if (hooks.onToast) hooks.onToast(state.toast);
  }

  function boardCompanionTruck(c, hot) {
    if (!c || !hot) return false;
    var id = hot.id;
    /* Shared pile-in stays primary-started (companions already ride when primary boards) */
    if (hot.mode === "shared") {
      state.toast = "Shared truck · primary INTERACT to pile in"; state.toastT = 1.6;
      if (hooks.onToast) hooks.onToast(state.toast);
      return false;
    }
    if (whoPilotsTruckId(id)) {
      state.toast = "Already boarded · pick another"; state.toastT = 1.6;
      if (hooks.onToast) hooks.onToast(state.toast);
      return false;
    }
    if (c.userData.inMech) exitCompanionMech(c);
    var tp = worldToThree(hot.x, hot.y);
    c.position.x = tp.x; c.position.z = tp.z;
    c.userData.inTruck = true;
    c.userData.truckId = id;
    c.userData.truckMode = hot.mode || "solo";
    c.userData.vehicleStyle = hot.vehicleStyle || (C.vehicleStyleOf ? C.vehicleStyleOf(hot) : "cybertruck");
    c.userData.vx = 0; c.userData.vz = 0;
    c.userData.zLift = 0; c.userData.zVel = 0;
    c.visible = false;
    state.scrap += 1;
    var vs = c.userData.vehicleStyle;
    state.toast = vs === "ripsaw" ? "Driving Ripsaw · tracked · hit the jumps!"
      : vs === "tank" ? (C.tankDrivingTip ? C.tankDrivingTip() : "Driving Tank · FIRE · EXIT INTERACT")
      : "Driving Cybertruck · hit the jumps!";
    state.exitTipT = 2.4; state.toastT = 2.5;
    if (hooks.onToast) hooks.onToast(state.toast);
    armVehicleLatch(c.userData);
    return true;
  }

  function whoPilotsSub() {
    if (!state) return null;
    if (state.inSub) return "primary";
    if (state.companions) {
      for (var si = 0; si < state.companions.length; si++) {
        var sc = state.companions[si];
        if (sc.userData.inSub) return sc;
      }
    }
    return null;
  }

  function exitCompanionSub(c) {
    if (!c || !c.userData.inSub) return;
    if (vehicleLatched(c.userData)) return;
    var parkW = threeToWorld(c.position.x, c.position.z);
    var parkSid = c.userData.subId || "submarine";
    if (C.setVehiclePark) C.setVehiclePark(parkSid, parkW.x, parkW.y);
    c.userData.inSub = false;
    c.userData.subId = null;
    c.userData.vehicleStyle = null;
    c.userData.zLift = 0; c.userData.zVel = 0;
    c.visible = true;
    if (state.parkedSub && !state.inSub) {
      var psp = worldToThree(parkW.x, parkW.y);
      state.parkedSub.position.set(psp.x, 0.22, psp.z);
      state.parkedSub.visible = true;
    }
    state.toast = (C.inPond && C.inPond(parkW.x, parkW.y)) ? "Surfaced · swimming" : "Sub parked · shore";
    state.toastT = 1.8; state.exitTipT = 0;
    if (hooks.onToast) hooks.onToast(state.toast);
  }

  function boardCompanionSub(c, hot) {
    if (!c || !hot) return false;
    if (whoPilotsSub()) {
      state.toast = "Already boarded · pick another"; state.toastT = 1.6;
      if (hooks.onToast) hooks.onToast(state.toast);
      return false;
    }
    if (c.userData.inMech) exitCompanionMech(c);
    if (c.userData.inTruck) exitCompanionTruck(c);
    var sp = worldToThree(hot.x, hot.y);
    c.position.x = sp.x; c.position.z = sp.z;
    c.userData.inSub = true;
    c.userData.subId = hot.id || "submarine";
    c.userData.vehicleStyle = "submarine";
    c.userData.vx = 0; c.userData.vz = 0;
    c.userData.zLift = 0; c.userData.zVel = 0;
    c.visible = false;
    if (!state.parkedSub) {
      state.parkedSub = makeSubMesh(hex((C.FROG_DEFS[c.userData.frogId] || {}).color || "#38bdf8"));
      scene.add(state.parkedSub);
    }
    state.parkedSub.visible = true;
    state.parkedSub.position.set(sp.x, -0.35, sp.z);
    state.scrap += 1;
    state.toast = "Submarine · diving underwater · EXIT INTERACT / E";
    state.toastT = 2.4; state.exitTipT = 2.4;
    if (hooks.onToast) hooks.onToast(state.toast);
    armVehicleLatch(c.userData);
    return true;
  }

  function companionAirKind(c) {
    if (!c || !c.userData) return null;
    if (c.userData.inHeli) return "heli";
    if (c.userData.inDrone) return "drone";
    return null;
  }

  function exitCompanionAir(c) {
    if (!c) return;
    var kind = companionAirKind(c);
    if (!kind) return;
    if (vehicleLatched(c.userData)) return;
    var AirX = global.FroggiesAir;
    if (!state.airWorld) state.airWorld = { air: null, hotspots: [] };
    var wpos = threeToWorld(c.position.x, c.position.z);
    var frogX = {
      id: c.userData.frogId, inHeli: !!c.userData.inHeli, inDrone: !!c.userData.inDrone,
      airSeat: c.userData.airSeat, x: wpos.x, y: wpos.y, z: (c.userData.zLift || 0) / 0.02
    };
    var craftX = AirX ? AirX.ensureCraft(state.airWorld, kind) : null;
    if (craftX) {
      craftX.x = wpos.x; craftX.y = wpos.y; craftX.z = (c.userData.zLift || 0) / 0.02;
      craftX.vx = (c.userData.vx || 0) / 0.02; craftX.vy = (c.userData.vz || 0) / 0.02;
      craftX.vz = (c.userData.zVel || 0) / 0.02;
    }
    var resX = AirX ? AirX.boardAir(state.airWorld, [frogX], frogX, { kind: kind, id: kind }) : { ok: false };
    if (resX && resX.denied) {
      state.toast = resX.toast || "Land + slow · then INTERACT to hop out";
      state.toastT = 1.8;
      if (hooks.onToast) hooks.onToast(state.toast);
      return;
    }
    c.userData.inHeli = false; c.userData.inDrone = false;
    c.userData.airKind = null; c.userData.airSeat = null;
    c.userData.zLift = 0; c.userData.zVel = 0;
    c.visible = true;
    if (craftX && C.setVehiclePark) C.setVehiclePark(kind, craftX.x, craftX.y);
    /* Park mesh if primary not flying same craft */
    if (!(state.inHeli && kind === "heli") && !(state.inDrone && kind === "drone")) {
      for (var pai = 0; pai < (state.parkedAir || []).length; pai++) {
        if (state.parkedAir[pai].kind === kind && state.parkedAir[pai].mesh && craftX) {
          var pp = worldToThree(craftX.x, craftX.y);
          state.parkedAir[pai].mesh.position.set(pp.x, airCraftDeckY(craftX.x, craftX.y, 0), pp.z);
          state.parkedAir[pai].mesh.visible = true;
        }
      }
    }
    state.toast = (resX && resX.toast) || "Parked · walking";
    state.toastT = 1.8; state.exitTipT = 0;
    if (hooks.onToast) hooks.onToast(state.toast);
  }

  function boardCompanionAir(c, hot) {
    if (!c || !hot) return false;
    var kind = C.airKindOf ? C.airKindOf(hot) : (hot.kind === "drone" ? "drone" : "heli");
    var AirB = global.FroggiesAir;
    if (!state.airWorld) state.airWorld = { air: null, hotspots: (C.HOTSPOTS || []).slice() };
    if (c.userData.inMech) exitCompanionMech(c);
    if (c.userData.inTruck) exitCompanionTruck(c);
    if (c.userData.inSub) exitCompanionSub(c);
    var ww = threeToWorld(c.position.x, c.position.z);
    var frogB = {
      id: c.userData.frogId, inHeli: false, inDrone: false,
      x: hot.x, y: hot.y, z: 0
    };
    /* Snap near craft for boardAir nearCraft check */
    var craft0 = AirB ? AirB.ensureCraft(state.airWorld, kind) : null;
    if (craft0) { frogB.x = craft0.x; frogB.y = craft0.y; }
    else { frogB.x = hot.x; frogB.y = hot.y; }
    var resB = AirB ? AirB.boardAir(state.airWorld, [frogB], frogB, hot) : { ok: false };
    if (resB && resB.denied) {
      state.toast = resB.toast || "Cannot board";
      state.toastT = 1.8;
      if (hooks.onToast) hooks.onToast(state.toast);
      return false;
    }
    if (!(resB && resB.ok)) return false;
    var tpB = worldToThree(frogB.x, frogB.y);
    c.position.x = tpB.x; c.position.z = tpB.z;
    c.userData.inHeli = kind === "heli";
    c.userData.inDrone = kind === "drone";
    c.userData.airKind = kind;
    c.userData.airSeat = frogB.airSeat != null ? frogB.airSeat : 0;
    c.userData.vx = 0; c.userData.vz = 0;
    c.userData.zLift = 0; c.userData.zVel = 0;
    c.visible = false;
    state.scrap += 1;
    state.toast = resB.toast || (kind === "drone" ? "Passenger drone · fly!" : "Helicopter · fly!");
    state.toastT = 2.6; state.exitTipT = 2.6;
    if (hooks.onToast) hooks.onToast(state.toast);
    armVehicleLatch(c.userData);
    return true;
  }

  function setInteractFromPad(padIndex, wx, wy) {
    var pIdx = padIndex != null ? (padIndex | 0) : null;
    if (pIdx != null && global.SimilarizeGamepad) {
      var snap = global.SimilarizeGamepad.pollPad(pIdx);
      if (snap && snap.buttonsPressed && snap.buttonsPressed.a && !claimHeldEdge("p" + pIdx, snap)) return;
    }
    wantInteract = true;
    interactPadIndex = pIdx;
    if (wx != null && wy != null) interactOrigin = { x: wx, y: wy };
  }

  function mergedSteer() {
    /* While piloting a mech or truck, the pad that boarded drives (padIndex local or primary) */
    var steerPad = null;
    if (state && state.inMech && state.mechPilotPadIndex != null) steerPad = state.mechPilotPadIndex;
    else if (state && state.inTruck && state.truckPilotPadIndex != null) steerPad = state.truckPilotPadIndex;
    else if (state && state.primaryPadIndex != null) steerPad = state.primaryPadIndex;
    /* Primary local pad (if claimed) OR keyboard/joy OR tap */
    if (steerPad != null && global.SimilarizeGamepad) {
      var gp = global.SimilarizeGamepad.pollPad(steerPad);
      if (gp && gp.connected) {
        var px = gp.lx || 0, py = gp.ly || 0;
        if (gp.dpad) {
          if (gp.dpad.l) px = -1;
          if (gp.dpad.r) px = 1;
          if (gp.dpad.u) py = -1;
          if (gp.dpad.d) py = 1;
        }
        if (px || py) return { x: px, y: py };
        /* edge buttons for primary pad */
        if (gp.buttonsPressed) {
          if (gp.buttonsPressed.a) {
            if (state && state.player) {
              var ow = threeToWorld(state.player.position.x, state.player.position.z);
              setInteractFromPad(steerPad, ow.x, ow.y);
            } else {
              setInteractFromPad(steerPad, null, null);
            }
          }
          if (gp.buttonsPressed.b || gp.buttonsPressed.x) {
            wantAbility = true;
            abilityPadIndex = steerPad;
          }
          if (gp.buttonsPressed.rb) {
            wantSpear = true;
            abilityPadIndex = steerPad;
          }
        }
      }
    }
    if (keySteer.x || keySteer.y) return keySteer;
    return tapSteer;
  }

  var renderer = null;
  var scene = null;
  var camera = null;
  var raf = 0;
  var state = null;
  var clock = null;

  function destroy() {
    active = false;
    keySteer.x = keySteer.y = 0; tapSteer.x = tapSteer.y = 0; tapHeld = false; tapMarker = null;
    wantInteract = wantAbility = wantSpear = false;
    interactPadIndex = abilityPadIndex = null;
    interactOrigin = null;
    var mel = document.getElementById("tap-steer-marker");
    if (mel) mel.classList.remove("is-on");
    if (raf) {
      cancelAnimationFrame(raf);
      raf = 0;
    }
    if (renderer) {
      try {
        renderer.dispose();
        var canvas = renderer.domElement;
        if (canvas && canvas.parentNode) canvas.parentNode.removeChild(canvas);
      } catch (e) { /* ignore */ }
      renderer = null;
    }
    scene = null;
    camera = null;
    state = null;
    var host = document.getElementById("engine-host");
    if (host) host.innerHTML = "";
  }

  function setSteer(x, y) {
    keySteer.x = x;
    keySteer.y = y;
  }
  function pulseInteract(padIndex) {
    /* interact2: optional padIndex from engine-boot; null = HUD/keyboard → primary only */
    /* drivefix2: ignore a second read of the same cached A edge (board then EXIT). */
    if (padIndex != null && padIndex !== undefined && padIndex !== "" && global.SimilarizeGamepad) {
      var snapI = global.SimilarizeGamepad.pollPad(padIndex | 0);
      if (snapI && snapI.buttonsPressed && snapI.buttonsPressed.a && !claimHeldEdge("p" + (padIndex | 0), snapI)) return;
    }
    wantInteract = true;
    if (padIndex != null && padIndex !== undefined && padIndex !== "") {
      interactPadIndex = padIndex | 0;
      var comp = companionForPad(interactPadIndex);
      if (comp) {
        var cow = threeToWorld(comp.position.x, comp.position.z);
        interactOrigin = { x: cow.x, y: cow.y };
      } else if (state && state.player &&
          (state.primaryPadIndex == null || state.primaryPadIndex === interactPadIndex)) {
        var ow = threeToWorld(state.player.position.x, state.player.position.z);
        interactOrigin = { x: ow.x, y: ow.y };
      }
    } else {
      interactPadIndex = null;
      interactOrigin = null;
    }
  }
  function pulseAbility(padIndex) {
    wantAbility = true;
    if (padIndex != null && padIndex !== undefined && padIndex !== "") {
      abilityPadIndex = padIndex | 0;
    } else {
      abilityPadIndex = null;
    }
  }
  function pulseSpear(padIndex) {
    /* spear1: dedicated SPEAR pulse (B / RB) — never steals FIRE */
    wantSpear = true;
    if (padIndex != null && padIndex !== undefined && padIndex !== "") {
      abilityPadIndex = padIndex | 0;
    }
  }
  function isActive() { return active; }

  function hex(c) {
    return parseInt(String(c).replace("#", ""), 16);
  }

  /* hop4: humanoid frog — torso + head + big springy hind legs (animate via setFrogSpring) */
  function makeFrogMesh(def, scale) {
    var s = (scale == null ? 1.55 : scale);
    var g = new THREE.Group();
    var bodyCol = hex(def.color);
    var accentCol = hex(def.accent);
    var bodyMat = new THREE.MeshStandardMaterial({
      color: bodyCol, roughness: 0.45, metalness: 0.08,
      emissive: bodyCol, emissiveIntensity: 0.32,
    });
    var accentMat = new THREE.MeshStandardMaterial({ color: accentCol, roughness: 0.5 });
    /* Torso (ellipse-ish) */
    var torso = new THREE.Mesh(new THREE.SphereGeometry(0.42 * s, 14, 12), bodyMat);
    torso.scale.set(0.95, 1.15, 0.75);
    torso.position.y = 0.72 * s;
    torso.castShadow = true;
    g.add(torso);
    var belly = new THREE.Mesh(
      new THREE.SphereGeometry(0.22 * s, 10, 8),
      new THREE.MeshStandardMaterial({ color: 0xfef3c7, roughness: 0.7 })
    );
    belly.position.set(0, 0.68 * s, 0.22 * s);
    belly.scale.set(0.9, 1.1, 0.55);
    g.add(belly);
    /* Head */
    var head = new THREE.Mesh(new THREE.SphereGeometry(0.32 * s, 12, 10), bodyMat);
    head.position.y = 1.28 * s;
    head.castShadow = true;
    g.add(head);
    /* Hat */
    var hat = new THREE.Mesh(
      new THREE.ConeGeometry(0.26 * s, 0.34 * s, 8),
      new THREE.MeshStandardMaterial({
        color: hex(def.hat), roughness: 0.55,
        emissive: hex(def.hat), emissiveIntensity: 0.15,
      })
    );
    hat.position.y = 1.62 * s;
    hat.castShadow = true;
    g.add(hat);
    /* Eyes on head (+Z = face) */
    var eyeWhite = new THREE.MeshStandardMaterial({ color: 0xffffff, emissive: 0xffffff, emissiveIntensity: 0.1 });
    var eyePupil = new THREE.MeshStandardMaterial({ color: accentCol });
    function eye(ox) {
      var ew = new THREE.Mesh(new THREE.SphereGeometry(0.1 * s, 8, 8), eyeWhite);
      ew.position.set(ox, 1.32 * s, 0.26 * s);
      g.add(ew);
      var ep = new THREE.Mesh(new THREE.SphereGeometry(0.045 * s, 6, 6), eyePupil);
      ep.position.set(ox, 1.32 * s, 0.34 * s);
      g.add(ep);
    }
    eye(-0.12 * s); eye(0.12 * s);
    /* Arms — refs for swim stroke */
    var armMeshes = [];
    for (var ai = 0; ai < 2; ai++) {
      var aside = ai === 0 ? -1 : 1;
      var arm = new THREE.Mesh(new THREE.CylinderGeometry(0.05 * s, 0.06 * s, 0.32 * s, 6), bodyMat);
      arm.position.set(aside * 0.38 * s, 0.78 * s, 0.05 * s);
      arm.rotation.z = aside * 0.55;
      arm.userData.basePos = arm.position.clone();
      arm.userData.side = aside;
      g.add(arm);
      armMeshes.push(arm);
    }
    /* Big springy hind legs — thigh + shin + foot; refs for hop spring */
    var legRoots = [];
    for (var li = 0; li < 2; li++) {
      var side = li === 0 ? -1 : 1;
      var root = new THREE.Group();
      root.position.set(side * 0.22 * s, 0.48 * s, -0.06 * s);
      var thigh = new THREE.Mesh(new THREE.CylinderGeometry(0.09 * s, 0.11 * s, 0.45 * s, 7), accentMat);
      thigh.position.y = -0.2 * s;
      thigh.castShadow = true;
      root.add(thigh);
      var knee = new THREE.Group();
      knee.position.y = -0.42 * s;
      var shin = new THREE.Mesh(new THREE.CylinderGeometry(0.07 * s, 0.09 * s, 0.4 * s, 7), bodyMat);
      shin.position.y = -0.18 * s;
      shin.castShadow = true;
      knee.add(shin);
      var foot = new THREE.Mesh(new THREE.SphereGeometry(0.11 * s, 8, 6), accentMat);
      foot.scale.set(1.4, 0.45, 1.1);
      foot.position.set(0, -0.38 * s, 0.06 * s);
      knee.add(foot);
      root.add(knee);
      g.add(root);
      legRoots.push({ root: root, knee: knee, side: side, s: s });
    }
    /* Ground selection ring */
    var ring = new THREE.Mesh(
      new THREE.RingGeometry(0.5 * s, 0.68 * s, 28),
      new THREE.MeshBasicMaterial({
        color: bodyCol, transparent: true, opacity: 0.55, side: THREE.DoubleSide, depthWrite: false,
      })
    );
    ring.rotation.x = -Math.PI / 2;
    ring.position.y = 0.09;
    ring.renderOrder = 2;
    g.add(ring);
    g.userData.ring = ring;
    g.userData.frogId = def.id;
    g.userData.legRoots = legRoots;
    g.userData.armMeshes = armMeshes;
    g.userData.torso = torso;
    g.userData.head = head;
    g.userData.frogScale = s;
    g.castShadow = true;
    setFrogSpring(g, 0);
    return g;
  }

  /* hop4: spring=0 tuck on land; spring=1 legs kick out mid-hop */
  function setFrogSpring(mesh, spring) {
    if (!mesh || !mesh.userData.legRoots) return;
    var sp = Math.max(0, Math.min(1.2, spring || 0));
    var legs = mesh.userData.legRoots;
    for (var i = 0; i < legs.length; i++) {
      var L = legs[i];
      var side = L.side;
      /* Tuck: thighs under body; spring: thighs kick back/out, shin unfolds */
      L.root.rotation.x = -0.55 + sp * 1.15;
      L.root.rotation.z = side * (0.35 - sp * 0.15);
      L.knee.rotation.x = 1.35 - sp * 1.55;
    }
  }

  /* mech7: freestyle swim — flatter body, alternating arm/leg stroke, no hop bounce */
  function setFrogSwim(mesh, phase, on) {
    if (!mesh || !mesh.userData.legRoots) return;
    if (!on) {
      mesh.rotation.x = 0;
      if (mesh.userData.torso) mesh.userData.torso.rotation.x = 0;
      if (mesh.userData.head) mesh.userData.head.rotation.x = 0;
      var arms0 = mesh.userData.armMeshes || [];
      for (var ai = 0; ai < arms0.length; ai++) {
        var a0 = arms0[ai];
        var side0 = a0.userData.side || (ai === 0 ? -1 : 1);
        if (a0.userData.basePos) a0.position.copy(a0.userData.basePos);
        a0.rotation.set(0, 0, side0 * 0.55);
      }
      return;
    }
    var ph = phase || 0;
    var stroke = Math.sin(ph);
    var strokeB = Math.sin(ph + Math.PI);
    /* Body flatter / horizontal in water */
    mesh.rotation.x = -1.05;
    if (mesh.userData.torso) mesh.userData.torso.rotation.x = 0.12;
    if (mesh.userData.head) mesh.userData.head.rotation.x = 0.35;
    var legs = mesh.userData.legRoots;
    for (var i = 0; i < legs.length; i++) {
      var L = legs[i];
      var kick = (L.side < 0 ? stroke : strokeB);
      L.root.rotation.x = 0.15 + kick * 0.85;
      L.root.rotation.z = L.side * 0.55;
      L.knee.rotation.x = 0.55 - kick * 0.7;
    }
    var arms = mesh.userData.armMeshes || [];
    for (var aj = 0; aj < arms.length; aj++) {
      var arm = arms[aj];
      var side = arm.userData.side || (aj === 0 ? -1 : 1);
      var padd = (side < 0 ? strokeB : stroke);
      arm.rotation.x = -0.35 + padd * 1.1;
      arm.rotation.z = side * (0.85 + padd * 0.25);
      arm.rotation.y = side * padd * 0.35;
      if (arm.userData.basePos) {
        arm.position.set(
          arm.userData.basePos.x,
          arm.userData.basePos.y + padd * 0.04 * (mesh.userData.frogScale || 1),
          arm.userData.basePos.z + padd * 0.08 * (mesh.userData.frogScale || 1)
        );
      }
    }
  }

  function labelSprite(text, color) {
    /* polish9/10: quieter plate nameplates */
    var canvas = document.createElement("canvas");
    canvas.width = 256;
    canvas.height = 64;
    var ctx = canvas.getContext("2d");
    ctx.clearRect(0, 0, 256, 64);
    ctx.font = "bold 22px Segoe UI, system-ui, sans-serif";
    ctx.textAlign = "center";
    var tw = Math.min(200, ctx.measureText(text).width + 22);
    ctx.fillStyle = "rgba(15, 23, 42, 0.62)";
    ctx.strokeStyle = color || "#fef3c7";
    ctx.lineWidth = 2;
    ctx.beginPath();
    if (ctx.roundRect) ctx.roundRect(128 - tw * 0.5, 18, tw, 28, 8);
    else ctx.rect(128 - tw * 0.5, 18, tw, 28);
    ctx.fill(); ctx.stroke();
    ctx.fillStyle = color || "#fff";
    ctx.fillText(text, 128, 38);
    var tex = new THREE.CanvasTexture(canvas);
    var mat = new THREE.SpriteMaterial({ map: tex, transparent: true, depthTest: false, opacity: 0.85 });
    var spr = new THREE.Sprite(mat);
    spr.scale.set(1.9, 0.48, 1);
    return spr;
  }

  /* NEVER chain .position on scene.add() — Object3D.add returns the scene, which
     silently moved scene.position and desynced the camera from all meshes. */
  function addLabel(text, color, x, y, z) {
    var spr = labelSprite(text, color);
    spr.position.set(x, y, z);
    scene.add(spr);
    return spr;
  }

  function worldToThree(x, y) {
    // Map ranch coords → three XZ plane (Y up)
    return { x: (x - C.MAP_W * 0.5) * 0.02, z: (y - C.MAP_H * 0.5) * 0.02 };
  }

  function threeToWorld(tx, tz) {
    return {
      x: tx / 0.02 + C.MAP_W * 0.5,
      y: tz / 0.02 + C.MAP_H * 0.5,
    };
  }

  /* qa1: scene Y of the shared deck. Same trackElevAt the feet stand on, times the XZ scale. */
  function ranchGroundY(wx, wy) {
    if (!C.onTrack || !C.trackElevAt || !C.onTrack(wx, wy)) return 0;
    return (C.trackElevAt(wx, wy) || 0) * 0.02;
  }
  /* padfix1: parked air craft sit above raised H/D pad discs (and orange zone) */
  var AIR_PAD_LIFT = 0.16;
  function airCraftDeckY(wx, wy, zLift) {
    var base = ranchGroundY(wx, wy) + AIR_PAD_LIFT;
    if (zLift != null && zLift > base) return zLift;
    return base;
  }

  /* qa1: screen basis from the camera's flattened view. right = forward × up.
     Check: fwd (0, -1) → right (1, 0). Old isometric fwd (-1,-1) still matches three-dir1. */
  function groundBasis(fwdX, fwdZ) {
    var fl = Math.hypot(fwdX, fwdZ) || 1;
    var fx = fwdX / fl, fz = fwdZ / fl;
    return { fx: fx, fz: fz, rx: -fz, rz: fx };
  }

  function cameraGroundBasis() {
    if (!camera) return groundBasis(-1, -1);
    camera.updateMatrixWorld();
    var e = camera.matrixWorld.elements;
    /* local +Z column is elements 8,10; camera looks down -Z */
    return groundBasis(-e[8], -e[10]);
  }

  function makeTruckMesh(accentHex) {
    /* polish8: angular stainless Cybertruck — light bar + wheel arches */
    var g = new THREE.Group();
    var bodyMat = new THREE.MeshStandardMaterial({ color: 0xc5ced8, metalness: 0.78, roughness: 0.22 });
    var body = new THREE.Mesh(new THREE.BoxGeometry(1.85, 0.36, 0.82), bodyMat);
    body.position.set(0.02, 0.34, 0); body.castShadow = true; g.add(body);
    /* Steep wedge nose */
    var nose = new THREE.Mesh(
      new THREE.BoxGeometry(0.72, 0.2, 0.78),
      new THREE.MeshStandardMaterial({ color: 0xa8b4c4, metalness: 0.8, roughness: 0.2 })
    );
    nose.position.set(0.95, 0.3, 0); nose.rotation.z = -0.32; g.add(nose);
    /* Cabin glass tint via accent */
    var cab = new THREE.Mesh(
      new THREE.BoxGeometry(0.58, 0.38, 0.68),
      new THREE.MeshStandardMaterial({ color: accentHex, metalness: 0.45, roughness: 0.3, emissive: accentHex, emissiveIntensity: 0.14 })
    );
    cab.position.set(0.12, 0.62, 0); cab.rotation.z = -0.08; g.add(cab);
    /* Bed rails */
    var railM = new THREE.MeshStandardMaterial({ color: 0x64748b, metalness: 0.55, roughness: 0.35 });
    var railL = new THREE.Mesh(new THREE.BoxGeometry(0.75, 0.1, 0.05), railM);
    railL.position.set(-0.5, 0.5, 0.36); g.add(railL);
    var railR = railL.clone(); railR.position.z = -0.36; g.add(railR);
    /* Full-width light bar */
    var hl = new THREE.Mesh(
      new THREE.BoxGeometry(0.1, 0.1, 0.7),
      new THREE.MeshStandardMaterial({ color: 0xfef08a, emissive: 0xfbbf24, emissiveIntensity: 0.65 })
    );
    hl.position.set(1.28, 0.32, 0); g.add(hl);
    /* Wheel arches (half-torus flares) */
    var archM = new THREE.MeshStandardMaterial({ color: 0x94a3b8, metalness: 0.6, roughness: 0.35 });
    g.userData.arches = [];
    function arch(x, z) {
      var a = new THREE.Mesh(new THREE.TorusGeometry(0.22, 0.03, 6, 10, Math.PI), archM);
      a.rotation.y = Math.PI / 2; a.rotation.z = Math.PI; a.position.set(x, 0.22, z); g.add(a);
      a.userData.baseY = 0.22;
      g.userData.arches.push(a);
    }
    arch(-0.5, 0.42); arch(-0.5, -0.42); arch(0.55, 0.42); arch(0.55, -0.42);
    /* Wheels — track3: refs for live monster scale */
    var wheelM = new THREE.MeshStandardMaterial({ color: 0x0f172a, roughness: 0.8 });
    g.userData.wheels = [];
    function wheel(x, z) {
      var w = new THREE.Mesh(new THREE.CylinderGeometry(0.18, 0.18, 0.14, 10), wheelM);
      w.rotation.z = Math.PI / 2; w.position.set(x, 0.16, z); g.add(w);
      w.userData.baseY = 0.16; w.userData.baseX = x; w.userData.baseZ = z;
      g.userData.wheels.push(w);
    }
    wheel(-0.5, 0.42); wheel(-0.5, -0.42); wheel(0.55, 0.42); wheel(0.55, -0.42);
    var edge = new THREE.LineSegments(
      new THREE.EdgesGeometry(new THREE.BoxGeometry(1.85, 0.36, 0.82)),
      new THREE.LineBasicMaterial({ color: 0x111827 })
    );
    edge.position.copy(body.position); g.add(edge);
    g.userData.bodyMat = bodyMat;
    /* truck1: kid-toy scale vs frog (~1.7 diam) — bigger than frog, not a building */
    var ts = (C.TRUCK_VIS && C.TRUCK_VIS.threeScale != null) ? C.TRUCK_VIS.threeScale : 2.05;
    g.scale.setScalar(ts);
    g.userData.truckScale = ts;
    return g;
  }

  function makeRipsawMesh(accentHex) {
    /* mech1: low tracked wedge + cage — reads as Ripsaw, not a wheeled truck */
    var g = new THREE.Group();
    var hullM = new THREE.MeshStandardMaterial({ color: accentHex || 0xa8a29e, metalness: 0.55, roughness: 0.4 });
    var trackM = new THREE.MeshStandardMaterial({ color: 0x1e293b, roughness: 0.85 });
    var hull = new THREE.Mesh(new THREE.BoxGeometry(1.9, 0.28, 0.7), hullM);
    hull.position.set(0.05, 0.32, 0); hull.castShadow = true; g.add(hull);
    var nose = new THREE.Mesh(new THREE.BoxGeometry(0.7, 0.18, 0.66), hullM);
    nose.position.set(1.05, 0.28, 0); nose.rotation.z = -0.38; g.add(nose);
    function track(z) {
      var t = new THREE.Mesh(new THREE.BoxGeometry(2.0, 0.22, 0.22), trackM);
      t.position.set(0, 0.14, z); g.add(t);
      for (var i = 0; i < 6; i++) {
        var pad = new THREE.Mesh(new THREE.BoxGeometry(0.16, 0.06, 0.24), new THREE.MeshStandardMaterial({ color: 0x334155 }));
        pad.position.set(-0.85 + i * 0.34, 0.04, z); g.add(pad);
      }
    }
    track(0.42); track(-0.42);
    var cageM = new THREE.MeshStandardMaterial({ color: 0xd6d3d1, metalness: 0.7, roughness: 0.3 });
    var cage = new THREE.Mesh(new THREE.BoxGeometry(0.7, 0.45, 0.55), cageM);
    cage.position.set(-0.15, 0.62, 0); g.add(cage);
    var bar = new THREE.Mesh(new THREE.BoxGeometry(0.08, 0.08, 0.55), new THREE.MeshStandardMaterial({ color: 0xfde68a, emissive: 0xfbbf24, emissiveIntensity: 0.4 }));
    bar.position.set(1.15, 0.36, 0); g.add(bar);
    g.userData.wheels = []; g.userData.arches = [];
    g.userData.bodyMat = hullM;
    var ts = (C.TRUCK_VIS && C.TRUCK_VIS.threeScale != null) ? C.TRUCK_VIS.threeScale : 2.05;
    g.scale.setScalar(ts); g.userData.truckScale = ts;
    g.userData.vehicleStyle = "ripsaw";
    return g;
  }

  function makeTankMesh(accentHex) {
    /* mech1: hull + tracks + turret/barrel */
    var g = new THREE.Group();
    var hullM = new THREE.MeshStandardMaterial({ color: accentHex || 0x6b7280, metalness: 0.45, roughness: 0.5 });
    var trackM = new THREE.MeshStandardMaterial({ color: 0x111827, roughness: 0.9 });
    var hull = new THREE.Mesh(new THREE.BoxGeometry(1.7, 0.35, 0.85), hullM);
    hull.position.set(0, 0.34, 0); hull.castShadow = true; g.add(hull);
    var glacis = new THREE.Mesh(new THREE.BoxGeometry(0.55, 0.22, 0.8), hullM);
    glacis.position.set(0.95, 0.32, 0); glacis.rotation.z = -0.25; g.add(glacis);
    function track(z) {
      var t = new THREE.Mesh(new THREE.BoxGeometry(1.85, 0.24, 0.24), trackM);
      t.position.set(0, 0.14, z); g.add(t);
    }
    track(0.48); track(-0.48);
    var tur = new THREE.Mesh(new THREE.CylinderGeometry(0.32, 0.36, 0.28, 12), new THREE.MeshStandardMaterial({ color: 0x374151, metalness: 0.5, roughness: 0.45 }));
    tur.position.set(0.05, 0.62, 0); g.add(tur);
    var barrel = new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.07, 1.1, 8), new THREE.MeshStandardMaterial({ color: 0x1f2937 }));
    barrel.rotation.z = Math.PI / 2; barrel.position.set(0.75, 0.62, 0); g.add(barrel);
    g.userData.wheels = []; g.userData.arches = [];
    g.userData.bodyMat = hullM;
    g.userData.turret = tur;
    g.userData.barrel = barrel;
    g.userData.muzzleLocal = new THREE.Vector3(1.35, 0.62, 0); /* mech2: shell spawn along +X hull */
    var ts = (C.TRUCK_VIS && C.TRUCK_VIS.threeScale != null) ? C.TRUCK_VIS.threeScale : 2.05;
    g.scale.setScalar(ts); g.userData.truckScale = ts;
    g.userData.vehicleStyle = "tank";
    return g;
  }

  function makeVehicleMesh(style, accentHex) {
    if (style === "ripsaw") return makeRipsawMesh(accentHex);
    if (style === "tank") return makeTankMesh(accentHex);
    if (style === "submarine") return makeSubMesh(accentHex);
    return makeTruckMesh(accentHex);
  }


  function makeHeliMesh(accentHex) {
    var g = new THREE.Group();
    g.name = "Helicopter";
    var body = new THREE.Mesh(
      new THREE.BoxGeometry(1.6, 0.45, 0.7),
      new THREE.MeshStandardMaterial({ color: accentHex || 0x94a3b8, metalness: 0.55, roughness: 0.35 })
    );
    body.position.set(0.1, 0.55, 0); body.castShadow = true; g.add(body);
    var nose = new THREE.Mesh(
      new THREE.BoxGeometry(0.55, 0.32, 0.55),
      new THREE.MeshStandardMaterial({ color: 0x7dd3fc, metalness: 0.3, roughness: 0.25, transparent: true, opacity: 0.75 })
    );
    nose.position.set(0.85, 0.58, 0); g.add(nose);
    var boom = new THREE.Mesh(
      new THREE.BoxGeometry(1.1, 0.12, 0.12),
      new THREE.MeshStandardMaterial({ color: 0x64748b, metalness: 0.5, roughness: 0.4 })
    );
    boom.position.set(-1.1, 0.55, 0); g.add(boom);
    var fin = new THREE.Mesh(
      new THREE.BoxGeometry(0.12, 0.45, 0.08),
      new THREE.MeshStandardMaterial({ color: 0xfbbf24, metalness: 0.4, roughness: 0.4 })
    );
    fin.position.set(-1.6, 0.7, 0); g.add(fin);
    /* Skids */
    var skidM = new THREE.MeshStandardMaterial({ color: 0x475569, metalness: 0.6, roughness: 0.4 });
    [[-0.35, 0.28], [-0.35, -0.28], [0.45, 0.28], [0.45, -0.28]].forEach(function (p) {
      var leg = new THREE.Mesh(new THREE.CylinderGeometry(0.03, 0.03, 0.35, 6), skidM);
      leg.position.set(p[0], 0.2, p[1]); g.add(leg);
    });
    var skL = new THREE.Mesh(new THREE.BoxGeometry(1.4, 0.05, 0.06), skidM);
    skL.position.set(0.05, 0.05, 0.28); g.add(skL);
    var skR = skL.clone(); skR.position.z = -0.28; g.add(skR);
    /* Main rotor */
    var rotor = new THREE.Group();
    rotor.position.set(0.05, 0.95, 0);
    var hub = new THREE.Mesh(new THREE.CylinderGeometry(0.08, 0.08, 0.08, 8), new THREE.MeshStandardMaterial({ color: 0x1e293b }));
    rotor.add(hub);
    var bladeM = new THREE.MeshStandardMaterial({ color: 0xe2e8f0, metalness: 0.4, roughness: 0.5 });
    for (var bi = 0; bi < 2; bi++) {
      var blade = new THREE.Mesh(new THREE.BoxGeometry(2.4, 0.03, 0.12), bladeM);
      blade.rotation.y = bi * Math.PI / 2;
      rotor.add(blade);
    }
    g.add(rotor);
    g.userData.rotor = rotor;
    g.userData.vehicleStyle = "heli";
    return g;
  }

  function makePassengerDroneMesh(accentHex) {
    var g = new THREE.Group();
    g.name = "PassengerDrone";
    var body = new THREE.Mesh(
      new THREE.BoxGeometry(0.7, 0.28, 0.55),
      new THREE.MeshStandardMaterial({ color: accentHex || 0x67e8f9, metalness: 0.45, roughness: 0.35 })
    );
    body.position.set(0, 0.35, 0); body.castShadow = true; g.add(body);
    var canopy = new THREE.Mesh(
      new THREE.SphereGeometry(0.22, 10, 8, 0, Math.PI * 2, 0, Math.PI * 0.55),
      new THREE.MeshStandardMaterial({ color: 0x0ea5e9, metalness: 0.2, roughness: 0.2, transparent: true, opacity: 0.65 })
    );
    canopy.position.set(0.05, 0.48, 0); g.add(canopy);
    var armM = new THREE.MeshStandardMaterial({ color: 0x475569, metalness: 0.5, roughness: 0.4 });
    var rotors = [];
    var arms = [[0.45, 0.45], [0.45, -0.45], [-0.45, 0.45], [-0.45, -0.45]];
    for (var ai = 0; ai < arms.length; ai++) {
      var ax = arms[ai][0], az = arms[ai][1];
      var arm = new THREE.Mesh(new THREE.BoxGeometry(0.55, 0.05, 0.05), armM);
      arm.position.set(ax * 0.5, 0.38, az * 0.5);
      arm.rotation.y = Math.atan2(az, ax);
      g.add(arm);
      var rg = new THREE.Group();
      rg.position.set(ax, 0.42, az);
      var disc = new THREE.Mesh(
        new THREE.CylinderGeometry(0.22, 0.22, 0.03, 12),
        new THREE.MeshStandardMaterial({ color: 0xa5f3fc, metalness: 0.3, roughness: 0.4, transparent: true, opacity: 0.7 })
      );
      rg.add(disc);
      var blade = new THREE.Mesh(new THREE.BoxGeometry(0.4, 0.02, 0.06), new THREE.MeshStandardMaterial({ color: 0xe0f2fe }));
      rg.add(blade);
      g.add(rg);
      rotors.push(rg);
    }
    g.userData.rotors = rotors;
    g.userData.vehicleStyle = "drone";
    return g;
  }

  function makeAirPadMesh(kind) {
    var g = new THREE.Group();
    g.name = kind === "drone" ? "DronePad" : "HeliPad";
    /* padfix1: larger discs raised above translucent orange house-zone floor (y≈0.04) */
    var R = kind === "drone" ? 1.85 : 2.85;
    var pad = new THREE.Mesh(
      new THREE.CircleGeometry(R, 48),
      new THREE.MeshStandardMaterial({
        color: 0x1e293b, roughness: 0.85, metalness: 0.15, side: THREE.DoubleSide,
        polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2,
      })
    );
    pad.rotation.x = -Math.PI / 2;
    pad.position.y = 0.12;
    pad.renderOrder = 2;
    g.add(pad);
    var ring = new THREE.Mesh(
      new THREE.RingGeometry(R * 0.72, R * 0.98, 48),
      new THREE.MeshBasicMaterial({
        color: kind === "drone" ? 0x67e8f9 : 0xfbbf24, transparent: true, opacity: 0.92,
        side: THREE.DoubleSide, depthWrite: false,
      })
    );
    ring.rotation.x = -Math.PI / 2;
    ring.position.y = 0.14;
    ring.renderOrder = 3;
    g.add(ring);
    var mark = labelSprite(kind === "drone" ? "D" : "H", kind === "drone" ? "#a5f3fc" : "#fde68a");
    mark.position.set(0, 0.55, 0);
    mark.scale.multiplyScalar(1.55);
    g.add(mark);
    var lab = labelSprite(kind === "drone" ? "DRONE PAD" : "HELIPAD", kind === "drone" ? "#ecfeff" : "#fef3c7");
    lab.position.set(0, 1.35, 0);
    g.add(lab);
    return g;
  }

  function makeSubMesh(accentHex) {
    var g = new THREE.Group();
    var hullMat = new THREE.MeshStandardMaterial({
      color: 0x0ea5e9, metalness: 0.55, roughness: 0.32, emissive: 0x0369a1, emissiveIntensity: 0.18
    });
    var hull = new THREE.Mesh(new THREE.SphereGeometry(1.05, 16, 12), hullMat);
    hull.scale.set(2.2, 0.72, 0.95);
    hull.castShadow = true;
    g.add(hull);
    var tower = new THREE.Mesh(
      new THREE.BoxGeometry(0.55, 0.7, 0.45),
      new THREE.MeshStandardMaterial({ color: accentHex || 0x7dd3fc, metalness: 0.4, roughness: 0.4 })
    );
    tower.position.set(0.15, 0.75, 0);
    g.add(tower);
    var peri = new THREE.Mesh(
      new THREE.CylinderGeometry(0.04, 0.04, 0.7, 6),
      new THREE.MeshStandardMaterial({ color: 0x94a3b8, metalness: 0.6, roughness: 0.35 })
    );
    peri.position.set(0.25, 1.25, 0);
    g.add(peri);
    var glass = new THREE.MeshStandardMaterial({ color: 0xe0f2fe, metalness: 0.2, roughness: 0.15, transparent: true, opacity: 0.85 });
    [[-0.55, 0.1, 0.35], [0.35, 0.1, 0.35], [0.9, 0.08, 0.28]].forEach(function (pos) {
      var port = new THREE.Mesh(new THREE.CircleGeometry(0.14, 10), glass);
      port.position.set(pos[0], pos[1], pos[2]);
      g.add(port);
      var port2 = port.clone(); port2.position.z = -pos[2]; g.add(port2);
    });
    g.userData.bodyMat = hullMat;
    g.userData.truckScale = 1.15;
    return g;
  }

  function applyTruckWheelScale(g, ws) {
    if (!g || !g.userData) return;
    ws = ws != null ? ws : (C.getWheelScale ? C.getWheelScale() : 1);
    if (ws < 1) ws = 1;
    if (ws > 2.8) ws = 2.8;
    var wheels = g.userData.wheels || [];
    for (var i = 0; i < wheels.length; i++) {
      var w = wheels[i];
      w.scale.set(ws, ws, ws);
      w.position.y = (w.userData.baseY || 0.16) * ws;
    }
    var arches = g.userData.arches || [];
    for (var j = 0; j < arches.length; j++) {
      var a = arches[j];
      a.scale.set(ws, ws, ws);
      a.position.y = (a.userData.baseY || 0.22) * ws;
    }
    /* Body rides up with clearance */
    var lift = (ws - 1) * 0.22;
    if (g.userData.bodyMat && g.children) {
      /* keep mesh group y offset via userData */
    }
    g.userData.wheelScale = ws;
    g.userData.wheelLift = lift;
  }

  function densifyRibbon(pts, step, close) {
    var out = [];
    if (!pts || pts.length < 2) return out;
    var nSeg = close ? pts.length : pts.length - 1;
    for (var i = 0; i < nSeg; i++) {
      var a = pts[i];
      var b = pts[(i + 1) % pts.length];
      var dist = Math.hypot(b[0] - a[0], b[1] - a[1]) || 1;
      var n = Math.max(1, Math.round(dist / step));
      for (var s = 0; s < n; s++) {
        var u = s / n;
        out.push([
          a[0] + (b[0] - a[0]) * u,
          a[1] + (b[1] - a[1]) * u,
        ]);
      }
    }
    if (!close) out.push([pts[pts.length - 1][0], pts[pts.length - 1][1]]);
    return out;
  }

  function addPathRibbon(pts, yBase, color, halfWidth, yLift, close) {
    /* Lane sits a few centimeters above the apron. Height is ranchGroundY at
       each sample, so a hill on the racing line is the hill under the tires. */
    var dense = densifyRibbon(pts, 12, !!close);
    if (dense.length < 2) return;
    var verts = [];
    var n = dense.length;
    var lift = yLift || 0;
    var closed = !!close;
    for (var i = 0; i < n; i++) {
      var prev = dense[closed ? (i - 1 + n) % n : Math.max(0, i - 1)];
      var next = dense[closed ? (i + 1) % n : Math.min(n - 1, i + 1)];
      var p0 = worldToThree(prev[0], prev[1]);
      var p1 = worldToThree(next[0], next[1]);
      var dx = p1.x - p0.x, dz = p1.z - p0.z;
      var len = Math.hypot(dx, dz) || 1;
      var px = -dz / len, pz = dx / len;
      var cur = worldToThree(dense[i][0], dense[i][1]);
      var ey = yBase + ranchGroundY(dense[i][0], dense[i][1]) + lift;
      verts.push(cur.x + px * halfWidth, ey, cur.z + pz * halfWidth);
      verts.push(cur.x - px * halfWidth, ey, cur.z - pz * halfWidth);
    }
    var idx = [];
    var segCount = closed ? n : n - 1;
    for (var s = 0; s < segCount; s++) {
      var a = (s % n) * 2, b = a + 1, c = ((s + 1) % n) * 2, d = c + 1;
      idx.push(a, c, b, b, c, d);
    }
    var geo = new THREE.BufferGeometry();
    geo.setAttribute("position", new THREE.Float32BufferAttribute(verts, 3));
    geo.setIndex(idx);
    geo.computeVertexNormals();
    var mesh = new THREE.Mesh(geo, new THREE.MeshStandardMaterial({
      color: color,
      roughness: 0.9,
      metalness: 0.02,
      polygonOffset: true,
      polygonOffsetFactor: -1 - lift * 40,
      polygonOffsetUnits: -1 - lift * 40,
      side: THREE.DoubleSide,
    }));
    mesh.receiveShadow = true;
    mesh.castShadow = false;
    mesh.renderOrder = 2 + Math.round(lift * 100);
    scene.add(mesh);
  }

  function addMech(m, color, h) {
    /* hop3 + mechwalk1: articulated robot in a Group so piloted mechs can lumber through the world */
    var p = worldToThree(m.x, m.y);
    var g = new THREE.Group();
    g.position.set(p.x, 0, p.z);
    var _isTriGold = (m.stories >= 1e12);
    var mat = new THREE.MeshStandardMaterial({
      color: color,
      metalness: _isTriGold ? 0.72 : 0.42,
      roughness: _isTriGold ? 0.28 : 0.4,
    });
    var dark = new THREE.MeshStandardMaterial({ color: 0x0f172a, metalness: 0.5, roughness: 0.35 });
    var band = C.mechBand ? C.mechBand(m.stories) : (m.stories >= 1e12 ? "trillion" : m.stories >= 1000 ? "1000" : m.stories >= 100 ? "100" : "10");
    var eyeCol = band === "trillion" ? 0xfef08a : band === "1000" ? 0xfbbf24 : band === "100" ? 0x67e8f9 : 0xa5b4fc;
    var eyeMat = new THREE.MeshStandardMaterial({ color: eyeCol, emissive: eyeCol, emissiveIntensity: 0.85, metalness: 0.2, roughness: 0.3 });
    if (band === "1000" || band === "trillion") {
      var haze = new THREE.Mesh(
        new THREE.SphereGeometry(h * (band === "trillion" ? 0.7 : 0.55), 12, 10),
        new THREE.MeshBasicMaterial({ color: eyeCol, transparent: true, opacity: band === "trillion" ? 0.16 : 0.12, depthWrite: false })
      );
      haze.position.set(0, h * 0.55, 0);
      haze.userData.homeMat = haze.material; /* mech8 */
      haze.userData.isMechHaze = true;
      g.add(haze);
    }
    var padR = h * (band === "trillion" ? 0.42 : 0.38);
    var padY = (band === "1000" || band === "trillion") ? 0.18 : 0.14;
    var pad = new THREE.Mesh(
      new THREE.CircleGeometry(padR, 28),
      new THREE.MeshStandardMaterial({
        color: 0x1e293b, metalness: 0.45, roughness: 0.65,
        polygonOffset: true, polygonOffsetFactor: -3, polygonOffsetUnits: -3,
        depthWrite: false, transparent: true, opacity: 0.96,
        side: THREE.DoubleSide,
      })
    );
    pad.rotation.x = -Math.PI / 2;
    pad.position.set(0, padY, 0);
    pad.renderOrder = 3;
    pad.userData.homeMat = pad.material; /* mech8 */
    g.add(pad);
    var padRing = new THREE.Mesh(
      new THREE.RingGeometry(padR * 0.7, padR * 0.92, 32),
      new THREE.MeshBasicMaterial({
        color: 0xfbbf24, side: THREE.DoubleSide, depthWrite: false,
        transparent: true, opacity: 0.88,
      })
    );
    padRing.rotation.x = -Math.PI / 2;
    padRing.position.set(0, padY + 0.02, 0);
    padRing.renderOrder = 4;
    padRing.userData.homeMat = padRing.material; /* mech8 */
    g.add(padRing);

    var legsL = [], legsR = [], armsL = [], armsR = [];
    function part(geo, material, x, y, z, bucket) {
      var mesh = new THREE.Mesh(geo, material);
      mesh.position.set(x, y, z);
      mesh.castShadow = true;
      mesh.userData.baseY = y;
      mesh.userData.baseX = x;
      mesh.userData.baseZ = z;
      mesh.userData.homeMat = material; /* mech8: restore after tank blast */
      g.add(mesh);
      if (bucket) bucket.push(mesh);
      return mesh;
    }
    var tw = h * 0.34, td = h * 0.22;
    /* Feet */
    part(new THREE.BoxGeometry(h * 0.16, h * 0.05, h * 0.22), mat, -h * 0.12, h * 0.03, 0, legsL);
    part(new THREE.BoxGeometry(h * 0.16, h * 0.05, h * 0.22), mat, h * 0.12, h * 0.03, 0, legsR);
    /* Lower / upper legs */
    part(new THREE.BoxGeometry(h * 0.1, h * 0.22, h * 0.12), mat, -h * 0.11, h * 0.16, 0, legsL);
    part(new THREE.BoxGeometry(h * 0.1, h * 0.22, h * 0.12), mat, h * 0.11, h * 0.16, 0, legsR);
    part(new THREE.BoxGeometry(h * 0.11, h * 0.2, h * 0.13), mat, -h * 0.1, h * 0.36, 0, legsL);
    part(new THREE.BoxGeometry(h * 0.11, h * 0.2, h * 0.13), mat, h * 0.1, h * 0.36, 0, legsR);
    /* Torso + chest glow (storymuzzle1: this plate is the omnigun muzzle) */
    part(new THREE.BoxGeometry(tw, h * 0.32, td), mat, 0, h * 0.58, 0);
    var chestGlow = part(new THREE.BoxGeometry(tw * 0.45, h * 0.08, td * 0.2), eyeMat, 0, h * 0.6, td * 0.52);
    chestGlow.userData.isChestMuzzle = true;
    /* Shoulders */
    part(new THREE.BoxGeometry(h * 0.14, h * 0.1, h * 0.14), mat, -tw * 0.62, h * 0.7, 0);
    part(new THREE.BoxGeometry(h * 0.14, h * 0.1, h * 0.14), mat, tw * 0.62, h * 0.7, 0);
    /* Arms + fists */
    part(new THREE.BoxGeometry(h * 0.08, h * 0.28, h * 0.08), mat, -tw * 0.72, h * 0.52, 0, armsL);
    part(new THREE.BoxGeometry(h * 0.08, h * 0.28, h * 0.08), mat, tw * 0.72, h * 0.52, 0, armsR);
    part(new THREE.BoxGeometry(h * 0.1, h * 0.1, h * 0.1), mat, -tw * 0.72, h * 0.36, 0, armsL);
    part(new THREE.BoxGeometry(h * 0.1, h * 0.1, h * 0.1), mat, tw * 0.72, h * 0.36, 0, armsR);
    /* Head + visor + eyes */
    part(new THREE.BoxGeometry(h * 0.2, h * 0.16, h * 0.18), mat, 0, h * 0.82, 0);
    part(new THREE.BoxGeometry(h * 0.16, h * 0.06, h * 0.04), dark, 0, h * 0.84, h * 0.1);
    part(new THREE.SphereGeometry(h * 0.025, 8, 6), eyeMat, -h * 0.045, h * 0.84, h * 0.12);
    part(new THREE.SphereGeometry(h * 0.025, 8, 6), eyeMat, h * 0.045, h * 0.84, h * 0.12);
    /* Antenna */
    var antTipCol = band === "trillion" ? 0xf59e0b : 0xf87171;
    part(new THREE.CylinderGeometry(h * 0.01, h * 0.01, h * 0.1, 6), dark, 0, h * 0.95, 0);
    part(new THREE.SphereGeometry(h * 0.02, 6, 5), new THREE.MeshStandardMaterial({ color: antTipCol, emissive: antTipCol, emissiveIntensity: 0.5 }), 0, h * 1.01, 0);

    /* goldsteam1: steam pipe + billow puffs — James trillion only */
    var steamPuffs = null;
    if (band === "trillion") {
      var pipeMat = new THREE.MeshStandardMaterial({ color: 0x44403c, metalness: 0.72, roughness: 0.38 });
      var rimMat = new THREE.MeshStandardMaterial({ color: 0x78716c, metalness: 0.55, roughness: 0.45 });
      var pipeX = h * 0.14, pipeZ = -h * 0.06;
      part(new THREE.CylinderGeometry(h * 0.028, h * 0.034, h * 0.26, 8), pipeMat, pipeX, h * 0.78, pipeZ);
      part(new THREE.CylinderGeometry(h * 0.045, h * 0.045, h * 0.035, 8), rimMat, pipeX, h * 0.92, pipeZ);
      part(new THREE.CylinderGeometry(h * 0.02, h * 0.02, h * 0.02, 6), dark, pipeX, h * 0.94, pipeZ);
      steamPuffs = [];
      for (var spi = 0; spi < 6; spi++) {
        var puff = new THREE.Mesh(
          new THREE.SphereGeometry(h * (0.035 + spi * 0.008), 6, 6),
          new THREE.MeshBasicMaterial({ color: 0xf1f5f9, transparent: true, opacity: 0.5, depthWrite: false })
        );
        puff.position.set(pipeX, h * 0.96 + spi * 0.06, pipeZ);
        puff.userData.phase = spi * 0.5;
        puff.userData.baseY = h * 0.96;
        puff.userData.pipeX = pipeX;
        puff.userData.pipeZ = pipeZ;
        puff.renderOrder = 6;
        g.add(puff);
        steamPuffs.push(puff);
      }
    }

    var labTxt = C.mechStoriesLabel ? C.mechStoriesLabel(m.stories) : (m.stories + "-story mech");
    if (C.mechOwnerName) {
      var on = C.mechOwnerName(m);
      if (on) labTxt = on + " · " + labTxt;
    }
    var lab = addLabel(labTxt, "#fff", p.x, h + 0.55, p.z);
    scene.add(g);
    var solidId = band === "trillion" ? "mechTrillion" : band === "1000" ? "mech1000" : band === "100" ? "mech100" : "mech10";
    var hotId = band === "trillion" ? "mech-trillion" : band === "1000" ? "mech-1000" : band === "100" ? "mech-100" : "mech-10";
    var entry = {
      id: hotId,
      solidId: solidId,
      stories: m.stories || 10,
      group: g,
      label: lab,
      homeX: p.x,
      homeZ: p.z,
      h: h,
      legsL: legsL,
      legsR: legsR,
      armsL: armsL,
      armsR: armsR,
      walkPhase: 0,
      steamPuffs: steamPuffs,
      /* storymuzzle1: local chest plate = omnigun origin (+Z face front) */
      chestGlow: chestGlow,
      chestMuzzleLocal: new THREE.Vector3(0, h * 0.6, td * 0.52),
    };
    if (!state.mechs) state.mechs = [];
    state.mechs.push(entry);
    return entry;
  }

  function buildCompound() {
    if (C.resetVehicleParks) C.resetVehicleParks();
    var cp = C.COMPOUND || {};
    var yard = cp.yard || { x: 100, y: 2100, w: 600, h: 360 };
    var yp = worldToThree(yard.x + yard.w / 2, yard.y + yard.h / 2);
    /* tapsteer1: flat backyard plane (was thick box fighting mech pad / ground) */
    var yardM = new THREE.Mesh(
      new THREE.PlaneGeometry(yard.w * 0.02, yard.h * 0.02),
      new THREE.MeshStandardMaterial({
        color: 0x468232, roughness: 0.95,
        polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -2,
      })
    );
    yardM.rotation.x = -Math.PI / 2;
    yardM.position.set(yp.x, 0.05, yp.z);
    yardM.receiveShadow = true;
    yardM.renderOrder = -1;
    scene.add(yardM);
    addLabel("Backyard", "#ecfccb", yp.x, 1.2, yp.z);
    state.pushables = state.pushables || [];
    for (var ai = 0; ai < 40; ai++) {
      var awx = yard.x + 30 + Math.random() * (yard.w - 60);
      var awy = yard.y + 40 + Math.random() * (yard.h - 80);
      var ap = worldToThree(awx, awy);
      var ar = 0.22 + Math.random() * 0.16;
      var animal = new THREE.Mesh(
        new THREE.SphereGeometry(ar, 8, 6),
        new THREE.MeshStandardMaterial({ color: ai % 3 === 0 ? 0xc4a574 : ai % 3 === 1 ? 0x8b6914 : 0xd6d3d1 })
      );
      animal.position.set(ap.x, ar, ap.z); animal.castShadow = true; scene.add(animal);
      var head = new THREE.Mesh(
        new THREE.SphereGeometry(ar * 0.45, 6, 5),
        new THREE.MeshStandardMaterial({ color: ai % 2 ? 0xc4a574 : 0x8b6914 })
      );
      head.position.set(ap.x + ar * 0.7, ar * 1.1, ap.z); scene.add(head);
      state.pushables.push({
        mesh: animal, head: head, x: awx, y: awy, vx: 0, vy: 0, r: 12 + ar * 20, kind: "animal", ar: ar,
        headOx: ar * 0.7, bounds: { x0: yard.x + 20, y0: yard.y + 30, x1: yard.x + yard.w - 20, y1: yard.y + yard.h - 20 },
      });
    }
    var gar = cp.garage || { x: 700, y: 1400, w: 480, h: 520 };
    var gp = worldToThree(gar.x + gar.w / 2, gar.y + gar.h / 2);
    var gw = gar.w * 0.02, gd = gar.h * 0.02;
    var gFloor = new THREE.Mesh(
      new THREE.BoxGeometry(gw, 0.08, gd),
      new THREE.MeshStandardMaterial({
        color: 0x57534e, roughness: 0.9,
        polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -1,
      })
    );
    gFloor.position.set(gp.x, 0.09, gp.z); gFloor.receiveShadow = true; scene.add(gFloor);
    var gMat = new THREE.MeshStandardMaterial({ color: 0x6b7280, roughness: 0.75, metalness: 0.15 });
    state.garageParts = state.garageParts || [];
    function gWall(wx, wz, ww, wd, wh) {
      var m = new THREE.Mesh(new THREE.BoxGeometry(ww, wh || 2.0, wd), gMat);
      m.position.set(wx, (wh || 2.0) * 0.5, wz); m.castShadow = true; scene.add(m);
      state.garageParts.push(m);
    }
    gWall(gp.x, gp.z - gd * 0.5 + 0.1, gw, 0.2);
    gWall(gp.x - gw * 0.5 + 0.1, gp.z, 0.2, gd);
    gWall(gp.x + gw * 0.5 - 0.1, gp.z, 0.2, gd);
    // garage1: south bay = full wall-to-wall entrance (not a floating center slab)
    var doorW = Math.max(1.2, gw - 0.36); // spans between side walls
    var jambT = 0.18;
    var southZ = gp.z + gd * 0.5 - 0.08;
    var jambMat = new THREE.MeshStandardMaterial({ color: 0x374151, roughness: 0.7, metalness: 0.1 });
    // Left / right jambs flush with side walls
    var jambL = new THREE.Mesh(new THREE.BoxGeometry(jambT, 2.0, 0.22), jambMat);
    jambL.position.set(gp.x - doorW * 0.5 - jambT * 0.5, 1.0, southZ); jambL.castShadow = true; scene.add(jambL);
    var jambR = new THREE.Mesh(new THREE.BoxGeometry(jambT, 2.0, 0.22), jambMat);
    jambR.position.set(gp.x + doorW * 0.5 + jambT * 0.5, 1.0, southZ); jambR.castShadow = true; scene.add(jambR);
    state.garageParts.push(jambL, jambR);
    state.garageBlast = { id: "garage", kind: "building", x: gar.x + gar.w * 0.5, y: gar.y + gar.h * 0.5, r: Math.max(gar.w, gar.h) * 0.42, boom: 3.6 };
    var lintel = new THREE.Mesh(new THREE.BoxGeometry(doorW + jambT * 2, 0.35, 0.24), new THREE.MeshStandardMaterial({ color: 0x111827 }));
    lintel.position.set(gp.x, 1.95, southZ); scene.add(lintel);
    state.garageDoor = new THREE.Mesh(
      new THREE.BoxGeometry(doorW, 1.7, 0.12),
      new THREE.MeshStandardMaterial({ color: 0x1f2937, metalness: 0.3, roughness: 0.55 })
    );
    state.garageDoor.position.set(gp.x, 0.95, southZ + 0.02);
    state.garageDoor.userData.y0 = 0.95;
    state.garageDoor.userData.h0 = 1.7;
    state.garageDoor.userData.cx = gp.x;
    state.garageDoor.userData.cz = gp.z + gd * 0.5;
    scene.add(state.garageDoor);
    state.garageParts.push(state.garageDoor);
    state.garageOpen = 0;
    state.garageOpenLabel = labelSprite("OPEN", "#bbf7d0");
    state.garageOpenLabel.position.set(gp.x, 2.2, gp.z + gd * 0.5);
    state.garageOpenLabel.visible = false;
    scene.add(state.garageOpenLabel);
    addLabel("Garage · James toys", "#fff", gp.x, 2.9, gp.z);
    state.pushables = state.pushables || [];
    for (var t = 0; t < 32; t++) {
      var twx = gar.x + 40 + (t % 8) * 48, twy = gar.y + 70 + Math.floor(t / 8) * 50;
      var tp = worldToThree(twx, twy);
      var toyG = new THREE.Group();
      var toyBody = new THREE.Mesh(new THREE.BoxGeometry(0.2, 0.14, 0.2), new THREE.MeshStandardMaterial({ color: 0xfbbf24 }));
      toyBody.position.y = 0.07; toyG.add(toyBody);
      var toyTop = new THREE.Mesh(new THREE.SphereGeometry(0.08, 8, 6), new THREE.MeshStandardMaterial({ color: 0xf97316 }));
      toyTop.position.y = 0.2; toyG.add(toyTop);
      toyG.position.set(tp.x, 0.05, tp.z); scene.add(toyG);
      var toy = toyG;
      state.pushables.push({
        mesh: toy, x: twx, y: twy, vx: 0, vy: 0, r: 10, kind: "toy",
        bounds: { x0: gar.x + 20, y0: gar.y + 50, x1: gar.x + gar.w - 20, y1: gar.y + gar.h - 40 },
      });
    }
    var house = cp.house || { x: 120, y: 1420, w: 520, h: 420 };
    var hp = worldToThree(house.x + house.w / 2, house.y + house.h / 2);
    var hw = house.w * 0.02, hd = house.h * 0.02;
    // Floor only + perimeter walls (HOLLOW) — spawn is inside house rect; solid box buried frogs
    var floor = new THREE.Mesh(
      new THREE.BoxGeometry(hw, 0.1, hd),
      new THREE.MeshStandardMaterial({
        color: 0xc4a574, roughness: 0.85,
        polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -1,
      })
    );
    floor.position.set(hp.x, 0.1, hp.z); floor.receiveShadow = true; scene.add(floor);
    var wallMat = new THREE.MeshStandardMaterial({ color: 0xd4b896, roughness: 0.7, side: THREE.DoubleSide });
    var wallH = 2.2, thick = 0.18;
    state.houseParts = state.houseParts || [];
    function wall(wx, wz, ww, wd) {
      var m = new THREE.Mesh(new THREE.BoxGeometry(ww, wallH, wd), wallMat);
      m.position.set(wx, wallH * 0.5, wz); m.castShadow = true; scene.add(m);
      state.houseParts.push(m);
    }
    // North/South (along X), East/West (along Z) — leave south gap as doorway
    wall(hp.x, hp.z - hd * 0.5 + thick * 0.5, hw, thick); // north
    wall(hp.x - hw * 0.28, hp.z + hd * 0.5 - thick * 0.5, hw * 0.4, thick); // south left
    wall(hp.x + hw * 0.28, hp.z + hd * 0.5 - thick * 0.5, hw * 0.4, thick); // south right (door gap)
    wall(hp.x - hw * 0.5 + thick * 0.5, hp.z, thick, hd); // west
    wall(hp.x + hw * 0.5 - thick * 0.5, hp.z, thick, hd); // east
    /* polish4: interior room props (still hollow — frogs walk through) */
    var sofa = new THREE.Mesh(
      new THREE.BoxGeometry(1.4, 0.45, 0.55),
      new THREE.MeshStandardMaterial({ color: 0x7c4a3a, roughness: 0.9 })
    );
    sofa.position.set(hp.x - 1.2, 0.35, hp.z - 0.8); scene.add(sofa);
    var table = new THREE.Mesh(
      new THREE.BoxGeometry(0.9, 0.35, 0.9),
      new THREE.MeshStandardMaterial({ color: 0x5c4030 })
    );
    table.position.set(hp.x + 1.1, 0.28, hp.z + 0.4); scene.add(table);
    var lamp = new THREE.Mesh(
      new THREE.SphereGeometry(0.22, 8, 6),
      new THREE.MeshStandardMaterial({ color: 0xfbbf24, emissive: 0xf59e0b, emissiveIntensity: 0.6 })
    );
    lamp.position.set(hp.x, 1.1, hp.z - 1.2); scene.add(lamp);
    /* Warm window panes on exterior walls */
    function windowPane(wx, wz, ww, wd) {
      var pane = new THREE.Mesh(
        new THREE.BoxGeometry(ww, 0.55, wd),
        new THREE.MeshStandardMaterial({ color: 0xfde68a, emissive: 0xfbbf24, emissiveIntensity: 0.35, transparent: true, opacity: 0.85 })
      );
      pane.position.set(wx, 1.15, wz); scene.add(pane);
    }
    windowPane(hp.x - 1.6, hp.z - hd * 0.5 + 0.05, 0.7, 0.08);
    windowPane(hp.x + 0.2, hp.z - hd * 0.5 + 0.05, 0.7, 0.08);
    windowPane(hp.x + 1.8, hp.z - hd * 0.5 + 0.05, 0.7, 0.08);
    var roof = new THREE.Mesh(
      new THREE.ConeGeometry(Math.max(hw, hd) * 0.62, 1.5, 4),
      new THREE.MeshStandardMaterial({ color: 0x6d4c41, transparent: true, opacity: 0.55 })
    );
    roof.position.set(hp.x, wallH + 0.85, hp.z); roof.rotation.y = Math.PI / 4; scene.add(roof);
    var chimney = new THREE.Mesh(
      new THREE.BoxGeometry(0.35, 1.1, 0.35),
      new THREE.MeshStandardMaterial({ color: 0x78716c })
    );
    chimney.position.set(hp.x + 1.4, wallH + 1.1, hp.z - 0.8); scene.add(chimney);
    /* polish8: porch depth + path + chimney smoke + Blue Bear (hollow house kept) */
    var porch = new THREE.Mesh(
      new THREE.BoxGeometry(hw * 0.9, 0.14, 1.1),
      new THREE.MeshStandardMaterial({ color: 0xb8956a, roughness: 0.85 })
    );
    porch.position.set(hp.x, 0.12, hp.z + hd * 0.5 + 0.4); porch.receiveShadow = true; scene.add(porch);
    state.houseParts.push(roof, chimney, sofa, table, lamp, porch);
    state.houseBlast = { id: "house", kind: "building", x: house.x + house.w * 0.5, y: house.y + house.h * 0.5, r: Math.max(house.w, house.h) * 0.42, boom: 4.2 };
    var path = new THREE.Mesh(
      new THREE.BoxGeometry(0.9, 0.06, 1.6),
      new THREE.MeshStandardMaterial({
        color: 0xa09070, roughness: 0.9,
        polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -1,
      })
    );
    path.position.set(hp.x, 0.09, hp.z + hd * 0.5 + 1.4); scene.add(path);
    for (var pi = 0; pi < 5; pi++) {
      var post = new THREE.Mesh(
        new THREE.BoxGeometry(0.1, 0.9, 0.1),
        new THREE.MeshStandardMaterial({ color: 0x5d4037 })
      );
      post.position.set(hp.x - hw * 0.35 + pi * (hw * 0.175), 0.55, hp.z + hd * 0.5 + 0.35);
      scene.add(post);
    }
    state.chimneySmoke = [];
    for (var sm = 0; sm < 4; sm++) {
      var puff = new THREE.Mesh(
        new THREE.SphereGeometry(0.12 + sm * 0.04, 6, 6),
        new THREE.MeshBasicMaterial({ color: 0xc8c8d0, transparent: true, opacity: 0.4 })
      );
      puff.position.set(hp.x + 1.4, wallH + 1.7 + sm * 0.25, hp.z - 0.8);
      puff.userData.phase = sm * 0.8; puff.userData.baseY = puff.position.y;
      scene.add(puff); state.chimneySmoke.push(puff);
    }
    var blueG = new THREE.Group();
    var blueBody = new THREE.Mesh(
      new THREE.SphereGeometry(0.28, 10, 8),
      new THREE.MeshStandardMaterial({ color: 0x3b82f6 })
    );
    blueBody.position.y = 0.28; blueG.add(blueBody);
    var earL = new THREE.Mesh(new THREE.SphereGeometry(0.1, 6, 6), new THREE.MeshStandardMaterial({ color: 0x60a5fa }));
    earL.position.set(-0.16, 0.48, 0); blueG.add(earL);
    var earR = earL.clone(); earR.position.x = 0.16; blueG.add(earR);
    var bp = worldToThree(320, 1920);
    blueG.position.set(bp.x, 0, bp.z);
    blueG.userData.bob = 0;
    scene.add(blueG); state.blueBear = blueG;
    addLabel("Blue Bear", "#bfdbfe", bp.x, 1.1, bp.z);
    addLabel("James · Ranch house", "#fff7ed", hp.x, wallH + 2.0, hp.z);
    state.mechs = [];
    addMech(Object.assign({}, cp.mech10 || { x: 820, y: 1680 }, { stories: 10 }), 0xa5b4fc, 1.9);
    addMech(Object.assign({}, cp.mech100 || { x: 980, y: 1700 }, { stories: 100 }), 0x67e8f9, 3.2);
    addMech(Object.assign({}, cp.mech1000 || { x: 340, y: 2420 }, { stories: 1000 }), 0xfcd34d, 8.2);
    addMech(Object.assign({}, cp.mechTrillion || { x: 600, y: 2170 }, { stories: 1e12 }), 0xffd700, 14.5);

    /* yard1: creek / trees / shrubs / rocks / flowers / fence */
    state.ranchBlastables = state.ranchBlastables || [];
    state.houseParts = state.houseParts || [];
    state.garageParts = state.garageParts || [];
    var stream = C.YARD_STREAM || [];
    if (stream.length >= 2) {
      var sPts = [];
      for (var si = 0; si < stream.length; si++) {
        var sp = worldToThree(stream[si][0], stream[si][1]);
        sPts.push(new THREE.Vector3(sp.x, 0.08, sp.z));
      }
      try {
        var sCurve = new THREE.CatmullRomCurve3(sPts, false);
        var creek = new THREE.Mesh(
          new THREE.TubeGeometry(sCurve, Math.max(20, stream.length * 4), (C.YARD_STREAM_HALF_W || 26) * 0.018, 8, false),
          new THREE.MeshStandardMaterial({ color: 0x22d3ee, roughness: 0.35, metalness: 0.15, transparent: true, opacity: 0.85 })
        );
        creek.receiveShadow = true; scene.add(creek);
      } catch (eCreek) {}
    }
    var trees = C.YARD_TREES || [];
    for (var ti = 0; ti < trees.length; ti++) {
      var tr = trees[ti];
      var tp = worldToThree(tr.x, tr.y);
      var ts = tr.s || 1;
      var trunk = new THREE.Mesh(
        new THREE.CylinderGeometry(0.08 * ts, 0.12 * ts, 0.9 * ts, 6),
        new THREE.MeshStandardMaterial({ color: 0x78350f, roughness: 0.9 })
      );
      trunk.position.set(tp.x, 0.45 * ts, tp.z); trunk.castShadow = true; scene.add(trunk);
      var canopy = new THREE.Mesh(
        new THREE.SphereGeometry((tr.r || 14) * 0.045 * ts, 8, 6),
        new THREE.MeshStandardMaterial({ color: 0x16a34a, roughness: 0.85 })
      );
      canopy.position.set(tp.x, 1.05 * ts, tp.z); canopy.castShadow = true; scene.add(canopy);
      state.ranchBlastables.push({
        id: "yard-tree-" + ti, kind: "tree", x: tr.x, y: tr.y, r: Math.max(18, (tr.r || 14) * 1.2),
        meshes: [trunk, canopy], boom: 2.4
      });
    }
    var shrubs = C.YARD_SHRUBS || [];
    for (var shi = 0; shi < shrubs.length; shi++) {
      var sb = shrubs[shi];
      var sbp = worldToThree(sb.x, sb.y);
      var bush = new THREE.Mesh(
        new THREE.SphereGeometry(0.22 * (sb.s || 0.8), 6, 5),
        new THREE.MeshStandardMaterial({ color: 0x4d7c0f, roughness: 0.9 })
      );
      bush.position.set(sbp.x, 0.18, sbp.z); scene.add(bush);
      state.ranchBlastables.push({
        id: "yard-shrub-" + shi, kind: "shrub", x: sb.x, y: sb.y, r: 12 * (sb.s || 0.8),
        meshes: [bush], boom: 1.3
      });
    }
    var rocks = C.YARD_ROCKS || [];
    for (var rki = 0; rki < rocks.length; rki++) {
      var rk = rocks[rki];
      var rkp = worldToThree(rk.x, rk.y);
      var rock = new THREE.Mesh(
        new THREE.DodecahedronGeometry((rk.r || 8) * 0.028, 0),
        new THREE.MeshStandardMaterial({ color: 0x78716c, roughness: 0.95 })
      );
      rock.position.set(rkp.x, 0.12, rkp.z); rock.castShadow = true; scene.add(rock);
      state.ranchBlastables.push({
        id: "yard-rock-" + rki, kind: "rock", x: rk.x, y: rk.y, r: (rk.r || 8) + 6,
        meshes: [rock], boom: 1.5
      });
    }
    var flowers = C.YARD_FLOWERS || [];
    for (var fli = 0; fli < flowers.length; fli++) {
      var fl = flowers[fli];
      var flp = worldToThree(fl.x, fl.y);
      var blossom = new THREE.Mesh(
        new THREE.SphereGeometry(0.07, 6, 5),
        new THREE.MeshStandardMaterial({ color: fl.c ? parseInt(String(fl.c).replace("#", ""), 16) : 0xf472b6, roughness: 0.6 })
      );
      blossom.position.set(flp.x, 0.2, flp.z); scene.add(blossom);
      state.ranchBlastables.push({
        id: "yard-flower-" + fli, kind: "flower", x: fl.x, y: fl.y, r: 10,
        meshes: [blossom], boom: 0.9
      });
    }
    var fence = C.YARD_FENCE || [];
    for (var fi = 0; fi < fence.length; fi++) {
      var fp = worldToThree(fence[fi].x, fence[fi].y);
      var post = new THREE.Mesh(
        new THREE.BoxGeometry(0.08, 0.55, 0.08),
        new THREE.MeshStandardMaterial({ color: 0x78716c, roughness: 0.9 })
      );
      post.position.set(fp.x, 0.28, fp.z); scene.add(post);
      state.ranchBlastables.push({
        id: "yard-fence-" + fi, kind: "fence", x: fence[fi].x, y: fence[fi].y, r: 14,
        meshes: [post], boom: 1.2
      });
    }
  }

  function elevRGB(y) {
    var t = y / 4.4;
    if (t < 0) t = 0;
    if (t > 1) t = 1;
    if (y < 0) t = Math.max(-0.35, y / 2);
    return [
      (0x3a + (0xc8 - 0x3a) * t) / 255,
      (0x2e + (0xb0 - 0x2e) * t) / 255,
      (0x24 + (0x8a - 0x24) * t) / 255,
    ];
  }

  function addTrackSurface() {
    var a = C.AREAS[1];
    var step = 8;
    var nx = Math.max(2, Math.round(a.w / step));
    var ny = Math.max(2, Math.round(a.h / step));
    var cols = nx + 1;
    var verts = [];
    var colors = [];
    function gy(ix, iy) {
      var wx = a.x + (ix / nx) * a.w;
      var wy = a.y + (iy / ny) * a.h;
      return ranchGroundY(wx, wy);
    }
    function pushV(ix, iy, yv) {
      var wx = a.x + (ix / nx) * a.w;
      var wy = a.y + (iy / ny) * a.h;
      var p = worldToThree(wx, wy);
      verts.push(p.x, yv, p.z);
      var rgb = elevRGB(yv);
      colors.push(rgb[0], rgb[1], rgb[2]);
    }
    for (var iy = 0; iy <= ny; iy++) {
      for (var ix = 0; ix <= nx; ix++) pushV(ix, iy, gy(ix, iy));
    }
    var idx = [];
    for (var jy = 0; jy < ny; jy++) {
      for (var jx = 0; jx < nx; jx++) {
        var v00 = jy * cols + jx;
        var v10 = v00 + 1;
        var v01 = v00 + cols;
        var v11 = v01 + 1;
        /* CCW from +Y. Check: flat +X/+Z quad → normal y > 0. */
        idx.push(v00, v01, v11, v00, v11, v10);
      }
    }
    function pushWall(ix, iy, jx, jy) {
      var ah = gy(ix, iy), bh = gy(jx, jy);
      if (Math.abs(ah) < 0.08 && Math.abs(bh) < 0.08) return;
      var base = verts.length / 3;
      pushV(ix, iy, 0);
      pushV(jx, jy, 0);
      pushV(jx, jy, bh);
      pushV(ix, iy, ah);
      idx.push(base, base + 1, base + 2, base, base + 2, base + 3);
    }
    for (var e = 0; e < nx; e++) {
      pushWall(e, 0, e + 1, 0);
      pushWall(e, ny, e + 1, ny);
    }
    for (var s = 0; s < ny; s++) {
      pushWall(0, s, 0, s + 1);
      pushWall(nx, s, nx, s + 1);
    }
    var geo = new THREE.BufferGeometry();
    geo.setAttribute("position", new THREE.Float32BufferAttribute(verts, 3));
    geo.setAttribute("color", new THREE.Float32BufferAttribute(colors, 3));
    geo.setIndex(idx);
    geo.computeVertexNormals();
    var mesh = new THREE.Mesh(geo, new THREE.MeshStandardMaterial({
      vertexColors: true,
      roughness: 0.94,
      metalness: 0.02,
      side: THREE.DoubleSide,
    }));
    mesh.receiveShadow = true;
    mesh.castShadow = false;
    mesh.renderOrder = 1;
    scene.add(mesh);
  }

  function buildTrack() {
    addTrackSurface();
    var mounds = C.TRACK_MOUNDS || [];
    for (var i = 0; i < mounds.length; i++) {
      var m = mounds[i], p = worldToThree(m.x, m.y);
      var deck = ranchGroundY(m.x, m.y);
      var name = m.h >= 1 ? "HILL" : (m.h < 0 ? "DIP" : "RISE");
      addLabel(name, "#fef3c7", p.x, deck + 0.55, p.z);
    }
    addPathRibbon(C.TRACK_MAIN, 0.02, 0x1c1917, 0.78, 0, true);
    addPathRibbon(C.TRACK_MAIN, 0.02, 0xfbbf24, 0.14, 0.02, true);
    addPathRibbon(C.TRACK_MAIN, 0.02, 0xfafaf9, 0.045, 0.035, true);
    addPathRibbon(C.TRACK_BRANCH_A, 0.02, 0x292524, 0.42, 0.01, true);
    addPathRibbon(C.TRACK_BRANCH_A, 0.02, 0xa8a29e, 0.1, 0.025, true);
    addPathRibbon(C.TRACK_BRANCH_B, 0.02, 0x292524, 0.38, 0.01, true);
    addPathRibbon(C.TRACK_BRANCH_B, 0.02, 0xa8a29e, 0.09, 0.025, true);
    /* view1: sensible pillars under elevated ribbon (paired posts + crossbeam) */
    (function addTrackSupports() {
      var list = C.TRACK_SUPPORTS || [];
      var postMat = new THREE.MeshStandardMaterial({ color: 0x78716c, roughness: 0.88, metalness: 0.12 });
      var beamMat = new THREE.MeshStandardMaterial({ color: 0x57534e, roughness: 0.9, metalness: 0.08 });
      for (var si = 0; si < list.length; si++) {
        var s = list[si];
        var sp = worldToThree(s.x, s.y);
        var topY = ranchGroundY(s.x, s.y);
        if (topY < 0.28) continue;
        var h = Math.max(0.35, topY - 0.02);
        var half = 0.38;
        for (var side = -1; side <= 1; side += 2) {
          var post = new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.08, h, 6), postMat);
          post.position.set(sp.x + side * half, h * 0.5, sp.z);
          post.castShadow = true;
          scene.add(post);
        }
        var beam = new THREE.Mesh(new THREE.BoxGeometry(half * 2 + 0.12, 0.07, 0.1), beamMat);
        beam.position.set(sp.x, topY - 0.02, sp.z);
        scene.add(beam);
      }
    })();
    var ramps = C.RAMPS || [];
    for (var r = 0; r < ramps.length; r++) {
      var rp = ramps[r], tp = worldToThree(rp.x, rp.y);
      var rampDeck = ranchGroundY(rp.x, rp.y);
      /* Crest marker, seated on the deck. */
      var ramp = new THREE.Mesh(
        new THREE.ConeGeometry(0.16, 0.28, 4),
        new THREE.MeshStandardMaterial({ color: 0xf59e0b, metalness: 0.15 })
      );
      ramp.position.set(tp.x, rampDeck + 0.16, tp.z);
      ramp.rotation.y = 0.4;
      scene.add(ramp);
    }
    /* Banks are in the apron height. The label marks the crown. */
    var banks = C.TRACK_BANKS || [];
    for (var bi = 0; bi < banks.length; bi++) {
      var bk = banks[bi], bp = worldToThree(bk.x, bk.y);
      var bankDeck = ranchGroundY(bk.x, bk.y);
      addLabel(bk.label || "BANK", "#fef3c7", bp.x, bankDeck + 0.45, bp.z);
    }
    /* Rocks sit in the bump the height already adds, crown just above the deck. */
    var rocks = C.TRACK_ROCKS || [];
    state.trackRocks = [];
    for (var rk = 0; rk < rocks.length; rk++) {
      var rko = rocks[rk], rp3 = worldToThree(rko.x, rko.y);
      var rockDeck = ranchGroundY(rko.x, rko.y);
      var rad = rko.r * 0.018;
      var sy = 0.85 + (rko.h || 1) * 0.25;
      var rock = new THREE.Mesh(
        new THREE.DodecahedronGeometry(rad, 0),
        new THREE.MeshStandardMaterial({ color: 0x57534e, roughness: 0.88, flatShading: true })
      );
      rock.scale.set(1, sy, 1);
      rock.position.set(rp3.x, rockDeck - rad * sy * 0.45, rp3.z);
      rock.castShadow = true;
      scene.add(rock);
      addLabel("ROCK", "#e7e5e4", rp3.x, rockDeck + rad * sy * 0.7, rp3.z);
      state.trackRocks.push({ data: rko, mesh: rock, id: "track-rock-" + rk, x: rko.x, y: rko.y, r: (rko.r || 10) + 8 });
    }

    /* polish9: start/finish gate */
    var gp = worldToThree(1870, 2225);
    var gateDeck = ranchGroundY(1870, 2225);
    var postMat = new THREE.MeshStandardMaterial({ color: 0xf8fafc, roughness: 0.4 });
    var leftPost = new THREE.Mesh(new THREE.BoxGeometry(0.12, 1.4, 0.12), postMat);
    leftPost.position.set(gp.x - 1.4, gateDeck + 0.7, gp.z); scene.add(leftPost);
    var rightPost = new THREE.Mesh(new THREE.BoxGeometry(0.12, 1.4, 0.12), postMat);
    rightPost.position.set(gp.x + 1.4, gateDeck + 0.7, gp.z); scene.add(rightPost);
    var banner = new THREE.Mesh(
      new THREE.BoxGeometry(2.8, 0.28, 0.06),
      new THREE.MeshStandardMaterial({ color: 0x0f172a, emissive: 0xfbbf24, emissiveIntensity: 0.25 })
    );
    banner.position.set(gp.x, gateDeck + 1.35, gp.z); scene.add(banner);
    addLabel("START / FINISH", "#fef3c7", gp.x, gateDeck + 1.75, gp.z);
  }

  function buildPondLife() {
    var pond = C.AREAS[2];
    state.fish = []; state.whales = []; state.schools = [];
    /* polish8: fish school centers */
    for (var sc = 0; sc < 4; sc++) {
      var scp = worldToThree(pond.x + 120 + Math.random() * (pond.w - 240), pond.y + 120 + Math.random() * (pond.h - 240));
      state.schools.push({ x: scp.x, z: scp.z, phase: Math.random() * Math.PI * 2 });
    }
    for (var f = 0; f < 28; f++) {
      var sch = state.schools[f % 4];
      var fp = { x: sch.x + (Math.random() - 0.5) * 1.2, z: sch.z + (Math.random() - 0.5) * 1.0 };
      var fishG = new THREE.Group();
      var fish = new THREE.Mesh(
        new THREE.SphereGeometry(0.18 + Math.random() * 0.08, 8, 6),
        new THREE.MeshStandardMaterial({ color: 0xfde68a, emissive: 0xb45309, emissiveIntensity: 0.3 })
      );
      fish.scale.set(1.5, 0.55, 0.7);
      fishG.add(fish);
      var fin = new THREE.Mesh(
        new THREE.ConeGeometry(0.08, 0.18, 5),
        new THREE.MeshStandardMaterial({ color: 0xf59e0b })
      );
      fin.position.set(-0.22, 0.02, 0); fin.rotation.z = Math.PI / 2; fishG.add(fin);
      fishG.position.set(fp.x, 0.14, fp.z);
      fishG.userData.phase = Math.random() * Math.PI * 2; fishG.userData.bx = fp.x; fishG.userData.bz = fp.z;
      fishG.userData.school = f % 4; fishG.userData.ox = fp.x - sch.x; fishG.userData.oz = fp.z - sch.z;
      scene.add(fishG); state.fish.push(fishG);
    }
    for (var w = 0; w < 5; w++) {
      var wp = worldToThree(pond.x + 160 + Math.random() * (pond.w - 320), pond.y + 140 + Math.random() * (pond.h - 280));
      var whaleG = new THREE.Group();
      var whale = new THREE.Mesh(
        new THREE.SphereGeometry(0.6 + Math.random() * 0.22, 12, 8),
        new THREE.MeshStandardMaterial({ color: 0x7dd3fc, roughness: 0.4, metalness: 0.18, emissive: 0x0c4a6e, emissiveIntensity: 0.2 })
      );
      whale.scale.set(1.85, 0.5, 1); whaleG.add(whale);
      var spout = new THREE.Mesh(
        new THREE.CylinderGeometry(0.04, 0.08, 0.55, 6),
        new THREE.MeshStandardMaterial({ color: 0xe0f2fe, transparent: true, opacity: 0.7 })
      );
      spout.position.set(0.15, 0.45, 0); whaleG.add(spout);
      whaleG.position.set(wp.x, 0.22, wp.z);
      whaleG.userData.phase = Math.random() * Math.PI * 2; whaleG.userData.bx = wp.x; whaleG.userData.bz = wp.z;
      whaleG.userData.breach = Math.random() * Math.PI * 2; whaleG.userData.breachAmp = 0.55 + Math.random() * 0.45;
      scene.add(whaleG); state.whales.push(whaleG);
      addLabel("whale", "#e0f2fe", wp.x, 1.15, wp.z);
    }
    var pl = worldToThree(pond.x + pond.w * 0.5, pond.y + 40);
    var pc = worldToThree(pond.x + pond.w / 2, pond.y + pond.h / 2);
    var shore = new THREE.Mesh(
      new THREE.RingGeometry(Math.min(pond.w, pond.h) * 0.0085, Math.min(pond.w, pond.h) * 0.0112, 64),
      new THREE.MeshBasicMaterial({ color: 0xe0f2fe, transparent: true, opacity: 0.72, side: THREE.DoubleSide })
    );
    shore.rotation.x = -Math.PI / 2;
    shore.position.set(pc.x, 0.16, pc.z);
    shore.scale.set(pond.w / Math.min(pond.w, pond.h), 1, pond.h / Math.min(pond.w, pond.h));
    scene.add(shore);
    addLabel("Pond · fishies & whales", "#ecfeff", pl.x, 1.5, pl.z);
    /* pond1: docked submarine at south perimeter */
    var dock = C.SUB_DOCK || { x: 2900, y: 1240 };
    var parkSub = C.vehiclePos ? C.vehiclePos("submarine", dock.x, dock.y) : dock;
    var sp = worldToThree(parkSub.x, parkSub.y);
    state.parkedSub = makeSubMesh(0x7dd3fc);
    state.parkedSub.position.set(sp.x, 0.22, sp.z);
    state.parkedSub.rotation.y = -Math.PI / 2; /* subyaw1: nose +X → default face (+Z) */
    scene.add(state.parkedSub);
    addLabel("Submarine", "#e0f2fe", sp.x, 1.6, sp.z);
  }

  function buildStarshipApproach() {
    var path = C.STARSHIP_APPROACH; if (!path || !path.length) return;
    var pts = path.map(function (pt) {
      var p = worldToThree(pt[0], pt[1]);
      return new THREE.Vector3(p.x, 0.08, p.z);
    });
    scene.add(new THREE.Mesh(
      new THREE.TubeGeometry(new THREE.CatmullRomCurve3(pts, false), 32, 0.12, 6, false),
      new THREE.MeshStandardMaterial({ color: 0xfbbf24, emissive: 0xb45309, emissiveIntensity: 0.25 })
    ));
  }

  function buildTrucks() {
    state.parkedTrucks = [];
    var spots = C.TRUCK_SPOTS || [];
    for (var i = 0; i < spots.length; i++) {
      var s = spots[i];
      var style = s.vehicleStyle || (C.vehicleStyleOf ? C.vehicleStyleOf("truck-" + s.id) : "cybertruck");
      var accent = s.id === "shared" ? 0xfbbf24
        : style === "ripsaw" ? 0xa8a29e
        : style === "tank" ? 0x6b7280
        : hex((C.FROG_DEFS[s.id] || C.FROG_DEFS.james).color);
      var truck = makeVehicleMesh(style, accent);
      var p = worldToThree(s.x, s.y);
      var spotDeck = ranchGroundY(s.x, s.y);
      truck.position.set(p.x, spotDeck, p.z);
      truck.rotation.y = -Math.PI / 2; /* polish11: nose +Z like idle frogs, not sideways +X */
      scene.add(truck);
      var label = s.id === "shared" ? "★ ALL ABOARD · 4"
        : style === "ripsaw" ? "Ripsaw · shared"
        : style === "tank" ? "Tank · shared"
        : ("Cybertruck · " + (C.FROG_DEFS[s.id] || {}).name);
      var lab = labelSprite(label, s.id === "shared" ? "#fef3c7" : (style === "ripsaw" || style === "tank" ? "#e2e8f0" : "#fde68a"));
      lab.position.set(p.x, (s.id === "shared" ? 1.85 : 1.5) + spotDeck, p.z); scene.add(lab);
      if (s.id === "shared") {
        var pad = new THREE.Mesh(
          new THREE.RingGeometry(1.1, 1.45, 32),
          new THREE.MeshBasicMaterial({ color: 0xfbbf24, transparent: true, opacity: 0.55, side: THREE.DoubleSide })
        );
        pad.rotation.x = -Math.PI / 2; pad.position.set(p.x, spotDeck + 0.06, p.z); scene.add(pad);
        var ids = ["james", "jimmy", "bubbles", "rexy"];
        for (var si = 0; si < 4; si++) {
          var col = hex((C.FROG_DEFS[ids[si]] || C.FROG_DEFS.james).color);
          var slot = new THREE.Mesh(new THREE.SphereGeometry(0.14, 8, 6), new THREE.MeshStandardMaterial({ color: col }));
          slot.position.set(p.x + (si - 1.5) * 0.35, spotDeck + 0.85, p.z); scene.add(slot);
        }
      }
      state.parkedTrucks.push({ spot: s, mesh: truck, label: lab });
    }
  }


  function buildAirCrafts() {
    var Air = global.FroggiesAir;
    state.airPads = [];
    state.parkedAir = [];
    var kinds = [
      { kind: "heli", pad: (C.HELI_PAD || { x: 980, y: 2000 }), accent: 0x94a3b8 },
      { kind: "drone", pad: (C.DRONE_PAD || { x: 1180, y: 2000 }), accent: 0x67e8f9 },
    ];
    for (var i = 0; i < kinds.length; i++) {
      var k = kinds[i];
      var home = C.vehiclePos ? C.vehiclePos(k.kind, k.pad.x, k.pad.y) : k.pad;
      var p = worldToThree(home.x, home.y);
      var deck = ranchGroundY(home.x, home.y);
      var padMesh = makeAirPadMesh(k.kind);
      padMesh.position.set(p.x, deck, p.z);
      scene.add(padMesh);
      state.airPads.push({ kind: k.kind, mesh: padMesh, wx: home.x, wy: home.y });
      var craft = k.kind === "drone" ? makePassengerDroneMesh(k.accent) : makeHeliMesh(k.accent);
      /* Sit just above raised pad disc so craft is never under orange zone */
      craft.position.set(p.x, deck + AIR_PAD_LIFT, p.z);
      craft.rotation.y = k.kind === "heli" ? Math.PI / 2 : -Math.PI / 2; /* heliyaw1: match faceAngle -PI/2 */
      scene.add(craft);
      state.parkedAir.push({ kind: k.kind, mesh: craft, wx: home.x, wy: home.y });
      if (Air && Air.ensureCraft) {
        /* ensure shared air state exists for Three solo session */
        if (!state.airWorld) state.airWorld = { air: null, hotspots: (C.HOTSPOTS || []).map(function (h) {
          return { id: h.id, kind: h.kind, x: h.x, y: h.y, r: h.r, tip: h.tip, mode: h.mode, vehicleStyle: h.vehicleStyle, seats: h.seats };
        }) };
        Air.ensureCraft(state.airWorld, k.kind);
      }
    }
  }

  function buildRanch() {
    scene = new THREE.Scene();
    scene.position.set(0, 0, 0);
    scene.background = new THREE.Color(0x7eb8d4);
    /* view3: softer haze — trees stay readable; was flat green wall at 48–145 */
    scene.fog = new THREE.Fog(0x8eb89a, 95, 290); /* mech5: farther fog for expanded ranch */

    // Fixed-angle isometric-ish camera — orbit LOCKED (no free-fly)
    var aspect = window.innerWidth / Math.max(1, window.innerHeight);
    camera = new THREE.PerspectiveCamera(46, aspect, 0.1, 400);
    camera.position.set(18, 22, 18);
    camera.lookAt(0, 0, 0);
    camera.userData.lockTarget = new THREE.Vector3(0, 0, 0);

    var hemi = new THREE.HemisphereLight(0xfff0d0, 0x3a5a2a, 0.85);
    scene.add(hemi);
    var sun = new THREE.DirectionalLight(0xffe6b0, 0.95);
    sun.position.set(12, 22, 8);
    sun.castShadow = true;
    sun.shadow.mapSize.set(2048, 2048);
    /* solid1: reduce shadow acne flicker on ranch floor while walking */
    sun.shadow.bias = -0.00035;
    sun.shadow.normalBias = 0.035;
    scene.add(sun);

    /* Ranch floor with a hole where the truck apron is. The apron mesh
       (including valleys below y=0) would be hidden by a solid plane. */
    var halfW = C.MAP_W * 0.01;
    var halfH = C.MAP_H * 0.01;
    var floorShape = new THREE.Shape();
    floorShape.moveTo(-halfW, -halfH);
    floorShape.lineTo(halfW, -halfH);
    floorShape.lineTo(halfW, halfH);
    floorShape.lineTo(-halfW, halfH);
    floorShape.closePath();
    var trackA = C.AREAS[1];
    function shapeX(wx) { return (wx - C.MAP_W * 0.5) * 0.02; }
    /* Rx(-90°): geometry +Y becomes world −Z, so shape Y is −world Z. */
    function shapeY(wy) { return -((wy - C.MAP_H * 0.5) * 0.02); }
    var hx0 = shapeX(trackA.x), hx1 = shapeX(trackA.x + trackA.w);
    var hy0 = shapeY(trackA.y), hy1 = shapeY(trackA.y + trackA.h);
    var hole = new THREE.Path();
    hole.moveTo(hx0, hy0);
    hole.lineTo(hx1, hy0);
    hole.lineTo(hx1, hy1);
    hole.lineTo(hx0, hy1);
    hole.closePath();
    floorShape.holes.push(hole);
    var ground = new THREE.Mesh(
      new THREE.ShapeGeometry(floorShape),
      new THREE.MeshStandardMaterial({
        color: 0x3d7a35, roughness: 0.9,
        polygonOffset: true, polygonOffsetFactor: 1, polygonOffsetUnits: 1,
      })
    );
    ground.rotation.x = -Math.PI / 2;
    ground.position.y = 0;
    ground.receiveShadow = true;
    ground.renderOrder = -2;
    scene.add(ground);
    var bowlC = worldToThree(trackA.x + trackA.w * 0.5, trackA.y + trackA.h * 0.5);
    var bowl = new THREE.Mesh(
      new THREE.PlaneGeometry(trackA.w * 0.02 + 0.4, trackA.h * 0.02 + 0.4),
      new THREE.MeshStandardMaterial({ color: 0x1c1410, roughness: 1 })
    );
    bowl.rotation.x = -Math.PI / 2;
    bowl.position.set(bowlC.x, -1.2, bowlC.z);
    bowl.receiveShadow = true;
    bowl.renderOrder = -3;
    scene.add(bowl);

    /* view3: continuous perimeter forest — SAME trunk/canopy recipe + scale as yard trees.
       Prior rings used s=1.35–3.0 + green cylinder berms → mismatched flat backdrop.
       mech5: rings follow expanded MAP_* (more driveable green; house size unchanged). */
    state.paraHills = [];
    state.forestRing = [];
    (function buildForestPerimeter() {
      var trunkMat = new THREE.MeshStandardMaterial({ color: 0x78350f, roughness: 0.9 });
      var canopyMats = [
        new THREE.MeshStandardMaterial({ color: 0x16a34a, roughness: 0.85 }),
        new THREE.MeshStandardMaterial({ color: 0x15803d, roughness: 0.88 }),
        new THREE.MeshStandardMaterial({ color: 0x4d7c0f, roughness: 0.9 }),
      ];
      var halfW = C.MAP_W * 0.01;
      var halfH = C.MAP_H * 0.01;
      /* Match yard1 plant: Cylinder 0.08/0.12/0.9 * s, Sphere (r||14)*0.045*s, s≈0.88–1.3 */
      function plantTree(x, z, s, ci, r) {
        var g = new THREE.Group();
        var ts = s || 1;
        var trunk = new THREE.Mesh(
          new THREE.CylinderGeometry(0.08 * ts, 0.12 * ts, 0.9 * ts, 6),
          trunkMat
        );
        trunk.position.y = 0.45 * ts;
        trunk.castShadow = true;
        g.add(trunk);
        var canopy = new THREE.Mesh(
          new THREE.SphereGeometry((r || 14) * 0.045 * ts, 8, 6),
          canopyMats[ci % canopyMats.length]
        );
        canopy.position.y = 1.05 * ts;
        canopy.castShadow = true;
        g.add(canopy);
        g.position.set(x, 0, z);
        scene.add(g);
        state.forestRing.push(g);
      }
      function plantShrub(x, z, s) {
        var bush = new THREE.Mesh(
          new THREE.SphereGeometry(0.22 * (s || 0.8), 6, 5),
          new THREE.MeshStandardMaterial({ color: 0x4d7c0f, roughness: 0.9 })
        );
        bush.position.set(x, 0.18, z);
        scene.add(bush);
        state.forestRing.push(bush);
      }
      /* Outer rings — forest forever at playable tree scale (not giant backdrop props) */
      var rings = [
        { rScale: 1.04, n: 68, s0: 0.88, s1: 1.18 },
        { rScale: 1.12, n: 84, s0: 0.92, s1: 1.22 },
        { rScale: 1.22, n: 96, s0: 0.95, s1: 1.28 },
        { rScale: 1.34, n: 92, s0: 0.9, s1: 1.3 },
        { rScale: 1.48, n: 80, s0: 0.98, s1: 1.28 },
        { rScale: 1.64, n: 72, s0: 1.0, s1: 1.3 },
        { rScale: 1.82, n: 64, s0: 0.95, s1: 1.25 },
      ];
      for (var ri = 0; ri < rings.length; ri++) {
        var rg = rings[ri];
        for (var ti = 0; ti < rg.n; ti++) {
          var ang = (ti / rg.n) * Math.PI * 2 + ri * 0.09;
          var jr = rg.rScale * (0.94 + (ti % 5) * 0.025);
          var s = rg.s0 + (ti % 7) * ((rg.s1 - rg.s0) / 7);
          var rr = 11 + (ti % 5);
          plantTree(Math.cos(ang) * halfW * jr, Math.sin(ang) * halfH * jr, s, ti + ri, rr);
          if (ti % 3 === 0) {
            plantShrub(
              Math.cos(ang + 0.04) * halfW * jr * 0.98,
              Math.sin(ang + 0.04) * halfH * jr * 0.98,
              0.65 + (ti % 4) * 0.08
            );
          }
        }
      }
      /* Dense corners so the woods read continuous (no flat sky gaps) */
      var corners = [
        [-halfW * 1.12, -halfH * 1.12], [halfW * 1.12, -halfH * 1.12],
        [-halfW * 1.12, halfH * 1.12], [halfW * 1.12, halfH * 1.12],
        [-halfW * 1.26, 0], [halfW * 1.26, 0], [0, -halfH * 1.26], [0, halfH * 1.26],
        [-halfW * 1.4, -halfH * 0.55], [halfW * 1.4, -halfH * 0.55],
        [-halfW * 1.4, halfH * 0.55], [halfW * 1.4, halfH * 0.55],
      ];
      for (var ci = 0; ci < corners.length; ci++) {
        for (var k = 0; k < 7; k++) {
          plantTree(
            corners[ci][0] + (k - 3) * 1.15,
            corners[ci][1] + ((k % 3) - 1) * 1.05,
            0.9 + (k % 5) * 0.08,
            ci + k,
            12 + (k % 4)
          );
        }
      }
    })();

    // Soft grid — lifted + no depth write (was z-fighting ground → floor shudder)
    var grid = new THREE.GridHelper(Math.max(C.MAP_W, C.MAP_H) * 0.02, 30, 0x2f5e2a, 0x2f5e2a);
    grid.position.y = 0.14;
    if (Array.isArray(grid.material)) {
      for (var gi = 0; gi < grid.material.length; gi++) {
        grid.material[gi].opacity = 0.22;
        grid.material[gi].transparent = true;
        grid.material[gi].depthWrite = false;
      }
    } else {
      grid.material.opacity = 0.22;
      grid.material.transparent = true;
      grid.material.depthWrite = false;
    }
    grid.renderOrder = -1;
    scene.add(grid);

    state.areaMeshes = [];
    for (var i = 0; i < C.AREAS.length; i++) {
      var a = C.AREAS[i];
      /* padfix1: house-zone orange floor stops before heli/drone pads (was burying craft) */
      var zoneH = a.h;
      var zoneCy = a.y + a.h / 2;
      if (a.id === "house") {
        var heliY = (C.HELI_PAD && C.HELI_PAD.y) || 2000;
        var droneY = (C.DRONE_PAD && C.DRONE_PAD.y) || 2000;
        var padStopY = Math.min(heliY, droneY) - 70;
        zoneH = Math.max(200, Math.min(a.h, padStopY - a.y));
        zoneCy = a.y + zoneH / 2;
      }
      var p = worldToThree(a.x + a.w / 2, zoneCy);
      /* The apron mesh is the track floor. A flat pad would cover the valleys. */
      if (a.id === "track") {
        addLabel(a.name, "#ffffff", p.x, 6.6, p.z);
        continue;
      }
      /* solid1: flat zone pads (not thick boxes) — thick boxes z-fought the ground plane */
      var mesh = new THREE.Mesh(
        new THREE.PlaneGeometry(a.w * 0.02, zoneH * 0.02),
        new THREE.MeshStandardMaterial({
          color: hex(a.color),
          roughness: 0.85,
          transparent: true,
          opacity: a.id === "house" ? 0.72 : 0.92,
          polygonOffset: true, polygonOffsetFactor: -1, polygonOffsetUnits: -2,
        })
      );
      mesh.rotation.x = -Math.PI / 2;
      mesh.position.set(p.x, 0.04, p.z);
      mesh.receiveShadow = true;
      mesh.renderOrder = -1;
      scene.add(mesh);
      state.areaMeshes.push(mesh);
      var edgeA = new THREE.LineSegments(
        new THREE.EdgesGeometry(new THREE.PlaneGeometry(a.w * 0.02, zoneH * 0.02)),
        new THREE.LineBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.4, depthWrite: false })
      );
      edgeA.rotation.x = -Math.PI / 2;
      edgeA.position.set(p.x, 0.05, p.z);
      scene.add(edgeA);
      addLabel(a.name, "#ffffff", p.x, 3.2, p.z);
    }

    buildCompound();
    buildTrack();
    buildPondLife();
    buildStarshipApproach();
    buildTrucks();
    buildAirCrafts();

    // Hotspots (non-truck rings — trucks drawn as Cybertrucks) — polish4 inviting
    state.hotMeshes = [];
    for (var h = 0; h < C.HOTSPOTS.length; h++) {
      var hs = C.HOTSPOTS[h];
      if (C.isTruckHotspot && C.isTruckHotspot(hs)) continue;
      if (C.isAirHotspot && C.isAirHotspot(hs)) continue;
      var hp = worldToThree(hs.x, hs.y);
      var ring = new THREE.Mesh(
        new THREE.RingGeometry(0.85, 1.2, 28),
        new THREE.MeshBasicMaterial({ color: 0xfbbf24, transparent: true, opacity: 0.55, side: THREE.DoubleSide })
      );
      var hotDeck = ranchGroundY(hs.x, hs.y);
      ring.rotation.x = -Math.PI / 2;
      ring.position.set(hp.x, hotDeck + 0.06, hp.z);
      scene.add(ring);
      var glow = new THREE.Mesh(
        new THREE.CircleGeometry(1.15, 20),
        new THREE.MeshBasicMaterial({ color: 0xfbbf24, transparent: true, opacity: 0.15, side: THREE.DoubleSide })
      );
      glow.rotation.x = -Math.PI / 2; glow.position.set(hp.x, hotDeck + 0.04, hp.z); scene.add(glow);
      if (hs.id === "phone") {
        var booth = new THREE.Mesh(
          new THREE.BoxGeometry(0.45, 0.9, 0.4),
          new THREE.MeshStandardMaterial({ color: 0x7c3aed, metalness: 0.2 })
        );
        booth.position.set(hp.x, hotDeck + 0.5, hp.z); scene.add(booth);
        addLabel("Phone → Purple Bear", "#e9d5ff", hp.x, hotDeck + 1.9, hp.z);
      } else if (hs.id === "sps") {
        var dish = new THREE.Mesh(
          new THREE.SphereGeometry(0.35, 10, 8, 0, Math.PI * 2, 0, Math.PI * 0.5),
          new THREE.MeshStandardMaterial({ color: 0x38bdf8, metalness: 0.4, side: THREE.DoubleSide })
        );
        dish.position.set(hp.x, hotDeck + 0.35, hp.z); dish.rotation.x = -0.5; scene.add(dish);
        addLabel("SPS → Optimus · Jimmy", "#bae6fd", hp.x, hotDeck + 1.9, hp.z);
      } else {
        addLabel(hs.label, "#fde68a", hp.x, hotDeck + 1.7, hp.z);
      }
      state.hotMeshes.push({ data: hs, ring: ring });
    }

    var ss = C.STARSHIP || { x: 360, y: 320 };
    var sp = worldToThree(ss.x, ss.y);
    var pad = new THREE.Mesh(
      new THREE.CylinderGeometry(2.4, 2.6, 0.12, 32),
      new THREE.MeshStandardMaterial({ color: 0x334155, metalness: 0.5, roughness: 0.4 })
    );
    pad.position.set(sp.x, 0.06, sp.z); scene.add(pad);
    var padRing = new THREE.Mesh(
      new THREE.RingGeometry(1.6, 2.2, 32),
      new THREE.MeshBasicMaterial({ color: 0xfbbf24, transparent: true, opacity: 0.7, side: THREE.DoubleSide })
    );
    padRing.rotation.x = -Math.PI / 2; padRing.position.set(sp.x, 0.14, sp.z); scene.add(padRing);
    var rocket = new THREE.Mesh(
      new THREE.ConeGeometry(0.45, 2.2, 10),
      new THREE.MeshStandardMaterial({ color: 0xe2e8f0, metalness: 0.5 })
    );
    rocket.position.set(sp.x, 1.3, sp.z); scene.add(rocket);
    var spotty = new THREE.Mesh(
      new THREE.SphereGeometry(0.35, 12, 10),
      new THREE.MeshStandardMaterial({ color: 0xfdba74 })
    );
    spotty.position.set(sp.x + 1.1, 0.45, sp.z); scene.add(spotty);
    addLabel("★ STARSHIP · SPACE", "#fef3c7", sp.x, 3.0, sp.z);
    addLabel("Spotty", "#fdba74", sp.x + 1.1, 1.3, sp.z);

    var def = C.FROG_DEFS[state.frogId];
    var spawnW = (C.COMPOUND && C.COMPOUND.spawn) || { x: 280, y: 1750 };
    state.player = makeFrogMesh(def, 1.7);
    var spawn = worldToThree(spawnW.x, spawnW.y);
    state.player.position.set(spawn.x, 0.02, spawn.z);
    if (state.player.userData.ring) state.player.userData.ring.material.opacity = 0.85;
    scene.add(state.player);
    /* polish6 + solid1: soft depth shadow — depthWrite off + lifted so they don't fight the floor */
    state.playerShadow = new THREE.Mesh(
      new THREE.CircleGeometry(0.55, 20),
      new THREE.MeshBasicMaterial({
        color: 0x000000, transparent: true, opacity: 0.32, side: THREE.DoubleSide,
        depthWrite: false,
      })
    );
    state.playerShadow.rotation.x = -Math.PI / 2;
    state.playerShadow.position.set(spawn.x, 0.07, spawn.z);
    state.playerShadow.renderOrder = 1;
    scene.add(state.playerShadow);
    state.playerShadowSoft = new THREE.Mesh(
      new THREE.CircleGeometry(0.85, 20),
      new THREE.MeshBasicMaterial({
        color: 0x000000, transparent: true, opacity: 0.14, side: THREE.DoubleSide,
        depthWrite: false,
      })
    );
    state.playerShadowSoft.rotation.x = -Math.PI / 2;
    state.playerShadowSoft.position.set(spawn.x, 0.065, spawn.z);
    state.playerShadowSoft.renderOrder = 1;
    scene.add(state.playerShadowSoft);
    state.nameTag = labelSprite(def.name, def.color || "#fff");
    state.nameTag.position.set(spawn.x, 2.95, spawn.z);
    state.nameTag.scale.set(2.8, 0.7, 1);
    scene.add(state.nameTag);
    /* polish9: aboard frog icons (shared truck) */
    state.aboardGroup = new THREE.Group();
    state.aboardGroup.visible = false;
    for (var abi = 0; abi < C.FROG_ORDER.length; abi++) {
      var aid = C.FROG_ORDER[abi];
      var adef = C.FROG_DEFS[aid];
      var dot = new THREE.Mesh(
        new THREE.SphereGeometry(0.16, 10, 8),
        new THREE.MeshStandardMaterial({ color: hex(adef.color), emissive: hex(adef.color), emissiveIntensity: 0.25 })
      );
      dot.position.set((abi - 1.5) * 0.38, 0.95, 0);
      state.aboardGroup.add(dot);
    }
    scene.add(state.aboardGroup);
    state.aboardLabel = labelSprite("Aboard", "#fef3c7");
    state.aboardLabel.scale.set(1.6, 0.4, 1);
    state.aboardLabel.visible = false;
    scene.add(state.aboardLabel);
    state.kitFxT = 0; state.kitFxKind = ""; state.lapSide = 0; state.lapCd = 0; state.lapCount = 0;
    /* view3: establishing shot south of the yard looking north — frames all four
       froggies + garage mouth + four Cybertrucks. Prior cam sat west/close and
       read as looking the wrong way through the garage bay. */
    var garBox = (C.COMPOUND && C.COMPOUND.garage) || { x: 700, y: 1400, w: 480, h: 520 };
    var garMouth = worldToThree(garBox.x + garBox.w * 0.5, garBox.y + garBox.h);
    var truckMid = worldToThree(2180, 1720);
    var lookX = spawn.x * 0.22 + garMouth.x * 0.28 + truckMid.x * 0.50;
    var lookZ = spawn.z * 0.28 + garMouth.z * 0.22 + truckMid.z * 0.50;
    var ESTAB_T = 3.2;
    var estCam = {
      x: lookX - 2,
      y: 38,
      z: Math.max(spawn.z, truckMid.z) + 48,
    };
    camera.userData.lockTarget.set(spawn.x, 0, spawn.z);
    camera.position.set(estCam.x, estCam.y, estCam.z);
    camera.lookAt(lookX, 0.6, lookZ);
    state.establishT = ESTAB_T;
    state.establishDur = ESTAB_T;
    state.establishLook = { x: lookX, z: lookZ };
    state.establishCam = estCam;

    state.driveTruck = makeTruckMesh(hex(def.color));
    state.driveTruck.visible = false;
    scene.add(state.driveTruck);
    state.waterPlane = new THREE.Mesh(
      new THREE.BoxGeometry(1.7, 0.08, 0.85),
      new THREE.MeshStandardMaterial({ color: 0x0e7490, transparent: true, opacity: 0.55 })
    );
    state.waterPlane.visible = false;
    scene.add(state.waterPlane);

    state.companions = [];
    for (var ci = 0; ci < C.FROG_ORDER.length; ci++) {
      var cid = C.FROG_ORDER[ci];
      if (cid === state.frogId) continue;
      var cdef = C.FROG_DEFS[cid];
      var cmesh = makeFrogMesh(cdef, 1.35);
      if (cmesh.userData.ring) cmesh.userData.ring.material.opacity = 0.35;
      var cp = worldToThree(spawnW.x + 40 + ci * 36, spawnW.y + 20 + (ci % 2) * 16);
      cmesh.position.set(cp.x, 0, cp.z);
      cmesh.userData.tx = cp.x;
      cmesh.userData.tz = cp.z;
      cmesh.userData.timer = 1 + Math.random();
      cmesh.userData.frogId = cid;
      cmesh.userData.followLag = 0.35 + ci * 0.12;
      cmesh.userData.idleBounce = Math.random() * 6;
      cmesh.userData.chatT = 0;
      scene.add(cmesh);
      var ctag = labelSprite(cdef.name, cdef.color || "#fff");
      ctag.scale.set(2.0, 0.5, 1);
      ctag.position.set(cp.x, 2.55, cp.z);
      scene.add(ctag);
      cmesh.userData.nameTag = ctag;
      state.companions.push(cmesh);
    }

    /* Couch multi-local: claim pads from seatMap — extras steerable, primary = camera */
    state.seatMap = (hooks && hooks.seatMap) || state.seatMap || null;
    state.primaryPadIndex = null;
    if (state.seatMap) {
      var sm = state.seatMap;
      var primarySeat = sm[state.frogId];
      if (primarySeat && primarySeat.local && primarySeat.padIndex != null) {
        state.primaryPadIndex = primarySeat.padIndex;
      }
      for (var li = 0; li < state.companions.length; li++) {
        var cm = state.companions[li];
        var cid2 = cm.userData.frogId;
        var seat = sm[cid2];
        if (seat && seat.human && seat.local) {
          cm.userData.local = true;
          cm.userData.padIndex = seat.padIndex != null ? seat.padIndex : null;
          cm.userData.vx = 0;
          cm.userData.vz = 0;
          cm.userData.ai = false;
          cm.userData.inMech = false;
          cm.userData.mechId = null;
          cm.userData.mechStories = 0;
          cm.userData.inTruck = false;
          cm.userData.truckId = null;
          cm.userData.truckMode = null;
          cm.userData.vehicleStyle = null;
          cm.userData.inSub = false;
          cm.userData.subId = null;
          cm.userData.inHeli = false;
          cm.userData.inDrone = false;
          cm.userData.airKind = null;
          cm.userData.airSeat = null;
          if (cm.userData.ring) cm.userData.ring.material.opacity = 0.75;
        }
      }
    }

    /* polish7: zone signs (world labels) + mini-map overlay canvas */
    var hostEl0 = document.getElementById("engine-host");
    if (hostEl0) {
      var oldMaps = hostEl0.querySelectorAll("canvas");
      /* keep three renderer canvas; drop prior mini-map canvases we tagged */
      for (var omi = 0; omi < oldMaps.length; omi++) {
        if (oldMaps[omi].dataset && oldMaps[omi].dataset.ffMinimap === "1") oldMaps[omi].remove();
      }
    }
    state.zoneSigns = [];
    var zlist = C.ZONE_SIGNS || [];
    for (var zi = 0; zi < zlist.length; zi++) {
      var zs = zlist[zi];
      var zp = worldToThree(zs.x, zs.y);
      var zlab = labelSprite(zs.label, zs.color || "#fef3c7");
      zlab.scale.set(3.2, 0.85, 1);
      zlab.position.set(zp.x, 3.2, zp.z);
      zlab.material.opacity = 0;
      zlab.material.transparent = true;
      zlab.userData.zone = zs;
      scene.add(zlab);
      state.zoneSigns.push(zlab);
    }
    state.miniMapCanvas = document.createElement("canvas");
    var vw3 = window.innerWidth || 800, vh3 = window.innerHeight || 600;
    var narrow3 = vw3 <= 520 || (vw3 <= 900 && vh3 <= 480);
    var land3 = vw3 <= 900 && vh3 <= 480;
    var mmW = narrow3 ? 72 : 108, mmH = narrow3 ? 54 : 82;
    var mmL = narrow3 ? 8 : 12, mmT = narrow3 ? 56 : 88;
    state.miniMapCanvas.width = mmW;
    state.miniMapCanvas.height = mmH;
    state.miniMapCanvas.dataset.ffMinimap = "1";
    state.miniMapCanvas.style.cssText = "position:absolute;left:" + mmL + "px;top:" + mmT + "px;width:" + mmW + "px;height:" + mmH + "px;pointer-events:none;z-index:5;border-radius:8px;opacity:" + (narrow3 ? "0.58" : "0.72") + ";";
    var hostEl = document.getElementById("engine-host");
    if (hostEl) hostEl.appendChild(state.miniMapCanvas);
    state.miniMapCanvas.style.display = land3 ? "none" : "";
    state.miniMapCtx = state.miniMapCanvas.getContext("2d");

    state.vx = 0;
    state.vz = 0;
    state.inTruck = false;
    state.truckMode = null;
    state.truckId = null;
    state.inMech = false;
    state.mechId = null;
    state.mechStories = 0;
    state.mechPilotPadIndex = null;
        state.waterSub = 0;
    state.inSwim = false;
    state.inSub = false;
    state.subId = null;
    state.driveSub = null;
    state.parkedSub = null;
    state.inHeli = false;
    state.inDrone = false;
    state.airKind = null;
    state.airSeat = null;
    state.driveAir = null;
    state.climbIn = 0;
    state.airBoost = false;
    state._climbKeyHeld = false;
    state._climbPulseT = 0;
state.zLift = 0;
    state.zVel = 0;
    state.scrap = 0;
    state.bouncePhase = 0;
    state.dustT = 0;
    state.prevNearId = null;
    state.shakeT = 0;
    state.rippleT = 0;
    state.ambient = [];
    /* polish5 + mobile1: ambient pollen / fireflies (fewer on phone) */
    var ambN3 = (window.innerWidth || 800) <= 520 ? 10 : 22;
    for (var ai = 0; ai < ambN3; ai++) {
      var kind = Math.random() < 0.55 ? "pollen" : "firefly";
      var amb = new THREE.Mesh(
        new THREE.SphereGeometry(kind === "firefly" ? 0.07 : 0.05, 6, 5),
        new THREE.MeshBasicMaterial({
          color: kind === "firefly" ? 0xfacc15 : 0xfef9c3,
          transparent: true,
          opacity: kind === "firefly" ? 0.85 : 0.5,
        })
      );
      amb.position.set((Math.random() - 0.5) * C.MAP_W * 0.018, 0.4 + Math.random() * 1.2, (Math.random() - 0.5) * C.MAP_H * 0.018);
      amb.userData.kind = kind;
      amb.userData.phase = Math.random() * Math.PI * 2;
      amb.userData.vx = (Math.random() - 0.5) * 0.6;
      amb.userData.vz = (Math.random() - 0.5) * 0.6;
      scene.add(amb);
      state.ambient.push(amb);
    }
    state.fx = [];
    state.toast = "three.js ranch · compound · squiggle track · whales · Cybertrucks · Starship";
    state.toastT = 3.5;
    state.cd = 0;
    state.near = null;
    state.facing = 1;
    state.faceYaw = 0; /* eyes1: local +Z is face front */
    state.mode = "ranch";
    state.bob = 0;
  }

  function buildSpace() {
    /* view2: hard leave-ranch — fresh scene, dark clear, no forest/CSS leak */
    scene = new THREE.Scene();
    scene.position.set(0, 0, 0);
    scene.background = new THREE.Color(0x020617);
    scene.fog = new THREE.FogExp2(0x020617, 0.012);
    if (renderer) {
      renderer.setClearColor(0x020617, 1);
      try {
        var hostEl = document.getElementById("engine-host");
        if (hostEl) hostEl.style.background = "#020617";
        document.body.classList.add("in-space");
      } catch (eSp) {}
    }
    /* Drop ranch establish / forest ring refs so follow cam cannot blend ranch */
    if (state) {
      state.establishT = 0;
      state.establishDur = 0;
      state.establishCam = null;
      state.establishLook = null;
      state.paraHills = [];
      state.forestRing = [];
      state.garageDoor = null;
      state.parkedTrucks = [];
      state.fish = [];
      state.whales = [];
      state.hotMeshes = [];
      state.mechEnts = null;
    }

    var hemi = new THREE.HemisphereLight(0x93c5fd, 0x020617, 0.45);
    scene.add(hemi);
    var sunLight = new THREE.PointLight(0xfff7ed, 1.45, 140, 2);
    sunLight.position.set(0, 2, 0);
    scene.add(sunLight);
    var fill = new THREE.DirectionalLight(0xcbd5e1, 0.22);
    fill.position.set(8, 12, -6);
    scene.add(fill);

    /* Deep starfield (sky dome points) — ranch never visible behind */
    var starGeo = new THREE.BufferGeometry();
    var positions = new Float32Array(2400);
    for (var i = 0; i < 800; i++) {
      positions[i * 3] = (Math.random() - 0.5) * 220;
      positions[i * 3 + 1] = (Math.random() - 0.5) * 120;
      positions[i * 3 + 2] = (Math.random() - 0.5) * 220;
    }
    starGeo.setAttribute("position", new THREE.BufferAttribute(positions, 3));
    scene.add(new THREE.Points(starGeo, new THREE.PointsMaterial({ color: 0xffffff, size: 0.16, sizeWrite: false })));
    /* Space PLAY plane — starfield floor (not ranch grass). Large + opaque. */
    var spaceGround = new THREE.Mesh(
      new THREE.CircleGeometry(95, 72),
      new THREE.MeshStandardMaterial({
        color: 0x020617, roughness: 1, metalness: 0.05,
        emissive: 0x0b1224, emissiveIntensity: 0.55,
      })
    );
    spaceGround.rotation.x = -Math.PI / 2;
    spaceGround.position.y = -0.04;
    spaceGround.receiveShadow = true;
    spaceGround.renderOrder = -2;
    scene.add(spaceGround);
    state.spaceGround = spaceGround;
    /* Dense star speckles ON the play plane */
    var planeStarGeo = new THREE.BufferGeometry();
    var psp = new Float32Array(900);
    for (var psi = 0; psi < 300; psi++) {
      var pa = Math.random() * Math.PI * 2;
      var pr = Math.pow(Math.random(), 0.65) * 88;
      psp[psi * 3] = Math.cos(pa) * pr;
      psp[psi * 3 + 1] = 0.05;
      psp[psi * 3 + 2] = Math.sin(pa) * pr;
    }
    planeStarGeo.setAttribute("position", new THREE.BufferAttribute(psp, 3));
    scene.add(new THREE.Points(planeStarGeo, new THREE.PointsMaterial({ color: 0xe2e8f0, size: 0.11, depthWrite: false })));
    /* Soft nebula wash so plane reads as void, not dirt */
    var neb = new THREE.Mesh(
      new THREE.CircleGeometry(70, 48),
      new THREE.MeshBasicMaterial({
        color: 0x1e1b4b, transparent: true, opacity: 0.22, depthWrite: false, side: THREE.DoubleSide,
      })
    );
    neb.rotation.x = -Math.PI / 2;
    neb.position.y = -0.01;
    scene.add(neb);

    /* Compressed Solar System (not to scale) — distances in Three units from Sun at origin */
    var SOLAR = [
      { id: "sun", name: "☉ Sun", dist: 0, base: 0, period: 0, r: 4.2, color: 0xfbbf24, emissive: 0xf59e0b, ei: 0.85, capture: false },
      { id: "mercury", name: "Mercury", dist: 8, base: 0.5, period: 22, r: 0.35, color: 0xa8a29e, capture: true, soft: 2.4, cap: 1.2 },
      { id: "venus", name: "Venus", dist: 11, base: 2.1, period: 34, r: 0.55, color: 0xeab308, capture: true, soft: 2.8, cap: 1.4 },
      { id: "earth", name: "Earth · home", dist: 15, base: 0.15, period: 48, r: 0.95, color: 0x0369a1, emissive: 0x0ea5e9, ei: 0.12, capture: true, soft: 3.6, cap: 1.9, home: true },
      { id: "mars", name: "Mars", dist: 20, base: 2.7, period: 68, r: 0.6, color: 0xb45309, emissive: 0x7c2d12, ei: 0.2, capture: true, soft: 3.1, cap: 1.6 },
      { id: "jupiter", name: "Jupiter", dist: 30, base: 4.1, period: 110, r: 1.8, color: 0xd97706, capture: true, soft: 5.0, cap: 2.6 },
      { id: "saturn", name: "Saturn", dist: 37, base: 5.3, period: 140, r: 1.45, color: 0xf59e0b, capture: true, soft: 4.5, cap: 2.3, rings: true },
      { id: "uranus", name: "Uranus", dist: 44, base: 1.0, period: 170, r: 0.95, color: 0x67e8f9, capture: true, soft: 3.5, cap: 1.8 },
      { id: "neptune", name: "Neptune", dist: 50, base: 3.4, period: 200, r: 0.9, color: 0x3b82f6, capture: true, soft: 3.4, cap: 1.7 },
      { id: "pluto", name: "Pluto · dwarf", dist: 56, base: 5.9, period: 240, r: 0.28, color: 0xcbd5e1, capture: true, soft: 2.0, cap: 1.0 },
    ];

    state.spaceTime = 0;
    state.solarBodies = [];
    state.planetMeshes = {};
    state.orbitPaths = [];

    for (var pi = 0; pi < SOLAR.length; pi++) {
      var def = SOLAR[pi];
      var ang = def.base;
      var x = Math.cos(ang) * def.dist;
      var z = Math.sin(ang) * def.dist;
      var mat = new THREE.MeshStandardMaterial({
        color: def.color,
        emissive: def.emissive || 0x000000,
        emissiveIntensity: def.ei || 0,
        roughness: def.id === "sun" ? 0.35 : 0.7,
        metalness: 0.05,
      });
      var mesh = new THREE.Mesh(new THREE.SphereGeometry(def.r, def.id === "sun" ? 32 : 20, def.id === "sun" ? 24 : 16), mat);
      mesh.position.set(x, def.r * 0.15, z);
      scene.add(mesh);
      if (def.rings) {
        var ring = new THREE.Mesh(
          new THREE.RingGeometry(def.r * 1.35, def.r * 2.1, 48),
          new THREE.MeshBasicMaterial({ color: 0xfde68a, transparent: true, opacity: 0.45, side: THREE.DoubleSide })
        );
        ring.rotation.x = -Math.PI / 2.4;
        mesh.add(ring);
      }
      if (def.id === "earth") {
        var atmo = new THREE.Mesh(
          new THREE.SphereGeometry(def.r * 1.08, 20, 16),
          new THREE.MeshBasicMaterial({ color: 0x7dd3fc, transparent: true, opacity: 0.18, side: THREE.BackSide })
        );
        mesh.add(atmo);
        function earthLand(ox, oy, oz, sx, sy, col) {
          var lm = new THREE.Mesh(
            new THREE.SphereGeometry(1, 10, 8),
            new THREE.MeshStandardMaterial({ color: col || 0x4ade80, roughness: 0.85 })
          );
          lm.scale.set(sx, sy, sx * 0.7);
          lm.position.set(ox, oy, oz);
          mesh.add(lm);
        }
        earthLand(-0.25, 0.15, 0.85, 0.35, 0.22, 0x4ade80);
        earthLand(0.3, -0.1, 0.8, 0.28, 0.18, 0x22c55e);
        state.earth = mesh;
      }
      if (def.id === "sun") {
        var sunGlow = new THREE.Mesh(
          new THREE.SphereGeometry(def.r * 1.25, 24, 18),
          new THREE.MeshBasicMaterial({ color: 0xfde68a, transparent: true, opacity: 0.22 })
        );
        mesh.add(sunGlow);
      }
      addLabel(def.name, def.home ? "#bbf7d0" : (def.id === "sun" ? "#fde68a" : "#e2e8f0"), x, def.r + 1.1, z);
      if (def.home) addLabel("ranch is here", "#86efac", x, def.r + 0.7, z);
      if (def.dist > 0) {
        var path = new THREE.Mesh(
          new THREE.RingGeometry(def.dist - 0.04, def.dist + 0.04, 96),
          new THREE.MeshBasicMaterial({ color: 0x64748b, transparent: true, opacity: 0.18, side: THREE.DoubleSide })
        );
        path.rotation.x = -Math.PI / 2;
        path.position.y = 0.02;
        scene.add(path);
        state.orbitPaths.push(path);
      }
      var body = {
        id: def.id,
        name: def.name.replace(" · home", "").replace(" · dwarf", ""),
        mesh: mesh,
        dist: def.dist,
        base: def.base,
        period: def.period,
        r: def.r,
        capture: !!def.capture,
        soft: def.soft || 0,
        cap: def.cap || 0,
        x: x,
        z: z,
      };
      state.solarBodies.push(body);
      state.planetMeshes[def.id] = body;
    }

    /* Asteroid belt beyond Mars */
    var beltGeo = new THREE.BufferGeometry();
    var beltPos = new Float32Array(180);
    for (var bi = 0; bi < 60; bi++) {
      var bang = Math.random() * Math.PI * 2;
      var bd = 24 + Math.random() * 4;
      beltPos[bi * 3] = Math.cos(bang) * bd;
      beltPos[bi * 3 + 1] = (Math.random() - 0.5) * 0.6;
      beltPos[bi * 3 + 2] = Math.sin(bang) * bd;
    }
    beltGeo.setAttribute("position", new THREE.BufferAttribute(beltPos, 3));
    state.asteroidBelt = new THREE.Points(beltGeo, new THREE.PointsMaterial({ color: 0xd6d3d1, size: 0.18 }));
    scene.add(state.asteroidBelt);
    addLabel("Asteroid belt", "#a8a29e", 26, 1.2, 0);
    addLabel("Scale compressed · not to scale", "#fde68a", 0, 6.5, 0);

    /* Moon + station orbit Earth */
    var earthBody = state.planetMeshes.earth;
    state.moonOrbitR = 2.4;
    state.stationOrbitR = 3.4;
    var moonMesh = new THREE.Mesh(
      new THREE.SphereGeometry(0.35, 14, 12),
      new THREE.MeshStandardMaterial({ color: 0xe2e8f0, roughness: 1 })
    );
    moonMesh.position.set(earthBody.x + state.moonOrbitR, 0.4, earthBody.z);
    scene.add(moonMesh);
    addLabel("☾ Moon", "#e2e8f0", earthBody.x + state.moonOrbitR, 1.2, earthBody.z);
    state.moonMesh = moonMesh;
    state.moonBody = {
      id: "moon", name: "Moon", mesh: moonMesh, r: 0.35, capture: true, soft: 2.6, cap: 1.35,
      x: moonMesh.position.x, z: moonMesh.position.z, parent: "earth",
    };
    state.solarBodies.push(state.moonBody);

    var stGroup = new THREE.Group();
    var stCore = new THREE.Mesh(
      new THREE.CylinderGeometry(0.18, 0.18, 0.7, 8),
      new THREE.MeshStandardMaterial({ color: 0x94a3b8, metalness: 0.4, roughness: 0.4 })
    );
    stCore.rotation.z = Math.PI / 2;
    stGroup.add(stCore);
    var panelL = new THREE.Mesh(
      new THREE.BoxGeometry(0.08, 0.5, 0.9),
      new THREE.MeshStandardMaterial({ color: 0x0384c7, emissive: 0x0ea5e9, emissiveIntensity: 0.35 })
    );
    panelL.position.set(0, 0, 0.55); stGroup.add(panelL);
    var panelR = panelL.clone(); panelR.position.z = -0.55; stGroup.add(panelR);
    stGroup.position.set(earthBody.x + state.stationOrbitR, 0.55, earthBody.z);
    scene.add(stGroup);
    state.stationMesh = stGroup;
    state.stationLabel = labelSprite("🛰 Station · orbits Earth", "#7dd3fc");
    state.stationLabel.scale.set(4.2, 0.55, 1);
    scene.add(state.stationLabel);
    state.stationHot = { id: "station", x: stGroup.position.x, z: stGroup.position.z, r: 1.2 };

    /* Mars invader tease (kept) */
    state.marsPos = { x: state.planetMeshes.mars.x, z: state.planetMeshes.mars.z };
    state.invSil = [];
    for (var isi = 0; isi < 4; isi++) {
      var inv = new THREE.Mesh(
        new THREE.BoxGeometry(0.25, 0.7, 0.18),
        new THREE.MeshBasicMaterial({ color: 0x7f1d1d, transparent: true, opacity: 0.55 })
      );
      inv.position.set(state.marsPos.x - 1.2 + isi * 0.55, 0.4, state.marsPos.z - 1.0);
      inv.visible = false;
      scene.add(inv);
      state.invSil.push(inv);
    }
    state.invLabel = labelSprite("Invader mechs · silhouette tease", "#fca5a5");
    state.invLabel.scale.set(4.2, 0.55, 1);
    state.invLabel.position.set(state.marsPos.x, 2.4, state.marsPos.z);
    state.invLabel.visible = false;
    scene.add(state.invLabel);

    var defFrog = C.FROG_DEFS[state.frogId];
    state.player = makeFrogMesh(defFrog, 1.5);
    /* Spawn near Earth (home) */
    state.player.position.set(earthBody.x - 3.2, 0, earthBody.z + 2.2);
    scene.add(state.player);
    state.playerShadow = new THREE.Mesh(
      new THREE.CircleGeometry(0.5, 16),
      new THREE.MeshBasicMaterial({ color: 0x000000, transparent: true, opacity: 0.28, side: THREE.DoubleSide })
    );
    state.playerShadow.rotation.x = -Math.PI / 2;
    state.playerShadow.position.copy(state.player.position);
    state.playerShadow.position.y = 0.05;
    scene.add(state.playerShadow);
    state.playerShadowSoft = null;
    state.paraHills = [];
    state.nameTag = labelSprite(defFrog.name, "#fff");
    state.nameTag.scale.set(2.6, 0.65, 1);
    scene.add(state.nameTag);
    camera.userData.lockTarget.set(state.player.position.x, 0, state.player.position.z);
    camera.position.set(state.player.position.x + 12, 16, state.player.position.z + 12);
    camera.lookAt(state.player.position.x, 0.5, state.player.position.z);

    state.jimmy = makeFrogMesh(C.FROG_DEFS.jimmy, 1.35);
    state.jimmy.position.set(earthBody.x + 1.5, 0, earthBody.z - 2.2);
    scene.add(state.jimmy);
    state.jimmyLabel = labelSprite("Jimmy", "#fb923c");
    scene.add(state.jimmyLabel);
    state.jimmyFlame = new THREE.Mesh(
      new THREE.ConeGeometry(0.18, 0.7, 8),
      new THREE.MeshBasicMaterial({ color: 0x38bdf8, transparent: true, opacity: 0.9 })
    );
    state.jimmyFlame.rotation.x = Math.PI;
    state.jimmyFlame.visible = false;
    scene.add(state.jimmyFlame);
    state.jimmyJetT = 0;
    state.jimmyVx = 2.2;
    state.jimmyVz = -1.4;

    /* Multi-planet gravity — active target refreshed in tick */
    state.planets = state.solarBodies.filter(function (b) { return b.capture; });
    state.planet = state.moonBody;
    state.destBeacon = new THREE.Mesh(
      new THREE.RingGeometry(2.0, 2.2, 48),
      new THREE.MeshBasicMaterial({ color: 0x7dd3fc, transparent: true, opacity: 0.4, side: THREE.DoubleSide })
    );
    state.destBeacon.rotation.x = -Math.PI / 2;
    state.destBeacon.position.set(state.moonBody.x, 0.12, state.moonBody.z);
    scene.add(state.destBeacon);
    state.destBeaconLabel = labelSprite("✦ heading", "#7dd3fc");
    state.destBeaconLabel.scale.set(3.2, 0.5, 1);
    state.destBeaconLabel.visible = false;
    scene.add(state.destBeaconLabel);
    state.inOrbit = false;
    state.orbitAngle = 0;
    state.orbitRadius = 2.2;
    state.orbitEscapeCool = 0;
    state.orbitCfg = (C && C.ORBIT_PHYSICS) || {};
    state.pullRing = new THREE.Mesh(
      new THREE.RingGeometry(3.0, 3.2, 48),
      new THREE.MeshBasicMaterial({ color: 0x7dd3fc, transparent: true, opacity: 0.3, side: THREE.DoubleSide })
    );
    state.pullRing.rotation.x = -Math.PI / 2;
    scene.add(state.pullRing);
    state.capRing = new THREE.Mesh(
      new THREE.RingGeometry(1.7, 1.9, 48),
      new THREE.MeshBasicMaterial({ color: 0x38bdf8, transparent: true, opacity: 0.5, side: THREE.DoubleSide })
    );
    state.capRing.rotation.x = -Math.PI / 2;
    scene.add(state.capRing);
    state.orbitRing = new THREE.Mesh(
      new THREE.RingGeometry(2.0, 2.15, 48),
      new THREE.MeshBasicMaterial({ color: 0xfacc15, transparent: true, opacity: 0.9, side: THREE.DoubleSide })
    );
    state.orbitRing.rotation.x = -Math.PI / 2;
    state.orbitRing.visible = false;
    scene.add(state.orbitRing);
    state.escapeBanner = labelSprite("ORBIT · ESCAPE / Esc · Ability thruster", "#fde68a");
    state.escapeBanner.scale.set(5.5, 0.7, 1);
    state.escapeBanner.visible = false;
    scene.add(state.escapeBanner);

    var germy = new THREE.Mesh(new THREE.SphereGeometry(0.28, 10, 8), new THREE.MeshStandardMaterial({ color: 0xb45309 }));
    germy.position.set(earthBody.x - 1.2, 0.28, earthBody.z + 1.0); scene.add(germy);
    addLabel("Germy", "#fbbf24", earthBody.x - 1.2, 1.1, earthBody.z + 1.0);
    var daisy = new THREE.Mesh(new THREE.SphereGeometry(0.26, 10, 8), new THREE.MeshStandardMaterial({ color: 0xd6d3d1 }));
    daisy.position.set(earthBody.x - 0.4, 0.26, earthBody.z + 1.6); scene.add(daisy);
    addLabel("Daisy", "#e7e5e4", earthBody.x - 0.4, 1.05, earthBody.z + 1.6);
    var spotty = new THREE.Mesh(new THREE.SphereGeometry(0.3, 10, 8), new THREE.MeshStandardMaterial({ color: 0xfdba74 }));
    spotty.position.set(earthBody.x - 4.5, 0.3, earthBody.z - 1.5); scene.add(spotty);
    addLabel("Spotty", "#fdba74", earthBody.x - 4.5, 1.2, earthBody.z - 1.5);
    var alex = new THREE.Mesh(new THREE.SphereGeometry(0.28, 10, 8), new THREE.MeshStandardMaterial({ color: 0x64748b }));
    alex.position.set(stGroup.position.x - 0.6, 0.35, stGroup.position.z + 0.4); scene.add(alex);
    state.alexMesh = alex;
    addLabel("Alex", "#e2e8f0", stGroup.position.x - 0.6, 1.2, stGroup.position.z + 0.4);
    var fred = new THREE.Mesh(new THREE.SphereGeometry(0.28, 10, 8), new THREE.MeshStandardMaterial({ color: 0x475569 }));
    fred.position.set(stGroup.position.x + 0.5, 0.35, stGroup.position.z + 0.5); scene.add(fred);
    state.fredMesh = fred;
    addLabel("Fred", "#e2e8f0", stGroup.position.x + 0.5, 1.2, stGroup.position.z + 0.5);

    /* Return pad → ranch — grounded ON Earth surface (not floating detached) */
    var padAng = 0.85;
    var padRad = earthBody.r * 0.78;
    var padX = earthBody.x + Math.cos(padAng) * padRad;
    var padZ = earthBody.z + Math.sin(padAng) * padRad;
    var padY = earthBody.mesh.position.y + Math.cos(0.35) * earthBody.r * 0.55;
    var pad = new THREE.Mesh(
      new THREE.CircleGeometry(0.42, 20),
      new THREE.MeshStandardMaterial({ color: 0x0284c7, emissive: 0x0c4a6e, emissiveIntensity: 0.55 })
    );
    pad.rotation.x = -Math.PI / 2.6;
    pad.rotation.z = padAng;
    /* Parent to Earth so pad rides the globe surface */
    pad.position.set(
      Math.cos(padAng) * earthBody.r * 0.92,
      Math.sin(0.25) * earthBody.r * 0.35,
      Math.sin(padAng) * earthBody.r * 0.92
    );
    earthBody.mesh.add(pad);
    addLabel("Ranch pad", "#bbf7d0", padX, padY + 0.9, padZ);
    addLabel("→ ranch hub", "#86efac", padX, padY + 0.55, padZ);
    state.returnPad = { x: padX, z: padZ };

    state.vx = 0;
    state.vz = 0;
    state.catches = 0;
    state.toast = "Solar System · near planets → orbit · Escape / thruster to leave";
    state.toastT = 3.5;
    state.mode = "space";
    state.travelMode = "ship";
    state.spaceZoom = 1;
    try { document.body.classList.add("in-space"); } catch (e) {}
    if (state.miniMapCanvas) state.miniMapCanvas.style.display = "none";
    state.near = null;
    state.inTruck = false;
    state.driveTruck = null;
    state.waterPlane = null;
    state.parkedTrucks = [];
    state.fish = [];
    state.whales = [];
    state.hotMeshes = [];
    state.companions = [];
    camera.position.set(state.player.position.x + 14, 18, state.player.position.z + 14);
  }

  function resize() {
    if (!renderer || !camera) return;
    var hostEl = document.getElementById("engine-host");
    var w = Math.max((hostEl && hostEl.clientWidth) || 0, window.innerWidth || 320);
    var h = Math.max((hostEl && hostEl.clientHeight) || 0, window.innerHeight || 480);
    renderer.setSize(w, h, false);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    camera.aspect = w / Math.max(1, h);
    camera.updateProjectionMatrix();
    if (state && state.miniMapCanvas) {
      var land = w <= 900 && h <= 480;
      var narrow = w <= 520 || land;
      var mmW = narrow ? 72 : 108, mmH = narrow ? 54 : 82;
      state.miniMapCanvas.width = mmW;
      state.miniMapCanvas.height = mmH;
      state.miniMapCanvas.style.width = mmW + "px";
      state.miniMapCanvas.style.height = mmH + "px";
      state.miniMapCanvas.style.left = (narrow ? 8 : 12) + "px";
      state.miniMapCanvas.style.top = (narrow ? 56 : 88) + "px";
      state.miniMapCanvas.style.opacity = narrow ? "0.58" : "0.72";
      state.miniMapCanvas.style.display = land ? "none" : "";
    }
  }

  function doInteract() {
    if (!state) return;
    if (interactConsumed) return;
    interactConsumed = true;
    /* Prefer hotspot at the pad/frog that pressed A; else primary near */
    refreshNearFromLocals();
    var companion = (interactPadIndex != null) ? companionForPad(interactPadIndex) : null;
    /* boardall1 / drivefix1: companion EXIT their own mech/truck (never eject primary) */
    if (state.mode === "ranch" && companion && companion.userData.inMech) {
      exitCompanionMech(companion);
      interactOrigin = null; interactPadIndex = null;
      return;
    }
    if (state.mode === "ranch" && companion && companion.userData.inTruck) {
      exitCompanionTruck(companion);
      interactOrigin = null; interactPadIndex = null;
      return;
    }
    if (state.mode === "ranch" && companion && companion.userData.inSub) {
      exitCompanionSub(companion);
      interactOrigin = null; interactPadIndex = null;
      return;
    }
    if (state.mode === "ranch" && companion && (companion.userData.inHeli || companion.userData.inDrone)) {
      exitCompanionAir(companion);
      interactOrigin = null; interactPadIndex = null;
      return;
    }
    /* interact2: primary EXIT only if this input owns the boarded vehicle */
    if (state.mode === "ranch" && (state.inMech || state.inTruck || state.inSub || state.inHeli || state.inDrone) && !companion) {
      /* drivefix2: duplicate INTERACT while the board button is still down stays aboard */
      if (vehicleLatched(state)) {
        interactOrigin = null; interactPadIndex = null;
        return;
      }
      if (!inputOwnsBoarded() && !(state.inHeli || state.inDrone)) {
        interactOrigin = null; interactPadIndex = null;
        return;
      }
      if (state.inHeli || state.inDrone) {
        var AirX = global.FroggiesAir;
        var kindX = state.inDrone ? "drone" : "heli";
        if (!state.airWorld) state.airWorld = { air: null, hotspots: [] };
        var frogX = { id: state.frogId, inHeli: state.inHeli, inDrone: state.inDrone, airSeat: state.airSeat,
          x: 0, y: 0, z: 0 };
        var wposX = threeToWorld(state.player.position.x, state.player.position.z);
        frogX.x = wposX.x; frogX.y = wposX.y; frogX.z = (state.zLift || 0) / 0.02;
        var craftX = AirX ? AirX.ensureCraft(state.airWorld, kindX) : null;
        if (craftX) {
          craftX.x = wposX.x; craftX.y = wposX.y; craftX.z = (state.zLift || 0) / 0.02;
          craftX.vx = (state.vx || 0) / 0.02; craftX.vy = (state.vz || 0) / 0.02; craftX.vz = (state.zVel || 0) / 0.02;
        }
        var resX = AirX ? AirX.boardAir(state.airWorld, [frogX], frogX, { kind: kindX, id: kindX }) : { ok: false };
        if (resX && resX.denied) {
          state.toast = resX.toast || "Land + slow · then INTERACT to hop out";
          state.toastT = 1.8;
          if (hooks.onToast) hooks.onToast(state.toast);
          interactOrigin = null; interactPadIndex = null;
          return;
        }
        state.inHeli = false; state.inDrone = false; state.airKind = null; state.airSeat = null;
        state.zLift = 0; state.zVel = 0; state.groundLift = 0;
        state.player.visible = true;
        if (state.driveAir) state.driveAir.visible = true;
        /* Park craft at exit */
        if (craftX) {
          var pp = worldToThree(craftX.x, craftX.y);
          if (state.driveAir) {
            state.driveAir.position.set(pp.x, airCraftDeckY(craftX.x, craftX.y, 0), pp.z);
          }
          if (C.setVehiclePark) C.setVehiclePark(kindX, craftX.x, craftX.y);
        }
        state.driveAir = null;
        state.toast = (resX && resX.toast) || "Parked · walking";
        state.toastT = 1.8; state.exitTipT = 0;
        interactOrigin = null; interactPadIndex = null;
        if (hooks.onToast) hooks.onToast(state.toast);
        return;
      }
      if (state.inMech) {
        var parkW = threeToWorld(state.player.position.x, state.player.position.z);
        var parkMid = state.mechId || "mech";
        if (C.setVehiclePark) C.setVehiclePark(parkMid, parkW.x, parkW.y);
        state.inMech = false; state.mechId = null; state.mechStories = 0;
        state.mechPilotPadIndex = null;
        state.zLift = 0; state.zVel = 0; state.groundLift = 0;
        state.toast = "Mech parked · walking"; state.toastT = 1.8; state.exitTipT = 0;
        interactOrigin = null; interactPadIndex = null;
        if (hooks.onToast) hooks.onToast(state.toast);
        return;
      }
      if (state.inSub) {
        var parkSw = threeToWorld(state.player.position.x, state.player.position.z);
        var parkSid = state.subId || "submarine";
        if (C.setVehiclePark) C.setVehiclePark(parkSid, parkSw.x, parkSw.y);
        state.inSub = false; state.subId = null; state.vehicleStyle = null;
        state.zLift = 0; state.zVel = 0; state.groundLift = 0;
        if (C.inPond && C.inPond(parkSw.x, parkSw.y)) {
          state.inSwim = true;
          state.waterSub = Math.max(state.waterSub || 0, 0.4);
          state.toast = "Surfaced · swimming";
        } else {
          state.inSwim = false;
          state.toast = "Sub parked · shore";
        }
        state.toastT = 1.8; state.exitTipT = 0;
        /* mech7: same hull stays parked at exit — no hidden clone */
        if (state.parkedSub) {
          var psp = worldToThree(parkSw.x, parkSw.y);
          state.parkedSub.position.set(psp.x, 0.22, psp.z);
          /* subyaw1: mesh nose +X; faceYaw aims +Z → -PI/2 */
          state.parkedSub.rotation.y = (state.faceYaw != null ? state.faceYaw : 0) - Math.PI / 2;
          state.parkedSub.visible = true;
          if (state.parkedSub.userData.bodyMat) {
            state.parkedSub.userData.bodyMat.opacity = 1;
            state.parkedSub.userData.bodyMat.transparent = false;
          }
        }
        state.driveSub = null;
        state.player.visible = true;
        setFrogSwim(state.player, 0, false);
        interactOrigin = null; interactPadIndex = null;
        if (hooks.onToast) hooks.onToast(state.toast);
        return;
      }
      if (state.inTruck) {
        var parkTw = threeToWorld(state.player.position.x, state.player.position.z);
        var parkTid = state.truckId || "truck";
        if (C.setVehiclePark) C.setVehiclePark(parkTid, parkTw.x, parkTw.y);
        state.inTruck = false; state.truckMode = null; state.truckId = null; state.vehicleStyle = null;
        state.truckPilotPadIndex = null;
        state.zLift = 0; state.zVel = 0; state.groundLift = 0;
        state.toast = "Parked · walking"; state.toastT = 1.8; state.exitTipT = 0;
        interactOrigin = null; interactPadIndex = null;
        if (hooks.onToast) hooks.onToast(state.toast);
        return;
      }
    }
    /* Companion pad while primary is boarded: fall through to board a free mech */
    if (!state.near) { interactOrigin = null; interactPadIndex = null; return; }
    var id = state.near.id;
    if (state.mode === "ranch") {
      if (C.isTruckHotspot && C.isTruckHotspot(state.near)) {
        /* drivefix1: couch companions board free Cybertruck/Ripsaw/Tank same as primary */
        if (companion) {
          boardCompanionTruck(companion, state.near);
          interactOrigin = null; interactPadIndex = null;
          return;
        }
        if (state.inMech || state.inTruck) {
          interactOrigin = null; interactPadIndex = null; return;
        }
        if (state.near.mode !== "shared" && whoPilotsTruckId(id)) {
          state.toast = "Already boarded · pick another"; state.toastT = 1.6;
          interactOrigin = null; interactPadIndex = null;
          if (hooks.onToast) hooks.onToast(state.toast);
          return;
        }
        var tp = worldToThree(state.near.x, state.near.y);
        state.player.position.x = tp.x; state.player.position.z = tp.z;
        state.inTruck = true; state.truckMode = state.near.mode || "solo"; state.truckId = id;
        state.vehicleStyle = state.near.vehicleStyle || (C.vehicleStyleOf ? C.vehicleStyleOf(state.near) : "cybertruck");
        state.truckPilotPadIndex = (interactPadIndex != null) ? interactPadIndex
          : (state.primaryPadIndex != null ? state.primaryPadIndex : null);
        state.scrap += 1;
        /* Swap drive mesh to match Ripsaw/Tank/Cybertruck */
        if (state.driveTruck && state.driveTruck.parent) state.driveTruck.parent.remove(state.driveTruck);
        var frogDef = C.FROG_DEFS[state.frogId] || C.FROG_DEFS.james;
        state.driveTruck = makeVehicleMesh(state.vehicleStyle, hex(frogDef.color));
        state.driveTruck.visible = true;
        scene.add(state.driveTruck);
        state.toast = state.truckMode === "shared"
          ? "All aboard! Four froggies · one Cybertruck · hit the jumps!"
          : state.vehicleStyle === "ripsaw" ? "Driving Ripsaw · tracked · hit the jumps!"
          : state.vehicleStyle === "tank" ? (C.tankDrivingTip ? C.tankDrivingTip() : "Driving Tank · FIRE (Space / X / button) · EXIT INTERACT")
          : "Driving Cybertruck · hit the jumps!";
        state.exitTipT = 2.4;
        armVehicleLatch(state);
      } else if (C.isSubHotspot && C.isSubHotspot(state.near)) {
        /* drivefix3: couch companions board free sub same as primary */
        if (companion) {
          boardCompanionSub(companion, state.near);
          interactOrigin = null; interactPadIndex = null;
          return;
        }
        if (state.inMech || state.inTruck || state.inSub) {
          interactOrigin = null; interactPadIndex = null; return;
        }
        var spb = worldToThree(state.near.x, state.near.y);
        state.player.position.x = spb.x; state.player.position.z = spb.z;
        state.inSub = true; state.subId = id; state.vehicleStyle = "submarine";
        state.inSwim = false;
        state.waterSub = Math.max(state.waterSub || 0, 0.85);
        state.player.visible = false;
        /* mech7: board the EXISTING docked hull — never spawn a second sub */
        if (!state.parkedSub) {
          state.parkedSub = makeSubMesh(hex((C.FROG_DEFS[state.frogId] || {}).color || "#38bdf8"));
          scene.add(state.parkedSub);
        }
        state.driveSub = state.parkedSub;
        state.driveSub.visible = true;
        state.driveSub.position.set(state.player.position.x, -0.35 - (state.waterSub || 0.85) * 0.25, state.player.position.z);
        state.toast = "Submarine · diving underwater · EXIT INTERACT / E";
        state.toastT = 2.4; state.exitTipT = 2.4;
        armVehicleLatch(state);
        if (hooks.onToast) hooks.onToast(state.toast);
      } else if (C.isAirHotspot && C.isAirHotspot(state.near)) {
        /* drivefix3: companions board heli/drone (passenger or free pilot) */
        if (companion) {
          boardCompanionAir(companion, state.near);
          interactOrigin = null; interactPadIndex = null;
          return;
        }
        if (state.inMech || state.inTruck || state.inSub || state.inHeli || state.inDrone) {
          interactOrigin = null; interactPadIndex = null; return;
        }
        var kindB = C.airKindOf ? C.airKindOf(state.near) : (state.near.kind === "drone" ? "drone" : "heli");
        var AirB = global.FroggiesAir;
        if (!state.airWorld) state.airWorld = { air: null, hotspots: (C.HOTSPOTS || []).slice() };
        var frogB = { id: state.frogId, inHeli: false, inDrone: false, x: state.near.x, y: state.near.y, z: 0 };
        var resB = AirB ? AirB.boardAir(state.airWorld, [frogB], frogB, state.near) : { ok: false };
        if (resB && resB.denied) {
          state.toast = resB.toast || "Cannot board";
          state.toastT = 1.8;
          if (hooks.onToast) hooks.onToast(state.toast);
          interactOrigin = null; interactPadIndex = null;
          return;
        }
        var tpB = worldToThree(state.near.x, state.near.y);
        state.player.position.x = tpB.x; state.player.position.z = tpB.z;
        state.inHeli = kindB === "heli";
        state.inDrone = kindB === "drone";
        state.airKind = kindB;
        state.airSeat = frogB.airSeat != null ? frogB.airSeat : 0;
        state.truckPilotPadIndex = (interactPadIndex != null) ? interactPadIndex
          : (state.primaryPadIndex != null ? state.primaryPadIndex : null);
        state.player.visible = false;
        /* Use parked air mesh as drive mesh */
        state.driveAir = null;
        for (var pai = 0; pai < (state.parkedAir || []).length; pai++) {
          if (state.parkedAir[pai].kind === kindB) {
            state.driveAir = state.parkedAir[pai].mesh;
            break;
          }
        }
        if (!state.driveAir) {
          state.driveAir = kindB === "drone" ? makePassengerDroneMesh(0x67e8f9) : makeHeliMesh(0x94a3b8);
          scene.add(state.driveAir);
        }
        state.driveAir.visible = true;
        state.driveAir.position.set(tpB.x, airCraftDeckY(state.near.x, state.near.y, 0), tpB.z);
        state.toast = resB.toast || (kindB === "drone" ? "Passenger drone · fly!" : "Helicopter · fly!");
        state.toastT = 2.6; state.exitTipT = 2.6;
        armVehicleLatch(state);
        if (hooks.onToast) hooks.onToast(state.toast);
      } else if (C.isMechHotspot && C.isMechHotspot(state.near)) {
        /* boardall1: companion boards their own mech; primary uses state.inMech */
        if (companion) {
          if (!boardCompanionMech(companion, state.near)) {
            /* drivefix3: wrong mech — try free truck/ripsaw/sub/air for this companion */
            var cww = threeToWorld(companion.position.x, companion.position.z);
            var altC = C.nearestHotspot(cww.x, cww.y, 110, { frogId: companion.userData.frogId, skipOwnMech: true });
            if (altC && C.isTruckHotspot && C.isTruckHotspot(altC)) boardCompanionTruck(companion, altC);
            else if (altC && C.isSubHotspot && C.isSubHotspot(altC)) boardCompanionSub(companion, altC);
            else if (altC && C.isAirHotspot && C.isAirHotspot(altC)) boardCompanionAir(companion, altC);
          }
          interactOrigin = null; interactPadIndex = null;
          return;
        }
        if (state.inMech || state.inTruck) {
          interactOrigin = null; interactPadIndex = null; return;
        }
        if (C.canBoardMech && !C.canBoardMech(state.frogId, state.near)) {
          /* drivefix1: locked mech — board free Cybertruck/Ripsaw in reach instead */
          var wpDeny = threeToWorld(state.player.position.x, state.player.position.z);
          var altT = C.nearestHotspot(wpDeny.x, wpDeny.y, 110, { frogId: state.frogId, skipOwnMech: true });
          if (altT && C.isTruckHotspot && C.isTruckHotspot(altT) && altT.mode !== "shared" && !whoPilotsTruckId(altT.id)) {
            state.near = altT;
            id = altT.id;
            var tpD = worldToThree(altT.x, altT.y);
            state.player.position.x = tpD.x; state.player.position.z = tpD.z;
            state.inTruck = true; state.truckMode = altT.mode || "solo"; state.truckId = id;
            state.vehicleStyle = altT.vehicleStyle || (C.vehicleStyleOf ? C.vehicleStyleOf(altT) : "cybertruck");
            state.truckPilotPadIndex = (interactPadIndex != null) ? interactPadIndex
              : (state.primaryPadIndex != null ? state.primaryPadIndex : null);
            state.scrap += 1;
            if (state.driveTruck && state.driveTruck.parent) state.driveTruck.parent.remove(state.driveTruck);
            var frogDefD = C.FROG_DEFS[state.frogId] || C.FROG_DEFS.james;
            state.driveTruck = makeVehicleMesh(state.vehicleStyle, hex(frogDefD.color));
            state.driveTruck.visible = true;
            scene.add(state.driveTruck);
            state.toast = state.vehicleStyle === "ripsaw" ? "Driving Ripsaw · tracked · hit the jumps!"
              : state.vehicleStyle === "tank" ? (C.tankDrivingTip ? C.tankDrivingTip() : "Driving Tank · FIRE · EXIT INTERACT")
              : "Driving Cybertruck · hit the jumps!";
            state.exitTipT = 2.4; state.toastT = 2.5;
            armVehicleLatch(state);
            interactOrigin = null; interactPadIndex = null;
            if (hooks.onToast) hooks.onToast(state.toast);
            return;
          }
          state.toast = C.mechDeniedTip ? C.mechDeniedTip(state.frogId, state.near) : "Wrong froggy for this mech";
          state.toastT = 2.2;
          interactOrigin = null; interactPadIndex = null;
          if (hooks.onToast) hooks.onToast(state.toast);
          return;
        }
        if (whoPilotsMechId(id)) {
          state.toast = "Already boarded · pick another"; state.toastT = 1.6;
          interactOrigin = null; interactPadIndex = null;
          if (hooks.onToast) hooks.onToast(state.toast);
          return;
        }
        var mp = worldToThree(state.near.x, state.near.y);
        state.player.position.x = mp.x; state.player.position.z = mp.z;
        state.inMech = true;
        state.mechId = id;
        state.mechStories = state.near.stories || 10;
        state.mechPilotPadIndex = (interactPadIndex != null) ? interactPadIndex
          : (state.primaryPadIndex != null ? state.primaryPadIndex : null);
        state.zLift = 0; state.zVel = 0; state.groundLift = 0;
        state.vx = 0; state.vz = 0;
        state.walkPhase = 0;
        state.scrap += 1;
        state.toast = "Boarding " + (C.mechStoriesLabel ? C.mechStoriesLabel(state.mechStories) : (state.mechStories + "-story mech")) + " · FIRE (Space / X / button)!";
        state.exitTipT = 2.4;
        armVehicleLatch(state);
      } else if (id === "fishies") {
        state.toast = "Splash! Fishies & whales scatter";
        state.scrap += 2;
        for (var i = 0; i < state.fish.length; i++) {
          state.fish[i].userData.bx += (Math.random() - 0.5) * 2;
          state.fish[i].userData.bz += (Math.random() - 0.5) * 1.6;
        }
        for (var wi = 0; wi < (state.whales || []).length; wi++) {
          state.whales[wi].userData.bx += (Math.random() - 0.5) * 2.5;
          state.whales[wi].userData.bz += (Math.random() - 0.5) * 2;
        }
      } else if (id === "phone") {
        state.toast = "Purple Bear: Check SPS · find Jimmy!";
      } else if (id === "sps") {
        state.toast = "SPS · Optimus kits (stub on three.js)";
        state.kitFxKind = "rocket"; state.kitFxT = 0.55;
      } else if (id === "starship") {
        state.toast = "Spotty: Launch!";
        buildSpace();
      }
      state.toastT = 2.5;
    } else {
      if (id === "jimmy") {
        state.catches++;
        var eCatch = (state.planetMeshes && state.planetMeshes.earth) || { x: 15, z: 0 };
        var ca = Math.random() * Math.PI * 2;
        state.jimmy.position.set(eCatch.x + Math.cos(ca) * 3.5, 0, eCatch.z + Math.sin(ca) * 3.5);
        state.jimmyVx = (Math.random() > 0.5 ? 1 : -1) * (2 + Math.random() * 2);
        state.jimmyVz = (Math.random() > 0.5 ? 1 : -1) * (1.5 + Math.random() * 2);
        state.toast = state.catches >= 2
          ? ("Almost! ×" + state.catches + " — station orbits Earth →")
          : ("Caught Jimmy! ×" + state.catches);
        state.toastT = 2.5;
      } else if (id === "station") {
        state.toast = "Space station · Alex & Fred aboard · orbits Earth";
        state.toastT = 2.5;
      } else if (id === "return") {
        try {
          document.body.classList.remove("in-space");
          var hostBack = document.getElementById("engine-host");
          if (hostBack) hostBack.style.background = "";
        } catch (e) {}
        if (renderer) renderer.setClearColor(0x7eb8d4, 1);
        buildRanch();
        state.toast = "Back at ranch · Earth home";
        state.toastT = 2;
      }
    }
    interactOrigin = null;
    interactPadIndex = null;
    if (hooks.onToast) hooks.onToast(state.toast);
  }

  function fireCompanionAirGun(c) {
    if (!c || !(c.userData.inHeli || c.userData.inDrone)) return false;
    var kind = c.userData.inDrone ? "drone" : "heli";
    var Air = global.FroggiesAir;
    if (Air && state.airWorld) {
      var craft = Air.ensureCraft(state.airWorld, kind);
      if (craft && Air.isPilot && !Air.isPilot(craft, { id: c.userData.frogId })) {
        state.toast = "Pilot fires · hang on!";
        state.toastT = 0.9;
        if (hooks.onToast) hooks.onToast(state.toast);
        return false;
      }
    }
    var cd = c.userData.cd || 0;
    if (cd > 0) {
      c.userData.hopWantT = Math.max(c.userData.hopWantT || 0, 0.15);
      return false;
    }
    var cfg = (C.airFireCfg) ? C.airFireCfg(kind) : { cd: 0.36, speed: 760, life: 1.4, muzzle: 1.7, blastR: 100, size: 2 };
    c.userData.cd = cfg.cd != null ? cfg.cd : 0.36;
    var yaw = c.userData.faceYaw != null ? c.userData.faceYaw : 0;
    var fx = Math.sin(yaw), fz = Math.cos(yaw);
    var muzzle = cfg.muzzle != null ? (cfg.muzzle > 8 ? cfg.muzzle / 34 : cfg.muzzle) : 1.7;
    var spd = cfg.speed != null ? (cfg.speed > 40 ? cfg.speed / 42 : cfg.speed) : 18;
    var life = cfg.life != null ? cfg.life : 1.4;
    var blastR3 = (cfg.blastR != null ? cfg.blastR : 100) * 0.02;
    var px = c.position.x + fx * muzzle;
    var py = 0.55 + (c.userData.zLift || 0);
    var pz = c.position.z + fz * muzzle;
    var col = kind === "drone" ? 0x84cc16 : 0xfbbf24;
    var shellGeo = (typeof THREE.CapsuleGeometry === "function")
      ? new THREE.CapsuleGeometry(0.18, 0.7, 6, 10)
      : new THREE.SphereGeometry(0.24, 10, 8);
    var mesh = new THREE.Mesh(shellGeo, new THREE.MeshBasicMaterial({ color: col, transparent: true, opacity: 1 }));
    if (typeof THREE.CapsuleGeometry === "function") mesh.rotation.z = Math.PI / 2;
    else mesh.scale.set(3.0, 1.0, 1.0);
    mesh.position.set(px, py, pz);
    mesh.rotation.y = yaw;
    scene.add(mesh);
    if (!state.shells) state.shells = [];
    state.shells.push({
      mesh: mesh, vx: fx * spd, vz: fz * spd, life: life, maxLife: life, yaw: yaw,
      blastR: blastR3, hitR: 0.5, size: cfg.size != null ? cfg.size : 2,
      airGun: true, airKind: kind, ownerId: c.userData.frogId,
    });
    state.toast = "FIRE!";
    state.toastT = 0.85;
    if (hooks.onToast) hooks.onToast(state.toast);
    if (hooks.onAbilityFire) hooks.onAbilityFire(c.userData.frogId || "james", "FIRE");
    return true;
  }

  function hopCompanionPad(padIndex) {
    var c = companionForPad(padIndex);
    if (!c || !c.userData.local) return false;
    /* tankfire1: a tank-seated companion fires; never falls through to hop */
    if (c.userData.inTruck && c.userData.vehicleStyle === "tank") {
      return fireCompanionTankShell(c);
    }
    /* airgun1: companion air pilot FIRE; passengers no-op */
    if (c.userData.inHeli || c.userData.inDrone) {
      return fireCompanionAirGun(c);
    }
    if (state.inTruck && state.truckMode === "shared") return false; /* seated — no solo hop */
    var cd = c.userData.cd || 0;
    if (cd > 0) {
      c.userData.hopWantT = Math.max(c.userData.hopWantT || 0, 0.15);
      return false;
    }
    c.userData.cd = 0.1;
    var yaw = c.userData.faceYaw != null ? c.userData.faceYaw : 0;
    var fx = Math.sin(yaw), fz = Math.cos(yaw);
    var zLift = c.userData.zLift || 0;
    var zVel = c.userData.zVel || 0;
    var cgHop = 0;
    if (c.position) {
      var chw = threeToWorld(c.position.x, c.position.z);
      cgHop = ranchGroundY(chw.x, chw.y);
    }
    var air = zLift > cgHop + 0.12;
    var combo = air ? Math.min(10, (c.userData.hopCombo || 0) + 1) : 1;
    c.userData.hopCombo = combo;
    var up = 9.8 + (combo - 1) * 1.2;
    c.userData.vx = (c.userData.vx || 0) + fx * 5.6;
    c.userData.vz = (c.userData.vz || 0) + fz * 5.6;
    if (air) c.userData.zVel = Math.max(0, zVel) + up * 0.7;
    else {
      c.userData.zVel = Math.max(zVel, up);
      c.userData.zLift = Math.max(zLift, cgHop + 0.25);
    }
    state.toast = combo > 1 ? ("HOP ×" + combo + "!") : "HOP!";
    state.toastT = 1.2;
    if (hooks.onToast) hooks.onToast(state.toast);
    if (hooks.onAbilityFire) hooks.onAbilityFire(c.userData.frogId || "james", "HOP");
    return true;
  }


  /* tankfire1: companion Tank FIRE — same shell/blast path, using the companion pose. */
  function fireCompanionTankShell(c) {
    if (!c || !c.userData || !c.userData.inTruck || c.userData.vehicleStyle !== "tank") return false;
    c.userData.hopWantT = 0; /* never queue or consume a hop while tank */
    if ((c.userData.cd || 0) > 0) {
      c.userData.fireWantT = Math.max(c.userData.fireWantT || 0, 0.15);
      return false;
    }
    c.userData.fireWantT = 0;
    var cfg = (C.TANK_FIRE) || { cd: 0.38, speed: 720, life: 1.55, muzzle: 1.9, blastR: 118, size: 2.4 };
    c.userData.cd = cfg.cd != null ? cfg.cd : 0.38;
    var yaw = (c.userData.faceYaw != null) ? c.userData.faceYaw : 0;
    var spA = Math.hypot(c.userData.vx || 0, c.userData.vz || 0);
    if (spA > 1.0) yaw = Math.atan2(c.userData.vx, c.userData.vz);
    var fx = Math.sin(yaw), fz = Math.cos(yaw);
    var muzzle = cfg.muzzle != null ? (cfg.muzzle > 8 ? cfg.muzzle / 34 : cfg.muzzle) : 1.9;
    var spd = cfg.speed != null ? (cfg.speed > 40 ? cfg.speed / 42 : cfg.speed) : 17;
    var life = cfg.life != null ? cfg.life : 1.55;
    var blastR3 = (cfg.blastR != null ? cfg.blastR : 118) * 0.02;
    var px = c.position.x + fx * muzzle;
    var py = 0.7 + (c.userData.zLift || 0);
    var pz = c.position.z + fz * muzzle;
    var shellGeo = (typeof THREE.CapsuleGeometry === "function")
      ? new THREE.CapsuleGeometry(0.22, 0.85, 6, 10)
      : new THREE.SphereGeometry(0.28, 10, 8);
    var mesh = new THREE.Mesh(shellGeo, new THREE.MeshBasicMaterial({ color: 0xfbbf24, transparent: true, opacity: 1 }));
    if (typeof THREE.CapsuleGeometry === "function") {
      mesh.rotation.z = Math.PI / 2;
    } else {
      mesh.scale.set(3.4, 1.1, 1.1);
    }
    mesh.position.set(px, py, pz);
    mesh.rotation.y = yaw;
    scene.add(mesh);
    if (!state.shells) state.shells = [];
    state.shells.push({
      mesh: mesh,
      vx: fx * spd,
      vz: fz * spd,
      life: life,
      maxLife: life,
      yaw: yaw,
      blastR: blastR3,
      hitR: 0.55,
      size: cfg.size != null ? cfg.size : 2.4,
      ownerId: c.userData.frogId,
    });
    if (state.shells.length > 14) {
      var old = state.shells.shift();
      if (old && old.mesh && old.mesh.parent) old.mesh.parent.remove(old.mesh);
    }
    if (!state.fx) state.fx = [];
    for (var zi = 0; zi < 12; zi++) {
      var spark = new THREE.Mesh(
        new THREE.SphereGeometry(0.08 + (zi % 3) * 0.04, 5, 4),
        new THREE.MeshBasicMaterial({ color: zi % 2 ? 0xfbbf24 : 0xf87171, transparent: true, opacity: 0.95 })
      );
      spark.position.set(px - fx * 0.15, py, pz - fz * 0.15);
      scene.add(spark);
      state.fx.push({ mesh: spark, life: 0.32 + zi * 0.02, rise: 1.6, vx: fx * (3 + zi * 0.35), vz: fz * (3 + zi * 0.35) });
    }
    state.toast = "FIRE!";
    state.toastT = 0.9;
    if (hooks.onToast) hooks.onToast(state.toast);
    if (hooks.onAbilityFire) hooks.onAbilityFire(c.userData.frogId, "FIRE");
    return true;
  }

  /* mech4: Tank FIRE — big missile; blast wrecks toys/animals/props. Never hop/reset. */
  function fireTankShell() {
    if (!state || !state.inTruck || state.vehicleStyle !== "tank") return false;
    if (state.cd > 0) {
      state.fireWantT = Math.max(state.fireWantT || 0, 0.15);
      state.hopWantT = 0; /* never queue a hop while tank */
      return false;
    }
    state.hopWantT = 0;
    state.fireWantT = 0;
    var cfg = (C.TANK_FIRE) || { cd: 0.38, speed: 720, life: 1.55, muzzle: 1.9, blastR: 118, size: 2.4 };
    state.cd = cfg.cd != null ? cfg.cd : 0.38;
    var yaw = (state.faceYaw != null) ? state.faceYaw : 0;
    var spA = Math.hypot(state.vx || 0, state.vz || 0);
    if (spA > 1.0) yaw = Math.atan2(state.vx, state.vz);
    var fx = Math.sin(yaw), fz = Math.cos(yaw);
    /* muzzle in three units — canon muzzle is world px; ~ / 34 */
    var muzzle = cfg.muzzle != null ? (cfg.muzzle > 8 ? cfg.muzzle / 34 : cfg.muzzle) : 1.9;
    var spd = cfg.speed != null ? (cfg.speed > 40 ? cfg.speed / 42 : cfg.speed) : 17;
    var life = cfg.life != null ? cfg.life : 1.55;
    var blastR3 = (cfg.blastR != null ? cfg.blastR : 118) * 0.02;
    var px = state.player.position.x + fx * muzzle;
    var py = 0.7 + (state.zLift || 0);
    var pz = state.player.position.z + fz * muzzle;
    var shellGeo = (typeof THREE.CapsuleGeometry === "function")
      ? new THREE.CapsuleGeometry(0.22, 0.85, 6, 10)
      : new THREE.SphereGeometry(0.28, 10, 8);
    var mesh = new THREE.Mesh(shellGeo, new THREE.MeshBasicMaterial({ color: 0xfbbf24, transparent: true, opacity: 1 }));
    if (typeof THREE.CapsuleGeometry === "function") {
      mesh.rotation.z = Math.PI / 2;
    } else {
      mesh.scale.set(3.4, 1.1, 1.1);
    }
    mesh.position.set(px, py, pz);
    mesh.rotation.y = yaw;
    scene.add(mesh);
    if (!state.shells) state.shells = [];
    state.shells.push({
      mesh: mesh,
      vx: fx * spd,
      vz: fz * spd,
      life: life,
      maxLife: life,
      yaw: yaw,
      blastR: blastR3,
      hitR: 0.55,
      size: cfg.size != null ? cfg.size : 2.4,
    });
    if (state.shells.length > 14) {
      var old = state.shells.shift();
      if (old && old.mesh && old.mesh.parent) old.mesh.parent.remove(old.mesh);
    }
    if (!state.fx) state.fx = [];
    for (var zi = 0; zi < 12; zi++) {
      var spark = new THREE.Mesh(
        new THREE.SphereGeometry(0.08 + (zi % 3) * 0.04, 5, 4),
        new THREE.MeshBasicMaterial({ color: zi % 2 ? 0xfbbf24 : 0xf87171, transparent: true, opacity: 0.95 })
      );
      spark.position.set(px - fx * 0.15, py, pz - fz * 0.15);
      scene.add(spark);
      state.fx.push({ mesh: spark, life: 0.32 + zi * 0.02, rise: 1.6, vx: fx * (3 + zi * 0.35), vz: fz * (3 + zi * 0.35) });
    }
    state.toast = "FIRE!";
    state.toastT = 0.9;
    if (hooks.onToast) hooks.onToast(state.toast);
    if (hooks.onAbilityFire) hooks.onAbilityFire(state.frogId, "FIRE");
    return true;
  }

  /* airgun1: heli / drone FIRE — forward bolt; tank-like boom; pilot only */
  function fireAirGun() {
    if (!state || !(state.inHeli || state.inDrone)) return false;
    var kind = state.inDrone ? "drone" : "heli";
    var Air = global.FroggiesAir;
    if (Air && state.airWorld) {
      var craft = Air.ensureCraft(state.airWorld, kind);
      if (craft && Air.isPilot && !Air.isPilot(craft, { id: state.frogId })) {
        state.toast = "Pilot fires · hang on!";
        state.toastT = 0.9;
        if (hooks.onToast) hooks.onToast(state.toast);
        return false;
      }
    }
    if (state.cd > 0) {
      state.fireWantT = Math.max(state.fireWantT || 0, 0.15);
      state.hopWantT = 0;
      return false;
    }
    state.hopWantT = 0;
    state.fireWantT = 0;
    var cfg = (C.airFireCfg) ? C.airFireCfg(kind) : (kind === "drone"
      ? { cd: 0.28, speed: 880, life: 1.25, muzzle: 1.5, blastR: 88, size: 1.7 }
      : { cd: 0.36, speed: 760, life: 1.45, muzzle: 1.8, blastR: 108, size: 2.15 });
    state.cd = cfg.cd != null ? cfg.cd : 0.36;
    var yaw = (state.faceYaw != null) ? state.faceYaw : 0;
    var spA = Math.hypot(state.vx || 0, state.vz || 0);
    if (spA > 1.0) yaw = Math.atan2(state.vx, state.vz);
    var fx = Math.sin(yaw), fz = Math.cos(yaw);
    var muzzle = cfg.muzzle != null ? (cfg.muzzle > 8 ? cfg.muzzle / 34 : cfg.muzzle) : (kind === "drone" ? 1.5 : 1.8);
    var spd = cfg.speed != null ? (cfg.speed > 40 ? cfg.speed / 42 : cfg.speed) : (kind === "drone" ? 21 : 18);
    var life = cfg.life != null ? cfg.life : 1.4;
    var blastR3 = (cfg.blastR != null ? cfg.blastR : 108) * 0.02;
    var px = state.player.position.x + fx * muzzle;
    var py = 0.55 + (state.zLift || 0) + (kind === "heli" ? 0.35 : 0.15);
    var pz = state.player.position.z + fz * muzzle;
    var col = kind === "drone" ? 0x84cc16 : 0xfbbf24;
    var colTip = kind === "drone" ? 0x4ade80 : 0xf97316;
    var shellGeo = (typeof THREE.CapsuleGeometry === "function")
      ? new THREE.CapsuleGeometry(kind === "drone" ? 0.16 : 0.2, kind === "drone" ? 0.65 : 0.8, 6, 10)
      : new THREE.SphereGeometry(kind === "drone" ? 0.22 : 0.26, 10, 8);
    var mesh = new THREE.Mesh(shellGeo, new THREE.MeshBasicMaterial({ color: col, transparent: true, opacity: 1 }));
    if (typeof THREE.CapsuleGeometry === "function") {
      mesh.rotation.z = Math.PI / 2;
    } else {
      mesh.scale.set(kind === "drone" ? 2.8 : 3.2, 1.0, 1.0);
    }
    mesh.position.set(px, py, pz);
    mesh.rotation.y = yaw;
    scene.add(mesh);
    if (!state.shells) state.shells = [];
    state.shells.push({
      mesh: mesh,
      vx: fx * spd,
      vz: fz * spd,
      life: life,
      maxLife: life,
      yaw: yaw,
      blastR: blastR3,
      hitR: kind === "drone" ? 0.45 : 0.52,
      size: cfg.size != null ? cfg.size : (kind === "drone" ? 1.7 : 2.15),
      airGun: true,
      airKind: kind,
      ownerId: state.frogId,
    });
    if (state.shells.length > 16) {
      var old = state.shells.shift();
      if (old && old.mesh && old.mesh.parent) old.mesh.parent.remove(old.mesh);
    }
    if (!state.fx) state.fx = [];
    for (var zi = 0; zi < (kind === "drone" ? 8 : 12); zi++) {
      var spark = new THREE.Mesh(
        new THREE.SphereGeometry(0.06 + (zi % 3) * 0.03, 5, 4),
        new THREE.MeshBasicMaterial({ color: zi % 2 ? col : colTip, transparent: true, opacity: 0.95 })
      );
      spark.position.set(px - fx * 0.12, py, pz - fz * 0.12);
      scene.add(spark);
      state.fx.push({ mesh: spark, life: 0.28 + zi * 0.02, rise: 1.4, vx: fx * (2.5 + zi * 0.3), vz: fz * (2.5 + zi * 0.3) });
    }
    state.toast = "FIRE!";
    state.toastT = 0.85;
    if (hooks.onToast) hooks.onToast(state.toast);
    if (hooks.onAbilityFire) hooks.onAbilityFire(state.frogId, "FIRE");
    return true;
  }

    /* mechgun1 + storymuzzle1: omnigun from glowing chest plate */
  function fireMechGun() {
    if (!state || !state.inMech) return false;
    if (state.cd > 0) {
      state.fireWantT = Math.max(state.fireWantT || 0, 0.15);
      state.hopWantT = 0;
      return false;
    }
    state.hopWantT = 0;
    state.fireWantT = 0;
    var cfg = (C.MECH_GUN) || { cd: 0.42, speed: 780, life: 1.7, muzzle: 2.2, blastR: 140, size: 2.8 };
    state.cd = cfg.cd != null ? cfg.cd : 0.42;
    var yaw = (state.faceYaw != null) ? state.faceYaw : 0;
    var spA = Math.hypot(state.vx || 0, state.vz || 0);
    if (spA > 1.0) yaw = Math.atan2(state.vx, state.vz);
    var fx = Math.sin(yaw), fz = Math.cos(yaw);
    var muzzle = cfg.muzzle != null ? (cfg.muzzle > 8 ? cfg.muzzle / 34 : cfg.muzzle) : 2.2;
    var spd = cfg.speed != null ? (cfg.speed > 40 ? cfg.speed / 42 : cfg.speed) : 18.5;
    var life = cfg.life != null ? cfg.life : 1.7;
    var blastR3 = (cfg.blastR != null ? cfg.blastR : 140) * 0.02;
    /* storymuzzle1: spawn at chest glow world pos (fallback = old ankle+forward) */
    var px = state.player.position.x + fx * muzzle;
    var py = 1.1 + (state.zLift || 0) + (state.inMech ? 1.4 : 0);
    var pz = state.player.position.z + fz * muzzle;
    var mentGun = null;
    for (var mgi = 0; mgi < (state.mechs || []).length; mgi++) {
      if (state.mechs[mgi] && mechSidOf(state.mechId) === state.mechs[mgi].solidId) {
        mentGun = state.mechs[mgi];
        break;
      }
    }
    if (mentGun && mentGun.group && mentGun.chestMuzzleLocal) {
      var chestCfg = (C.mechChestMuzzle) ? C.mechChestMuzzle(mentGun.stories || state.mechStories) : null;
      var extraZ = chestCfg && chestCfg.threeExtraZ != null ? chestCfg.threeExtraZ : 0.18;
      var loc = mentGun.chestMuzzleLocal.clone();
      loc.z += extraZ; /* past the plate so bolt clears the torso */
      mentGun.group.updateMatrixWorld(true);
      mentGun.group.localToWorld(loc);
      px = loc.x;
      py = loc.y;
      pz = loc.z;
    }
    var shellGeo = (typeof THREE.CapsuleGeometry === "function")
      ? new THREE.CapsuleGeometry(0.26, 1.0, 6, 10)
      : new THREE.SphereGeometry(0.32, 10, 8);
    var mesh = new THREE.Mesh(shellGeo, new THREE.MeshBasicMaterial({ color: 0x22d3ee, transparent: true, opacity: 1 }));
    if (typeof THREE.CapsuleGeometry === "function") {
      mesh.rotation.z = Math.PI / 2;
    } else {
      mesh.scale.set(3.6, 1.2, 1.2);
    }
    mesh.position.set(px, py, pz);
    mesh.rotation.y = yaw;
    scene.add(mesh);
    if (!state.shells) state.shells = [];
    state.shells.push({
      mesh: mesh,
      vx: fx * spd,
      vz: fz * spd,
      life: life,
      maxLife: life,
      yaw: yaw,
      blastR: blastR3,
      hitR: 0.7,
      size: cfg.size != null ? cfg.size : 2.8,
      omnigun: true,
      ownerId: state.frogId,
      ownerMechId: state.mechId,
    });
    if (state.shells.length > 14) {
      var old = state.shells.shift();
      if (old && old.mesh && old.mesh.parent) old.mesh.parent.remove(old.mesh);
    }
    if (!state.fx) state.fx = [];
    for (var zi = 0; zi < 14; zi++) {
      var spark = new THREE.Mesh(
        new THREE.SphereGeometry(0.09 + (zi % 3) * 0.04, 5, 4),
        new THREE.MeshBasicMaterial({ color: zi % 2 ? 0xf472b6 : 0x67e8f9, transparent: true, opacity: 0.95 })
      );
      spark.position.set(px - fx * 0.15, py, pz - fz * 0.15);
      scene.add(spark);
      state.fx.push({ mesh: spark, life: 0.34 + zi * 0.02, rise: 1.8, vx: fx * (3.2 + zi * 0.35), vz: fz * (3.2 + zi * 0.35) });
    }
    state.toast = "FIRE!";
    state.toastT = 0.9;
    if (hooks.onToast) hooks.onToast(state.toast);
    if (hooks.onAbilityFire) hooks.onAbilityFire(state.frogId, "FIRE");
    return true;
  }

  /* speartank1: thousand SPEAR cone → wreck tank (parked or driven) */
  function wreckTankSpearThree(ox, oz, yaw, range, halfArc) {
    if (!state || !state.parkedTrucks) return false;
    var hitR = 1.6;
    for (var ti = 0; ti < state.parkedTrucks.length; ti++) {
      var ptk = state.parkedTrucks[ti];
      if (!ptk || ptk.goneForever) continue;
      var spot = ptk.spot || {};
      var style = spot.vehicleStyle || (ptk.mesh && ptk.mesh.userData && ptk.mesh.userData.vehicleStyle);
      if (style !== "tank") continue;
      var hid = spot.id === "shared" ? "truck-shared" : "truck-" + spot.id;
      if (C.isPermaGone && C.isPermaGone(hid)) {
        ptk.goneForever = true;
        if (ptk.mesh) ptk.mesh.visible = false;
        if (ptk.label) ptk.label.visible = false;
        continue;
      }
      var tx = ptk.mesh ? ptk.mesh.position.x : 0;
      var tz = ptk.mesh ? ptk.mesh.position.z : 0;
      if (state.inTruck && state.vehicleStyle === "tank" && (state.truckId === hid || !state.truckId)) {
        tx = state.player.position.x; tz = state.player.position.z;
      } else if (state.companions) {
        for (var ci = 0; ci < state.companions.length; ci++) {
          var cc = state.companions[ci];
          if (cc && cc.userData.inTruck && cc.userData.vehicleStyle === "tank" &&
              (cc.userData.truckId === hid || !cc.userData.truckId)) {
            tx = cc.position.x; tz = cc.position.z; break;
          }
        }
      }
      var dx = tx - ox, dz = tz - oz;
      var dist = Math.hypot(dx, dz);
      if (dist > range + hitR) continue;
      var aim = Math.atan2(dx, dz);
      var da = aim - yaw;
      while (da > Math.PI) da -= Math.PI * 2;
      while (da < -Math.PI) da += Math.PI * 2;
      if (Math.abs(da) > halfArc && dist > hitR * 0.55) continue;
      if (C.markPermaGone) C.markPermaGone(hid);
      ptk.goneForever = true;
      if (ptk.mesh) ptk.mesh.visible = false;
      if (ptk.label) ptk.label.visible = false;
      if (state.inTruck && (state.truckId === hid || state.vehicleStyle === "tank")) {
        state.inTruck = false; state.truckId = null; state.truckMode = null; state.vehicleStyle = null;
        state.truckPilotPadIndex = null;
      }
      if (state.companions) {
        for (var ej = 0; ej < state.companions.length; ej++) {
          var ce = state.companions[ej];
          if (!ce || !ce.userData.inTruck) continue;
          if (ce.userData.truckId !== hid && ce.userData.vehicleStyle !== "tank") continue;
          ce.userData.inTruck = false; ce.userData.truckId = null;
          ce.userData.truckMode = null; ce.userData.vehicleStyle = null;
          ce.userData.zLift = 0; ce.userData.zVel = 0;
        }
      }
      spawnThreeBoom(tx, 0.6, tz, 2.4);
      return true;
    }
    return false;
  }

  /* spearvis1: visible lance from mech chest toward aim — sized to ment.h (1000≈8.2) */
  function mechEntryBySid(sid) {
    for (var mi = 0; mi < (state.mechs || []).length; mi++) {
      if (state.mechs[mi] && state.mechs[mi].solidId === sid) return state.mechs[mi];
    }
    return null;
  }
  function spawnSpearThrustThree(ox, oz, yaw, mechH) {
    if (!state || !scene) return;
    if (!state.fx) state.fx = [];
    var cfg = C.MECH_SPEAR || {};
    var h = (mechH > 0.5) ? mechH : 8.2;
    var range = cfg.range3 != null ? cfg.range3 : 5.6;
    /* stick past body; hit cone still uses range alone */
    var spearLen = Math.max(range * 1.2, h * 1.15);
    var rad = Math.max(0.22, h * 0.05);
    var tipR = Math.max(0.35, h * 0.08);
    var tipLen = Math.max(0.7, h * 0.16);
    var y = (state.zLift || 0) + h * 0.55;
    var life = cfg.thrustLife != null ? cfg.thrustLife : 0.55;
    if (life < 0.45) life = 0.55;
    var fx = Math.sin(yaw), fz = Math.cos(yaw);
    var shaft = new THREE.Mesh(
      new THREE.CylinderGeometry(rad * 0.85, rad, spearLen, 8),
      new THREE.MeshBasicMaterial({ color: 0xfef3c7, transparent: true, opacity: 1, depthWrite: false })
    );
    shaft.rotation.x = Math.PI / 2;
    shaft.position.set(ox + fx * (spearLen * 0.52), y, oz + fz * (spearLen * 0.52));
    shaft.rotation.y = yaw;
    shaft.renderOrder = 8;
    scene.add(shaft);
    state.fx.push({ mesh: shaft, life: life, maxLife: life, rise: 0, spear: true });
    var tip = new THREE.Mesh(
      new THREE.ConeGeometry(tipR, tipLen, 8),
      new THREE.MeshBasicMaterial({ color: 0xfbbf24, transparent: true, opacity: 1, depthWrite: false })
    );
    tip.rotation.x = Math.PI / 2;
    tip.position.set(ox + fx * spearLen, y, oz + fz * spearLen);
    tip.rotation.y = yaw;
    tip.renderOrder = 9;
    scene.add(tip);
    state.fx.push({ mesh: tip, life: life, maxLife: life, rise: 0, spear: true });
    /* thin glow sheath so it reads against gold mech */
    var glow = new THREE.Mesh(
      new THREE.CylinderGeometry(rad * 1.55, rad * 1.35, spearLen * 0.92, 8),
      new THREE.MeshBasicMaterial({ color: 0xfde68a, transparent: true, opacity: 0.35, depthWrite: false })
    );
    glow.rotation.x = Math.PI / 2;
    glow.position.set(ox + fx * (spearLen * 0.48), y, oz + fz * (spearLen * 0.48));
    glow.rotation.y = yaw;
    glow.renderOrder = 7;
    scene.add(glow);
    state.fx.push({ mesh: glow, life: life * 0.9, maxLife: life * 0.9, rise: 0, spear: true });
  }

  /* spear1: Rexy 1000-mech SPEAR — cone hit vs trillion; speartank1 also wrecks tank */
  function fireMechSpear() {
    if (!state || !state.inMech) return false;
    if (!C.canSpearPilot || !C.canSpearPilot({ inMech: true, frogId: state.frogId, id: state.frogId, mechId: state.mechId, mechStories: state.mechStories })) {
      return false;
    }
    if (C.isPilotKnocked && C.isPilotKnocked({ inMech: true, mechId: state.mechId, mechStories: state.mechStories })) return false;
    if ((state.spearCd || 0) > 0) return false;
    var cfg = C.MECH_SPEAR || { cd: 0.55, range3: 5.6, halfArc: 0.95, thrustLife: 0.28 };
    state.spearCd = cfg.cd != null ? cfg.cd : 0.55;
    var yaw = (state.faceYaw != null) ? state.faceYaw : 0;
    var spA = Math.hypot(state.vx || 0, state.vz || 0);
    if (spA > 1.0) yaw = Math.atan2(state.vx, state.vz);
    var fx = Math.sin(yaw), fz = Math.cos(yaw);
    var range = cfg.range3 != null ? cfg.range3 : 5.6;
    var halfArc = cfg.halfArc != null ? cfg.halfArc : 0.95;
    /* spearvis1: mech-scaled tip/thrust (hit cone unchanged below) */
    var mentSelf = mechEntryBySid(mechSidOf(state.mechId)) || mechEntryBySid("mech1000");
    spawnSpearThrustThree(state.player.position.x, state.player.position.z, yaw, mentSelf ? mentSelf.h : 8.2);
    /* speartank1: tank wreck first (same cone); then spear1 trillion knock */
    var tankHit = wreckTankSpearThree(state.player.position.x, state.player.position.z, yaw, range, halfArc);
    var ment = null;
    for (var mi = 0; mi < (state.mechs || []).length; mi++) {
      if (state.mechs[mi] && state.mechs[mi].solidId === "mechTrillion") { ment = state.mechs[mi]; break; }
    }
    var triHit = false;
    var alreadyDown = false;
    if (ment && ment.group) {
      var tx = ment.group.position.x, tz = ment.group.position.z;
      if (state.inMech && mechSidOf(state.mechId) === "mechTrillion") {
        tx = state.player.position.x; tz = state.player.position.z;
      } else if (state.companions) {
        for (var ci = 0; ci < state.companions.length; ci++) {
          var cc = state.companions[ci];
          if (cc && cc.userData.inMech && mechSidOf(cc.userData.mechId) === "mechTrillion") {
            tx = cc.position.x; tz = cc.position.z; break;
          }
        }
      }
      var dx = tx - state.player.position.x, dz = tz - state.player.position.z;
      var dist = Math.hypot(dx, dz);
      var hitR = 2.8;
      var inRange = dist <= range + hitR;
      var aim = Math.atan2(dx, dz);
      var da = aim - yaw;
      while (da > Math.PI) da -= Math.PI * 2;
      while (da < -Math.PI) da += Math.PI * 2;
      var inArc = Math.abs(da) <= halfArc || dist <= hitR * 0.55;
      if (inRange && inArc) {
        if (C.isMechKnocked && C.isMechKnocked("mechTrillion")) {
          alreadyDown = true;
        } else if (C.knockMechDown && C.knockMechDown("mechTrillion")) {
          spawnThreeBoom(tx, 1.2, tz, 1.8);
          triHit = true;
        }
      }
    }
    if (tankHit) {
      state.toast = "SPEAR · tank WRECKED!";
      state.toastT = 1.6;
    } else if (triHit) {
      state.toast = "SPEAR · trillion DOWN!";
      state.toastT = 1.6;
    } else if (alreadyDown) {
      state.toast = "Already down!";
      state.toastT = 0.9;
    } else {
      state.toast = "SPEAR · miss";
      state.toastT = 0.85;
    }
    if (hooks.onToast) hooks.onToast(state.toast);
    if ((tankHit || triHit) && hooks.onAbilityFire) hooks.onAbilityFire(state.frogId, "SPEAR");
    return true;
  }

  function mashGetUpThree() {
    if (!state || !state.inMech) return false;
    if (!C.isPilotKnocked || !C.isPilotKnocked({ inMech: true, mechId: state.mechId, mechStories: state.mechStories })) return false;
    state.cd = 0.08;
    var recovered = C.mashMechKnock ? C.mashMechKnock(state.mechId || "mechTrillion", 1) : false;
    if (recovered) {
      state.toast = "Back up!";
      state.toastT = 1.2;
    } else {
      var st = C.mechKnockState ? C.mechKnockState(state.mechId || "mechTrillion") : null;
      var m = st ? (st.mash || 0) : 0;
      var need = st ? (st.need || 8) : 8;
      state.toast = "MASH! " + m + "/" + need;
      state.toastT = 0.7;
    }
    if (hooks.onToast) hooks.onToast(state.toast);
    return true;
  }

  function spearCompanionPad(padIndex) {
    var c = companionForPad(padIndex);
    if (!c || !c.userData.local || !c.userData.inMech) return false;
    var ent = {
      inMech: true,
      frogId: c.userData.frogId || c.userData.id,
      id: c.userData.frogId || c.userData.id,
      mechId: c.userData.mechId,
      mechStories: c.userData.mechStories,
    };
    if (C.isPilotKnocked && C.isPilotKnocked(ent)) {
      var rec = C.mashMechKnock ? C.mashMechKnock(c.userData.mechId || "mechTrillion", 1) : false;
      state.toast = rec ? "Back up!" : "MASH!";
      state.toastT = rec ? 1.2 : 0.7;
      if (hooks.onToast) hooks.onToast(state.toast);
      return true;
    }
    if (!C.canSpearPilot || !C.canSpearPilot(ent)) return false;
    if ((c.userData.spearCd || 0) > 0) return false;
    var cfg = C.MECH_SPEAR || { cd: 0.55, range3: 5.6, halfArc: 0.95 };
    c.userData.spearCd = cfg.cd != null ? cfg.cd : 0.55;
    var yaw = c.userData.faceYaw != null ? c.userData.faceYaw : 0;
    var fx = Math.sin(yaw), fz = Math.cos(yaw);
    var range = cfg.range3 != null ? cfg.range3 : 5.6;
    var halfArc = cfg.halfArc != null ? cfg.halfArc : 0.95;
    /* spearvis1: companion SPEAR also shows tip/thrust */
    var mentC = mechEntryBySid(mechSidOf(c.userData.mechId)) || mechEntryBySid("mech1000");
    spawnSpearThrustThree(c.position.x, c.position.z, yaw, mentC ? mentC.h : 8.2);
    /* speartank1 + spear1 */
    var tankHit = wreckTankSpearThree(c.position.x, c.position.z, yaw, range, halfArc);
    var ment = null;
    for (var mi = 0; mi < (state.mechs || []).length; mi++) {
      if (state.mechs[mi] && state.mechs[mi].solidId === "mechTrillion") { ment = state.mechs[mi]; break; }
    }
    var triHit = false;
    var alreadyDown = false;
    if (ment && ment.group) {
      var tx = ment.group.position.x, tz = ment.group.position.z;
      if (state.inMech && mechSidOf(state.mechId) === "mechTrillion") {
        tx = state.player.position.x; tz = state.player.position.z;
      } else if (state.companions) {
        for (var ci2 = 0; ci2 < state.companions.length; ci2++) {
          var cc2 = state.companions[ci2];
          if (cc2 && cc2 !== c && cc2.userData.inMech && mechSidOf(cc2.userData.mechId) === "mechTrillion") {
            tx = cc2.position.x; tz = cc2.position.z; break;
          }
        }
      }
      var dx = tx - c.position.x, dz = tz - c.position.z;
      var dist = Math.hypot(dx, dz);
      var hitR = 2.8;
      var inRange = dist <= range + hitR;
      var aim = Math.atan2(dx, dz);
      var da = aim - yaw;
      while (da > Math.PI) da -= Math.PI * 2;
      while (da < -Math.PI) da += Math.PI * 2;
      var inArc = Math.abs(da) <= halfArc || dist <= hitR * 0.55;
      if (inRange && inArc) {
        if (C.isMechKnocked && C.isMechKnocked("mechTrillion")) {
          alreadyDown = true;
        } else if (C.knockMechDown && C.knockMechDown("mechTrillion")) {
          spawnThreeBoom(tx, 1.2, tz, 1.8);
          triHit = true;
        }
      }
    }
    if (tankHit) {
      state.toast = "SPEAR · tank WRECKED!"; state.toastT = 1.6;
    } else if (triHit) {
      state.toast = "SPEAR · trillion DOWN!"; state.toastT = 1.6;
    } else if (alreadyDown) {
      state.toast = "Already down!"; state.toastT = 0.9;
    } else {
      state.toast = "SPEAR · miss"; state.toastT = 0.85;
    }
    if (hooks.onToast) hooks.onToast(state.toast);
    return true;
  }

  function spawnThreeBoom(x, y, z, power) {
    if (!state) return;
    if (!state.fx) state.fx = [];
    var p = power != null ? power : 1.2;
    var core = new THREE.Mesh(
      new THREE.SphereGeometry(0.35 * p, 10, 8),
      new THREE.MeshBasicMaterial({ color: 0xf97316, transparent: true, opacity: 0.95 })
    );
    core.position.set(x, y + 0.3, z);
    scene.add(core);
    state.fx.push({ mesh: core, life: 0.45, rise: 0.4, boom: true, grow: 2.8 * p });
    for (var bi = 0; bi < 14; bi++) {
      var ang = (bi / 14) * Math.PI * 2;
      var bit = new THREE.Mesh(
        new THREE.SphereGeometry(0.08 + (bi % 3) * 0.03, 5, 4),
        new THREE.MeshBasicMaterial({ color: bi % 2 ? 0xfbbf24 : 0xef4444, transparent: true, opacity: 0.9 })
      );
      bit.position.set(x, y + 0.25, z);
      scene.add(bit);
      state.fx.push({
        mesh: bit,
        life: 0.5 + (bi % 5) * 0.04,
        rise: 2.2,
        vx: Math.sin(ang) * (4 + bi * 0.25),
        vz: Math.cos(ang) * (4 + bi * 0.25),
      });
    }
  }

  function blastWreckThree(hx, hz, radius) {
    if (!state) return 0;
    var n = 0;
    var R = radius != null ? radius : 2.4;
    if (state.pushables) {
      for (var i = state.pushables.length - 1; i >= 0; i--) {
        var pu = state.pushables[i];
        if (!pu || pu.wrecked) continue;
        var pt = worldToThree(pu.x, pu.y);
        var dx = pt.x - hx, dz = pt.z - hz;
        var d = Math.hypot(dx, dz);
        var pr = (pu.r || 10) * 0.02;
        if (d > R + pr) continue;
        var fall = 1 - d / (R + pr + 0.01);
        var nx = d > 0.05 ? dx / d : 0, nz = d > 0.05 ? dz / d : 0;
        pu.vx = (pu.vx || 0) + nx * 380 * fall;
        pu.vy = (pu.vy || 0) + nz * 380 * fall;
        if (pu.homeX == null) {
          pu.homeX = pu.x; pu.homeY = pu.y; pu.homeR = pu.r;
          if (pu.mesh) {
            pu.homeScale = pu.mesh.scale.clone();
            pu.homeMat = pu.mesh.material;
            pu.homeMeshY = pu.mesh.position.y;
          }
        }
        pu.wrecked = true;
        pu.wreckT = 0.85 + Math.random() * 0.4;
        pu.respawnT = (C && C.BLAST_RESPAWN_SEC) ? C.BLAST_RESPAWN_SEC : 7;
        if (pu.mesh) {
          pu.mesh.material = new THREE.MeshBasicMaterial({ color: 0x78716c, transparent: true, opacity: 0.85 });
          pu.mesh.scale.multiplyScalar(0.75);
        }
        if (pu.head) {
          if (pu.head.parent) pu.head.parent.remove(pu.head);
          pu.head = null;
        }
        n++;
      }
    }
    /* mech5: ONLY Rexy 1000-story mech among mechs */
    n += blastDestroyMech1000Three(hx, hz, R);
    return n;
  }

  function blastDestroyMech1000Three(hx, hz, radius) {
    if (!state || !state.mechs || !C || !C.isTankBlastableMech) return 0;
    if (C.isMechDestroyed && C.isMechDestroyed("mech1000")) return 0;
    var ment = null;
    for (var mi = 0; mi < state.mechs.length; mi++) {
      if (state.mechs[mi] && state.mechs[mi].solidId === "mech1000") { ment = state.mechs[mi]; break; }
    }
    if (!ment || ment.destroyed) return 0;
    var mx = ment.group ? ment.group.position.x : ment.homeX;
    var mz = ment.group ? ment.group.position.z : ment.homeZ;
    var md = Math.hypot(mx - hx, mz - hz);
    var mr = (ment.h || 8.2) * 0.42; /* pad-ish hit radius */
    if (md > (radius || 2.4) + mr) return 0;
    if (!C.markMechDestroyed("mech1000")) return 0;
    ment.destroyed = true;
    ment.respawnT = (C && C.BLAST_RESPAWN_SEC) ? C.BLAST_RESPAWN_SEC : 7;
    /* eject pilots */
    if (state.inMech && mechSidOf(state.mechId) === "mech1000") {
      state.inMech = false; state.mechId = null; state.mechStories = 0; state.mechPilotPadIndex = null;
      state.toast = "BOOM · Rexy 1000-story mech!"; state.toastT = 2.2;
      if (hooks.onToast) hooks.onToast(state.toast);
    }
    if (state.companions) {
      for (var ci = 0; ci < state.companions.length; ci++) {
        var c = state.companions[ci];
        if (c && c.userData.inMech && mechSidOf(c.userData.mechId) === "mech1000") {
          c.userData.inMech = false; c.userData.mechId = null; c.userData.mechStories = 0;
        }
      }
    }
    spawnThreeBoom(mx, (ment.h || 8) * 0.45, mz, 2.8);
    if (ment.group) {
      ment.group.traverse(function (ch) {
        if (ch.isMesh && ch.material) {
          /* mech8: keep original mats so respawn is full mech, not solid haze-ball */
          if (!ch.userData.homeMat) ch.userData.homeMat = ch.material;
          ch.material = new THREE.MeshBasicMaterial({ color: 0x78716c, transparent: true, opacity: 0.7 });
        }
      });
      ment.wreckT = 1.15;
    }
    if (ment.label) ment.label.visible = false;
    return 1;
  }

  function mechSidOfShell(sh) {
    if (!sh) return null;
    if (sh.ownerMechId) return mechSidOf(sh.ownerMechId);
    if (state && state.inMech && state.frogId === sh.ownerId) return mechSidOf(state.mechId);
    return null;
  }

  /* mechgun1: permanent session kill in Three */
  function blastOmnigunThree(hx, hz, radius, sh) {
    if (!state) return 0;
    var n = 0;
    var R = radius != null ? radius : 2.8;
    var ownSid = mechSidOfShell(sh);
    var ownerId = sh && sh.ownerId;

    if (state.pushables) {
      for (var i = state.pushables.length - 1; i >= 0; i--) {
        var pu = state.pushables[i];
        if (!pu || pu.goneForever) continue;
        var pt = worldToThree(pu.x, pu.y);
        var dx = pt.x - hx, dz = pt.z - hz;
        var d = Math.hypot(dx, dz);
        var pr = (pu.r || 10) * 0.02;
        if (d > R + pr) continue;
        pu.goneForever = true;
        pu.wrecked = true;
        pu.wreckT = 0.9;
        pu.respawnT = 1e12;
        if (pu.mesh) {
          pu.mesh.material = new THREE.MeshBasicMaterial({ color: 0xf472b6, transparent: true, opacity: 0.75 });
          pu.mesh.scale.multiplyScalar(0.6);
        }
        if (pu.head) {
          if (pu.head.parent) pu.head.parent.remove(pu.head);
          pu.head = null;
        }
        n++;
      }
    }

    if (state.mechs) {
      for (var mi = 0; mi < state.mechs.length; mi++) {
        var ment = state.mechs[mi];
        if (!ment || ment.destroyed || ment.goneForever) continue;
        if (ownSid && ment.solidId === ownSid) continue;
        var mx = ment.group ? ment.group.position.x : ment.homeX;
        var mz = ment.group ? ment.group.position.z : ment.homeZ;
        /* piloted mech uses pilot world pos */
        if (state.inMech && mechSidOf(state.mechId) === ment.solidId) {
          mx = state.player.position.x; mz = state.player.position.z;
        }
        if (state.companions) {
          for (var ci0 = 0; ci0 < state.companions.length; ci0++) {
            var c0 = state.companions[ci0];
            if (c0 && c0.userData.inMech && mechSidOf(c0.userData.mechId) === ment.solidId) {
              mx = c0.position.x; mz = c0.position.z;
            }
          }
        }
        var md = Math.hypot(mx - hx, mz - hz);
        var mr = (ment.h || 8.2) * 0.42;
        if (md > R + mr) continue;
        if (C.markMechDestroyed) C.markMechDestroyed(ment.solidId, { permanent: true });
        ment.destroyed = true;
        ment.goneForever = true;
        ment.respawnT = 1e12;
        if (state.inMech && mechSidOf(state.mechId) === ment.solidId) {
          /* should not happen for ownSid — safety */
        } else {
          if (state.companions) {
            for (var ci = 0; ci < state.companions.length; ci++) {
              var c = state.companions[ci];
              if (c && c.userData.inMech && mechSidOf(c.userData.mechId) === ment.solidId) {
                c.userData.inMech = false; c.userData.mechId = null; c.userData.mechStories = 0;
                c.userData.sessionDead = true;
                c.visible = false;
              }
            }
          }
        }
        spawnThreeBoom(mx, (ment.h || 8) * 0.45, mz, 3.0);
        if (ment.group) {
          ment.group.traverse(function (ch) {
            if (ch.isMesh && ch.material) {
              if (!ch.userData.homeMat) ch.userData.homeMat = ch.material;
              ch.material = new THREE.MeshBasicMaterial({ color: 0xf472b6, transparent: true, opacity: 0.65 });
            }
          });
          ment.wreckT = 1.1;
        }
        if (ment.label) ment.label.visible = false;
        n++;
        state.toast = "GONE · story mech!";
        state.toastT = 1.8;
        if (hooks.onToast) hooks.onToast(state.toast);
      }
    }

    /* parked trucks */
    if (state.parkedTrucks) {
      for (var ti = 0; ti < state.parkedTrucks.length; ti++) {
        var ptk = state.parkedTrucks[ti];
        if (!ptk || ptk.goneForever) continue;
        var spot = ptk.spot || {};
        var hid = spot.id === "shared" ? "truck-shared" : "truck-" + spot.id;
        if (C.isPermaGone && C.isPermaGone(hid)) { ptk.goneForever = true; if (ptk.mesh) ptk.mesh.visible = false; continue; }
        var tx = ptk.mesh ? ptk.mesh.position.x : 0;
        var tz = ptk.mesh ? ptk.mesh.position.z : 0;
        if (state.inTruck && state.truckId === hid) {
          tx = state.player.position.x; tz = state.player.position.z;
        }
        if (Math.hypot(tx - hx, tz - hz) > R + 1.2) continue;
        if (C.markPermaGone) C.markPermaGone(hid);
        ptk.goneForever = true;
        if (ptk.mesh) ptk.mesh.visible = false;
        if (ptk.label) ptk.label.visible = false;
        if (state.inTruck && state.truckId === hid) {
          state.inTruck = false; state.truckId = null; state.truckMode = null; state.vehicleStyle = null;
        }
        spawnThreeBoom(tx, 0.6, tz, 2.4);
        n++;
        state.toast = "GONE · vehicle!";
        state.toastT = 1.5;
        if (hooks.onToast) hooks.onToast(state.toast);
      }
    }

    /* parked air */
    if (state.parkedAir) {
      for (var ai = 0; ai < state.parkedAir.length; ai++) {
        var pa = state.parkedAir[ai];
        if (!pa || pa.goneForever) continue;
        if (C.isPermaGone && C.isPermaGone(pa.kind)) { pa.goneForever = true; if (pa.mesh) pa.mesh.visible = false; continue; }
        var ax = pa.mesh ? pa.mesh.position.x : 0;
        var az = pa.mesh ? pa.mesh.position.z : 0;
        if ((state.inHeli && pa.kind === "heli") || (state.inDrone && pa.kind === "drone")) {
          ax = state.player.position.x; az = state.player.position.z;
        }
        if (Math.hypot(ax - hx, az - hz) > R + 1.1) continue;
        if (C.markPermaGone) C.markPermaGone(pa.kind);
        pa.goneForever = true;
        if (pa.mesh) pa.mesh.visible = false;
        if (pa.kind === "heli" && state.inHeli) { state.inHeli = false; }
        if (pa.kind === "drone" && state.inDrone) { state.inDrone = false; }
        spawnThreeBoom(ax, 0.8, az, 2.5);
        n++;
        state.toast = "GONE · " + pa.kind + "!";
        state.toastT = 1.5;
        if (hooks.onToast) hooks.onToast(state.toast);
      }
    }

    /* submarine */
    if (state.parkedSub && !state.parkedSub.userData.goneForever) {
      if (!(C.isPermaGone && C.isPermaGone("submarine"))) {
        var sx = state.parkedSub.position.x, sz = state.parkedSub.position.z;
        if (state.inSub) { sx = state.player.position.x; sz = state.player.position.z; }
        if (Math.hypot(sx - hx, sz - hz) <= R + 1.3) {
          if (C.markPermaGone) C.markPermaGone("submarine");
          state.parkedSub.userData.goneForever = true;
          state.parkedSub.visible = false;
          if (state.inSub) { state.inSub = false; state.subId = null; state.vehicleStyle = null; }
          spawnThreeBoom(sx, 0.5, sz, 2.3);
          n++;
        }
      }
    }

    /* mechgun2: yard decor (trees/rocks/fence/shrubs/flowers) */
    if (state.ranchBlastables) {
      for (var rbi = 0; rbi < state.ranchBlastables.length; rbi++) {
        var rb = state.ranchBlastables[rbi];
        if (!rb || rb.goneForever) continue;
        if (C.isPermaGone && C.isPermaGone(rb.id)) {
          rb.goneForever = true;
          if (rb.meshes) for (var rm0 = 0; rm0 < rb.meshes.length; rm0++) if (rb.meshes[rm0]) rb.meshes[rm0].visible = false;
          continue;
        }
        var rpt = worldToThree(rb.x, rb.y);
        var rpr = (rb.r || 14) * 0.02;
        if (Math.hypot(rpt.x - hx, rpt.z - hz) > R + rpr) continue;
        if (C.markPermaGone) C.markPermaGone(rb.id);
        rb.goneForever = true;
        if (rb.meshes) {
          for (var rm = 0; rm < rb.meshes.length; rm++) {
            var msh = rb.meshes[rm];
            if (!msh) continue;
            if (rb.kind === "tree") {
              /* stump rubble: shrink canopy away, flatten trunk */
              msh.visible = true;
              if (msh.geometry && msh.geometry.type && String(msh.geometry.type).indexOf("Sphere") >= 0) {
                msh.visible = false;
              } else {
                msh.scale.y *= 0.18;
                msh.position.y *= 0.25;
                if (msh.material) {
                  msh.material = new THREE.MeshBasicMaterial({ color: 0x5c3a1a });
                }
              }
            } else {
              msh.visible = false;
            }
          }
        }
        spawnThreeBoom(rpt.x, rb.kind === "tree" ? 0.9 : 0.35, rpt.z, rb.boom || 1.5);
        n++;
        if (rb.kind === "tree") {
          state.toast = "GONE · tree!";
          state.toastT = 1.4;
          if (hooks.onToast) hooks.onToast(state.toast);
        }
      }
    }

    /* mechgun2: track rocks */
    if (state.trackRocks) {
      for (var tri = 0; tri < state.trackRocks.length; tri++) {
        var trk = state.trackRocks[tri];
        if (!trk || trk.goneForever) continue;
        var tid = trk.id || ("track-rock-" + tri);
        if (C.isPermaGone && C.isPermaGone(tid)) {
          trk.goneForever = true;
          if (trk.mesh) trk.mesh.visible = false;
          continue;
        }
        var trx = trk.mesh ? trk.mesh.position.x : 0;
        var trz = trk.mesh ? trk.mesh.position.z : 0;
        if (Math.hypot(trx - hx, trz - hz) > R + 0.9) continue;
        if (C.markPermaGone) C.markPermaGone(tid);
        trk.goneForever = true;
        if (trk.mesh) trk.mesh.visible = false;
        spawnThreeBoom(trx, 0.5, trz, 1.6);
        n++;
      }
    }

    /* mechgun2: fish + whales */
    function killThreeSwimmers(arr, rad, boom) {
      if (!arr) return;
      for (var si = 0; si < arr.length; si++) {
        var sw = arr[si];
        if (!sw || sw.userData.goneForever) continue;
        var sdx = sw.position.x - hx, sdz = sw.position.z - hz;
        if (sdx * sdx + sdz * sdz > (R + rad) * (R + rad)) continue;
        sw.userData.goneForever = true;
        sw.visible = false;
        spawnThreeBoom(sw.position.x, 0.3, sw.position.z, boom);
        n++;
      }
    }
    killThreeSwimmers(state.fish, 0.45, 1.2);
    killThreeSwimmers(state.whales, 0.9, 2.2);

    /* mechgun2: house + garage buildings — hide walls, leave faint rubble cubes */
    function wreckBuilding(blastInfo, parts, label) {
      if (!blastInfo || blastInfo.goneForever) return;
      if (C.isPermaGone && C.isPermaGone(blastInfo.id)) {
        blastInfo.goneForever = true;
        if (parts) for (var pi0 = 0; pi0 < parts.length; pi0++) if (parts[pi0]) parts[pi0].visible = false;
        return;
      }
      var bp = worldToThree(blastInfo.x, blastInfo.y);
      var br = (blastInfo.r || 200) * 0.02;
      if (Math.hypot(bp.x - hx, bp.z - hz) > R + br) return;
      if (C.markPermaGone) C.markPermaGone(blastInfo.id);
      blastInfo.goneForever = true;
      if (parts) {
        for (var pi = 0; pi < parts.length; pi++) {
          if (parts[pi]) parts[pi].visible = false;
        }
      }
      /* rubble chunks kids can see */
      for (var rc = 0; rc < 6; rc++) {
        var chunk = new THREE.Mesh(
          new THREE.BoxGeometry(0.35 + Math.random() * 0.45, 0.18 + Math.random() * 0.22, 0.3 + Math.random() * 0.4),
          new THREE.MeshStandardMaterial({ color: rc % 2 ? 0x78716c : 0x57534e, roughness: 0.95 })
        );
        chunk.position.set(bp.x + (Math.random() - 0.5) * 3.2, 0.12, bp.z + (Math.random() - 0.5) * 2.8);
        chunk.rotation.set(Math.random(), Math.random(), Math.random());
        scene.add(chunk);
      }
      spawnThreeBoom(bp.x, 1.2, bp.z, blastInfo.boom || 3.5);
      n++;
      state.toast = label;
      state.toastT = 2.0;
      if (hooks.onToast) hooks.onToast(state.toast);
      if (blastInfo.id === "house" && C.markPermaGone) C.markPermaGone("blue-bear");
    }
    wreckBuilding(state.houseBlast, state.houseParts, "GONE · ranch house!");
    wreckBuilding(state.garageBlast, state.garageParts, "GONE · garage!");
    if (state.blueBear && !state.blueBear.userData.goneForever) {
      if ((C.isPermaGone && C.isPermaGone("blue-bear")) ||
          Math.hypot(state.blueBear.position.x - hx, state.blueBear.position.z - hz) <= R + 0.7) {
        if (C.markPermaGone) C.markPermaGone("blue-bear");
        state.blueBear.userData.goneForever = true;
        state.blueBear.visible = false;
        spawnThreeBoom(state.blueBear.position.x, 0.4, state.blueBear.position.z, 1.8);
        n++;
      }
    }

    /* companion froggies */
    if (state.companions) {
      for (var cgi = 0; cgi < state.companions.length; cgi++) {
        var cg = state.companions[cgi];
        if (!cg || cg.userData.sessionDead) continue;
        if (cg.userData.frogId === ownerId) continue;
        if (cg.userData.inMech && ownSid && mechSidOf(cg.userData.mechId) === ownSid) continue;
        var cdx = cg.position.x - hx, cdz = cg.position.z - hz;
        if (cdx * cdx + cdz * cdz > (R + 0.6) * (R + 0.6)) continue;
        cg.userData.sessionDead = true;
        cg.userData.inMech = false; cg.userData.inTruck = false;
        cg.userData.inHeli = false; cg.userData.inDrone = false; cg.userData.inSub = false;
        cg.visible = false;
        spawnThreeBoom(cg.position.x, 0.5, cg.position.z, 1.8);
        n++;
        state.toast = "GONE · " + (cg.userData.name || cg.userData.frogId || "froggy") + "!";
        state.toastT = 1.4;
        if (hooks.onToast) hooks.onToast(state.toast);
      }
    }

    /* soft-handle: if blast hits local player avatar while NOT the shooter (e.g. companion view) — rare */
    if (state.frogId !== ownerId && !state.sessionDead && state.player) {
      var pdx = state.player.position.x - hx, pdz = state.player.position.z - hz;
      if (pdx * pdx + pdz * pdz <= (R + 0.55) * (R + 0.55)) {
        if (!(state.inMech && ownSid && mechSidOf(state.mechId) === ownSid)) {
          state.sessionDead = true;
          state.inTruck = false; state.inMech = false; state.inSub = false;
          state.inHeli = false; state.inDrone = false;
          if (state.player) state.player.visible = false;
          n++;
          state.toast = "OUT · session";
          state.toastT = 2.2;
          if (hooks.onToast) hooks.onToast(state.toast);
        }
      }
    }

    return n;
  }

  function tickTankShells(dt) {
    if (!state || !state.shells) return;
    for (var i = state.shells.length - 1; i >= 0; i--) {
      var sh = state.shells[i];
      sh.life -= dt;
      var hit = false;
      if (sh.mesh) {
        sh.mesh.position.x += sh.vx * dt;
        sh.mesh.position.z += sh.vz * dt;
        if (sh.mesh.material) sh.mesh.material.opacity = Math.max(0.25, sh.life / (sh.maxLife || 1.55));
        if (sh.mesh.material && sh.mesh.material.transparent !== true) {
          sh.mesh.material.transparent = true;
        }
        /* collide: tank = props + Rexy 1000; omnigun = everything (not own mech) */
        if (sh.omnigun) {
          var ownSidH = mechSidOfShell(sh);
          if (state.pushables) {
            for (var pi = 0; pi < state.pushables.length && !hit; pi++) {
              var pu = state.pushables[pi];
              if (!pu || pu.wrecked || pu.goneForever) continue;
              var pt = worldToThree(pu.x, pu.y);
              var dx = pt.x - sh.mesh.position.x, dz = pt.z - sh.mesh.position.z;
              var rr = (sh.hitR || 0.7) + (pu.r || 10) * 0.02;
              if (dx * dx + dz * dz < rr * rr) hit = true;
            }
          }
          if (!hit && state.mechs) {
            for (var mci = 0; mci < state.mechs.length && !hit; mci++) {
              var mentH = state.mechs[mci];
              if (!mentH || mentH.destroyed || mentH.goneForever) continue;
              if (ownSidH && mentH.solidId === ownSidH) continue;
              var mxh = mentH.group ? mentH.group.position.x : mentH.homeX;
              var mzh = mentH.group ? mentH.group.position.z : mentH.homeZ;
              var mrr = (sh.hitR || 0.7) + (mentH.h || 8.2) * 0.42;
              var mdx = mxh - sh.mesh.position.x, mdz = mzh - sh.mesh.position.z;
              if (mdx * mdx + mdz * mdz < mrr * mrr) hit = true;
            }
          }
          if (!hit && state.parkedTrucks) {
            for (var pti = 0; pti < state.parkedTrucks.length && !hit; pti++) {
              var ptk = state.parkedTrucks[pti];
              if (!ptk || ptk.goneForever || !ptk.mesh || !ptk.mesh.visible) continue;
              var tdx = ptk.mesh.position.x - sh.mesh.position.x, tdz = ptk.mesh.position.z - sh.mesh.position.z;
              if (tdx * tdx + tdz * tdz < (sh.hitR || 0.7) + 1.0) hit = true;
            }
          }
          if (!hit && state.parkedAir) {
            for (var pai = 0; pai < state.parkedAir.length && !hit; pai++) {
              var pa = state.parkedAir[pai];
              if (!pa || pa.goneForever || !pa.mesh || !pa.mesh.visible) continue;
              var adx = pa.mesh.position.x - sh.mesh.position.x, adz = pa.mesh.position.z - sh.mesh.position.z;
              if (adx * adx + adz * adz < (sh.hitR || 0.7) + 1.0) hit = true;
            }
          }
          /* driven truck / flying craft at player */
          if (!hit && state.inTruck && state.frogId !== sh.ownerId) {
            var dtx = state.player.position.x - sh.mesh.position.x, dtz = state.player.position.z - sh.mesh.position.z;
            if (dtx * dtx + dtz * dtz < ((sh.hitR || 0.7) + 1.1) * ((sh.hitR || 0.7) + 1.1)) hit = true;
          }
          if (!hit && (state.inHeli || state.inDrone) && state.frogId !== sh.ownerId) {
            var fax = state.player.position.x - sh.mesh.position.x, faz = state.player.position.z - sh.mesh.position.z;
            if (fax * fax + faz * faz < ((sh.hitR || 0.7) + 1.0) * ((sh.hitR || 0.7) + 1.0)) hit = true;
          }
          if (!hit && state.ranchBlastables) {
            for (var rhi = 0; rhi < state.ranchBlastables.length && !hit; rhi++) {
              var rhb = state.ranchBlastables[rhi];
              if (!rhb || rhb.goneForever) continue;
              if (C.isPermaGone && C.isPermaGone(rhb.id)) continue;
              var rht = worldToThree(rhb.x, rhb.y);
              var rhdx = rht.x - sh.mesh.position.x, rhdz = rht.z - sh.mesh.position.z;
              var rhrr = (sh.hitR || 0.7) + (rhb.r || 14) * 0.02;
              if (rhdx * rhdx + rhdz * rhdz < rhrr * rhrr) hit = true;
            }
          }
          if (!hit && state.trackRocks) {
            for (var thi = 0; thi < state.trackRocks.length && !hit; thi++) {
              var thr = state.trackRocks[thi];
              if (!thr || thr.goneForever || !thr.mesh || !thr.mesh.visible) continue;
              var thdx = thr.mesh.position.x - sh.mesh.position.x, thdz = thr.mesh.position.z - sh.mesh.position.z;
              if (thdx * thdx + thdz * thdz < ((sh.hitR || 0.7) + 0.85) * ((sh.hitR || 0.7) + 0.85)) hit = true;
            }
          }
          if (!hit && state.fish) {
            for (var fhi = 0; fhi < state.fish.length && !hit; fhi++) {
              var fh = state.fish[fhi];
              if (!fh || !fh.visible || fh.userData.goneForever) continue;
              var fhdx = fh.position.x - sh.mesh.position.x, fhdz = fh.position.z - sh.mesh.position.z;
              if (fhdx * fhdx + fhdz * fhdz < ((sh.hitR || 0.7) + 0.4) * ((sh.hitR || 0.7) + 0.4)) hit = true;
            }
          }
          if (!hit && state.whales) {
            for (var whi = 0; whi < state.whales.length && !hit; whi++) {
              var wh = state.whales[whi];
              if (!wh || !wh.visible || wh.userData.goneForever) continue;
              var whdx = wh.position.x - sh.mesh.position.x, whdz = wh.position.z - sh.mesh.position.z;
              if (whdx * whdx + whdz * whdz < ((sh.hitR || 0.7) + 0.85) * ((sh.hitR || 0.7) + 0.85)) hit = true;
            }
          }
          function hitBuilding(info) {
            if (!info || info.goneForever) return false;
            if (C.isPermaGone && C.isPermaGone(info.id)) return false;
            var bpt = worldToThree(info.x, info.y);
            var bdx = bpt.x - sh.mesh.position.x, bdz = bpt.z - sh.mesh.position.z;
            var brr = (sh.hitR || 0.7) + (info.r || 200) * 0.018;
            return bdx * bdx + bdz * bdz < brr * brr;
          }
          if (!hit && hitBuilding(state.houseBlast)) hit = true;
          if (!hit && hitBuilding(state.garageBlast)) hit = true;
          if (!hit && state.companions) {
            for (var cgi = 0; cgi < state.companions.length && !hit; cgi++) {
              var cg = state.companions[cgi];
              if (!cg || !cg.visible || cg.userData.sessionDead) continue;
              if (cg.userData.frogId === sh.ownerId) continue;
              var cdx = cg.position.x - sh.mesh.position.x, cdz = cg.position.z - sh.mesh.position.z;
              if (cdx * cdx + cdz * cdz < (sh.hitR || 0.7) + 0.55) hit = true;
            }
          }
        } else {
          if (state.pushables) {
            for (var pi2 = 0; pi2 < state.pushables.length && !hit; pi2++) {
              var pu2 = state.pushables[pi2];
              if (!pu2 || pu2.wrecked || pu2.goneForever) continue;
              var pt2 = worldToThree(pu2.x, pu2.y);
              var dx2 = pt2.x - sh.mesh.position.x, dz2 = pt2.z - sh.mesh.position.z;
              var rr2 = (sh.hitR || 0.55) + (pu2.r || 10) * 0.02;
              if (dx2 * dx2 + dz2 * dz2 < rr2 * rr2) hit = true;
            }
          }
          if (!hit && state.mechs && !(C.isMechDestroyed && C.isMechDestroyed("mech1000"))) {
            for (var mci2 = 0; mci2 < state.mechs.length && !hit; mci2++) {
              var mentH2 = state.mechs[mci2];
              if (!mentH2 || mentH2.solidId !== "mech1000" || mentH2.destroyed) continue;
              var mxh2 = mentH2.group ? mentH2.group.position.x : mentH2.homeX;
              var mzh2 = mentH2.group ? mentH2.group.position.z : mentH2.homeZ;
              var mrr2 = (sh.hitR || 0.55) + (mentH2.h || 8.2) * 0.42;
              var mdx2 = mxh2 - sh.mesh.position.x, mdz2 = mzh2 - sh.mesh.position.z;
              if (mdx2 * mdx2 + mdz2 * mdz2 < mrr2 * mrr2) hit = true;
            }
          }
        }
      }
      if (hit || sh.life <= 0) {
        var bx = sh.mesh ? sh.mesh.position.x : 0;
        var by = sh.mesh ? sh.mesh.position.y : 0.5;
        var bz = sh.mesh ? sh.mesh.position.z : 0;
        if (hit || sh.life <= 0) {
          if (sh.omnigun) {
            blastOmnigunThree(bx, bz, sh.blastR != null ? sh.blastR : 2.8, sh);
            spawnThreeBoom(bx, by, bz, sh.size != null ? sh.size * 0.65 : 1.6);
            if (!state.toast || state.toastT <= 0) {
              state.toast = "BOOM!";
              state.toastT = 0.7;
              if (hooks.onToast) hooks.onToast(state.toast);
            }
          } else {
            blastWreckThree(bx, bz, sh.blastR != null ? sh.blastR : 2.4);
            spawnThreeBoom(bx, by, bz, sh.size != null ? sh.size * 0.5 : 1.2);
            state.toast = "BOOM!";
            state.toastT = 0.7;
            if (hooks.onToast) hooks.onToast(state.toast);
          }
        }
        if (sh.mesh && sh.mesh.parent) sh.mesh.parent.remove(sh.mesh);
        state.shells.splice(i, 1);
      }
    }
    /* mech6: wreck fade then respawn props (~7s) — do not delete */
    if (state.pushables) {
      for (var wi = 0; wi < state.pushables.length; wi++) {
        var wp = state.pushables[wi];
        if (!wp || !wp.wrecked) continue;
        if ((wp.wreckT || 0) > 0) {
          wp.wreckT -= dt;
          if (wp.mesh) {
            wp.mesh.position.y = (wp.mesh.position.y || 0) + dt * 1.4;
            if (wp.mesh.material && wp.mesh.material.opacity != null) {
              wp.mesh.material.transparent = true;
              wp.mesh.material.opacity = Math.max(0, (wp.wreckT || 0) * 1.1);
            }
            wp.mesh.rotation.y += dt * 4;
          }
        } else if (wp.mesh) {
          wp.mesh.visible = false;
          if (wp.head) wp.head.visible = false;
        }
        if (wp.goneForever) {
          if ((wp.wreckT || 0) <= 0 && wp.mesh) wp.mesh.visible = false;
          continue; /* mechgun1: permanent */
        }
        wp.respawnT = (wp.respawnT != null ? wp.respawnT : 7) - dt;
        if (wp.respawnT <= 0) {
          wp.wrecked = false;
          wp.wreckT = 0;
          wp.respawnT = 0;
          wp.vx = 0; wp.vy = 0;
          wp.x = wp.homeX != null ? wp.homeX : wp.x;
          wp.y = wp.homeY != null ? wp.homeY : wp.y;
          if (wp.homeR != null) wp.r = wp.homeR;
          if (wp.mesh) {
            var pt = worldToThree(wp.x, wp.y);
            wp.mesh.position.set(pt.x, wp.homeMeshY != null ? wp.homeMeshY : 0.2, pt.z);
            wp.mesh.visible = true;
            wp.mesh.rotation.set(0, 0, 0);
            if (wp.homeScale) wp.mesh.scale.copy(wp.homeScale);
            if (wp.homeMat) wp.mesh.material = wp.homeMat;
            else if (wp.mesh.material) { wp.mesh.material.opacity = 1; wp.mesh.material.transparent = false; }
          }
          if (wp.head) wp.head.visible = true;
        }
      }
    }
    /* mech6: fade destroyed 1000-mech then respawn (timer in canon) */
    if (state.mechs) {
      for (var wmi = 0; wmi < state.mechs.length; wmi++) {
        var wm = state.mechs[wmi];
        if (!wm || !wm.destroyed) continue;
        if ((wm.wreckT || 0) > 0) {
          wm.wreckT -= dt;
          if (wm.group) {
            wm.group.position.y += dt * 0.8;
            wm.group.rotation.z += dt * 0.9;
            wm.group.traverse(function (ch) {
              if (ch.isMesh && ch.material && ch.material.opacity != null) {
                ch.material.transparent = true;
                ch.material.opacity = Math.max(0, (wm.wreckT || 0) * 0.85);
              }
            });
          }
        } else if (wm.group) {
          wm.group.visible = false;
          if (wm.label) wm.label.visible = false;
        }
      }
    }
    if (C.tickMechRespawn) {
      var revived3 = C.tickMechRespawn(dt);
      if (revived3 && revived3.length) {
        for (var rmi = 0; rmi < (state.mechs || []).length; rmi++) {
          var rm = state.mechs[rmi];
          if (!rm || revived3.indexOf(rm.solidId) < 0) continue;
          if (rm.goneForever || (C.isPermaGone && C.isPermaGone(rm.solidId))) continue;
          rm.destroyed = false;
          rm.wreckT = 0;
          rm.respawnT = 0;
          if (rm.group) {
            rm.group.visible = true;
            /* park / home XZ kept; clear wreck float + tumble */
            rm.group.position.y = 0;
            rm.group.rotation.x = 0;
            rm.group.rotation.z = 0;
            /* mech8: restore saved materials (haze stays faint sphere; body = articulated boxes) */
            rm.group.traverse(function (ch) {
              if (!ch.isMesh) return;
              if (ch.userData.homeMat) {
                ch.material = ch.userData.homeMat;
              } else if (ch.material) {
                ch.material.transparent = false;
                ch.material.opacity = 1;
              }
              if (ch.userData.baseX != null) ch.position.x = ch.userData.baseX;
              if (ch.userData.baseY != null) ch.position.y = ch.userData.baseY;
              if (ch.userData.baseZ != null) ch.position.z = ch.userData.baseZ;
            });
          }
          if (rm.label) rm.label.visible = true;
          state.toast = (rm.solidId === "mech1000" ? "Rexy 1000-story mech is back!" : "Mech is back!");
          state.toastT = 2.0;
          if (hooks.onToast) hooks.onToast(state.toast);
        }
      }
    }
    /* spear1: knockdown timer */
    if (C.tickMechKnock) {
      var gotUp3 = C.tickMechKnock(dt);
      if (gotUp3 && gotUp3.length) {
        state.toast = "Trillion mech is back up!";
        state.toastT = 1.4;
        if (hooks.onToast) hooks.onToast(state.toast);
      }
    }
    if ((state.spearCd || 0) > 0) state.spearCd = Math.max(0, state.spearCd - dt);
  }

  function doAbility() {
    /* interact2: HOP only the frog whose pad/HUD pressed — never all locals */
    if (!state) return;
    /* Secondary pad → hop that companion only */
    if (abilityPadIndex != null &&
        (state.primaryPadIndex == null || abilityPadIndex !== state.primaryPadIndex) &&
        companionForPad(abilityPadIndex)) {
      hopCompanionPad(abilityPadIndex);
      abilityPadIndex = null;
      return;
    }
    /* Primary / keyboard / HUD → camera frog only */
    /* airgun1: ability / Space / X = FIRE while flying (pilot). Climb = R/C/RT. */
    if (state.inHeli || state.inDrone) {
      state.hopWantT = 0;
      fireAirGun();
      abilityPadIndex = null;
      return;
    }
    /* mech4: Tank FIRE ONLY (ability / Space / X / FIRE) — never hop / reload / reset */
    if (state.inTruck && state.vehicleStyle === "tank") {
      state.hopWantT = 0;
      fireTankShell();
      abilityPadIndex = null;
      return;
    }
    /* spear1: knocked trillion — ability/Space/X = MASH (not FIRE) */
    if (state.inMech && C.isPilotKnocked && C.isPilotKnocked({ inMech: true, mechId: state.mechId, mechStories: state.mechStories })) {
      state.hopWantT = 0;
      mashGetUpThree();
      abilityPadIndex = null;
      return;
    }
    /* mechgun1: story-mech omnigun FIRE (replaces hop while piloting) */
    if (state.inMech) {
      var pilot = state.mechPilotPadIndex;
      if (abilityPadIndex != null && pilot != null && abilityPadIndex !== pilot) {
        abilityPadIndex = null;
        return;
      }
      if (abilityPadIndex == null && pilot != null && pilot !== state.primaryPadIndex) {
        abilityPadIndex = null;
        return;
      }
      state.hopWantT = 0;
      fireMechGun();
      abilityPadIndex = null;
      return;
    }
    /* ctrl1: buffer if short CD still ticking (do NOT silent-return — that ate X) */
    if (state.cd > 0) {
      state.hopWantT = Math.max(state.hopWantT || 0, 0.15);
      abilityPadIndex = null;
      return;
    }
    state.cd = 0.1;
    var yaw = (state.faceYaw != null) ? state.faceYaw : 0;
    var spA = Math.hypot(state.vx || 0, state.vz || 0);
    if (spA > 1.0) yaw = Math.atan2(state.vx, state.vz);
    var fx = Math.sin(yaw), fz = Math.cos(yaw);
    if (state.mode === "space") {
      if (state.inOrbit) {
        state.inOrbit = false;
        state.orbitEscapeCool = 1.4;
        var kick = ((state.orbitCfg && state.orbitCfg.hardThrustImpulse) || 320) * 0.02;
        state.vx = Math.cos(state.orbitAngle || 0) * kick;
        state.vz = Math.sin(state.orbitAngle || 0) * kick;
        state.toast = "HOP · left " + ((state.planet && state.planet.name) || "planet") + " orbit";
      } else {
        var impulseS = 5.5;
        state.vx += fx * impulseS;
        state.vz += fz * impulseS;
        state.toast = "HOP · thruster!";
        if (state.jimmy) {
          state.jimmyVx = (Math.random() > 0.5 ? 1 : -1) * 4;
          state.jimmyVz = -3;
          state.jimmyJetT = 0.7;
        }
      }
    } else if (state.inSwim && !state.inSub && !state.inTruck && !state.inMech) {
      /* mech7: water HOP = swim surge (no aerial hop bounce) */
      state.vx += fx * 6.4;
      state.vz += fz * 6.4;
      state.zLift = state.groundLift || 0;
      state.zVel = 0;
      state.hopStretch = 0;
      state.swimPhase = (state.swimPhase || 0) + 1.2;
      state.toast = "🏊 Stroke!";
    } else {
      var fwd = state.inTruck ? 7.2 : state.inMech ? 4.8 : 5.6;
      var up = state.inTruck ? 8.0 : state.inMech ? 8.4 : 9.8;
      var gndL = state.groundLift || 0;
      var air = (state.zLift || 0) > gndL + 0.12;
      var landAge = state.hopLandT != null ? state.hopLandT : 999;
      var canStack = air || landAge <= 0.15;
      if (canStack) state.hopCombo = Math.min(10, (state.hopCombo || 0) + 1);
      else state.hopCombo = 1;
      var combo = state.hopCombo || 1;
      var stackBonus = 0;
      for (var sci = 1; sci < combo; sci++) stackBonus += up * (0.30 * Math.pow(0.86, sci - 1));
      var totalUp = up + stackBonus;
      state.vx += fx * fwd;
      state.vz += fz * fwd;
      if (air) state.zVel = Math.max(0, state.zVel || 0) + totalUp * (0.68 + 0.02 * Math.min(combo, 8));
      else {
        state.zVel = Math.max(state.zVel || 0, totalUp);
        state.zLift = Math.max(state.zLift || 0, gndL + 0.25);
      }
      state.hopLandT = 999;
      state.hopSquash = 0; state.hopStretch = 1;
      state.toast = state.inTruck ? "HOP · truck jump!"
        : state.inMech ? "HOP · mech jump!"
        : (combo > 1 ? ("HOP ×" + combo + "!") : "HOP!");
      if (!state.fx) state.fx = [];
      for (var zi = 0; zi < 8; zi++) {
        var spark = new THREE.Mesh(
          new THREE.SphereGeometry(0.05 + (zi % 3) * 0.02, 5, 4),
          new THREE.MeshBasicMaterial({ color: zi % 2 ? 0x86efac : 0x4ade80, transparent: true, opacity: 0.9 })
        );
        spark.position.set(
          state.player.position.x - fx * (0.15 + zi * 0.1),
          0.25 + Math.random() * 0.15,
          state.player.position.z - fz * (0.15 + zi * 0.1)
        );
        scene.add(spark);
        state.fx.push({ mesh: spark, life: 0.35 + zi * 0.02, rise: 0.8, vx: -fx * (1.2 + zi * 0.15), vz: -fz * (1.2 + zi * 0.15) });
      }
    }
    state.toastT = 1.8;
    if (hooks.onToast) hooks.onToast(state.toast);
    if (hooks.onAbilityFire) hooks.onAbilityFire(state.frogId, "HOP");
    abilityPadIndex = null;
  }

  function tick() {
    if (!active || !state) return;
    raf = requestAnimationFrame(tick);
    var dt = Math.min(0.05, clock.getDelta());
    releaseVehicleLatches(); /* drivefix2: drop latch only after the board button is up */
    state.cd = Math.max(0, state.cd - dt);
    state.toastT = Math.max(0, state.toastT - dt);
    state.exitTipT = Math.max(0, (state.exitTipT || 0) - dt);
    state.bob += dt * 10;
    tickTankShells(dt);

    if (state.mode === "space" && state.solarBodies) {
      state.spaceTime = (state.spaceTime || 0) + dt;
      var tSp = state.spaceTime;
      /* Advance planet positions (slow orbits) */
      for (var sbi = 0; sbi < state.solarBodies.length; sbi++) {
        var sb = state.solarBodies[sbi];
        if (sb.parent) continue;
        if (sb.period > 0 && sb.mesh) {
          var sang = sb.base + tSp / sb.period;
          sb.x = Math.cos(sang) * sb.dist;
          sb.z = Math.sin(sang) * sb.dist;
          sb.mesh.position.x = sb.x;
          sb.mesh.position.z = sb.z;
        }
      }
      var earthB = state.planetMeshes && state.planetMeshes.earth;
      if (earthB) {
        if (state.earth) state.earth.rotation.y += dt * 0.08;
        var moonAng = tSp * 0.55 + 0.8;
        if (state.moonMesh && state.moonBody) {
          state.moonBody.x = earthB.x + Math.cos(moonAng) * state.moonOrbitR;
          state.moonBody.z = earthB.z + Math.sin(moonAng) * state.moonOrbitR;
          state.moonMesh.position.set(state.moonBody.x, 0.4, state.moonBody.z);
        }
        var stAng = tSp * 0.32 + 2.4;
        if (state.stationMesh) {
          var sx = earthB.x + Math.cos(stAng) * state.stationOrbitR;
          var sz = earthB.z + Math.sin(stAng) * state.stationOrbitR;
          state.stationMesh.position.set(sx, 0.55, sz);
          state.stationHot = { id: "station", x: sx, z: sz, r: 1.2 };
          if (state.stationLabel) state.stationLabel.position.set(sx, 1.6, sz);
          if (state.alexMesh) state.alexMesh.position.set(sx - 0.6, 0.35, sz + 0.4);
          if (state.fredMesh) state.fredMesh.position.set(sx + 0.5, 0.35, sz + 0.5);
        }
        if (state.planetMeshes.mars) {
          state.marsPos = { x: state.planetMeshes.mars.x, z: state.planetMeshes.mars.z };
          if (state.invLabel) state.invLabel.position.set(state.marsPos.x, 2.4, state.marsPos.z);
        }
      }
      if (state.asteroidBelt) state.asteroidBelt.rotation.y += dt * 0.02;

      /* Multi-planet gravity wells */
      var cfg = state.orbitCfg || {};
      var pullA = (cfg.pullAccel || 420) * 0.02;
      if (state.orbitEscapeCool > 0) state.orbitEscapeCool -= dt;

      var best = null, bestD = 1e9;
      var plist = state.planets || [];
      for (var pii = 0; pii < plist.length; pii++) {
        var pl = plist[pii];
        var ddd = Math.hypot(state.player.position.x - pl.x, state.player.position.z - pl.z);
        if (ddd < bestD) { bestD = ddd; best = pl; }
      }
      if (best) state.planet = best;
      var softR = best ? (best.soft || 3.2) : 3.2;
      var capR = best ? (best.cap || 1.6) : 1.6;
      var dx = best ? state.player.position.x - best.x : 0;
      var dz = best ? state.player.position.z - best.z : 0;
      var dP = bestD;

      if (state.pullRing && best) {
        state.pullRing.visible = !state.inOrbit && dP < softR * 1.3;
        state.pullRing.position.set(best.x, 0.08, best.z);
        var ps = softR / 3.1;
        state.pullRing.scale.set(ps, ps, ps);
      }
      if (state.capRing && best) {
        state.capRing.visible = !state.inOrbit && dP < softR * 1.3;
        state.capRing.position.set(best.x, 0.09, best.z);
        var cs = capR / 1.8;
        state.capRing.scale.set(cs, cs, cs);
      }
      if (state.orbitRing) {
        state.orbitRing.visible = !!state.inOrbit;
        if (state.inOrbit && state.planet) {
          state.orbitRing.position.set(state.planet.x, 0.1, state.planet.z);
          var os = (state.orbitRadius || 2.2) / 2.1;
          state.orbitRing.scale.set(os, os, os);
        }
      }
      if (state.destBeacon && best) {
        var spdB = Math.hypot(state.vx || 0, state.vz || 0);
        var heading = !!state.inOrbit;
        if (!state.inOrbit && dP < softR * 1.6 && spdB > 0.8) {
          var hx = best.x - state.player.position.x;
          var hz = best.z - state.player.position.z;
          var dot = ((state.vx || 0) * hx + (state.vz || 0) * hz) / (spdB * (dP || 1));
          heading = dot > 0.35;
        }
        var pulse = 0.3 + 0.4 * Math.sin(state.bob * 2.5);
        state.destBeacon.position.set(best.x, 0.12, best.z);
        state.destBeacon.material.opacity = heading ? pulse : (dP < softR ? 0.25 : 0.1);
        state.destBeacon.scale.setScalar((softR / 3.2) * (1 + Math.sin(state.bob * 2) * 0.04));
        if (state.destBeaconLabel) {
          state.destBeaconLabel.visible = heading;
          state.destBeaconLabel.position.set(best.x, 2.2, best.z);
        }
      }
      if (state.escapeBanner) {
        state.escapeBanner.visible = !!state.inOrbit;
        if (state.inOrbit) state.escapeBanner.position.set(state.player.position.x, 2.2, state.player.position.z);
      }
      if (state.inOrbit && state.planet) {
        /* Keep orbit centered on moving body */
        var oSteer = mergedSteer();
        state.orbitRadius = Math.max(state.planet.r + 0.9, Math.min(softR * 0.9, state.orbitRadius + (oSteer.y || 0) * 1.2 * dt));
        state.orbitAngle += (0.85 + (oSteer.x || 0) * 0.35) * dt;
        state.player.position.x = state.planet.x + Math.cos(state.orbitAngle) * state.orbitRadius;
        state.player.position.z = state.planet.z + Math.sin(state.orbitAngle) * state.orbitRadius;
        state.vx = 0; state.vz = 0;
      } else if (state.orbitEscapeCool <= 0 && best) {
        if (dP < softR && dP > 0.2) {
          var ang = Math.atan2(dz, dx);
          var pull = pullA * (1 - dP / softR) * dt;
          state.vx -= Math.cos(ang) * pull;
          state.vz -= Math.sin(ang) * pull;
        }
        if (dP < capR) {
          state.inOrbit = true;
          state.orbitAngle = Math.atan2(dz, dx);
          state.orbitRadius = Math.max(best.r + 1.1, Math.min(2.4, softR * 0.55));
          state.vx = 0; state.vz = 0;
          state.toast = "Orbit locked · " + best.name + " · Escape or hard thruster to leave";
          state.toastT = 3;
        }
      }
    } else if (state.earth && state.mode === "space") {
      state.earth.rotation.y += dt * 0.08;
    }

    /* air1: flight control stack — never walk + fly same frame */
    if (state.mode === "ranch" && (state.inHeli || state.inDrone)) {
      var AirF = global.FroggiesAir;
      var kindF = state.inDrone ? "drone" : "heli";
      if (!state.airWorld) state.airWorld = { air: null, hotspots: [] };
      var craftF = AirF ? AirF.ensureCraft(state.airWorld, kindF) : null;
      var wposF = threeToWorld(state.player.position.x, state.player.position.z);
      if (craftF) {
        craftF.x = wposF.x; craftF.y = wposF.y;
        craftF.z = (state.zLift || 0) / 0.02;
        craftF.vx = (state.vx || 0) / 0.02;
        craftF.vy = (state.vz || 0) / 0.02;
        craftF.vz = (state.zVel || 0) / 0.02;
        if (!craftF.pilotId) craftF.pilotId = state.frogId;
        if (craftF.seats && craftF.seats.indexOf(state.frogId) < 0) {
          for (var sfi = 0; sfi < craftF.seats.length; sfi++) {
            if (!craftF.seats[sfi]) { craftF.seats[sfi] = state.frogId; break; }
          }
        }
      }
      var steerF = mergedSteer();
      var basisF = cameraGroundBasis();
      var mxF = 0, myF = 0;
      if (steerF.x || steerF.y) {
        var lenF = Math.hypot(steerF.x, steerF.y) || 1;
        var ixF = steerF.x / lenF, iyF = steerF.y / lenF;
        mxF = basisF.rx * ixF - basisF.fx * iyF;
        myF = basisF.rz * ixF - basisF.fz * iyF;
        /* Convert three XZ delta to world XY steer: world x ~ three x, world y ~ three z */
        /* tickFlight expects world-space steer in same units as canvas (pixels). Scale. */
        mxF *= 50; myF *= 50;
      }
      if (state._climbPulseT > 0) {
        state._climbPulseT -= dt;
        if (!state.climbIn) state.climbIn = 1;
        if (state._climbPulseT <= 0 && state.climbIn > 0 && !state._climbKeyHeld) state.climbIn = 0;
      }
      /* airgun1: RT climb / LT descend (Space is FIRE) */
      if (!state._climbKeyHeld && !(state._climbPulseT > 0) && global.SimilarizeGamepad) {
        var padClimb = state.truckPilotPadIndex != null ? state.truckPilotPadIndex
          : (state.primaryPadIndex != null ? state.primaryPadIndex : 0);
        var gpCl = global.SimilarizeGamepad.pollPad(padClimb);
        if (gpCl && gpCl.connected) {
          var rtF = gpCl.rtValue != null ? gpCl.rtValue : (gpCl.rt ? 1 : 0);
          var ltF = gpCl.ltValue != null ? gpCl.ltValue : (gpCl.lt ? 1 : 0);
          if (rtF > 0.2) state.climbIn = rtF;
          else if (ltF > 0.2) state.climbIn = -ltF;
          else if (gpCl.y || gpCl.lb) state.climbIn = 1;
          else if ((state.climbIn || 0) !== 0) state.climbIn = 0;
        }
      }
      var climbF = state.climbIn || 0;
      var boostF = !!state.airBoost;
      var frogF = { id: state.frogId, inHeli: state.inHeli, inDrone: state.inDrone };
      if (AirF && craftF) {
        AirF.tickFlight(state.airWorld, [frogF], frogF, dt, mxF, myF, climbF, boostF);
        var tpF = worldToThree(craftF.x, craftF.y);
        state.player.position.x = tpF.x;
        state.player.position.z = tpF.z;
        state.zLift = (craftF.z || 0) * 0.02;
        state.zVel = (craftF.vz || 0) * 0.02;
        state.vx = (craftF.vx || 0) * 0.02;
        state.vz = (craftF.vy || 0) * 0.02;
        state.faceYaw = craftF.faceAngle != null ? -craftF.faceAngle + Math.PI / 2 : state.faceYaw;
        state.groundLift = 0;
        state.player.position.y = state.zLift;
        if (state.driveAir) {
          state.driveAir.visible = true;
          state.driveAir.position.set(tpF.x, airCraftDeckY(craftF.x, craftF.y, state.zLift), tpF.z);
          if (craftF.faceAngle != null) {
            /* heliyaw1: heli mesh nose = +X; faceAngle = atan2(vy,vx) → yaw = -faceAngle.
               Old +PI/2 made heli crab sideways. Drone stays +PI/2 (Ben: drone OK). */
            state.driveAir.rotation.y = state.inHeli
              ? -craftF.faceAngle
              : (-craftF.faceAngle + Math.PI / 2);
          }
          if (state.driveAir.userData.rotor) state.driveAir.userData.rotor.rotation.y = craftF.rotor || 0;
          if (state.driveAir.userData.rotors) {
            for (var ri = 0; ri < state.driveAir.userData.rotors.length; ri++) {
              state.driveAir.userData.rotors[ri].rotation.y = (craftF.rotor || 0) * 1.3 + ri;
            }
          }
        }
        state.player.visible = false;
        if (state.toastT <= 0.2) {
          state.toast = AirF.flyingTip(craftF, kindF);
          /* soft refresh tip without toast spam */
        }
      }
      /* Skip ground locomotion this frame */
    } else {
    /* polish3: snappier locomotion (Canvas feel port) */
    /* tapsteer1: noticeably snappier walk + drive */
    /* mechwalk1: lumber slower/heavier than frog hop; continuous thrust while piloted */
    /* mech5: per-vehicle / per-mech drive (Ripsaw fastest auto) */
    var vStat3 = ((state.inTruck || state.inSub) && C.vehicleDriveStats) ? C.vehicleDriveStats({ vehicleStyle: state.inSub ? "submarine" : state.vehicleStyle, wheelScale: C.getWheelScale ? C.getWheelScale() : 1 }) : null;
    var mStat3 = (state.inMech && C.mechDriveStats) ? C.mechDriveStats(state.mechStories || 10) : null;
    var maxSp = state.mode === "space" ? 11.5 : state.inSub ? 9.2 : state.inTruck ? 15.8 : state.inMech ? 6.8 : state.inSwim ? 8.5 : 13.6;
    var accel = state.mode === "space" ? 22 : state.inSub ? 22 : state.inTruck ? 38 : state.inMech ? 16 : state.inSwim ? 22 : 34;
    var fric = state.mode === "space" ? 3.0 : state.inSub ? 5.5 : state.inTruck ? 4.8 : state.inMech ? 5.2 : state.inSwim ? 6.2 : 7.8;
    if (vStat3) { maxSp *= vStat3.maxSp || 1; accel *= vStat3.accel || 1; fric *= vStat3.fric || 1; }
    if (mStat3) { maxSp *= mStat3.maxSp || 1; accel *= mStat3.accel || 1; fric *= mStat3.fric || 1; }
    /* spear1: freeze while knocked; stick-flick mash */
    var knockedPilot = !!(state.inMech && C.isPilotKnocked && C.isPilotKnocked({ inMech: true, mechId: state.mechId, mechStories: state.mechStories }));
    if (knockedPilot) {
      maxSp = 0; accel = 0;
      state.vx = 0; state.vz = 0;
      if (global.SimilarizeGamepad) {
        var mashPad2 = state.mechPilotPadIndex != null ? state.mechPilotPadIndex : (state.primaryPadIndex != null ? state.primaryPadIndex : 0);
        var gpM2 = global.SimilarizeGamepad.pollPad(mashPad2);
        if (gpM2 && gpM2.connected) {
          var sm2 = Math.hypot(gpM2.lx || 0, gpM2.ly || 0);
          if (sm2 > 0.72 && !state._mashStickArmed) { state._mashStickArmed = true; mashGetUpThree(); }
          else if (sm2 < 0.35) state._mashStickArmed = false;
        }
      }
    }
    /* ctrl1: RT accel / LT brake on truck + mech — read primary/pilot pad (frame-cached) */
    if (state.mode === "ranch" && (state.inTruck || state.inMech) && global.SimilarizeGamepad) {
      var thrPad = state.inMech && state.mechPilotPadIndex != null ? state.mechPilotPadIndex
        : (state.primaryPadIndex != null ? state.primaryPadIndex : 0);
      var tgp = global.SimilarizeGamepad.pollPad(thrPad);
      if (tgp && tgp.connected) {
        var rtV = tgp.rtValue != null ? tgp.rtValue : (tgp.rt ? 1 : 0);
        var ltV = tgp.ltValue != null ? tgp.ltValue : (tgp.lt ? 1 : 0);
        if (rtV > 0.05) { maxSp *= 1 + rtV * 0.45; accel *= 1 + rtV * 0.55; }
        if (ltV > 0.05) { fric *= 1 + ltV * 2.4; maxSp *= Math.max(0.32, 1 - ltV * 0.6); }
      }
    }

    // Map screen WASD/D-pad → ground plane from the camera that is actually up.
    // steer.y < 0 = Up/W (screen up). Do not hardcode the old +X+Z isometric basis:
    // view3 parked the camera mostly south, so that basis walked diagonal to the screen.
    var hopMx = 0, hopMz = 0, wantMove3 = false;
    if (!(state.mode === "space" && state.inOrbit)) {
    var steer = mergedSteer();
    var airFoot3 = state.mode === "ranch" && !state.inTruck && !state.inMech && ((state.zLift || 0) - (state.groundLift || 0)) > 0.08;
    if (steer.x || steer.y) {
      var len = Math.hypot(steer.x, steer.y) || 1;
      var ix = steer.x / len;
      var iy = steer.y / len; // Up/W is negative
      var basis = cameraGroundBasis();
      var mx = basis.rx * ix + basis.fx * (-iy);
      var mz = basis.rz * ix + basis.fz * (-iy);
      if (Math.abs(mx) + Math.abs(mz) > 0.01) {
        wantMove3 = true;
        hopMx = mx; hopMz = mz;
        var aimYaw = Math.atan2(mx, mz);
        if (state.inMech) {
          /* mechwalk1: heavy robot walk — slow turn, thrust mostly along facing */
          var curM = (state.faceYaw != null) ? state.faceYaw : aimYaw;
          var turnM = 2.35 * (mStat3 && mStat3.turn != null ? mStat3.turn : 1);
          if (C.approachAngle) state.faceYaw = C.approachAngle(curM, aimYaw, turnM * dt);
          else {
            var dM = aimYaw - curM;
            while (dM > Math.PI) dM -= Math.PI * 2;
            while (dM < -Math.PI) dM += Math.PI * 2;
            var stM = turnM * dt;
            if (dM > stM) dM = stM; if (dM < -stM) dM = -stM;
            state.faceYaw = curM + dM;
          }
          state.facing = Math.sin(state.faceYaw) >= 0 ? 1 : -1;
          var mfx = Math.sin(state.faceYaw), mfz = Math.cos(state.faceYaw);
          state.vx += (mfx * 0.72 + mx * 0.28) * accel * dt;
          state.vz += (mfz * 0.72 + mz * 0.28) * accel * dt;
          state.walkPhase = (state.walkPhase || 0) + dt * (5.2 + Math.hypot(state.vx, state.vz) * 0.35);
        } else if (state.inTruck) {
          /* truck1: smooth yaw toward aim; thrust along facing */
          var curY = (state.faceYaw != null) ? state.faceYaw : aimYaw;
          var turnRate = (3.8 + Math.min(2.2, Math.hypot(state.vx, state.vz) / 6)) * (vStat3 && vStat3.turn != null ? vStat3.turn : 1);
          if (C.approachAngle) state.faceYaw = C.approachAngle(curY, aimYaw, turnRate * dt);
          else {
            var dY = aimYaw - curY;
            while (dY > Math.PI) dY -= Math.PI * 2;
            while (dY < -Math.PI) dY += Math.PI * 2;
            var st = turnRate * dt;
            if (dY > st) dY = st; if (dY < -st) dY = -st;
            state.faceYaw = curY + dY;
          }
          state.facing = Math.sin(state.faceYaw) >= 0 ? 1 : -1;
          var fxx = Math.sin(state.faceYaw), fzz = Math.cos(state.faceYaw);
          var blend = 0.2;
          var ax = fxx * (1 - blend) + mx * blend;
          var az = fzz * (1 - blend) + mz * blend;
          var al = Math.hypot(ax, az) || 1;
          state.vx += (ax / al) * accel * dt;
          state.vz += (az / al) * accel * dt;
        } else if (airFoot3) {
          /* hop2: mild air steer during hop arc */
          state.vx += mx * accel * 0.38 * dt;
          state.vz += mz * accel * 0.38 * dt;
          state.facing = mx >= 0 ? 1 : -1;
          state.faceYaw = aimYaw;
        } else if (state.mode === "space") {
          state.vx += mx * accel * dt;
          state.vz += mz * accel * dt;
          state.facing = mx >= 0 ? 1 : -1;
          state.faceYaw = aimYaw;
        } else {
          /* hop2 ranch: plant on brief ground — no hover-slide */
          state.facing = mx >= 0 ? 1 : -1;
          state.faceYaw = aimYaw;
        }
      }
    }
    var fricUse = (state.mode === "ranch" && !state.inTruck && !state.inMech && !airFoot3) ? 14 : fric;
    if (state.inMech && !(steer.x || steer.y)) {
      /* decay lumber gait when stick released */
      state.walkPhase = (state.walkPhase || 0) * 0.9;
    }
    state.vx *= Math.max(0, 1 - fricUse * dt);
    state.vz *= Math.max(0, 1 - fricUse * dt);
    var sp = Math.hypot(state.vx, state.vz);
    if (sp > maxSp) {
      state.vx = (state.vx / sp) * maxSp;
      state.vz = (state.vz / sp) * maxSp;
    }
    state.player.position.x += state.vx * dt;
    state.player.position.z += state.vz * dt;
    } else {
      var sp = 0;
    }
    state.player.position.y = Math.abs(Math.sin(state.bob)) * (sp > 0.5 ? 0.06 : 0.02);
    state.player.scale.x = 1; /* eyes1: yaw instead of flip */
    state.player.rotation.y = (state.faceYaw != null) ? state.faceYaw : 0;

    /* Ranch truck water / ramp / shared pile-in (parity) — after steer, before clamp */
    if (state.mode === "ranch") {
      var wpos0 = threeToWorld(state.player.position.x, state.player.position.z);
      /* truck2: track elev ground plane — same XY scale (0.02); no extra damp */
      var elevZ = 0;
      var onDeck = !!(C.onTrack && C.onTrack(wpos0.x, wpos0.y));
      var rideDeck = onDeck && !state.inSwim && !state.inSub;
      if (rideDeck && C.trackElevAt) {
        elevZ = (C.trackElevAt(wpos0.x, wpos0.y) || 0) * 0.02;
      }
      var prevG = state.groundLift != null ? state.groundLift : elevZ;
      if (rideDeck) {
        /* Snappy follow so hills/ramps are felt, not lerped flat */
        state.groundLift = prevG + (elevZ - prevG) * Math.min(1, 22 * dt);
      } else {
        state.groundLift = (state.groundLift || 0) * Math.exp(-7 * dt);
        if (Math.abs(state.groundLift) < 0.01) state.groundLift = 0;
      }
      var groundLift = state.groundLift || 0;
      var ws3 = C.getWheelScale ? C.getWheelScale() : 1;
      var jumpMul3 = C.wheelJumpMul ? C.wheelJumpMul(ws3) : (0.9 + (ws3 - 1) * 0.55);
      var bounceMul3 = C.wheelBounceMul ? C.wheelBounceMul(ws3) : (0.85 + ws3 * 0.55);
      var clear3 = (ws3 - 1) * 0.12;
      groundLift = groundLift + clear3;
      var airL = (state.zLift || 0) - groundLift;
      if (state.inTruck && C.rampAt) {
        var ramp = C.rampAt(wpos0.x, wpos0.y);
        if (ramp && sp > 1.0 && airL < 0.35 + clear3 * 0.5) {
          state.zVel = Math.max(state.zVel || 0, 6.2 * (ramp.boost || 1.3) * jumpMul3);
          state.zLift = Math.max(state.zLift || 0, groundLift + 0.22);
          state.scrap += 0.02;
        }
      }
      state.rockCool = Math.max(0, (state.rockCool || 0) - dt);
      if (state.inTruck && state.rockCool <= 0 && airL < 0.5 && C.rockHitAt && sp > 1.2) {
        var rock = C.rockHitAt(wpos0.x, wpos0.y, 10 + ws3 * 6);
        if (rock) {
          var into = Math.hypot(wpos0.x - rock.x, wpos0.y - rock.y) || 1;
          var nx = (wpos0.x - rock.x) / into;
          var ny = (wpos0.y - rock.y) / into;
          var rb = (rock.bounce || 1.8) * bounceMul3 * Math.min(1.9, Math.max(0.55, sp / 5));
          state.zVel = Math.max(state.zVel || 0, 5.5 * rb);
          state.zLift = Math.max(state.zLift || 0, groundLift + 0.28);
          state.vx += nx * (2.8 + sp * 0.35) * rb;
          state.vz += ny * (2.8 + sp * 0.35) * rb;
          state.rockCool = 0.28;
          state.scrap += 0.05;
          state.toast = "ROCK HIT!"; state.toastT = 1.2;
          state.shakeT = Math.max(state.shakeT || 0, 0.18);
        }
      }
      var dG = (state.groundLift || 0) - prevG;
      if (state.inTruck && airL < 0.28 + clear3 * 0.4 && sp > 2.4 && dG < -0.035) {
        var crest = Math.min(10.5, (sp * 0.85 + (-dG) * 28) * jumpMul3);
        if (crest > 1.2) {
          state.zVel = Math.max(state.zVel || 0, crest);
          state.zLift = Math.max(state.zLift || 0, groundLift + 0.18);
          state.toast = "AIR!"; state.toastT = Math.max(state.toastT || 0, 0.9);
        }
      }
      /* polish9 + truck1: air hang + land bounce; hop2: snappier foot gravity */
      airL = (state.zLift || 0) - groundLift;
      var gFall = state.inTruck ? 14 : 22;
      if (state.inTruck && airL > 0.55 && Math.abs(state.zVel || 0) < 2.2) gFall *= 0.38;
      if (airL > 0.02 || (state.zVel || 0) !== 0) {
        state.zVel = (state.zVel || 0) - gFall * dt;
        state.zLift = (state.zLift || 0) + state.zVel * dt;
        if (state.zLift <= groundLift) {
          var impact = Math.max(0, -(state.zVel || 0));
          state.zLift = groundLift;
          state.hopLandT = 0;
          if (impact > 1.2) state.zVel = Math.min(3.2 + ws3 * 0.9, impact * 0.28 * bounceMul3);
          else state.zVel = 0;
        }
      } else {
        state.zLift = groundLift;
        state.zVel = 0;
      }
      /* polish9: lap sparkle at gate */
      if (state.inTruck && C.onTrack && C.onTrack(wpos0.x, wpos0.y) && ((state.zLift || 0) - (state.groundLift || 0)) < 0.45) {
        state.lapCd = Math.max(0, (state.lapCd || 0) - dt);
        var side = (wpos0.x - 1870) * 0.55 + (wpos0.y - 2225) * (-0.85);
        var nearG = Math.abs(wpos0.x - 1870) < 110 && Math.abs(wpos0.y - 2225) < 60;
        if (nearG && state.lapSide && side * state.lapSide < 0 && state.lapCd <= 0) {
          state.lapCount = (state.lapCount || 0) + 1; state.lapCd = 2.4; state.scrap += 8;
          state.toast = "LAP " + state.lapCount + " · sparkle finish!"; state.toastT = 1.6;
          var gp2 = worldToThree(1870, 2225);
          for (var lpi = 0; lpi < 12; lpi++) {
            var spark = new THREE.Mesh(
              new THREE.SphereGeometry(0.06, 6, 5),
              new THREE.MeshBasicMaterial({ color: 0xfde68a, transparent: true, opacity: 0.95 })
            );
            spark.position.set(gp2.x + (Math.random() - 0.5) * 0.8, 0.4 + Math.random() * 0.8, gp2.z + (Math.random() - 0.5) * 0.8);
            scene.add(spark);
            if (!state.fx) state.fx = [];
            state.fx.push({ mesh: spark, life: 0.55, rise: 1.2 });
          }
        }
        if (nearG || Math.abs(side) > 40) state.lapSide = side >= 0 ? 1 : -1;
      }
      var wet = (C.inPond && C.inPond(wpos0.x, wpos0.y)) || (C.inYardStream && C.inYardStream(wpos0.x, wpos0.y));
      /* pond1: walk into pond → swim; leave shore → stop swim */
      if (!state.inTruck && !state.inMech && !state.inSub) {
        if (C.inPond && C.inPond(wpos0.x, wpos0.y)) {
          if (!state.inSwim) {
            state.inSwim = true;
            state.waterSub = Math.max(state.waterSub || 0, 0.35);
            state.toast = "🏊 Swimming · Submarine at the shore";
            state.toastT = 1.6;
          }
        } else if (state.inSwim) {
          state.inSwim = false;
        }
      }
      if (state.inSub) {
        state.inSwim = false;
        state.waterSub = Math.min(1.15, Math.max(0.8, (state.waterSub || 0.85) + 0.1));
        /* keep sub in pond */
        var pondB = C.AREAS && C.AREAS[2];
        if (pondB) {
          var ww = threeToWorld(state.player.position.x, state.player.position.z);
          var pad = 40;
          var cx = Math.max(pondB.x + pad, Math.min(pondB.x + pondB.w - pad, ww.x));
          var cy = Math.max(pondB.y + pad, Math.min(pondB.y + pondB.h - pad, ww.y));
          if (cx !== ww.x || cy !== ww.y) {
            var tp = worldToThree(cx, cy);
            state.player.position.x = tp.x;
            state.player.position.z = tp.z;
          }
        }
      }
      if (state.inTruck && wet) {
        var plunge = Math.max(0, -state.zVel) + (((state.zLift || 0) - (state.groundLift || 0)) > 0.4 ? 1 : 0);
        state.waterSub = Math.min(1.15, 0.45 + plunge * 0.2);
        state.vx *= Math.max(0, 1 - 1.5 * dt);
        state.vz *= Math.max(0, 1 - 1.5 * dt);
      } else {
        state.waterSub = Math.max(0, (state.waterSub || 0) - dt * 1.5);
      }
      /* mechwalk1: hide frog while piloting — full story-height mech mesh follows player */
      state.player.visible = !state.inTruck && !state.inMech && !state.inSub && !state.inHeli && !state.inDrone;
      if (!state.inTruck && !state.inMech && !state.inSub) {
        state.player.scale.set(1, 1, 1);
      }
      /* mech7: single sub hull — drive = parked mesh while boarded */
      if (state.inSub) {
        if (!state.driveSub && state.parkedSub) state.driveSub = state.parkedSub;
        if (state.driveSub) {
          state.driveSub.visible = true;
          var diveY = -0.35 - (state.waterSub || 0.85) * 0.25;
          state.driveSub.position.set(state.player.position.x, diveY, state.player.position.z);
          /* subyaw1: nose +X like truck; faceYaw is frog +Z — same -PI/2 as polish11 */
          var yawS = (state.faceYaw != null) ? state.faceYaw : 0;
          var spS = Math.hypot(state.vx || 0, state.vz || 0);
          if (spS > 1.2) yawS = Math.atan2(state.vx, state.vz);
          state.driveSub.rotation.y = yawS - Math.PI / 2;
          if (state.driveSub.userData.bodyMat) {
            state.driveSub.userData.bodyMat.opacity = 0.78;
            state.driveSub.userData.bodyMat.transparent = true;
          }
        }
      } else if (state.parkedSub) {
        state.parkedSub.visible = true;
      }
      /* polish4: bounce + spray / bubbles / walk dust */
      state.bouncePhase = (state.bouncePhase || 0) + dt * (3 + sp * 0.4);
      var airNow = (state.zLift || 0) - (state.groundLift || 0);
      var bounceY = state.inTruck && airNow < 0.35
        ? Math.sin(state.bouncePhase * 2.4) * Math.min(1.2, sp / 8) * 0.08 * (0.85 + ws3 * 0.55) : 0;
      if (!state.fx) state.fx = [];
      if (!state.inTruck && !wet && sp > 1.2) {
        state.dustT = (state.dustT || 0) - dt;
        if (state.dustT <= 0) {
          state.dustT = 0.22;
          var dust = new THREE.Mesh(
            new THREE.SphereGeometry(0.08, 6, 5),
            new THREE.MeshBasicMaterial({ color: 0xb8a070, transparent: true, opacity: 0.5 })
          );
          dust.position.set(state.player.position.x - state.facing * 0.2, (state.groundLift || 0) + 0.08, state.player.position.z);
          scene.add(dust);
          state.fx.push({ mesh: dust, life: 0.35, rise: 0.2 });
        }
      }
      /* polish5: track race dust */
      if (state.inTruck && !wet && sp > 3.5 && airNow < 0.35 && C.onTrack && C.onTrack(wpos0.x, wpos0.y)) {
        if (Math.random() < dt * 3) {
          var td = new THREE.Mesh(
            new THREE.SphereGeometry(0.1 + Math.random() * 0.06, 6, 5),
            new THREE.MeshBasicMaterial({ color: 0xb8a070, transparent: true, opacity: 0.55 })
          );
          td.position.set(state.player.position.x - state.facing * 0.5, (state.groundLift || 0) + 0.1, state.player.position.z);
          scene.add(td);
          state.fx.push({ mesh: td, life: 0.4, rise: 0.25 });
        }
      }
      if (state.inTruck && wet) {
        if (state.waterSub > 0.7 && Math.random() < dt * 5) {
          var bub = new THREE.Mesh(
            new THREE.SphereGeometry(0.06 + Math.random() * 0.05, 6, 5),
            new THREE.MeshBasicMaterial({ color: 0xbae6fd, transparent: true, opacity: 0.65 })
          );
          bub.position.set(state.player.position.x + (Math.random() - 0.5) * 0.6, 0.2, state.player.position.z + (Math.random() - 0.5) * 0.4);
          scene.add(bub);
          state.fx.push({ mesh: bub, life: 0.55, rise: 1.2 });
        } else if (state.waterSub <= 0.7 && sp > 1 && Math.random() < dt * 4) {
          var spr = new THREE.Mesh(
            new THREE.SphereGeometry(0.07, 6, 5),
            new THREE.MeshBasicMaterial({ color: 0xe0f2fe, transparent: true, opacity: 0.7 })
          );
          spr.position.set(state.player.position.x - state.facing * 0.4, 0.15, state.player.position.z);
          scene.add(spr);
          state.fx.push({ mesh: spr, life: 0.4, rise: 0.9 });
          var rip = new THREE.Mesh(
            new THREE.RingGeometry(0.15, 0.22, 16),
            new THREE.MeshBasicMaterial({ color: 0xbae6fd, transparent: true, opacity: 0.6, side: THREE.DoubleSide })
          );
          rip.rotation.x = -Math.PI / 2;
          rip.position.set(state.player.position.x, 0.06, state.player.position.z);
          scene.add(rip);
          state.fx.push({ mesh: rip, life: 0.8, rise: 0, grow: 1.8 });
        }
      }
      /* polish5: ambient pond ripples */
      state.rippleT = (state.rippleT || 0) - dt;
      if (state.rippleT <= 0) {
        state.rippleT = 0.7 + Math.random() * 0.9;
        var pondA = (C.AREAS && C.AREAS[2]) || null;
        if (pondA) {
          var rpx = pondA.x + 80 + Math.random() * (pondA.w - 160);
          var rpy = pondA.y + 80 + Math.random() * (pondA.h - 160);
          var rp3 = worldToThree(rpx, rpy);
          var rip2 = new THREE.Mesh(
            new THREE.RingGeometry(0.12, 0.18, 16),
            new THREE.MeshBasicMaterial({ color: 0xe0f2fe, transparent: true, opacity: 0.55, side: THREE.DoubleSide })
          );
          rip2.rotation.x = -Math.PI / 2;
          rip2.position.set(rp3.x, 0.07, rp3.z);
          scene.add(rip2);
          state.fx.push({ mesh: rip2, life: 1.0, rise: 0, grow: 2.0 });
        }
      }
      /* polish5 / drivefix3: garage door open-near — ANY local frog (not camera-primary only) */
      if (state.garageDoor) {
        var gdx = state.player.position.x - state.garageDoor.userData.cx;
        var gdz = state.player.position.z - state.garageDoor.userData.cz;
        var wantG = Math.hypot(gdx, gdz) < 5.5 ? 1 : 0;
        if (!wantG && state.companions) {
          for (var gci = 0; gci < state.companions.length; gci++) {
            var gc = state.companions[gci];
            if (!gc.userData.local) continue;
            var gdx2 = gc.position.x - state.garageDoor.userData.cx;
            var gdz2 = gc.position.z - state.garageDoor.userData.cz;
            if (Math.hypot(gdx2, gdz2) < 5.5) { wantG = 1; break; }
          }
        }
        state.garageOpen = Math.max(0, Math.min(1, (state.garageOpen || 0) + (wantG ? 2.2 : -1.4) * dt));
        var lift = state.garageOpen * 1.35;
        state.garageDoor.position.y = state.garageDoor.userData.y0 + lift * 0.5;
        state.garageDoor.scale.y = Math.max(0.08, 1 - state.garageOpen * 0.9);
        if (state.garageOpenLabel) state.garageOpenLabel.visible = state.garageOpen > 0.35;
      }
      /* polish5: ambient drift */
      for (var ami = 0; ami < (state.ambient || []).length; ami++) {
        var am = state.ambient[ami];
        am.userData.phase += dt * (am.userData.kind === "firefly" ? 3.2 : 1.4);
        am.position.x += (am.userData.vx + Math.sin(am.userData.phase) * 0.25) * dt;
        am.position.z += (am.userData.vz + Math.cos(am.userData.phase * 0.7) * 0.2) * dt;
        am.position.y = 0.35 + Math.abs(Math.sin(am.userData.phase)) * 0.5;
        if (am.userData.kind === "firefly") am.material.opacity = 0.35 + 0.65 * Math.abs(Math.sin(am.userData.phase));
      }
      /* polish5: tiny land shake */
      var wasAir = state._wasAir;
      var airLand = (state.zLift || 0) - (state.groundLift || 0);
      state._wasAir = airLand > 0.45;
      if (wasAir && airLand <= 0.08 && state.inTruck) {
        state.shakeT = Math.max(state.shakeT || 0, 0.1);
      }
      if ((state.shakeT || 0) > 0) {
        state.shakeT -= dt;
        camera.position.x += (Math.random() - 0.5) * 0.08;
        camera.position.y += (Math.random() - 0.5) * 0.05;
      }
      for (var fxi = state.fx.length - 1; fxi >= 0; fxi--) {
        var fx = state.fx[fxi];
        fx.life -= dt;
        fx.mesh.position.y += (fx.rise != null ? fx.rise : 0.3) * dt;
        if (fx.grow) fx.mesh.scale.multiplyScalar(1 + fx.grow * dt);
        if (fx.vx) { fx.mesh.position.x += fx.vx * dt; fx.vx *= 0.96; }
        if (fx.vz) { fx.mesh.position.z += fx.vz * dt; }
        if (fx.spear && fx.maxLife > 0) {
          fx.mesh.material.opacity = Math.max(0, fx.life / fx.maxLife);
        } else {
          fx.mesh.material.opacity = Math.max(0, fx.life * 1.4);
        }
        if (fx.life <= 0) { scene.remove(fx.mesh); state.fx.splice(fxi, 1); }
      }
      if (state.driveTruck) {
        state.driveTruck.visible = !!state.inTruck;
        if (state.inTruck) {
          /* truck2: zLift already three-Y — was *0.08 (invisible hills) */
          var wsVis = C.getWheelScale ? C.getWheelScale() : 1;
          if (typeof applyTruckWheelScale === "function") applyTruckWheelScale(state.driveTruck, wsVis);
          var wLift = state.driveTruck.userData.wheelLift || 0;
          state.driveTruck.position.set(state.player.position.x, 0.08 + state.zLift + bounceY + wLift, state.player.position.z);
          /* polish11: yaw follows travel so nose matches steer (mesh nose +X → -PI/2 vs frog +Z) */
          var ts0 = state.driveTruck.userData.truckScale || 2.05;
          state.driveTruck.scale.set(ts0, ts0, ts0);
          var yawT = (state.faceYaw != null) ? state.faceYaw : 0;
          var spT = Math.hypot(state.vx || 0, state.vz || 0);
          if (spT > 1.2) yawT = Math.atan2(state.vx, state.vz);
          state.driveTruck.rotation.y = yawT - Math.PI / 2;
          var dive = state.waterSub > 0.7;
          if (state.driveTruck.userData.bodyMat) {
            state.driveTruck.userData.bodyMat.color.setHex(dive ? 0x64748b : 0x9ca3af);
            state.driveTruck.userData.bodyMat.opacity = dive ? 0.72 : 1;
            state.driveTruck.userData.bodyMat.transparent = dive;
          }
        }
      }
      /* mechwalk1: sync story-height mech mesh to pilot; bob/stride while lumbering */
      if (state.mechs && state.mechs.length) {
        for (var mi = 0; mi < state.mechs.length; mi++) {
          var ment = state.mechs[mi];
          if (ment.destroyed || (C.isMechDestroyed && C.isMechDestroyed(ment.solidId))) {
            if (ment.group) ment.group.visible = !!ment.destroyed; /* fading */
            if (ment.label) ment.label.visible = false;
            continue;
          }
          var pilotPos = null; var wpM = 0; var faceY = 0; var lumber = false;
          if (state.inMech && mechSidOf(state.mechId) === ment.solidId) {
            pilotPos = state.player.position;
            wpM = state.walkPhase || 0;
            faceY = state.faceYaw != null ? state.faceYaw : 0;
            lumber = Math.hypot(state.vx || 0, state.vz || 0) > 0.6;
          } else if (state.companions) {
            for (var pci = 0; pci < state.companions.length; pci++) {
              var pc = state.companions[pci];
              if (pc.userData.inMech && mechSidOf(pc.userData.mechId) === ment.solidId) {
                pilotPos = pc.position;
                wpM = pc.userData.walkPhase || 0;
                faceY = pc.userData.faceYaw != null ? pc.userData.faceYaw : 0;
                lumber = Math.hypot(pc.userData.vx || 0, pc.userData.vz || 0) > 0.6;
                break;
              }
            }
          }
          var piloting = !!pilotPos;
          ment.group.visible = true;
          if (ment.label) ment.label.visible = !piloting;
          var tippedNow = !!(C.isMechKnocked && C.isMechKnocked(ment.solidId));
          if (piloting) {
            var bobAmp = (C.mechBand && C.mechBand(ment.stories) === "trillion") ? 0.32 : ment.stories >= 1000 ? 0.22 : ment.stories >= 100 ? 0.12 : 0.07;
            var bobY = (lumber && !tippedNow) ? Math.abs(Math.sin(wpM)) * bobAmp : 0;
            var deckY = (pilotPos === state.player.position)
              ? (state.zLift || 0)
              : ((pilotPos.userData && pilotPos.userData.zLift) || pilotPos.y || 0);
            ment.group.position.x = pilotPos.x;
            ment.group.position.z = pilotPos.z;
            ment.group.position.y = deckY + bobY + (tippedNow ? ment.h * 0.08 : 0);
            ment.group.rotation.y = faceY;
            ment.group.rotation.z = tippedNow ? Math.PI / 2.1 : (lumber ? Math.sin(wpM) * 0.04 : 0);
            ment.group.rotation.x = tippedNow ? 0.15 : (lumber ? Math.sin(wpM * 2) * 0.015 : 0);
            var stride = lumber ? Math.sin(wpM) * (ment.h * 0.04) : 0;
            function offsetLimbs(arr, zOff, yOff) {
              for (var li = 0; li < arr.length; li++) {
                var lm = arr[li];
                lm.position.z = (lm.userData.baseZ || 0) + zOff;
                lm.position.y = (lm.userData.baseY || 0) + yOff;
              }
            }
            offsetLimbs(ment.legsL || [], stride, Math.max(0, -stride) * 0.15);
            offsetLimbs(ment.legsR || [], -stride, Math.max(0, stride) * 0.15);
            offsetLimbs(ment.armsL || [], tippedNow ? 0 : -stride * 0.6, 0);
            offsetLimbs(ment.armsR || [], tippedNow ? 0 : stride * 0.6, 0);
            if (!tippedNow) ment.group.rotation.x = lumber ? Math.sin(wpM * 2) * 0.015 : ment.group.rotation.x;
          } else {
            /* park1: stay at last EXIT / park pos (home only if never parked) */
            var parkM = C.getVehiclePark ? (C.getVehiclePark(ment.id) || C.getVehiclePark(ment.solidId)) : null;
            if (parkM) {
              var ppM = worldToThree(parkM.x, parkM.y);
              ment.group.position.set(ppM.x, tippedNow ? (ment.h * 0.08) : 0, ppM.z);
            } else {
              ment.group.position.set(ment.homeX, tippedNow ? (ment.h * 0.08) : 0, ment.homeZ);
            }
            if (tippedNow) ment.group.rotation.set(0.15, 0, Math.PI / 2.1);
            else ment.group.rotation.set(0, 0, 0);
            function resetLimbs(arr) {
              for (var ri = 0; ri < (arr || []).length; ri++) {
                var rm = arr[ri];
                rm.position.x = rm.userData.baseX || 0;
                rm.position.y = rm.userData.baseY || 0;
                rm.position.z = rm.userData.baseZ || 0;
              }
            }
            resetLimbs(ment.legsL); resetLimbs(ment.legsR);
            resetLimbs(ment.armsL); resetLimbs(ment.armsR);
          }
          /* goldsteam1: animate trillion steam billows (always; stronger when occupied/moving) */
          if (ment.steamPuffs && ment.steamPuffs.length) {
            var stBoost = piloting ? (lumber ? 1.55 : 1.2) : 0.9;
            var stDt = Math.min(0.05, (typeof dt === "number" && dt > 0) ? dt : 0.016);
            for (var spi2 = 0; spi2 < ment.steamPuffs.length; spi2++) {
              var sp = ment.steamPuffs[spi2];
              if (!sp) continue;
              sp.userData.phase = (sp.userData.phase || 0) + stDt * (1.15 * stBoost);
              var life = sp.userData.phase % 2.4;
              sp.position.y = (sp.userData.baseY || 0) + life * 0.55 * stBoost;
              sp.position.x = (sp.userData.pipeX || 0) + Math.sin(sp.userData.phase) * 0.1 * stBoost;
              sp.position.z = (sp.userData.pipeZ || 0) + Math.cos(sp.userData.phase * 0.7) * 0.06;
              var sc = 0.65 + life * 0.95;
              sp.scale.set(sc, sc * 0.85, sc);
              if (sp.material) {
                sp.material.opacity = Math.max(0, (0.55 - life * 0.2) * (piloting ? 1 : 0.78));
                sp.visible = !ment.destroyed;
              }
            }
          }
        }
      }
      if (state.waterPlane) {
        state.waterPlane.visible = !!((state.inTruck && wet) || state.inSub || (state.inSwim && wet));
        if (wet && state.inTruck) {
          state.waterPlane.position.set(state.player.position.x, 0.12 + state.waterSub * 0.08, state.player.position.z);
          state.waterPlane.material.opacity = 0.35 + state.waterSub * 0.4;
        }
      }
      for (var pti = 0; pti < (state.parkedTrucks || []).length; pti++) {
        var pt = state.parkedTrucks[pti];
        var hid = pt.spot.id === "shared" ? "truck-shared" : "truck-" + pt.spot.id;
        /* speartank1 / mechgun1: stay gone after wreck */
        if (pt.goneForever || (C.isPermaGone && (C.isPermaGone(hid) || C.isPermaGone(pt.spot.id)))) {
          pt.goneForever = true;
          if (pt.mesh) pt.mesh.visible = false;
          if (pt.label) pt.label.visible = false;
          continue;
        }
        var takenPrimary = state.inTruck && (state.truckId === hid || (state.truckMode === "shared" && pt.spot.id === "shared"));
        var takenComp = null;
        if (!takenPrimary && state.companions) {
          for (var cti = 0; cti < state.companions.length; cti++) {
            var ct = state.companions[cti];
            if (ct.userData.inTruck && ct.userData.truckId === hid) { takenComp = ct; break; }
          }
        }
        var taken = !!(takenPrimary || takenComp);
        /* drivefix1: companion-driven truck mesh follows companion (like mech sync) */
        if (takenComp) {
          pt.mesh.visible = true;
          if (pt.label) pt.label.visible = false;
          var wLiftC = pt.mesh.userData.wheelLift || 0;
          var bounceC = Math.abs(Math.sin((takenComp.userData.walkPhase || 0))) * 0.04;
          pt.mesh.position.set(takenComp.position.x, 0.08 + (takenComp.userData.zLift || 0) + bounceC + wLiftC, takenComp.position.z);
          var yawC = takenComp.userData.faceYaw != null ? takenComp.userData.faceYaw : 0;
          var spC = Math.hypot(takenComp.userData.vx || 0, takenComp.userData.vz || 0);
          if (spC > 1.2) yawC = Math.atan2(takenComp.userData.vx, takenComp.userData.vz);
          pt.mesh.rotation.y = yawC - Math.PI / 2;
          var ptsC = pt.mesh.userData.truckScale || ((C.TRUCK_VIS && C.TRUCK_VIS.threeScale) || 2.05);
          pt.mesh.scale.setScalar(ptsC);
        } else {
          pt.mesh.visible = !takenPrimary;
          if (pt.label) pt.label.visible = !takenPrimary;
          if (!takenPrimary) {
            var parkTr = C.getVehiclePark ? C.getVehiclePark(hid) : null;
            if (parkTr) {
              var ptp = worldToThree(parkTr.x, parkTr.y);
              pt.mesh.position.x = ptp.x;
              pt.mesh.position.z = ptp.z;
              if (pt.label) { pt.label.position.x = ptp.x; pt.label.position.z = ptp.z; }
            }
            var dTruck = Math.hypot(state.player.position.x - pt.mesh.position.x, state.player.position.z - pt.mesh.position.z);
            var nearT = dTruck < 2.4;
            pt.mesh.position.y = nearT ? 0.06 + Math.abs(Math.sin(state.bob * 1.5)) * 0.08 : 0;
            var pts = pt.mesh.userData.truckScale || ((C.TRUCK_VIS && C.TRUCK_VIS.threeScale) || 2.05);
            pt.mesh.scale.setScalar(pts * (nearT ? 1.06 : 1));
          }
        }
      }
      if (global.FroggiesEngines && global.FroggiesEngines.setWheelPanelVisible) {
        global.FroggiesEngines.setWheelPanelVisible(!!state.inTruck);
      }
      /* truck2: player Y tracks full elev (hidden while driving; cam/companions use it) */
      /* hop2: Y lift + squash/stretch + always-hop cycle (land only) */
      var airH = Math.max(0, (state.zLift || 0) - (state.groundLift || 0));
      if (state._wasHopAir && airH < 0.04) state.hopSquash = 1;
      state._wasHopAir = airH > 0.2;
      if (state.hopSquash > 0) state.hopSquash = Math.max(0, state.hopSquash - dt * 4);
      if (state.hopStretch > 0) state.hopStretch = Math.max(0, state.hopStretch - dt * 2.5);
      var swimNow = !!(state.inSwim && !state.inSub && !state.inTruck && !state.inMech);
      if (swimNow) {
        /* mech7: kill hop bounce in water — stay sunk, stroke pose */
        state.zLift = state.groundLift || 0;
        state.zVel = 0;
        state.hopSquash = 0;
        state.hopStretch = 0;
        state.swimPhase = (state.swimPhase || 0) + dt * (5.5 + Math.min(6, Math.hypot(state.vx || 0, state.vz || 0) * 0.35));
        var sinkY = 0.28 + Math.min(0.35, (state.waterSub || 0.35) * 0.35);
        var strokeBob = Math.sin(state.swimPhase) * 0.03;
        state.player.position.y = (state.groundLift || 0) - sinkY + strokeBob;
        state.player.scale.set(1.05, 0.78, 1.12);
        setFrogSwim(state.player, state.swimPhase, true);
      } else {
        setFrogSwim(state.player, 0, false);
        var bobY = (!state.inTruck && airH < 0.05)
          ? Math.abs(Math.sin(state.bob)) * 0.02 : 0;
        state.player.position.y = (state.zLift || 0) + bobY;
        if (!state.inTruck && !state.inMech) {
          var sq = state.hopSquash || 0;
          var st3 = state.hopStretch || 0;
          var sy = 1 + Math.min(0.4, airH * 0.4) + st3 * 0.18 - sq * 0.3;
          var sx = 1 - Math.min(0.26, airH * 0.26) - st3 * 0.12 + sq * 0.34;
          state.player.scale.set(sx, sy, sx);
          /* hop4: legs spring out mid-air, tuck on land */
          setFrogSpring(state.player, Math.min(1.15, airH * 1.1 + st3 * 0.5 - sq * 0.7));
          if (C.tickLocoHop) {
            state.groundLift = state.groundLift || 0;
            var launched3 = C.tickLocoHop(state, dt, {
              moving: wantMove3,
              zKey: "zLift",
              zvKey: "zVel",
              gndKey: "groundLift",
              up: 7.0,
              lift: 0.30,
              groundHold: 0.011,
              groundEps: 0.08,
            });
            if (launched3 && wantMove3) {
              var hopSp3 = Math.min(maxSp * 0.98, 16.8);
              state.vx = hopMx * hopSp3;
              state.vz = hopMz * hopSp3;
            }
          }
        } else if (state.inTruck) {
          state.player.scale.set(1, 1, 1);
        }
      }
      /* hop1: shove small props + sync meshes */
      if (C.shoveSmallProp && C.tickPushable && state.pushables) {
        var wFrog = threeToWorld(state.player.position.x, state.player.position.z);
        var fvx = (state.vx || 0) / 0.02, fvy = (state.vz || 0) / 0.02;
        for (var pui = 0; pui < state.pushables.length; pui++) {
          var pu = state.pushables[pui];
          if (pu.wrecked) continue;
          if (!state.inTruck) C.shoveSmallProp(pu, wFrog.x, wFrog.y, 20, fvx, fvy, { propR: pu.r, strength: pu.kind === "animal" ? 0.85 : 1 });
          C.tickPushable(pu, dt, { friction: 4.6, bounce: 0.4, bounds: pu.bounds });
          var pt3 = worldToThree(pu.x, pu.y);
          if (pu.mesh) {
            pu.mesh.position.x = pt3.x;
            pu.mesh.position.z = pt3.z;
            if (pu.ar != null) pu.mesh.position.y = pu.ar;
          }
          if (pu.head) {
            pu.head.position.x = pt3.x + (pu.headOx || 0);
            pu.head.position.z = pt3.z;
            if (pu.ar != null) pu.head.position.y = pu.ar * 1.1;
          }
        }
      }
      if (C.shoveSmallProp && state.ambient && !state.inTruck) {
        var wF2 = threeToWorld(state.player.position.x, state.player.position.z);
        var fvx = (state.vx || 0) / 0.02, fvy = (state.vz || 0) / 0.02;
        for (var ami3 = 0; ami3 < state.ambient.length; ami3++) {
          var am3 = state.ambient[ami3];
          var ww = threeToWorld(am3.position.x, am3.position.z);
          var prop = { x: ww.x, y: ww.y, vx: 0, vy: 0, r: 6 };
          if (C.shoveSmallProp(prop, wF2.x, wF2.y, 18, fvx, fvy, { propR: 6, strength: 0.55 })) {
            var at3 = worldToThree(prop.x, prop.y);
            am3.position.x = at3.x;
            am3.position.z = at3.z;
            am3.userData.vx = (am3.userData.vx || 0) + prop.vx * 0.012;
            am3.userData.vz = (am3.userData.vz || 0) + prop.vy * 0.012;
          }
        }
      }
    }

    // solid1 + mechwalk1: solid walls / mech pads / parked trucks; ignore own pad while piloting
    if (state.mode === "ranch" && C.resolveSolid) {
      var wHit = threeToWorld(state.player.position.x, state.player.position.z);
      var radW = state.inTruck ? 38 : state.inSub ? 36 : state.inMech ? 30 : 22;
      var ignoreMech = null;
      if (state.inMech && C.mechSolidId) ignoreMech = C.mechSolidId(state.mechId);
      else if (state.inMech && state.mechId) ignoreMech = String(state.mechId).replace(/^mech-/, "mech");
      var airH3 = Math.max(0, (state.zLift || 0) - (state.groundLift || 0));
      var solidOpts = {
        garageOpen: state.garageOpen || 0,
        inTruck: !!state.inTruck,
        inMech: !!state.inMech,
        inSwim: !!state.inSwim,
        inSub: !!state.inSub,
        ignoreMechId: ignoreMech,
        softPond: !state.inTruck && !state.inMech && !state.inSwim && !state.inSub,
        airHeight: airH3 / 0.02, /* three→world-ish units for canon clear threshold */
        airClearHeight: state.inMech ? 22 : 28,
      };
      var resolved = C.resolveSolid(wHit.x, wHit.y, radW, solidOpts);
      if (resolved.hit) {
        var tHit = worldToThree(resolved.x, resolved.y);
        /* Kill velocity into the blocker so we don't shudder into the wall */
        var pdx = tHit.x - state.player.position.x;
        var pdz = tHit.z - state.player.position.z;
        if (pdx * state.vx + pdz * state.vz < 0) {
          /* incoming — zero component along push */
          var plen = Math.hypot(pdx, pdz) || 1;
          var nx = pdx / plen, nz = pdz / plen;
          var into = state.vx * nx + state.vz * nz;
          if (into < 0) { state.vx -= nx * into; state.vz -= nz * into; }
        } else {
          state.vx *= 0.55; state.vz *= 0.55;
        }
        state.player.position.x = tHit.x;
        state.player.position.z = tHit.z;
      }
    }

    // Clamp ranch bounds — mech5: follows expanded MAP_* (more green/dirt; house size unchanged)
    if (state.mode === "ranch") {
      var halfW = C.MAP_W * 0.01;
      var halfH = C.MAP_H * 0.01;
      state.player.position.x = Math.max(-halfW + 0.5, Math.min(halfW - 0.5, state.player.position.x));
      state.player.position.z = Math.max(-halfH + 0.5, Math.min(halfH - 0.5, state.player.position.z));
    } else {
      /* solarsys1: full compressed solar system reach (Pluto ~56) */
      state.player.position.x = Math.max(-62, Math.min(62, state.player.position.x));
      state.player.position.z = Math.max(-62, Math.min(62, state.player.position.z));
    }

    if (state.nameTag) {
      var tagH = state.inMech ? ((C.mechBand && C.mechBand(state.mechStories) === "trillion") ? 14.5 : (state.mechStories || 10) >= 1000 ? 9.2 : (state.mechStories || 10) >= 100 ? 4.0 : 2.6) : 2.95;
      state.nameTag.position.set(state.player.position.x, tagH + (state.player.position.y || 0), state.player.position.z);
      state.nameTag.visible = !state.inTruck;
    }
    /* polish9: aboard icons when shared */
    if (state.aboardGroup) {
      var sharedOn = !!(state.inTruck && state.truckMode === "shared");
      state.aboardGroup.visible = sharedOn;
      if (sharedOn) {
        state.aboardGroup.position.set(state.player.position.x, (state.zLift || 0) + 0.35, state.player.position.z);
      }
      if (state.aboardLabel) {
        state.aboardLabel.visible = sharedOn;
        state.aboardLabel.position.set(state.player.position.x, 1.55 + (state.zLift || 0), state.player.position.z);
      }
    }
    if ((state.kitFxT || 0) > 0) {
      state.kitFxT -= dt;
      if (!state.kitFxMesh) {
        state.kitFxMesh = new THREE.Mesh(
          new THREE.SphereGeometry(0.35, 10, 8),
          new THREE.MeshBasicMaterial({ color: 0xfbbf24, transparent: true, opacity: 0.55 })
        );
        scene.add(state.kitFxMesh);
      }
      state.kitFxMesh.visible = state.kitFxT > 0;
      state.kitFxMesh.position.set(state.player.position.x, 1.2 + (0.55 - state.kitFxT) * 1.5, state.player.position.z);
      state.kitFxMesh.scale.setScalar(1 + (0.55 - state.kitFxT) * 2);
      state.kitFxMesh.material.opacity = Math.min(1, state.kitFxT * 2) * 0.55;
      if (state.kitFxT <= 0) state.kitFxMesh.visible = false;
    }
    } /* end else !flying ground locomotion */

    // Locked orbit follow — camera offset fixed, no orbit controls / no FPS look
    var target = camera.userData.lockTarget;
    /* spacefix1: while inOrbit, lock cam on planet so starfield/plane stay stable; only frog orbits */
    var followK = Math.min(1, 14 * dt);
    var followX = state.player.position.x;
    var followZ = state.player.position.z;
    if (state.mode === "space" && state.inOrbit && state.planet) {
      followX = state.planet.x;
      followZ = state.planet.z;
    }
    target.x += (followX - target.x) * followK;
    target.z += (followZ - target.z) * followK;
    target.y = 0;
    var camDist = (state.mode === "ranch" ? 18.5 : 12) * (userZoom || 1);
    var camH = state.mode === "ranch" ? 20.5 : 14;
    if (state.mode === "ranch" && (state.inHeli || state.inDrone)) {
      camH += 4 + Math.min(18, (state.zLift || 0) * 1.1);
      camDist += 2 + Math.min(8, (state.zLift || 0) * 0.35);
      target.y = Math.min(12, (state.zLift || 0) * 0.85);
    }
    /* polish6: slight walk bob / tilt */
    var walkBob = 0, walkTilt = 0;
    if (state.mode === "ranch" && !state.inTruck && Math.hypot(state.vx || 0, state.vz || 0) > 1.2) {
      state.walkBobT = (state.walkBobT || 0) + dt * 10;
      walkBob = Math.sin(state.walkBobT) * 0.05;
      walkTilt = Math.sin(state.walkBobT * 0.5) * 0.006;
    }
    /* view3: south-biased follow — look north at open yard / garage mouth (not through bay) */
    var wantCamX = target.x + camDist * 0.22;
    var wantCamY = camH + walkBob;
    var wantCamZ = target.z + camDist * 1.05;
    var wantLookX = target.x;
    var wantLookY = 0.5 + walkTilt + ((state.inHeli || state.inDrone) ? Math.min(10, (state.zLift || 0) * 0.7) : 0);
    var wantLookZ = target.z - (state.mode === "ranch" ? camDist * 0.06 : 0);
    if (state.mode === "ranch" && (state.establishT || 0) > 0 && state.establishCam) {
      state.establishT -= dt;
      var estDur = state.establishDur || 3.2;
      var u = Math.max(0, Math.min(1, 1 - state.establishT / estDur));
      var ease = u * u * (3 - 2 * u);
      var ec = state.establishCam;
      var el = state.establishLook || { x: target.x, z: target.z };
      wantCamX = ec.x + (wantCamX - ec.x) * ease;
      wantCamY = ec.y + (wantCamY - ec.y) * ease;
      wantCamZ = ec.z + (wantCamZ - ec.z) * ease;
      wantLookX = el.x + (wantLookX - el.x) * ease;
      wantLookZ = el.z + (wantLookZ - el.z) * ease;
      if (state.establishT <= 0) {
        state.establishT = 0;
        state.establishCam = null;
        state.establishLook = null;
      }
    }
    camera.position.set(wantCamX, wantCamY, wantCamZ);
    /* view3 + polish10: soft dusk — keep sky blue so forest trees read (was flat forest-green void) */
    if (state.mode === "ranch" && scene) {
      state.dayT = (state.dayT || 0) + dt;
      var day = (state.dayT % 420) / 420;
      var dusk = day < 0.45 ? 0 : (day < 0.7 ? (day - 0.45) / 0.25 : (day < 0.9 ? 1 : Math.max(0, 1 - (day - 0.9) / 0.1)));
      var col = scene.background && scene.background.isColor ? scene.background : new THREE.Color();
      /* 0x7eb8d4 day → warm dusk; never the old 0.10/0.22/0.14 green slab */
      col.setRGB(0.49 + dusk * 0.28, 0.72 - dusk * 0.32, 0.83 - dusk * 0.48);
      scene.background = col;
      if (scene.fog && scene.fog.color) {
        scene.fog.color.setRGB(0.55 + dusk * 0.12, 0.72 - dusk * 0.18, 0.60 - dusk * 0.12);
      }
    }
    camera.lookAt(wantLookX, wantLookY, wantLookZ);
    /* polish6: depth shadow under player */
    if (state.playerShadow) {
      var shS = state.inTruck ? 1.7 : 1;
      var shA = ((state.zLift || 0) - (state.groundLift || 0)) > 0.45 ? 0.12 : 0.32;
      state.playerShadow.position.set(state.player.position.x, (state.groundLift || 0) + 0.04, state.player.position.z);
      state.playerShadow.scale.set(shS, shS, shS);
      state.playerShadow.material.opacity = shA;
      if (state.playerShadowSoft) {
        state.playerShadowSoft.position.set(state.player.position.x, (state.groundLift || 0) + 0.035, state.player.position.z);
        state.playerShadowSoft.scale.set(shS * 1.2, shS * 1.2, shS * 1.2);
        state.playerShadowSoft.material.opacity = shA * 0.45;
      }
    }
    /* polish6: parallax-lite — hills drift slower than camera target */
    if (state.paraHills && state.mode === "ranch") {
      for (var phi = 0; phi < state.paraHills.length; phi++) {
        var ph = state.paraHills[phi];
        var para = ph.userData.para || 0.15;
        ph.position.x = -8 + phi * 10 + target.x * para * 0.15;
      }
    }
    /* polish6: Rexy 1000-story mech wow tip */
    if (state.mode === "ranch") {
      var m1000 = (C.COMPOUND && C.COMPOUND.mech1000) || { x: 340, y: 2420 };
      var mp = worldToThree(m1000.x, m1000.y);
      var dM = Math.hypot(state.player.position.x - mp.x, state.player.position.z - mp.z);
      if (dM < 3.4 && state.toastT <= 0.3) {
        state.toast = "★ WOW · Rexy 1000-story mech · scale tease";
        state.toastT = 1.8;
        if (hooks.onToast) hooks.onToast(state.toast);
      }
    }

    if (state.mode === "ranch") {
      for (var sci = 0; sci < (state.schools || []).length; sci++) {
        var scc = state.schools[sci]; scc.phase += dt * 0.4;
        scc.x += Math.cos(scc.phase) * 0.35 * dt; scc.z += Math.sin(scc.phase * 0.7) * 0.28 * dt;
      }
      for (var fi = 0; fi < (state.fish || []).length; fi++) {
        var fish = state.fish[fi];
        if (fish.userData.goneForever) { fish.visible = false; continue; }
        fish.userData.phase += dt * 2;
        var sch2 = state.schools && state.schools[fish.userData.school];
        if (sch2) {
          fish.position.x = sch2.x + (fish.userData.ox || 0) + Math.sin(fish.userData.phase) * 0.15;
          fish.position.z = sch2.z + (fish.userData.oz || 0) + Math.cos(fish.userData.phase * 0.7) * 0.12;
        } else {
          fish.position.x = fish.userData.bx + Math.sin(fish.userData.phase) * 0.4;
          fish.position.z = fish.userData.bz + Math.cos(fish.userData.phase * 0.7) * 0.28;
        }
      }
      for (var wj = 0; wj < (state.whales || []).length; wj++) {
        var wh = state.whales[wj];
        if (wh.userData.goneForever) { wh.visible = false; continue; }
        wh.userData.phase += dt * 0.7;
        wh.userData.breach = (wh.userData.breach || 0) + dt * 0.7;
        var bl = Math.max(0, Math.sin(wh.userData.breach)) * (wh.userData.breachAmp || 0.7);
        wh.position.x = wh.userData.bx + Math.sin(wh.userData.phase) * 0.9;
        wh.position.z = wh.userData.bz + Math.cos(wh.userData.phase * 0.55) * 0.55;
        wh.position.y = 0.22 + bl; /* polish8 breach arc */
      }
      /* polish8: Blue Bear pet bounce + chimney smoke */
      if (state.blueBear) {
        if (state.blueBear.userData.goneForever || (C.isPermaGone && C.isPermaGone("blue-bear"))) {
          state.blueBear.visible = false;
        } else {
          state.blueBear.userData.bob = (state.blueBear.userData.bob || 0) + dt * 3.2;
          state.blueBear.position.y = Math.abs(Math.sin(state.blueBear.userData.bob)) * 0.18;
        }
      }
      for (var smi = 0; smi < (state.chimneySmoke || []).length; smi++) {
        var puff = state.chimneySmoke[smi];
        puff.userData.phase += dt * 1.2;
        puff.position.y = puff.userData.baseY + (puff.userData.phase % 2.5) * 0.35;
        puff.position.x += Math.sin(puff.userData.phase) * 0.01;
        puff.material.opacity = 0.45 - (puff.userData.phase % 2.5) * 0.12;
      }
      for (var ci = 0; ci < state.companions.length; ci++) {
        var c = state.companions[ci];
        var lag = c.userData.followLag || 0.4;
        c.userData.idleBounce = (c.userData.idleBounce || 0) + dt * 4.2;
        if (c.userData.local && c.userData.padIndex != null && global.SimilarizeGamepad &&
            !(state.inTruck && state.truckMode === "shared")) {
          /* Couch local: steer this froggy from its claimed pad */
          var lgp = global.SimilarizeGamepad.pollPad(c.userData.padIndex);
          var lsx = 0, lsy = 0;
          if (lgp && lgp.connected) {
            lsx = lgp.lx || 0; lsy = lgp.ly || 0;
            if (lgp.dpad) {
              if (lgp.dpad.l) lsx = -1;
              if (lgp.dpad.r) lsx = 1;
              if (lgp.dpad.u) lsy = -1;
              if (lgp.dpad.d) lsy = 1;
            }
            /* interact2: secondary pad A/B only for THAT companion frog */
            if (lgp.buttonsPressed && lgp.buttonsPressed.a) {
              var cow = threeToWorld(c.position.x, c.position.z);
              setInteractFromPad(c.userData.padIndex, cow.x, cow.y);
            }
            if (lgp.buttonsPressed && (lgp.buttonsPressed.b || lgp.buttonsPressed.x)) {
              wantAbility = true;
              abilityPadIndex = c.userData.padIndex;
            }
            if (lgp.buttonsPressed && lgp.buttonsPressed.rb) {
              wantSpear = true;
              abilityPadIndex = c.userData.padIndex;
            }
          }
          /* boardall1: companion lumber-walks their own boarded mech */
          if ((c.userData.spearCd || 0) > 0) c.userData.spearCd = Math.max(0, c.userData.spearCd - dt);
        /* spear1: freeze companion while their mech is knocked */
        if (c.userData.inMech && C.isPilotKnocked && C.isPilotKnocked({ inMech: true, mechId: c.userData.mechId, mechStories: c.userData.mechStories })) {
          c.userData.vx = 0; c.userData.vz = 0;
          continue;
        }
        /* drivefix1/3: companion truck/ripsaw/sub/air drive feel */
        var cAirKind = companionAirKind(c);
        if (cAirKind && global.FroggiesAir) {
          /* Companion pilots or rides heli/drone */
          var AirC = global.FroggiesAir;
          if (!state.airWorld) state.airWorld = { air: null, hotspots: [] };
          var craftC = AirC.ensureCraft(state.airWorld, cAirKind);
          var isPilotC = craftC && craftC.pilotId === c.userData.frogId;
          var wposC = threeToWorld(c.position.x, c.position.z);
          if (craftC && isPilotC) {
            craftC.x = wposC.x; craftC.y = wposC.y;
            craftC.z = (c.userData.zLift || 0) / 0.02;
            craftC.vx = (c.userData.vx || 0) / 0.02;
            craftC.vy = (c.userData.vz || 0) / 0.02;
            craftC.vz = (c.userData.zVel || 0) / 0.02;
            var mxC = 0, myC = 0;
            if (lsx || lsy) {
              var llenC = Math.hypot(lsx, lsy) || 1;
              var lbasisC = cameraGroundBasis();
              mxC = (lbasisC.rx * (lsx / llenC) + lbasisC.fx * (-lsy / llenC)) * 50;
              myC = (lbasisC.rz * (lsx / llenC) + lbasisC.fz * (-lsy / llenC)) * 50;
            }
            /* airgun1: B/Y/LB climb while companion flies (X = FIRE via ability) */
            var climbC = 0;
            if (lgp && lgp.connected && (lgp.b || lgp.y || lgp.lb)) climbC = 1;
            if (lgp && lgp.connected) {
              var rtC = lgp.rtValue != null ? lgp.rtValue : (lgp.rt ? 1 : 0);
              var ltC = lgp.ltValue != null ? lgp.ltValue : (lgp.lt ? 1 : 0);
              if (rtC > 0.2) climbC = rtC;
              else if (ltC > 0.2) climbC = -ltC;
            }
            var frogCF = { id: c.userData.frogId, inHeli: !!c.userData.inHeli, inDrone: !!c.userData.inDrone };
            AirC.tickFlight(state.airWorld, [frogCF], frogCF, dt, mxC, myC, climbC, false);
            var tpC = worldToThree(craftC.x, craftC.y);
            c.position.x = tpC.x; c.position.z = tpC.z;
            c.userData.zLift = (craftC.z || 0) * 0.02;
            c.userData.zVel = (craftC.vz || 0) * 0.02;
            c.userData.vx = (craftC.vx || 0) * 0.02;
            c.userData.vz = (craftC.vy || 0) * 0.02;
            c.userData.faceYaw = craftC.faceAngle != null ? -craftC.faceAngle + Math.PI / 2 : c.userData.faceYaw;
            if (C.setVehiclePark) C.setVehiclePark(cAirKind, craftC.x, craftC.y);
            for (var paiC = 0; paiC < (state.parkedAir || []).length; paiC++) {
              if (state.parkedAir[paiC].kind === cAirKind && state.parkedAir[paiC].mesh) {
                if (!(state.inHeli && cAirKind === "heli") && !(state.inDrone && cAirKind === "drone")) {
                  state.parkedAir[paiC].mesh.visible = true;
                  state.parkedAir[paiC].mesh.position.set(tpC.x, airCraftDeckY(craftC.x, craftC.y, c.userData.zLift), tpC.z);
                  if (craftC.faceAngle != null) {
                    state.parkedAir[paiC].mesh.rotation.y = (cAirKind === "heli")
                      ? -craftC.faceAngle
                      : (-craftC.faceAngle + Math.PI / 2);
                  }
                }
              }
            }
          } else if (craftC) {
            /* Passenger: sync to craft */
            var tpP = worldToThree(craftC.x, craftC.y);
            c.position.x = tpP.x; c.position.z = tpP.z;
            c.userData.zLift = (craftC.z || 0) * 0.02;
            c.userData.vx = 0; c.userData.vz = 0;
          }
          c.visible = false;
          c.position.y = c.userData.zLift || 0;
          /* Skip ground drive below */
        } else {
        var lmax = c.userData.inMech ? 6.8 : c.userData.inSub ? 9.2 : c.userData.inTruck ? 15.8 : 11.5;
          var lacc = c.userData.inMech ? 16 : c.userData.inSub ? 22 : c.userData.inTruck ? 38 : 28;
          var lfric = c.userData.inMech ? 5.2 : c.userData.inSub ? 5.5 : c.userData.inTruck ? 4.8 : 7.5;
          if ((c.userData.inTruck || c.userData.inSub) && C.vehicleDriveStats) {
            var cvs = C.vehicleDriveStats({ vehicleStyle: c.userData.inSub ? "submarine" : c.userData.vehicleStyle, wheelScale: C.getWheelScale ? C.getWheelScale() : 1 });
            if (cvs) {
              if (cvs.maxSp) lmax *= cvs.maxSp;
              if (cvs.accel) lacc *= cvs.accel;
              if (cvs.fric) lfric *= cvs.fric;
            }
          }
          if (lsx || lsy) {
            var llen = Math.hypot(lsx, lsy) || 1;
            var lix = lsx / llen, liy = lsy / llen;
            var lbasis = cameraGroundBasis();
            var lmx = lbasis.rx * lix + lbasis.fx * (-liy);
            var lmz = lbasis.rz * lix + lbasis.fz * (-liy);
            c.userData.vx = (c.userData.vx || 0) + lmx * lacc * dt;
            c.userData.vz = (c.userData.vz || 0) + lmz * lacc * dt;
            c.userData.faceYaw = Math.atan2(lmx, lmz);
          }
          c.userData.vx = (c.userData.vx || 0) * Math.max(0, 1 - lfric * dt);
          c.userData.vz = (c.userData.vz || 0) * Math.max(0, 1 - lfric * dt);
          var lsp = Math.hypot(c.userData.vx, c.userData.vz);
          if (lsp > lmax) {
            c.userData.vx = (c.userData.vx / lsp) * lmax;
            c.userData.vz = (c.userData.vz / lsp) * lmax;
          }
          c.position.x += (c.userData.vx || 0) * dt;
          c.position.z += (c.userData.vz || 0) * dt;
          if ((c.userData.inTruck && c.userData.truckId) || (c.userData.inMech && c.userData.mechId) || (c.userData.inSub && c.userData.subId)) {
            var parkW = threeToWorld(c.position.x, c.position.z);
            var parkId = c.userData.inTruck ? c.userData.truckId : (c.userData.inMech ? c.userData.mechId : c.userData.subId);
            if (C.setVehiclePark) C.setVehiclePark(parkId, parkW.x, parkW.y);
          }
          if (c.userData.inSub && state.parkedSub && !state.inSub) {
            state.parkedSub.visible = true;
            state.parkedSub.position.set(c.position.x, -0.35 - 0.2, c.position.z);
            if (c.userData.faceYaw != null) state.parkedSub.rotation.y = c.userData.faceYaw - Math.PI / 2; /* subyaw1 */
          }
        }
          if (!cAirKind) {
          /* interact2: per-companion hop arc (not while flying) */
          var lsp = Math.hypot(c.userData.vx || 0, c.userData.vz || 0);
          c.userData.cd = Math.max(0, (c.userData.cd || 0) - dt);
          if ((c.userData.hopWantT || 0) > 0) {
            c.userData.hopWantT = Math.max(0, c.userData.hopWantT - dt);
            if ((c.userData.cd || 0) <= 0) {
              c.userData.hopWantT = 0;
              hopCompanionPad(c.userData.padIndex);
            }
          }
          var czv = c.userData.zVel || 0;
          var czl = c.userData.zLift || 0;
          var cww = threeToWorld(c.position.x, c.position.z);
          var cgnd = ranchGroundY(cww.x, cww.y);
          if (czl > cgnd + 0.02 || czv !== 0 || czl < cgnd - 0.02) {
            czv -= 28 * dt;
            czl += czv * dt;
            if (czl <= cgnd) { czl = cgnd; czv = 0; c.userData.hopCombo = 0; }
            c.userData.zVel = czv;
            c.userData.zLift = czl;
          } else {
            c.userData.zLift = cgnd;
            c.userData.zVel = 0;
          }
          if (c.userData.inMech || c.userData.inTruck || c.userData.inSub) {
            c.visible = false;
            c.userData.walkPhase = (c.userData.walkPhase || 0) + dt * (5.5 + lsp * 0.04);
            c.position.y = c.userData.inTruck ? ((c.userData.zLift || 0) + 0.08) : (c.userData.inSub ? -0.2 : 0);
          } else {
            c.visible = true;
            c.position.y = (c.userData.zLift || 0) + Math.abs(Math.sin(c.userData.idleBounce)) * (lsp > 0.8 ? 0.12 : 0.05);
          }
          }
        } else if (state.inTruck && state.truckMode === "shared") {
          var ox = (ci - 1) * 0.45, oz = -0.35 - (ci % 2) * 0.25;
          c.position.x += (state.player.position.x + ox - c.position.x) * Math.min(1, 8 * dt);
          c.position.z += (state.player.position.z + oz - c.position.z) * Math.min(1, 8 * dt);
          c.position.y = 0.7 + (state.zLift || 0) + Math.abs(Math.sin(c.userData.idleBounce)) * 0.05;
          c.visible = true;
          c.userData.faceYaw = state.faceYaw || 0;
        } else if (!c.userData.local) {
          /* drivefix2: boarded AI stays in the vehicle — follow must not yank them out */
          if (c.userData.inTruck || c.userData.inMech || c.userData.inSub || c.userData.inHeli || c.userData.inDrone) {
            c.visible = !(c.userData.inTruck || c.userData.inMech || c.userData.inSub);
            c.userData.vx = 0; c.userData.vz = 0;
          } else {
          c.visible = true;
          var fw = threeToWorld(c.position.x, c.position.z);
          c.position.y = ranchGroundY(fw.x, fw.y) + Math.abs(Math.sin(c.userData.idleBounce)) * 0.14;
          c.userData.timer -= dt;
          if (c.userData.timer <= 0) {
            var behind = -(state.facing || 1) * (1.2 + lag * 2.2);
            c.userData.tx = state.player.position.x + behind + (Math.random() - 0.5) * (3 + lag * 2);
            c.userData.tz = state.player.position.z + (Math.random() - 0.5) * (3 + lag * 2);
            c.userData.timer = 0.9 + lag + Math.random() * (1.3 + lag);
          }
          var fk = Math.min(1, (1.05 / (0.7 + lag)) * dt);
          var cdx = c.userData.tx - c.position.x;
          var cdz = c.userData.tz - c.position.z;
          c.position.x += cdx * fk;
          c.position.z += cdz * fk;
          if (Math.hypot(cdx, cdz) > 0.05) c.userData.faceYaw = Math.atan2(cdx, cdz);
          }
        } else {
          c.visible = true;
          c.position.y = Math.abs(Math.sin(c.userData.idleBounce)) * 0.08;
        }
        c.scale.x = 1;
        setFrogSpring(c, Math.abs(Math.sin(c.userData.idleBounce || 0)) * 0.35);
        c.rotation.y = (c.userData.faceYaw != null) ? c.userData.faceYaw : 0;
        if (c.userData.nameTag) {
          c.userData.nameTag.position.set(c.position.x, c.position.y + 2.55, c.position.z);
          c.userData.nameTag.visible = c.visible;
        }
      }
      /* polish7: zone signs fade when approaching */
      var wpos2 = threeToWorld(state.player.position.x, state.player.position.z);
      for (var zsi = 0; zsi < (state.zoneSigns || []).length; zsi++) {
        var zl = state.zoneSigns[zsi];
        var za = C.zoneSignAlpha ? C.zoneSignAlpha(zl.userData.zone, wpos2.x, wpos2.y) : 0;
        zl.material.opacity = za;
        zl.visible = za > 0.02;
      }
      /* polish7: mini-map lite */
      if (state.miniMapCtx && state.miniMapCanvas) {
        var mctx = state.miniMapCtx, mc = state.miniMapCanvas;
        var mw = mc.width, mh = mc.height;
        mctx.clearRect(0, 0, mw, mh);
        mctx.fillStyle = "rgba(15,23,42,0.78)";
        mctx.fillRect(0, 0, mw, mh);
        mctx.strokeStyle = "rgba(251,191,36,0.55)";
        mctx.strokeRect(0.5, 0.5, mw - 1, mh - 1);
        function mmx(x) { return (x / C.MAP_W) * mw; }
        function mmy(y) { return (y / C.MAP_H) * mh; }
        var marks = [[380,1630,"#fbbf24"],[2780,2270,"#a8a29e"],[3160,670,"#67e8f9"],[940,1660,"#fdba74"],[360,320,"#fde68a"]];
        for (var mi = 0; mi < marks.length; mi++) {
          mctx.fillStyle = marks[mi][2];
          mctx.beginPath(); mctx.arc(mmx(marks[mi][0]), mmy(marks[mi][1]), 2.8, 0, Math.PI * 2); mctx.fill();
        }
        var meW = threeToWorld(state.player.position.x, state.player.position.z);
        mctx.fillStyle = (C.FROG_DEFS[state.frogId] || {}).color || "#fff";
        mctx.beginPath(); mctx.arc(mmx(meW.x), mmy(meW.y), 4, 0, Math.PI * 2); mctx.fill();
        for (var cmi = 0; cmi < state.companions.length; cmi++) {
          var cm = state.companions[cmi];
          var cw = threeToWorld(cm.position.x, cm.position.z);
          var cdef2 = C.FROG_DEFS[cm.userData.frogId] || {};
          mctx.fillStyle = cdef2.color || "#fff";
          mctx.beginPath(); mctx.arc(mmx(cw.x), mmy(cw.y), 2.6, 0, Math.PI * 2); mctx.fill();
        }
        mctx.fillStyle = "#fef3c7";
        mctx.font = "bold 9px system-ui,sans-serif";
        mctx.fillText("MAP", 6, 11);
      }
      var wpos = threeToWorld(state.player.position.x, state.player.position.z);
      /* drivefix2: occupied rig's hotspot rides with the pilot (not a stale mech underfoot) */
      if (C.setVehiclePark) {
        if (state.inTruck && state.truckId) C.setVehiclePark(state.truckId, wpos.x, wpos.y);
        else if (state.inMech && state.mechId) C.setVehiclePark(state.mechId, wpos.x, wpos.y);
        else if (state.inSub && state.subId) C.setVehiclePark(state.subId, wpos.x, wpos.y);
      }
      refreshNearFromLocals();
      if (state.mode === "ranch" && state.near && state.near.id !== state.prevNearId) {
        for (var spi = 0; spi < 10; spi++) {
          var ang = Math.random() * Math.PI * 2, ssp = 0.8 + Math.random() * 1.6;
          var spk = new THREE.Mesh(
            new THREE.SphereGeometry(0.06, 6, 5),
            new THREE.MeshBasicMaterial({ color: 0xfde68a, transparent: true, opacity: 0.95 })
          );
          var np = worldToThree(state.near.x, state.near.y);
          spk.position.set(np.x, 0.6, np.z);
          scene.add(spk);
          state.fx.push({ mesh: spk, life: 0.45, rise: 0.6, vx: Math.cos(ang) * ssp, vz: Math.sin(ang) * ssp });
        }
        state.prevNearId = state.near.id;
      } else if (!state.near) state.prevNearId = null;
      for (var hi = 0; hi < state.hotMeshes.length; hi++) {
        var hg = state.hotMeshes[hi];
        hg.ring.material.opacity = state.near && state.near.id === hg.data.id ? 0.85 : 0.35;
      }
    } else {
      var earthChase = (state.planetMeshes && state.planetMeshes.earth) || { x: 15, z: 0 };
      state.jimmy.position.x += state.jimmyVx * dt;
      state.jimmy.position.z += state.jimmyVz * dt;
      var jdx2 = state.jimmy.position.x - earthChase.x;
      var jdz2 = state.jimmy.position.z - earthChase.z;
      if (Math.hypot(jdx2, jdz2) > 5.5) {
        state.jimmyVx *= -1;
        state.jimmyVz *= -1;
        var jang = Math.atan2(jdz2, jdx2);
        state.jimmy.position.x = earthChase.x + Math.cos(jang) * 5.2;
        state.jimmy.position.z = earthChase.z + Math.sin(jang) * 5.2;
      }
      if (state.jimmyJetT > 0) state.jimmyJetT -= dt;
      var jetBoost = state.jimmyJetT > 0 ? 0.55 + state.jimmyJetT * 0.8 : 0;
      state.jimmy.position.y = 0.15 + Math.abs(Math.sin(state.bob * 1.4)) * 0.35 + jetBoost;
      if (state.jimmyLabel) {
        state.jimmyLabel.position.set(state.jimmy.position.x, 1.8 + jetBoost, state.jimmy.position.z);
      }
      if (state.jimmyFlame) {
        state.jimmyFlame.visible = state.jimmyJetT > 0;
        if (state.jimmyFlame.visible) {
          state.jimmyFlame.position.set(state.jimmy.position.x, state.jimmy.position.y - 0.4, state.jimmy.position.z);
          state.jimmyFlame.material.opacity = Math.min(1, state.jimmyJetT * 2);
        }
      }
      var dJ = state.player.position.distanceTo(state.jimmy.position);
      var dR = Math.hypot(state.player.position.x - state.returnPad.x, state.player.position.z - state.returnPad.z);
      var dSt = state.stationHot
        ? Math.hypot(state.player.position.x - state.stationHot.x, state.player.position.z - state.stationHot.z)
        : 99;
      state.near = null;
      if (dJ < 1.1) state.near = { id: "jimmy", tip: "Catch Jimmy!" };
      else if (dSt < 1.5) state.near = { id: "station", tip: "Space station · orbits Earth" };
      else if (dR < 1.6) state.near = { id: "return", tip: "Return to ranch · Earth home" };
      /* polish6: invader silhouettes when near Mars */
      if (state.marsPos) {
        var dMars = Math.hypot(state.player.position.x - state.marsPos.x, state.player.position.z - state.marsPos.z);
        var nearMars = dMars < 3.2;
        for (var isi2 = 0; isi2 < (state.invSil || []).length; isi2++) {
          state.invSil[isi2].visible = nearMars;
          if (nearMars) state.invSil[isi2].material.opacity = 0.4 + 0.2 * Math.sin(state.bob * 2 + isi2);
        }
        if (state.invLabel) state.invLabel.visible = nearMars;
        if (nearMars && state.toastT <= 0.2) {
          state.toast = "Invader mechs · distant silhouette tease";
          state.toastT = 1.6;
        }
      }
    }

    interactConsumed = false;
    if (wantInteract) {
      wantInteract = false;
      doInteract();
    }
    if (wantAbility) {
      wantAbility = false;
      doAbility();
    }
    /* spear1 */
    if (wantSpear) {
      wantSpear = false;
      if (abilityPadIndex != null &&
          (state.primaryPadIndex == null || abilityPadIndex !== state.primaryPadIndex) &&
          companionForPad(abilityPadIndex)) {
        spearCompanionPad(abilityPadIndex);
      } else if (state.inMech && C.isPilotKnocked && C.isPilotKnocked({ inMech: true, mechId: state.mechId, mechStories: state.mechStories })) {
        mashGetUpThree();
      } else {
        fireMechSpear();
      }
      abilityPadIndex = null;
    }
    /* mech4/mechgun1/airgun1: buffered FIRE while tank OR story mech OR air (never hop) */
    if ((state.inTruck && state.vehicleStyle === "tank") || state.inMech || state.inHeli || state.inDrone) {
      state.hopWantT = 0; /* hop must never consume ability while firing */
      if ((state.fireWantT || 0) > 0) {
        state.fireWantT = Math.max(0, state.fireWantT - dt);
        if (state.cd <= 0) {
          state.fireWantT = 0;
          doAbility();
        }
      }
    } else if ((state.hopWantT || 0) > 0) {
      /* ctrl1: buffered X — hop when CD ready (non-tank) */
      state.hopWantT = Math.max(0, state.hopWantT - dt);
      if (state.cd <= 0) {
        state.hopWantT = 0;
        doAbility();
      }
    }
    /* tankfire1: drain buffered FIRE for couch companions independently of primary vehicle state. */
    if (state.companions) {
      for (var cfi = 0; cfi < state.companions.length; cfi++) {
        var cf = state.companions[cfi];
        if (!cf || !cf.userData || !cf.userData.inTruck || cf.userData.vehicleStyle !== "tank") continue;
        cf.userData.hopWantT = 0;
        if ((cf.userData.fireWantT || 0) > 0) {
          cf.userData.fireWantT = Math.max(0, cf.userData.fireWantT - dt);
          if ((cf.userData.cd || 0) <= 0) {
            cf.userData.fireWantT = 0;
            fireCompanionTankShell(cf);
          }
        }
      }
    }
    interactOrigin = null;
    interactPadIndex = null;
    abilityPadIndex = null;

    updateTapMarkerVisual(dt);
    renderer.render(scene, camera);

    if (hooks.onHud) {
      var def = C.FROG_DEFS[state.frogId];
      var label;
      if (state.mode === "space") label = "Space · Solar System · three.js";
      else {
        var wp = threeToWorld(state.player.position.x, state.player.position.z);
        label = C.areaNameAt(wp.x, wp.y) + " · three.js";
      }
      hooks.onHud({
        mode: state.mode,
        label: label,
        scrap: state.mode === "space" ? state.catches : state.scrap,
        tip: (state.inHeli || state.inDrone)
          ? (state.toastT > 0 ? state.toast : ((global.FroggiesAir && global.FroggiesAir.flyingTip)
              ? global.FroggiesAir.flyingTip(global.FroggiesAir.ensureCraft(state.airWorld || { air: null }, state.inDrone ? "drone" : "heli"), state.inDrone ? "drone" : "heli")
              : "land + INTERACT to hop out"))
          : (state.inTruck || state.inMech || state.inSub)
          ? (state.toastT > 0 ? state.toast : ((state.exitTipT || 0) > 0 ? "EXIT · INTERACT / E" : (state.inSub ? "🛸 Diving · EXIT · INTERACT / E" : (
              (state.inTruck && state.vehicleStyle === "tank") || state.inMech
                ? (state.inMech && C.isPilotKnocked && C.isPilotKnocked({ inMech: true, mechId: state.mechId, mechStories: state.mechStories })
                    ? ((C.mashGetUpTip ? C.mashGetUpTip() : "MASH to get up!") + " · Space / X / B / stick")
                    : (state.inMech && C.canSpearPilot && C.canSpearPilot({ inMech: true, frogId: state.frogId, id: state.frogId, mechId: state.mechId, mechStories: state.mechStories })
                        ? (C.spearTip ? C.spearTip() : "SPEAR · B / RB · FIRE · Space / X · EXIT INTERACT")
                        : ((C.mechGunTip && state.inMech) ? C.mechGunTip() : "FIRE · Space / X / button · EXIT INTERACT")))
                : ""
            ))))
          : (state.toastT > 0 ? state.toast : state.inOrbit ? "Orbit locked · Escape or hard thruster" : state.near ? (
            (C.isMechHotspot && C.isMechHotspot(state.near) && C.canBoardMech && !C.canBoardMech(state.frogId, state.near))
              ? ((C.mechDeniedTip ? C.mechDeniedTip(state.frogId, state.near) : state.near.tip) + " · INTERACT")
              : ((C.isAirHotspot && C.isAirHotspot(state.near))
                  ? ((global.FroggiesAir && global.FroggiesAir.nearPadTip) ? global.FroggiesAir.nearPadTip(C.airKindOf(state.near)) : ("Walk | " + (state.near.kind === "drone" ? "Drone" : "Heli") + " · INTERACT / E"))
                  : ((((C.isTruckHotspot && C.isTruckHotspot(state.near)) || (C.isMechHotspot && C.isMechHotspot(state.near)) || (C.isSubHotspot && C.isSubHotspot(state.near))) ? "BOARD · " : "⚡ ") + state.near.tip + " · INTERACT / E"))
          ) : (state.inSwim ? "🏊 Swimming · Submarine at shore · INTERACT / E" : (state.invLabel && state.invLabel.visible ? "Mars · invader silhouettes" : ""))),
        inOrbit: !!state.inOrbit,
        inTruck: !!state.inTruck,
        inSub: !!state.inSub,
        inSwim: !!state.inSwim,
        inMech: !!state.inMech,
        inHeli: !!state.inHeli,
        inDrone: !!state.inDrone,
        near: (state.inTruck || state.inMech || state.inSub || state.inHeli || state.inDrone) ? true : state.near,
        ability: (state.inMech && C.isPilotKnocked && C.isPilotKnocked({ inMech: true, mechId: state.mechId, mechStories: state.mechStories })) ? "MASH" : (((state.inTruck && state.vehicleStyle === "tank") || state.inMech || state.inHeli || state.inDrone) ? "FIRE" : "HOP"),
        cd: state.cd,
        walk: (function () {
          if (state.mode === "space") return state.inOrbit ? "🌍 Orbit" : "🚀 Space";
          if (state.inMech) return "🤖 Mech · " + (C.mechStoriesLabel ? C.mechStoriesLabel(state.mechStories).replace(" mech", "") : ((state.mechStories || "?") + "-story"));
          if (state.inSub) return "🛸 Sub · under";
          if (state.inHeli) return (state.zLift || 0) > 0.3 ? "🚁 Heli" : "🚁 Heli · pad";
          if (state.inDrone) return (state.zLift || 0) > 0.3 ? "🛸 Drone" : "🛸 Drone · pad";
          if (state.inSwim) return "🏊 Swim";
          if (!state.inTruck) return "🐸 Walk";
          var wp2 = threeToWorld(state.player.position.x, state.player.position.z);
          var wet2 = C.inPond && C.inPond(wp2.x, wp2.y);
          if (((state.zLift || 0) - (state.groundLift || 0)) > 0.45) return "🚚 AIR!";
          if (wet2 && state.waterSub > 0.75) return "🚚 Under";
          if (wet2) return "🚚 On water";
          return state.truckMode === "shared" ? "🚚 All aboard" : "🚚 Drive";
        })(),
      });
    }
  }


  function ensureTapMarkerEl() {
    var el = document.getElementById("tap-steer-marker");
    if (el) return el;
    el = document.createElement("div");
    el.id = "tap-steer-marker";
    el.setAttribute("aria-hidden", "true");
    el.innerHTML = '<span class="tap-ring"></span><span class="tap-arrow"></span>';
    document.body.appendChild(el);
    return el;
  }

  function showTapMarker(clientX, clientY, ang) {
    var el = ensureTapMarkerEl();
    el.classList.add("is-on");
    el.style.left = clientX + "px";
    el.style.top = clientY + "px";
    el.style.setProperty("--tap-ang", ang + "rad");
    tapMarker = { sx: clientX, sy: clientY, life: 0.55, ang: ang };
  }

  function hideTapMarkerSoon() {
    if (tapMarker) tapMarker.life = Math.min(tapMarker.life, 0.28);
  }

  function updateTapMarkerVisual(dt) {
    var el = document.getElementById("tap-steer-marker");
    if (!tapMarker) {
      if (el) el.classList.remove("is-on");
      return;
    }
    tapMarker.life -= dt;
    if (tapMarker.life <= 0) {
      tapMarker = null;
      if (el) el.classList.remove("is-on");
      return;
    }
    if (el) {
      el.classList.add("is-on");
      el.style.opacity = String(Math.min(1, tapMarker.life * 2.2));
    }
  }

  function canvasLocalPointer(e) {
    if (!renderer || !renderer.domElement) return null;
    var rect = renderer.domElement.getBoundingClientRect();
    return { x: e.clientX - rect.left, y: e.clientY - rect.top, cx: e.clientX, cy: e.clientY, w: rect.width, h: rect.height };
  }

  function applyTapAim(e) {
    if (!active || !state || !state.player || !camera || !renderer) return;
    var loc = canvasLocalPointer(e);
    if (!loc) return;
    var v = new THREE.Vector3(state.player.position.x, 0.6 + (state.zLift || 0) * 0.08, state.player.position.z);
    v.project(camera);
    var sx = (v.x * 0.5 + 0.5) * loc.w;
    var sy = (-v.y * 0.5 + 0.5) * loc.h;
    var dx = loc.x - sx;
    var dy = loc.y - sy;
    var len = Math.hypot(dx, dy);
    if (len < 14) return;
    tapSteer.x = dx / len;
    tapSteer.y = dy / len;
    tapHeld = true;
    showTapMarker(loc.cx, loc.cy, Math.atan2(dy, dx));
  }

  function clearTapAim() {
    tapHeld = false;
    tapSteer.x = 0;
    tapSteer.y = 0;
    hideTapMarkerSoon();
  }

  function wireTapSteer(el) {
    if (!el || el.dataset.ffTapSteer === "1") return;
    el.dataset.ffTapSteer = "1";
    el.addEventListener("pointerdown", function (e) {
      if (!active) return;
      /* joy2: stick is primary on touch; mouse playfield aim optional on desktop */
      if (e.pointerType === "touch") return;
      if (e.button != null && e.button !== 0) return;
      e.preventDefault();
      try { el.setPointerCapture(e.pointerId); } catch (err) {}
      applyTapAim(e);
    });
    el.addEventListener("pointermove", function (e) {
      if (!active || !tapHeld) return;
      if (e.pointerType === "touch") return;
      applyTapAim(e);
    });
    function up(e) {
      if (!tapHeld) return;
      clearTapAim();
    }
    el.addEventListener("pointerup", up);
    el.addEventListener("pointercancel", up);
    el.addEventListener("pointerleave", function (e) {
      if (tapHeld && e.pressure === 0) clearTapAim();
    });
  }

  function boot(opts) {
    destroy();
    hooks = opts || {};
    if (typeof THREE === "undefined") {
      console.error("three.js CDN not loaded");
      if (hooks.onToast) hooks.onToast("three.js failed to load");
      throw new Error("three.js CDN not loaded");
    }
    var host = document.getElementById("engine-host");
    if (!host) {
      console.error("engine-host missing");
      throw new Error("engine-host missing");
    }
    host.hidden = false;
    host.innerHTML = "";
    var view = document.getElementById("view");
    if (view) view.style.display = "none";

    state = {
      frogId: (opts && opts.frogId) || "james",
      seatMap: (opts && opts.seatMap) || null,
      primaryPadIndex: null,
      truckPilotPadIndex: null,
      mechPilotPadIndex: null,
    };

    host.style.display = "block";
    host.removeAttribute("hidden");
    host.hidden = false;
    var hostW = Math.max(host.clientWidth || 0, window.innerWidth || 320);
    var hostH = Math.max(host.clientHeight || 0, window.innerHeight || 480);
    renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false, powerPreference: "high-performance" });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.setSize(hostW, hostH, false);
    renderer.shadowMap.enabled = true;
    renderer.setClearColor(0x7eb8d4, 1);
    host.appendChild(renderer.domElement);
    renderer.domElement.style.display = "block";
    renderer.domElement.style.width = "100%";
    renderer.domElement.style.height = "100%";
    renderer.domElement.style.touchAction = "none";
    renderer.domElement.setAttribute("aria-label", "Four Froggies three.js ranch");
    wireTapSteer(renderer.domElement);

    clock = new THREE.Clock();
    buildRanch();
    active = true;
    window.addEventListener("resize", resize);
    window.addEventListener("orientationchange", function () { setTimeout(resize, 60); });
    if (hooks.onReady) hooks.onReady({ engine: "three", frogId: state.frogId });
    if (hooks.onToast) hooks.onToast(state.toast);
    tick();
  }

  // Cleanup resize on destroy
  var _destroy = destroy;
  destroy = function () {
    window.removeEventListener("resize", resize);
    _destroy();
  };

  function leaveOrbitThree() {
    if (!state || state.mode !== "space" || !state.inOrbit) return false;
    state.inOrbit = false;
    state.orbitEscapeCool = 1.4;
    var kick = ((state.orbitCfg && state.orbitCfg.hardThrustImpulse) || 320) * 0.02;
    state.vx = Math.cos(state.orbitAngle || 0) * kick;
    state.vz = Math.sin(state.orbitAngle || 0) * kick;
    state.toast = "Escape · left " + ((state.planet && state.planet.name) || "planet") + " orbit";
    state.toastT = 2.2;
    return true;
  }

  global.FroggiesThree = {
    boot: boot,
    destroy: destroy,
    setSteer: setSteer,
    pulseInteract: pulseInteract,
    setInteractHeld: setInteractHeld,
    pulseAbility: pulseAbility,
    pulseSpear: pulseSpear,
    setAirControls: function (opts) {
      if (!state) return;
      opts = opts || {};
      if (opts.climb != null) {
        state.climbIn = opts.climb;
        state._climbKeyHeld = opts.climb !== 0;
      }
      if (opts.boost != null) state.airBoost = !!opts.boost;
    },
    adjustZoom: function (delta) {
      /* positive delta = zoom IN (closer); negative = zoom OUT — match canvas wheel */
      userZoom = Math.max(0.55, Math.min(1.45, (userZoom || 1) - (delta || 0) * 2.2));
      return userZoom;
    },
    getUserZoom: function () { return userZoom || 1; },
    isActive: isActive,
    leaveOrbit: leaveOrbitThree,
  };
})(typeof window !== "undefined" ? window : globalThis);
