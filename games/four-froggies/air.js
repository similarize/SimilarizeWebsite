/* air1: shared helipad / helicopter + passenger-drone pad / drone for Canvas + Three.
   Board/exit uses the same INTERACT path as trucks/mechs/sub. Flight: WASD + climb on
   ability/hop or R, descend on C (F stays INTERACT), Shift boost. Landed+slow → EXIT. */
(function (global) {
  "use strict";

  function C() { return global.FroggiesCanon; }

  function ensureCraft(world, kind) {
    if (!world) return null;
    if (!world.air) world.air = {};
    var key = kind === "drone" ? "drone" : "heli";
    if (world.air[key]) return world.air[key];
    var Ca = C();
    var pad = key === "drone"
      ? (Ca && Ca.DRONE_PAD) || { x: 1180, y: 2000 }
      : (Ca && Ca.HELI_PAD) || { x: 980, y: 2000 };
    var stats = (Ca && Ca.airStats) ? Ca.airStats(key) : { seats: key === "drone" ? 2 : 4 };
    var seats = [];
    var n = stats.seats || (key === "drone" ? 2 : 4);
    for (var i = 0; i < n; i++) seats.push(null);
    var park = (Ca && Ca.vehiclePos) ? Ca.vehiclePos(key, pad.x, pad.y) : pad;
    world.air[key] = {
      kind: key,
      x: park.x,
      y: park.y,
      z: 0,
      vx: 0,
      vy: 0,
      vz: 0,
      faceAngle: -Math.PI / 2,
      seats: seats,
      pilotId: null,
      rotor: 0,
      landed: true,
    };
    return world.air[key];
  }

  function craftOf(world, kind) {
    return ensureCraft(world, kind);
  }

  function frogAirKind(frog) {
    if (!frog) return null;
    if (frog.inHeli) return "heli";
    if (frog.inDrone) return "drone";
    return null;
  }

  function isAirborneFrog(frog) {
    return !!(frog && (frog.inHeli || frog.inDrone));
  }

  function seatCount(craft) {
    return craft && craft.seats ? craft.seats.length : 0;
  }

  function occupiedCount(craft) {
    if (!craft || !craft.seats) return 0;
    var n = 0;
    for (var i = 0; i < craft.seats.length; i++) if (craft.seats[i]) n++;
    return n;
  }

  function findSeatOf(craft, frogId) {
    if (!craft || !craft.seats) return -1;
    for (var i = 0; i < craft.seats.length; i++) {
      if (craft.seats[i] === frogId) return i;
    }
    return -1;
  }

  function firstFreeSeat(craft) {
    if (!craft || !craft.seats) return -1;
    for (var i = 0; i < craft.seats.length; i++) {
      if (!craft.seats[i]) return i;
    }
    return -1;
  }

  function clearSeat(craft, frogId) {
    if (!craft || !craft.seats) return;
    for (var i = 0; i < craft.seats.length; i++) {
      if (craft.seats[i] === frogId) craft.seats[i] = null;
    }
    if (craft.pilotId === frogId) {
      craft.pilotId = null;
      for (var j = 0; j < craft.seats.length; j++) {
        if (craft.seats[j]) { craft.pilotId = craft.seats[j]; break; }
      }
    }
  }

  function syncPassengers(world, frogs, craft) {
    if (!craft || !frogs) return;
    for (var i = 0; i < frogs.length; i++) {
      var f = frogs[i];
      if (!f) continue;
      var kind = frogAirKind(f);
      if (!kind || kind !== craft.kind) continue;
      if (findSeatOf(craft, f.id) < 0) continue;
      f.x = craft.x;
      f.y = craft.y;
      f.z = craft.z;
      f.vx = craft.vx;
      f.vy = craft.vy;
      f.zVel = craft.vz;
      f.faceAngle = craft.faceAngle;
      f.groundZ = 0;
    }
  }

  function leaveGroundVehicles(frog) {
    if (!frog) return;
    frog.inTruck = false;
    frog.truckMode = null;
    frog.truckId = null;
    frog.vehicleStyle = null;
    frog.inMech = false;
    frog.mechId = null;
    frog.mechStories = 0;
    frog.inSub = false;
    frog.subId = null;
    frog.inSwim = false;
  }

  /** Board or exit. Returns { ok, boarded, exited, toast, denied }. */
  function boardAir(world, frogs, frog, hotspot) {
    var Ca = C();
    if (!frog) return { ok: false };
    var kind = null;
    if (frog.inHeli) kind = "heli";
    else if (frog.inDrone) kind = "drone";
    else if (hotspot) {
      if (Ca && Ca.airKindOf) kind = Ca.airKindOf(hotspot);
      else if (hotspot.kind === "heli" || hotspot.kind === "drone") kind = hotspot.kind;
    }
    if (!kind) return { ok: false };

    var craft = ensureCraft(world, kind);
    var stats = (Ca && Ca.airStats) ? Ca.airStats(kind) : { landZ: 14, landSp: 55 };

    /* EXIT while aboard — only when landed + slow */
    if (frogAirKind(frog) === kind) {
      var spd = Math.hypot(craft.vx || 0, craft.vy || 0);
      var z = craft.z || 0;
      var canExit = z <= (stats.landZ || 14) + 2 && spd <= (stats.landSp || 55) + 10;
      if (!canExit) {
        return {
          ok: false,
          denied: true,
          toast: "Land + slow · then INTERACT to hop out",
        };
      }
      var wasSeat = findSeatOf(craft, frog.id);
      clearSeat(craft, frog.id);
      frog.inHeli = false;
      frog.inDrone = false;
      frog.airKind = null;
      frog.airSeat = null;
      frog.vehicleStyle = null;
      frog.z = 0;
      frog.zVel = 0;
      frog.groundZ = 0;
      /* Exit beside skids / rotors */
      var side = (wasSeat % 2 === 0) ? 1 : -1;
      frog.x = craft.x + side * 28;
      frog.y = craft.y + 18;
      frog.vx = 0;
      frog.vy = 0;
      if (occupiedCount(craft) === 0) {
        craft.vx = craft.vy = craft.vz = 0;
        craft.z = 0;
        craft.landed = true;
        craft.x = frog.x - side * 28;
        craft.y = frog.y - 18;
        if (Ca && Ca.setVehiclePark) Ca.setVehiclePark(kind, craft.x, craft.y);
        if (world && world.hotspots) {
          for (var hi = 0; hi < world.hotspots.length; hi++) {
            var hs = world.hotspots[hi];
            if (hs && (hs.id === kind || hs.kind === kind)) {
              hs.x = craft.x; hs.y = craft.y;
            }
          }
        }
      }
      syncPassengers(world, frogs, craft);
      return {
        ok: true,
        exited: true,
        toast: kind === "drone" ? "Drone parked · walking" : "Heli parked · walking",
      };
    }

    /* BOARD */
    if (frog.inHeli || frog.inDrone || frog.inTruck || frog.inMech || frog.inSub) {
      return { ok: false, denied: true, toast: "Exit current ride first" };
    }
    /* Must be near craft (not an empty pad while craft is away) */
    var nearCraft = Math.hypot((frog.x || 0) - craft.x, (frog.y || 0) - craft.y) < 70;
    if (!nearCraft) {
      return { ok: false, denied: true, toast: "Get closer to the " + (kind === "drone" ? "drone" : "heli") };
    }
    if (!craft.landed && (craft.z || 0) > 24) {
      return { ok: false, denied: true, toast: "Wait for landing" };
    }
    var seat = firstFreeSeat(craft);
    if (seat < 0) {
      return {
        ok: false,
        denied: true,
        toast: kind === "drone" ? "Drone full · 2 seats" : "Heli full · 4 seats",
      };
    }
    leaveGroundVehicles(frog);
    craft.seats[seat] = frog.id;
    if (!craft.pilotId) craft.pilotId = frog.id;
    frog.x = craft.x;
    frog.y = craft.y;
    frog.z = craft.z || 0;
    frog.vx = 0;
    frog.vy = 0;
    frog.zVel = 0;
    frog.groundZ = 0;
    frog.inHeli = kind === "heli";
    frog.inDrone = kind === "drone";
    frog.airKind = kind;
    frog.airSeat = seat;
    frog.vehicleStyle = kind;
    craft.landed = (craft.z || 0) < 4;
    return {
      ok: true,
      boarded: true,
      toast: seat === 0 || craft.pilotId === frog.id
        ? (kind === "drone"
          ? "Passenger drone · pilot · WASD fly · Space/R climb · C descend · Shift boost"
          : "Helicopter · pilot · WASD fly · Space/R climb · C descend · Shift boost")
        : (kind === "drone"
          ? "Drone rider · seat " + (seat + 1)
          : "Heli passenger · seat " + (seat + 1)),
    };
  }

  function isPilot(craft, frog) {
    return !!(craft && frog && craft.pilotId === frog.id);
  }

  /**
   * Tick flight for the craft that frog pilots. Non-pilots are synced only.
   * climb: +1 up / -1 down / 0 coast. boost: boolean.
   */
  function tickFlight(world, frogs, frog, dt, steerX, steerY, climb, boost) {
    var kind = frogAirKind(frog);
    if (!kind) return null;
    var craft = ensureCraft(world, kind);
    if (!craft) return null;
    var Ca = C();
    var stats = (Ca && Ca.airStats) ? Ca.airStats(kind) : {
      maxSp: 380, accel: 1000, fric: 4, climb: 220, descend: 180, ceiling: 400, boost: 1.4, landZ: 14, landSp: 55
    };

    if (!isPilot(craft, frog)) {
      syncPassengers(world, frogs, craft);
      return craft;
    }

    var mx = steerX || 0;
    var my = steerY || 0;
    /* Screen steer → world already applied by caller when needed */
    var mag = Math.hypot(mx, my);
    if (mag > 1) { mx /= mag; my /= mag; }

    var maxSp = (stats.maxSp || 380) * (boost ? (stats.boost || 1.45) : 1);
    var accel = stats.accel || 1000;
    var friction = stats.fric || 4;

    if (mag > 0.05) {
      craft.vx += mx * accel * dt;
      craft.vy += my * accel * dt;
      var aim = Math.atan2(my, mx);
      var cur = craft.faceAngle != null ? craft.faceAngle : aim;
      var da = aim - cur;
      while (da > Math.PI) da -= Math.PI * 2;
      while (da < -Math.PI) da += Math.PI * 2;
      craft.faceAngle = cur + Math.max(-3.5 * dt, Math.min(3.5 * dt, da));
    }
    craft.vx *= Math.exp(-friction * dt);
    craft.vy *= Math.exp(-friction * dt);
    var sp = Math.hypot(craft.vx, craft.vy);
    if (sp > maxSp) {
      craft.vx *= maxSp / sp;
      craft.vy *= maxSp / sp;
    }

    var climbIn = climb || 0;
    if (climbIn > 0.05) craft.vz += (stats.climb || 220) * climbIn * dt;
    else if (climbIn < -0.05) craft.vz -= (stats.descend || 180) * (-climbIn) * dt;
    else craft.vz -= 90 * dt; /* gentle auto settle */
    craft.vz *= Math.exp(-2.8 * dt);

    craft.x += craft.vx * dt;
    craft.y += craft.vy * dt;
    craft.z = Math.max(0, (craft.z || 0) + craft.vz * dt);
    var ceil = stats.ceiling || 400;
    if (craft.z > ceil) { craft.z = ceil; if (craft.vz > 0) craft.vz = 0; }

    var mapW = (Ca && Ca.MAP_W) || 5600;
    var mapH = (Ca && Ca.MAP_H) || 4200;
    if (craft.x < 40) craft.x = 40;
    if (craft.y < 40) craft.y = 40;
    if (craft.x > mapW - 40) craft.x = mapW - 40;
    if (craft.y > mapH - 40) craft.y = mapH - 40;

    var landZ = stats.landZ || 14;
    var landSp = stats.landSp || 55;
    var spd2 = Math.hypot(craft.vx, craft.vy);
    if (climbIn <= 0.05 && craft.z <= landZ && spd2 <= landSp && craft.vz <= 40) {
      craft.z = Math.max(0, craft.z * 0.85);
      if (craft.z < 3) {
        craft.z = 0;
        craft.vz = 0;
        craft.landed = true;
      }
    } else {
      craft.landed = false;
    }

    craft.rotor = (craft.rotor || 0) + dt * (8 + spd2 * 0.02 + Math.max(0, craft.z) * 0.01);
    syncPassengers(world, frogs, craft);

    /* Hotspot follows craft while occupied (passenger board at skids); park when empty */
    if (Ca && Ca.setVehiclePark) {
      Ca.setVehiclePark(kind, craft.x, craft.y);
    }
    if (world && world.hotspots) {
      for (var hi = 0; hi < world.hotspots.length; hi++) {
        var hs = world.hotspots[hi];
        if (hs && (hs.id === kind || hs.kind === kind)) {
          hs.x = craft.x; hs.y = craft.y;
        }
      }
    }
    return craft;
  }

  function canExitNow(craft, kind) {
    if (!craft) return false;
    var Ca = C();
    var stats = (Ca && Ca.airStats) ? Ca.airStats(kind || craft.kind) : { landZ: 14, landSp: 55 };
    var spd = Math.hypot(craft.vx || 0, craft.vy || 0);
    return (craft.z || 0) <= (stats.landZ || 14) + 2 && spd <= (stats.landSp || 55) + 10;
  }

  function flyingTip(craft, kind) {
    if (canExitNow(craft, kind)) return "Landed · INTERACT / E to hop out";
    return "Flying · land + INTERACT to hop out · Space/R climb · C descend · Shift boost";
  }

  function nearPadTip(kind) {
    if (kind === "drone") return "Walk · Drone · INTERACT / E board";
    return "Walk · Heli · INTERACT / E board";
  }

  function resetAir(world) {
    if (!world) return;
    world.air = null;
    ensureCraft(world, "heli");
    ensureCraft(world, "drone");
  }

  global.FroggiesAir = {
    ensureCraft: ensureCraft,
    craftOf: craftOf,
    frogAirKind: frogAirKind,
    isAirborneFrog: isAirborneFrog,
    seatCount: seatCount,
    occupiedCount: occupiedCount,
    findSeatOf: findSeatOf,
    boardAir: boardAir,
    isPilot: isPilot,
    tickFlight: tickFlight,
    canExitNow: canExitNow,
    flyingTip: flyingTip,
    nearPadTip: nearPadTip,
    syncPassengers: syncPassengers,
    resetAir: resetAir,
  };
})(typeof window !== "undefined" ? window : globalThis);
