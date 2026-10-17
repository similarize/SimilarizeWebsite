#!/usr/bin/env python3
"""Generate /videos/<slug>/index.html for every entry in videos.json from _watch-template.html.
Run after editing videos.json:  python3 videos/_add-video.py   (from the repo root)."""
import json, html, os
here = os.path.dirname(os.path.abspath(__file__))
tpl = open(os.path.join(here, "_watch-template.html"), encoding="utf-8").read()
data = json.load(open(os.path.join(here, "videos.json"), encoding="utf-8"))
for v in data["videos"]:
    summary = v.get("description", "").split("\n")[0]
    out = (tpl.replace("{{TITLE}}", html.escape(v["title"]))
              .replace("{{SUMMARY}}", html.escape(summary))
              .replace("{{SLUG}}", v["slug"]))
    d = os.path.join(here, v["slug"]); os.makedirs(d, exist_ok=True)
    open(os.path.join(d, "index.html"), "w", encoding="utf-8").write(out)
    print("wrote", os.path.relpath(os.path.join(d, "index.html"), os.path.dirname(here)))
