# Pasión River Model: Hark's Version (12 episodes), handoff to the Grok Bot

*From Hark Bot, 2026-10-18. Requested by Bill.*

## What this is

A separate 12-part documentary series about the Pasión River Model, **made by Hark** (narration by Grok voice "orion"). It is meant to sit on **bookofmormon.similarize.com** next to the Grok-made episodes, **clearly labelled as Hark's Version**.

Hark can't upload to media.similarize.com, so the media is on Google Drive below. Everything is already packaged in the same HLS ladder the site uses for `four-froggies-come-home-v4`. Nothing live was changed in this repo: no edits to `videos/videos.json`, index pages or watch pages. This commit only adds `bookofmormon/HARK_VERSION_HANDOFF.md` and `bookofmormon/hark_entries.json`.

## What we're asking the Grok Bot to do

1. Add Hark's set to bookofmormon.similarize.com as a **separate series, `series-2`, titled "The Pasión River Model — Hark's Version"**, alongside `series-1` (Grok), and include it under "All series". Proposed paths (also in each entry as `page_path` / `media_path`):
   - pages: `/series-2/episode-NN-<slug>/`
   - media: `/media/series-2/episode-NN-<slug>/` (master.m3u8, 720p/, 480p/, 360p/, poster.jpg, thumb.jpg)
   - Each Drive zip's top folder keeps its media folder name (`media_folder`, e.g. `hark-pasion-river-01-the-river-sidon/`); copy its contents to the matching `media_path`. The mapping table is below.
2. Download the 12 zips from the Drive folder, unzip each one, and host it at its `media_path` above (or at `https://media.similarize.com/<slug>/` if you serve HLS from there). Each zip's top folder is the slug, holding `master.m3u8`, `720p/`, `480p/`, `360p/`, `poster.jpg` and `thumb.jpg`. The source MP4 is in each zip too, for archiving; it doesn't need to be served.
3. List all 12 episodes in series-2, using the entries in `bookofmormon/hark_entries.json` (same fields as `videos/videos.json`). If you also want watch pages under `/videos/`, add the entries to `videos.json` and run `videos/_add-video.py`.
4. Keep the labelling requirement below.

## Labelling requirement (please keep)

- Show the Hark episodes under their **own heading or note that says "Hark's Version"**, kept **separate from the Grok-made episodes**. Don't interleave the two sets.
- Every entry has `"badge": "Hark's Version"`, and every title ends in "(Hark's Version)". Please keep both.
- Show this series note above the Hark set:

  > Hark's Version: a separate 12-part series made by Hark (narration by Grok voice 'orion'), alongside the Grok-made episodes.

- Every identification is the **Pasión River Model's proposal by Robert C. Roylance and Dr. Richard E. Terry, not Church doctrine**. The descriptions are already written that way, so please don't reword them into statements of fact.

## Media on Google Drive

Folder (anyone with the link can view): **https://drive.google.com/drive/folders/1czFrLEx3rBGSyQxg4W_CFsVrqtsMudL1**

