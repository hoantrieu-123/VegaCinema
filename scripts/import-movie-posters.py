"""Copy every supplied poster without modifying it and regenerate the seed manifest."""
import hashlib
import json
from pathlib import Path
import re
import shutil
import unicodedata

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Phim"
DESTINATION = ROOT / "wwwroot" / "images" / "movies"
MANIFEST = ROOT / "src" / "CinemaBooking.DataAccess" / "SeedData" / "movie-posters.json"
EXTENSIONS = {".jpg", ".jpeg", ".png", ".webp", ".avif"}


def slug(title):
    title = title.replace("Đ", "D").replace("đ", "d")
    plain = unicodedata.normalize("NFKD", title).encode("ascii", "ignore").decode("ascii")
    return re.sub(r"[^a-z0-9]+", "-", plain.lower()).strip("-")


def main():
    sources = sorted((p for p in SOURCE.rglob("*") if p.is_file() and p.suffix.lower() in EXTENSIONS), key=lambda p: p.name.casefold())
    if not sources:
        raise SystemExit("No poster images found in Phim/.")
    entries, names = [], set()
    for source in sources:
        filename = slug(source.stem) + source.suffix.lower()
        if not filename or filename in names:
            raise SystemExit("Duplicate poster filename: " + source.name)
        names.add(filename)
        entries.append({"Title": source.stem.strip(), "PosterUrl": "/images/movies/" + filename})
    DESTINATION.mkdir(parents=True, exist_ok=True)
    MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    for source, entry in zip(sources, entries):
        target = DESTINATION / Path(entry["PosterUrl"]).name
        shutil.copy2(source, target)
        if hashlib.sha256(source.read_bytes()).digest() != hashlib.sha256(target.read_bytes()).digest():
            raise SystemExit("Poster copy verification failed: " + source.name)
    MANIFEST.write_text(json.dumps(entries, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Imported {len(entries)} posters; original image bytes verified.")


if __name__ == "__main__":
    main()
