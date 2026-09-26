/* Local heuristic AI for companion froggies — no network / paid APIs */
(function (global) {
  "use strict";

  function desiredLane(frog, hazards, invaders, lanes) {
    const threats = [];
    for (const h of hazards) {
      if (h.x > frog.x - 20 && h.x < frog.x + 220) threats.push(h);
    }
    for (const inv of invaders) {
      if (inv.x > frog.x - 20 && inv.x < frog.x + 260) threats.push(inv);
    }
    let best = frog.lane;
    let bestScore = -1e9;
    for (let lane = 0; lane < lanes; lane++) {
      let score = 0;
      // Prefer staying near current lane a little (less jitter)
      score -= Math.abs(lane - frog.lane) * 4;
      // Prefer center lanes slightly
      score -= Math.abs(lane - (lanes - 1) / 2) * 1.5;
      for (const t of threats) {
        if (t.lane === lane) {
          const dist = Math.max(20, t.x - frog.x);
          score -= 400 / dist;
        }
      }
      if (score > bestScore) {
        bestScore = score;
        best = lane;
      }
    }
    return best;
  }

  function shouldUseAbility(frog, state) {
    if (frog.cd > 0 || frog.human) return false;
    const nearThreat = state.hazards.some(
      (h) => h.x > frog.x - 10 && h.x < frog.x + 200 && Math.abs(h.lane - frog.lane) <= 1
    );
    const nearInv = state.invaders.some(
      (inv) => inv.x > frog.x - 10 && inv.x < frog.x + 280
    );
    const convoyHurtSoon = state.lives <= 2;

    switch (frog.id) {
      case "james":
        return nearThreat && frog.lane !== desiredLane(frog, state.hazards, state.invaders, state.laneCount);
      case "jimmy":
        return (nearThreat || nearInv) && (convoyHurtSoon || Math.random() < 0.35);
      case "bubbles":
        return nearInv;
      case "rexy":
        return nearThreat || nearInv;
      default:
        return false;
    }
  }

  function tickAI(frogs, state, dt) {
    for (const frog of frogs) {
      if (frog.human) continue;
      frog.aiTimer = (frog.aiTimer || 0) - dt;
      if (frog.aiTimer <= 0) {
        frog.aiTimer = 0.18 + Math.random() * 0.22;
        frog.targetLane = desiredLane(frog, state.hazards, state.invaders, state.laneCount);
      }
      if (frog.lane !== frog.targetLane && !frog.dashing) {
        // Smooth lane move via steer intent
        frog.steer = frog.targetLane > frog.lane ? 1 : -1;
      } else if (!frog.dashing) {
        frog.steer = 0;
      }
      if (shouldUseAbility(frog, state)) {
        state.requestAbility(frog);
      }
    }
  }

  global.FroggiesAI = { tickAI, desiredLane };
})(typeof window !== "undefined" ? window : globalThis);