| Ep | Zip on Drive | Size | Contents |
|---|---|---|---|
| 1 | [hark-pasion-river-01-the-river-sidon.zip](https://drive.google.com/file/d/1QRvpuN93ZhgwKwuHtS9w4XuZhT2pqigb/view?usp=drivesdk) | 435 MB | `hark-pasion-river-01-the-river-sidon/` (master.m3u8, 720p/, 480p/, 360p/, poster.jpg, thumb.jpg) + `Ep01_The_River_Sidon_v2.mp4` |
| 2 | [hark-pasion-river-02-zarahemla-rising.zip](https://drive.google.com/file/d/1luaOeOCw1aFd0mL4AeGEU8ddeIpDetiC/view?usp=drivesdk) | 448 MB | `hark-pasion-river-02-zarahemla-rising/` (master.m3u8, 720p/, 480p/, 360p/, poster.jpg, thumb.jpg) + `Ep02_Zarahemla_Rising_v1.mp4` |
| 3 | [hark-pasion-river-03-manti-and-the-hill-riplah-ambush.zip](https://drive.google.com/file/d/1W4n9UtDVA_sdwwo_1QhUxiQShaAcSqOL/view?usp=drivesdk) | 442 MB | `hark-pasion-river-03-manti-and-the-hill-riplah-ambush/` (master.m3u8, 720p/, 480p/, 360p/, poster.jpg, thumb.jpg) + `Ep03_Manti_and_the_Hill_Riplah_Ambush_v1.mp4` |
| 4 | [hark-pasion-river-04-the-land-of-nephi.zip](https://drive.google.com/file/d/1MFiZhRNjOwTPMWC2LwY73bbdrQTyfB9z/view?usp=drivesdk) | 449 MB | `hark-pasion-river-04-the-land-of-nephi/` (master.m3u8, 720p/, 480p/, 360p/, poster.jpg, thumb.jpg) + `Ep04_The_Land_of_Nephi_v1.mp4` |
| 5 | [hark-pasion-river-05-the-wall-in-the-jungle.zip](https://drive.google.com/file/d/1enO_qYbId4mD_nqK17Ufjjqb7ovKCUmw/view?usp=drivesdk) | 450 MB | `hark-pasion-river-05-the-wall-in-the-jungle/` (master.m3u8, 720p/, 480p/, 360p/, poster.jpg, thumb.jpg) + `Ep05_The_Wall_in_the_Jungle_v1.mp4` |
| 6 | [hark-pasion-river-06-three-days-of-darkness.zip](https://drive.google.com/file/d/1UdrAY2q3JyeHd5lXexkjlRJbmvAR7E8b/view?usp=drivesdk) | 384 MB | `hark-pasion-river-06-three-days-of-darkness/` (master.m3u8, 720p/, 480p/, 360p/, poster.jpg, thumb.jpg) + `Ep06_Three_Days_of_Darkness_v1.mp4` |
| 7 | [hark-pasion-river-07-bountiful.zip](https://drive.google.com/file/d/1Qd5m37NDH7NQhoXzv_fgC1gXbo7mw5X6/view?usp=drivesdk) | 337 MB | `hark-pasion-river-07-bountiful/` (master.m3u8, 720p/, 480p/, 360p/, poster.jpg, thumb.jpg) + `Ep07_Bountiful_v1.mp4` |
| 8 | [hark-pasion-river-08-kings-of-the-war-years.zip](https://drive.google.com/file/d/1XBK0n740GdIGM75JQv77lYPy8H2PBfzW/view?usp=drivesdk) | 654 MB | `hark-pasion-river-08-kings-of-the-war-years/` (master.m3u8, 720p/, 480p/, 360p/, poster.jpg, thumb.jpg) + `Ep08_Kings_of_the_War_Years_v1.mp4` |
| 9 | [hark-pasion-river-09-the-final-nephite-wars.zip](https://drive.google.com/file/d/1I2egjBw51JjTpXGcnKWj1RJ7FgIrYZqR/view?usp=drivesdk) | 645 MB | `hark-pasion-river-09-the-final-nephite-wars/` (master.m3u8, 720p/, 480p/, 360p/, poster.jpg, thumb.jpg) + `Ep09_The_Final_Nephite_Wars_v2.mp4` |
| 10 | [hark-pasion-river-10-cumorah.zip](https://drive.google.com/file/d/1f851VYimjs14053V5v3scuVonaX3fjjd/view?usp=drivesdk) | 441 MB | `hark-pasion-river-10-cumorah/` (master.m3u8, 720p/, 480p/, 360p/, poster.jpg, thumb.jpg) + `Ep10_Cumorah_v1.mp4` |
| 11 | [hark-pasion-river-11-the-jaredites-barges-to-belize.zip](https://drive.google.com/file/d/19LJ4bc4ozDChaqQjTN3zflR1hu6ypseG/view?usp=drivesdk) | 397 MB | `hark-pasion-river-11-the-jaredites-barges-to-belize/` (master.m3u8, 720p/, 480p/, 360p/, poster.jpg, thumb.jpg) + `Ep11_The_Jaredites_Barges_to_Belize_v1.mp4` |
| 12 | [hark-pasion-river-12-shiz-and-coriantumr.zip](https://drive.google.com/file/d/1whq7hNKlpS606O_g2C8SEe-LuJ9GpzDc/view?usp=drivesdk) | 482 MB | `hark-pasion-river-12-shiz-and-coriantumr/` (master.m3u8, 720p/, 480p/, 360p/, poster.jpg, thumb.jpg) + `Ep12_Shiz_and_Coriantumr_v1.mp4` |

## HLS ladder (matches four-froggies-come-home-v4)

- `master.m3u8` (EXT-X-VERSION 6) → `720p/index.m3u8`, `480p/index.m3u8`, `360p/index.m3u8`
- 720p: 1280x720, H.264 Main `avc1.4d401f`, about 2.8 Mbps video plus 128k AAC
- 480p: 854x480, `avc1.4d401e`, about 1.4 Mbps plus 96k AAC
- 360p: 640x360, `avc1.4d401e`, about 0.8 Mbps plus 96k AAC
- AAC-LC 48 kHz stereo (`mp4a.40.2`), MPEG-TS segments `seg_NNN.ts`, 6 s segments, VOD playlists with INDEPENDENT-SEGMENTS. Source is 30 fps (the froggies were 24 fps), with a keyframe every 6 s.
- Each master's BANDWIDTH and AVERAGE-BANDWIDTH come from the actual segments. Every master was read end to end with ffmpeg/ffprobe, and its duration matches the source MP4 to within 0.5 s.

## Proposed paths, media folder mapping and durations

| Ep | Page path | Media path | Media folder in zip | Duration (s) | Runtime | poster_at (s) |
|---|---|---|---|---|---|---|
| 1 | `/series-2/episode-01-the-river-sidon/` | `/media/series-2/episode-01-the-river-sidon/` | `hark-pasion-river-01-the-river-sidon/` | 419 | 6:59 | 27 |
| 2 | `/series-2/episode-02-zarahemla-rising/` | `/media/series-2/episode-02-zarahemla-rising/` | `hark-pasion-river-02-zarahemla-rising/` | 459 | 7:39 | 27 |
| 3 | `/series-2/episode-03-manti-and-the-hill-riplah-ambush/` | `/media/series-2/episode-03-manti-and-the-hill-riplah-ambush/` | `hark-pasion-river-03-manti-and-the-hill-riplah-ambush/` | 384 | 6:24 | 25 |
| 4 | `/series-2/episode-04-the-land-of-nephi/` | `/media/series-2/episode-04-the-land-of-nephi/` | `hark-pasion-river-04-the-land-of-nephi/` | 428 | 7:08 | 24 |
| 5 | `/series-2/episode-05-the-wall-in-the-jungle/` | `/media/series-2/episode-05-the-wall-in-the-jungle/` | `hark-pasion-river-05-the-wall-in-the-jungle/` | 447 | 7:27 | 27 |
| 6 | `/series-2/episode-06-three-days-of-darkness/` | `/media/series-2/episode-06-three-days-of-darkness/` | `hark-pasion-river-06-three-days-of-darkness/` | 387 | 6:27 | 25 |
| 7 | `/series-2/episode-07-bountiful/` | `/media/series-2/episode-07-bountiful/` | `hark-pasion-river-07-bountiful/` | 324 | 5:24 | 23 |
| 8 | `/series-2/episode-08-kings-of-the-war-years/` | `/media/series-2/episode-08-kings-of-the-war-years/` | `hark-pasion-river-08-kings-of-the-war-years/` | 568 | 9:28 | 31 |
| 9 | `/series-2/episode-09-the-final-nephite-wars/` | `/media/series-2/episode-09-the-final-nephite-wars/` | `hark-pasion-river-09-the-final-nephite-wars/` | 578 | 9:38 | 29 |
| 10 | `/series-2/episode-10-cumorah/` | `/media/series-2/episode-10-cumorah/` | `hark-pasion-river-10-cumorah/` | 430 | 7:10 | 27 |
| 11 | `/series-2/episode-11-the-jaredites-barges-to-belize/` | `/media/series-2/episode-11-the-jaredites-barges-to-belize/` | `hark-pasion-river-11-the-jaredites-barges-to-belize/` | 398 | 6:38 | 25 |
| 12 | `/series-2/episode-12-shiz-and-coriantumr/` | `/media/series-2/episode-12-shiz-and-coriantumr/` | `hark-pasion-river-12-shiz-and-coriantumr/` | 455 | 7:35 | 25 |

## Entries JSON

Also committed as `bookofmormon/hark_entries.json`:

```json
{
  "series": "Pasión River Model (Hark's Version)",
  "series_note": "Hark's Version: a separate 12-part series made by Hark (narration by Grok voice 'orion'), alongside the Grok-made episodes.",
  "media_base": "https://media.similarize.com",
  "videos": [
    {
      "slug": "hark-pasion-river-01-the-river-sidon",
      "title": "Pasión River Model, Episode 1: The River Sidon (Hark's Version)",
      "badge": "Hark's Version",
      "description": "In the Pasión River Model of Robert C. Roylance and Dr. Richard E. Terry, the River Sidon is Guatemala's Pasión River, with Ceibal as Zarahemla and Cancuén as Manti. We walk through why they think the river fits, and where their case is still an inference, not Church doctrine.",
      "duration": 419,
      "audience": "Adults & families",
      "published": "2026-10-18",
      "status": "published",
      "hls": "hark-pasion-river-01-the-river-sidon/master.m3u8",
      "poster": "hark-pasion-river-01-the-river-sidon/poster.jpg",
      "thumb": "hark-pasion-river-01-the-river-sidon/thumb.jpg",
      "poster_at": 27,
      "related": [
        {
          "slug": "hark-pasion-river-02-zarahemla-rising",
          "label": "Next: Episode 2, Zarahemla Rising"
        }
      ],
      "episode": 1,
      "page_path": "/series-2/episode-01-the-river-sidon/",
      "media_path": "/media/series-2/episode-01-the-river-sidon/",
      "media_folder": "hark-pasion-river-01-the-river-sidon"
    },
    {
      "slug": "hark-pasion-river-02-zarahemla-rising",
      "title": "Pasión River Model, Episode 2: Zarahemla Rising (Hark's Version)",
      "badge": "Hark's Version",
      "description": "The Pasión River Model of Robert C. Roylance and Dr. Richard E. Terry proposes that Ceibal, on a bluff above the Pasión, was Zarahemla. Here's how the Mulekites, Mosiah and the city's rise fit their reading, and why the city's decline date is still an open question.",
      "duration": 459,
      "audience": "Adults & families",
      "published": "2026-10-18",
      "status": "published",
      "hls": "hark-pasion-river-02-zarahemla-rising/master.m3u8",
      "poster": "hark-pasion-river-02-zarahemla-rising/poster.jpg",
      "thumb": "hark-pasion-river-02-zarahemla-rising/thumb.jpg",
      "poster_at": 27,
      "related": [
        {
          "slug": "hark-pasion-river-01-the-river-sidon",
          "label": "Previous: Episode 1, The River Sidon"
        },
        {
          "slug": "hark-pasion-river-03-manti-and-the-hill-riplah-ambush",
          "label": "Next: Episode 3, Manti and the Hill Riplah Ambush"
        }
      ],
      "episode": 2,
      "page_path": "/series-2/episode-02-zarahemla-rising/",
      "media_path": "/media/series-2/episode-02-zarahemla-rising/",
      "media_folder": "hark-pasion-river-02-zarahemla-rising"
    },
    {
      "slug": "hark-pasion-river-03-manti-and-the-hill-riplah-ambush",
      "title": "Pasión River Model, Episode 3: Manti and the Hill Riplah Ambush (Hark's Version)",
      "badge": "Hark's Version",
      "description": "Roylance and Terry propose Cancuén as the city of Manti and a hill called Chabilchoch as Hill Riplah, where Moroni sets his ambush in Alma 43. It's a geography-first case for the Pasión River Model, offered as a proposal rather than Church doctrine.",
      "duration": 384,
      "audience": "Adults & families",
      "published": "2026-10-18",
      "status": "published",
      "hls": "hark-pasion-river-03-manti-and-the-hill-riplah-ambush/master.m3u8",
      "poster": "hark-pasion-river-03-manti-and-the-hill-riplah-ambush/poster.jpg",
      "thumb": "hark-pasion-river-03-manti-and-the-hill-riplah-ambush/thumb.jpg",
      "poster_at": 25,
      "related": [
        {
          "slug": "hark-pasion-river-02-zarahemla-rising",
          "label": "Previous: Episode 2, Zarahemla Rising"
        },
        {
          "slug": "hark-pasion-river-04-the-land-of-nephi",
          "label": "Next: Episode 4, The Land of Nephi"
        }
      ],
      "episode": 3,
      "page_path": "/series-2/episode-03-manti-and-the-hill-riplah-ambush/",
      "media_path": "/media/series-2/episode-03-manti-and-the-hill-riplah-ambush/",
      "media_folder": "hark-pasion-river-03-manti-and-the-hill-riplah-ambush"
    },
    {
      "slug": "hark-pasion-river-04-the-land-of-nephi",
      "title": "Pasión River Model, Episode 4: The Land of Nephi (Hark's Version)",
      "badge": "Hark's Version",
      "description": "The Pasión River Model, by Robert C. Roylance and Dr. Richard E. Terry, places the Land of Nephi in Guatemala's Salamá Valley and Lehi's landing near Izapa. We follow those proposed places on the map and note where the book's own drafts disagree.",
      "duration": 428,
      "audience": "Adults & families",
      "published": "2026-10-18",
      "status": "published",
      "hls": "hark-pasion-river-04-the-land-of-nephi/master.m3u8",
      "poster": "hark-pasion-river-04-the-land-of-nephi/poster.jpg",
      "thumb": "hark-pasion-river-04-the-land-of-nephi/thumb.jpg",
      "poster_at": 24,
      "related": [
        {
          "slug": "hark-pasion-river-03-manti-and-the-hill-riplah-ambush",
          "label": "Previous: Episode 3, Manti and the Hill Riplah Ambush"
        },
        {
          "slug": "hark-pasion-river-05-the-wall-in-the-jungle",
          "label": "Next: Episode 5, The Wall in the Jungle"
        }
      ],
      "episode": 4,
      "page_path": "/series-2/episode-04-the-land-of-nephi/",
      "media_path": "/media/series-2/episode-04-the-land-of-nephi/",
      "media_folder": "hark-pasion-river-04-the-land-of-nephi"
    },
    {
      "slug": "hark-pasion-river-05-the-wall-in-the-jungle",
      "title": "Pasión River Model, Episode 5: The Wall in the Jungle (Hark's Version)",
      "badge": "Hark's Version",
      "description": "For the narrow neck of land, the Pasión River Model of Robert C. Roylance and Dr. Richard E. Terry points to the great earthwork between Tikal and Uaxactún. We look at that idea honestly, including a 2007 study that dates the wall to around A.D. 400 to 550 and doubts it was ever defensive.",
      "duration": 447,
      "audience": "Adults & families",
      "published": "2026-10-18",
      "status": "published",
      "hls": "hark-pasion-river-05-the-wall-in-the-jungle/master.m3u8",
      "poster": "hark-pasion-river-05-the-wall-in-the-jungle/poster.jpg",
      "thumb": "hark-pasion-river-05-the-wall-in-the-jungle/thumb.jpg",
      "poster_at": 27,
      "related": [
        {
          "slug": "hark-pasion-river-04-the-land-of-nephi",
          "label": "Previous: Episode 4, The Land of Nephi"
        },
        {
          "slug": "hark-pasion-river-06-three-days-of-darkness",
          "label": "Next: Episode 6, Three Days of Darkness"
        }
      ],
      "episode": 5,
      "page_path": "/series-2/episode-05-the-wall-in-the-jungle/",
      "media_path": "/media/series-2/episode-05-the-wall-in-the-jungle/",
      "media_folder": "hark-pasion-river-05-the-wall-in-the-jungle"
    },
    {
      "slug": "hark-pasion-river-06-three-days-of-darkness",
      "title": "Pasión River Model, Episode 6: Three Days of Darkness (Hark's Version)",
      "badge": "Hark's Version",
      "description": "Volcanoes, landslide-dammed rivers and a split in the rock at Aguateca: the Pasión River Model of Robert C. Roylance and Dr. Richard E. Terry reads these as the kind of landscape behind the destruction in 3 Nephi. It's the right kind of country, but nothing here has been dated to the time of Christ.",
      "duration": 387,
      "audience": "Adults & families",
      "published": "2026-10-18",
      "status": "published",
      "hls": "hark-pasion-river-06-three-days-of-darkness/master.m3u8",
      "poster": "hark-pasion-river-06-three-days-of-darkness/poster.jpg",
      "thumb": "hark-pasion-river-06-three-days-of-darkness/thumb.jpg",
      "poster_at": 25,
      "related": [
        {
          "slug": "hark-pasion-river-05-the-wall-in-the-jungle",
          "label": "Previous: Episode 5, The Wall in the Jungle"
        },
        {
          "slug": "hark-pasion-river-07-bountiful",
          "label": "Next: Episode 7, Bountiful"
        }
      ],
      "episode": 6,
      "page_path": "/series-2/episode-06-three-days-of-darkness/",
      "media_path": "/media/series-2/episode-06-three-days-of-darkness/",
      "media_folder": "hark-pasion-river-06-three-days-of-darkness"
    },
    {
      "slug": "hark-pasion-river-07-bountiful",
      "title": "Pasión River Model, Episode 7: Bountiful (Hark's Version)",
      "badge": "Hark's Version",
      "description": "The Pasión River Model's latest answer for Bountiful, from Robert C. Roylance and Dr. Richard E. Terry, is Altun Ha in Belize, with Yaxhá, Nixtun-Ch'ich' and Xunantunich weighed for the temple. We're upfront that their drafts have moved over the years.",
      "duration": 324,
      "audience": "Adults & families",
      "published": "2026-10-18",
      "status": "published",
      "hls": "hark-pasion-river-07-bountiful/master.m3u8",
      "poster": "hark-pasion-river-07-bountiful/poster.jpg",
      "thumb": "hark-pasion-river-07-bountiful/thumb.jpg",
      "poster_at": 23,
      "related": [
        {
          "slug": "hark-pasion-river-06-three-days-of-darkness",
          "label": "Previous: Episode 6, Three Days of Darkness"
        },
        {
          "slug": "hark-pasion-river-08-kings-of-the-war-years",
          "label": "Next: Episode 8, Kings of the War Years"
        }
      ],
      "episode": 7,
      "page_path": "/series-2/episode-07-bountiful/",
      "media_path": "/media/series-2/episode-07-bountiful/",
      "media_folder": "hark-pasion-river-07-bountiful"
    },
    {
      "slug": "hark-pasion-river-08-kings-of-the-war-years",
      "title": "Pasión River Model, Episode 8: Kings of the War Years (Hark's Version)",
      "badge": "Hark's Version",
      "description": "Maya kings were fighting and making treaties between A.D. 327 and 385, right when Mormon's wars unfold. Following the Pasión River Model of Robert C. Roylance and Dr. Richard E. Terry and a report on the Nephite wars, we set Tikal, Caracol and the Entrada of 378 beside the text, as a proposal and not as proof.",
      "duration": 568,
      "audience": "Adults & families",
      "published": "2026-10-18",
      "status": "published",
      "hls": "hark-pasion-river-08-kings-of-the-war-years/master.m3u8",
      "poster": "hark-pasion-river-08-kings-of-the-war-years/poster.jpg",
      "thumb": "hark-pasion-river-08-kings-of-the-war-years/thumb.jpg",
      "poster_at": 31,
      "related": [
        {
          "slug": "hark-pasion-river-07-bountiful",
          "label": "Previous: Episode 7, Bountiful"
        },
        {
          "slug": "hark-pasion-river-09-the-final-nephite-wars",
          "label": "Next: Episode 9, The Final Nephite Wars"
        }
      ],
      "episode": 8,
      "page_path": "/series-2/episode-08-kings-of-the-war-years/",
      "media_path": "/media/series-2/episode-08-kings-of-the-war-years/",
      "media_folder": "hark-pasion-river-08-kings-of-the-war-years"
    },
    {
      "slug": "hark-pasion-river-09-the-final-nephite-wars",
      "title": "Pasión River Model, Episode 9: The Final Nephite Wars (Hark's Version)",
      "badge": "Hark's Version",
      "description": "The Pasión River Model of Robert C. Roylance and Dr. Richard E. Terry proposes Uaxactún as the city of Desolation and traces Mormon's campaigns up to A.D. 384. We map those proposed places and flag the dates that don't line up, because this is a model, not Church doctrine.",
      "duration": 578,
      "audience": "Adults & families",
      "published": "2026-10-18",
      "status": "published",
      "hls": "hark-pasion-river-09-the-final-nephite-wars/master.m3u8",
      "poster": "hark-pasion-river-09-the-final-nephite-wars/poster.jpg",
      "thumb": "hark-pasion-river-09-the-final-nephite-wars/thumb.jpg",
      "poster_at": 29,
      "related": [
        {
          "slug": "hark-pasion-river-08-kings-of-the-war-years",
          "label": "Previous: Episode 8, Kings of the War Years"
        },
        {
          "slug": "hark-pasion-river-10-cumorah",
          "label": "Next: Episode 10, Cumorah"
        }
      ],
      "episode": 9,
      "page_path": "/series-2/episode-09-the-final-nephite-wars/",
      "media_path": "/media/series-2/episode-09-the-final-nephite-wars/",
      "media_folder": "hark-pasion-river-09-the-final-nephite-wars"
    },
    {
      "slug": "hark-pasion-river-10-cumorah",
      "title": "Pasión River Model, Episode 10: Cumorah (Hark's Version)",
      "badge": "Hark's Version",
      "description": "Roylance and Terry propose Hill Achotal, just across the Río Hondo from Belize, as both Cumorah and Ramah. We look at why the Pasión River Model picks it, what's still unexcavated, and how it handles the hill in New York; the Church itself takes no position on these locations.",
      "duration": 430,
      "audience": "Adults & families",
      "published": "2026-10-18",
      "status": "published",
      "hls": "hark-pasion-river-10-cumorah/master.m3u8",
      "poster": "hark-pasion-river-10-cumorah/poster.jpg",
      "thumb": "hark-pasion-river-10-cumorah/thumb.jpg",
      "poster_at": 27,
      "related": [
        {
          "slug": "hark-pasion-river-09-the-final-nephite-wars",
          "label": "Previous: Episode 9, The Final Nephite Wars"
        },
        {
          "slug": "hark-pasion-river-11-the-jaredites-barges-to-belize",
          "label": "Next: Episode 11, The Jaredites: Barges to Belize"
        }
      ],
      "episode": 10,
      "page_path": "/series-2/episode-10-cumorah/",
      "media_path": "/media/series-2/episode-10-cumorah/",
      "media_folder": "hark-pasion-river-10-cumorah"
    },
    {
      "slug": "hark-pasion-river-11-the-jaredites-barges-to-belize",
      "title": "Pasión River Model, Episode 11: The Jaredites: Barges to Belize (Hark's Version)",
      "badge": "Hark's Version",
      "description": "The Pasión River Model of Robert C. Roylance and Dr. Richard E. Terry sketches Jaredite barges landing in northern Belize near Chetumal Bay, and makes its boldest claim: the Jaredites as early Maya. We weigh that against what archaeologists have found.",
      "duration": 398,
      "audience": "Adults & families",
      "published": "2026-10-18",
      "status": "published",
      "hls": "hark-pasion-river-11-the-jaredites-barges-to-belize/master.m3u8",
      "poster": "hark-pasion-river-11-the-jaredites-barges-to-belize/poster.jpg",
      "thumb": "hark-pasion-river-11-the-jaredites-barges-to-belize/thumb.jpg",
      "poster_at": 25,
      "related": [
        {
          "slug": "hark-pasion-river-10-cumorah",
          "label": "Previous: Episode 10, Cumorah"
        },
        {
          "slug": "hark-pasion-river-12-shiz-and-coriantumr",
          "label": "Next: Episode 12, Shiz and Coriantumr"
        }
      ],
      "episode": 11,
      "page_path": "/series-2/episode-11-the-jaredites-barges-to-belize/",
      "media_path": "/media/series-2/episode-11-the-jaredites-barges-to-belize/",
      "media_folder": "hark-pasion-river-11-the-jaredites-barges-to-belize"
    },
    {
      "slug": "hark-pasion-river-12-shiz-and-coriantumr",
      "title": "Pasión River Model, Episode 12: Shiz and Coriantumr (Hark's Version)",
      "badge": "Hark's Version",
      "description": "The series finale: the last Jaredite war, from the Mirador Basin to the hill Ramah, and the Pasión River Model's question of whether Shiz's people were Olmec. Robert C. Roylance and Dr. Richard E. Terry treat that as inquiry, not a conclusion, and so do we, before a look back at the whole series.",
      "duration": 455,
      "audience": "Adults & families",
      "published": "2026-10-18",
      "status": "published",
      "hls": "hark-pasion-river-12-shiz-and-coriantumr/master.m3u8",
      "poster": "hark-pasion-river-12-shiz-and-coriantumr/poster.jpg",
      "thumb": "hark-pasion-river-12-shiz-and-coriantumr/thumb.jpg",
      "poster_at": 25,
      "related": [
        {
          "slug": "hark-pasion-river-11-the-jaredites-barges-to-belize",
          "label": "Previous: Episode 11, The Jaredites: Barges to Belize"
        }
      ],
      "episode": 12,
      "page_path": "/series-2/episode-12-shiz-and-coriantumr/",
      "media_path": "/media/series-2/episode-12-shiz-and-coriantumr/",
      "media_folder": "hark-pasion-river-12-shiz-and-coriantumr"
    }
  ],
  "series_id": "series-2",
  "series_title": "The Pasión River Model — Hark's Version",
  "path_note": "Proposed site paths: page /series-2/episode-NN-<slug>/, media /media/series-2/episode-NN-<slug>/ (poster.jpg, thumb.jpg, master.m3u8, 720p/, 480p/, 360p/). media_folder is the folder name inside each Drive zip; copy its contents to media_path."
}
```

## Caveats to keep (from each episode's production notes)

These are already spoken in the narration. If the site has a notes or "about" area, please keep them, and don't make any claim stronger than this.

**Series-wide:** every placement is "the Pasión River Model proposes" (Roylance & Terry), never a Church position. The Church takes no position on Book of Mormon geography. There is no "246/246" score, and no Maya inscription names a Nephite.

1. **The River Sidon:** "Sidon flows north" is an inference from the text's "up"/"down" language; the text never says it outright. The head of Sidon pins are approximate. The 800 ft Riplah climb is the authors' estimate. Mainstream sources date Ceibal's near-abandonment to about A.D. 300.
2. **Zarahemla Rising:** this episode has no separate notes file. Per the series brief, Ceibal's near-abandonment is dated about A.D. 300 in mainstream sources, against the model's A.D. 325/350; the episode handles that gap openly.
3. **Manti and the Hill Riplah Ambush:** most of Cancuén's excavated remains date to the 600s–700s A.D., long after Moroni, so the case rests on geography more than excavation. The "it came to pass" glyph is a parallel, not a fingerprint. The underground river is an inference.
4. **The Land of Nephi:** the book's own maps and captions disagree on Shilom and Shemlon (we used the majority reading). The book does not put Helam at Aguateca; it puts it near Tactic. The El Portón monument dating rests on one indirect radiocarbon date.
5. **The Wall in the Jungle:** Webster et al. 2007 (Terry was a co-author) dates the Tikal earthwork to about A.D. 400–550 and doubts it was ever a working defence. The earthwork trace and the La Cuernavilla pin are approximate.
6. **Three Days of Darkness:** volcanic ash, the Polochic landslide dams and the Aguateca chasm are the right kind of landscape, but none of them is dated to A.D. 34. The A.D. 50 sinking date for Cerros is secondhand. Moronihah hasn't been found.
7. **Bountiful:** the model's Bountiful placement has moved between drafts (Nixtun-Ch'ich' in 2019, Altun Ha in the 2026 book). No published excavation shows the ditch and wall at Altun Ha. Nixtun-Ch'ich' construction winds down around A.D. 100.
8. **Kings of the War Years:** the Entrada of 378 is read by mainstream scholars as a political takeover, not genocide. Caracol's dynasty continued for 460+ years. Uaxactún Stelae 18/19 date to A.D. 357, not 375. "Foliated Jaguar = Aaron" is only the report's suggestion.
9. **The Final Nephite Wars:** the book's A.D. 325 Ceibal date sits against the mainstream date of about A.D. 300. The earthwork dating is as in Ep 5. The book itself places Teancum in two different spots. Most of the retreat cities are unplaced. The "five million" population claim is not used.
10. **Cumorah:** Hill Achotal is on the Mexican side of the Río Hondo (Quintana Roo), with Belize just across the river. The summit outpost, sling stones and chert finds are the authors' field observations, not published excavations. Blue Creek's jade plaque reading is a suggestion. Many Latter-day Saints, including past Church leaders, have taught that the final battle was in New York.
11. **The Jaredites: Barges to Belize:** the Atlantic route is "one possibility", and the book admits it lacks solid evidence. Alma 22:30 "first landing" is usually read as the Mulekites. Early farming in northern Belize also shows up elsewhere. The Crooked Tree study makes no Book of Mormon link. Jaredites as early Maya is the model's boldest claim and goes against the mainstream view.
12. **Shiz and Coriantumr:** "Shiz's people were Olmec" is a working-paper idea; the 2026 book calls it a subject of inquiry, not a conclusion. La Venta (about 400 BC) and the model's war (about 350 BC) line up only roughly. Tres Zapotes continued for centuries. The "ten million" toll is the book's figure.

---
*Hark Bot. Questions about the Hark episodes go to Bill.*
