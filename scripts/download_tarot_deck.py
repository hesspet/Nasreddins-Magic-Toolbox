#!/usr/bin/env python3
"""Download the Rider-Waite tarot deck images from Wikimedia Commons.

The project used to vendor the card images directly in the repository.  Because
binary files cause issues on the review platform we now fetch them on demand
instead.  The filenames match the original structure so existing code keeps
working.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Dict, Iterable, Tuple
from urllib import parse, request
from urllib.error import HTTPError, URLError

API_ENDPOINT = "https://commons.wikimedia.org/w/api.php"
USER_AGENT = "NasreddinsMagicToolbox/1.0 (https://github.com/)"

MAJOR_ARCANA: Tuple[Tuple[str, str], ...] = (
    ("Arcane_Major_00_Fool.jpg", "RWS_Tarot_00_Fool.jpg"),
    ("Arcane_Major_01_Magician.jpg", "RWS_Tarot_01_Magician.jpg"),
    ("Arcane_Major_02_High_Priestess.jpg", "RWS_Tarot_02_High_Priestess.jpg"),
    ("Arcane_Major_03_Empress.jpg", "RWS_Tarot_03_Empress.jpg"),
    ("Arcane_Major_04_Emperor.jpg", "RWS_Tarot_04_Emperor.jpg"),
    ("Arcane_Major_05_Hierophant.jpg", "RWS_Tarot_05_Hierophant.jpg"),
    ("Arcane_Major_06_Lovers.jpg", "RWS_Tarot_06_Lovers.jpg"),
    ("Arcane_Major_07_Chariot.jpg", "RWS_Tarot_07_Chariot.jpg"),
    ("Arcane_Major_08_Strength.jpg", "RWS_Tarot_08_Strength.jpg"),
    ("Arcane_Major_09_Hermit.jpg", "RWS_Tarot_09_Hermit.jpg"),
    ("Arcane_Major_10_Wheel_of_Fortune.jpg", "RWS_Tarot_10_Wheel_of_Fortune.jpg"),
    ("Arcane_Major_11_Justice.jpg", "RWS_Tarot_11_Justice.jpg"),
    ("Arcane_Major_12_Hanged_Man.jpg", "RWS_Tarot_12_Hanged_Man.jpg"),
    ("Arcane_Major_13_Death.jpg", "RWS_Tarot_13_Death.jpg"),
    ("Arcane_Major_14_Temperance.jpg", "RWS_Tarot_14_Temperance.jpg"),
    ("Arcane_Major_15_Devil.jpg", "RWS_Tarot_15_Devil.jpg"),
    ("Arcane_Major_16_Tower.jpg", "RWS_Tarot_16_Tower.jpg"),
    ("Arcane_Major_17_Star.jpg", "RWS_Tarot_17_Star.jpg"),
    ("Arcane_Major_18_Moon.jpg", "RWS_Tarot_18_Moon.jpg"),
    ("Arcane_Major_19_Sun.jpg", "RWS_Tarot_19_Sun.jpg"),
    ("Arcane_Major_20_Judgement.jpg", "RWS_Tarot_20_Judgement.jpg"),
    ("Arcane_Major_21_World.jpg", "RWS_Tarot_21_World.jpg"),
)


def _suit(name: str, count: int, remote_prefix: str) -> Tuple[Tuple[str, str], ...]:
    return tuple(
        (f"{name}{index:02}.jpg", f"RWS_Tarot_{remote_prefix}{index:02}.jpg")
        for index in range(1, count + 1)
    )


CUPS = _suit("Cups", 14, "Cups")
PENTACLES = _suit("Pents", 14, "Pentacles")
SWORDS = _suit("Swords", 14, "Swords")
WANDS = _suit("Wands", 14, "Wands")

CARD_FILES: Dict[str, str] = dict(MAJOR_ARCANA + CUPS + PENTACLES + SWORDS + WANDS)


def build_manifest() -> Iterable[Tuple[str, str]]:
    """Return the (local_name, remote_filename) manifest for the deck."""

    return CARD_FILES.items()


def commons_image_url(remote_filename: str) -> str:
    """Fetch the canonical download URL for a Wikimedia Commons file."""

    query_params = {
        "action": "query",
        "titles": f"File:{remote_filename}",
        "prop": "imageinfo",
        "iiprop": "url",
        "format": "json",
    }
    url = f"{API_ENDPOINT}?{parse.urlencode(query_params)}"
    req = request.Request(url, headers={"User-Agent": USER_AGENT})
    with request.urlopen(req) as response:
        payload = json.load(response)

    pages = payload.get("query", {}).get("pages", {})
    if not pages:
        raise RuntimeError(f"No pages returned for {remote_filename}")

    page = next(iter(pages.values()))
    imageinfo = page.get("imageinfo")
    if not imageinfo:
        raise RuntimeError(f"No imageinfo data for {remote_filename}")

    return imageinfo[0]["url"]


def download_file(url: str, destination: Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    req = request.Request(url, headers={"User-Agent": USER_AGENT})
    with request.urlopen(req) as response, destination.open("wb") as fh:
        fh.write(response.read())


def download_cards(target_dir: Path, *, force: bool, dry_run: bool) -> None:
    for local_name, remote_filename in build_manifest():
        output_path = target_dir / local_name
        if output_path.exists() and not force:
            print(f"Skipping {output_path} (already exists)")
            continue

        if dry_run:
            print(f"[DRY-RUN] Would download {remote_filename} -> {output_path}")
            continue

        try:
            url = commons_image_url(remote_filename)
        except (HTTPError, URLError, RuntimeError) as error:
            print(f"Failed to resolve {remote_filename}: {error}", file=sys.stderr)
            continue

        try:
            download_file(url, output_path)
        except (HTTPError, URLError) as error:
            print(f"Failed to download {url}: {error}", file=sys.stderr)
            continue

        print(f"Downloaded {remote_filename} -> {output_path}")


def parse_args(argv: Iterable[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--target",
        type=Path,
        default=Path("Images") / "TarotDeck_Wikipedia",
        help="Directory where the images should be stored",
    )
    parser.add_argument(
        "--force",
        action="store_true",
        help="Re-download files even if they already exist",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Do not download anything, just print the planned operations",
    )
    return parser.parse_args(argv)


def main(argv: Iterable[str] | None = None) -> int:
    args = parse_args(sys.argv[1:] if argv is None else argv)
    try:
        download_cards(args.target, force=args.force, dry_run=args.dry_run)
    except KeyboardInterrupt:
        print("Aborted by user", file=sys.stderr)
        return 130
    return 0


if __name__ == "__main__":
    sys.exit(main())
