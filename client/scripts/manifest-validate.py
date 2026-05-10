from __future__ import annotations

import csv
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PUBLIC_ROOT = ROOT / "public"
DATASETS_ROOT = PUBLIC_ROOT / "datasets"
MANIFEST_PATH = DATASETS_ROOT / "manifest.json"
BASE_PATH = DATASETS_ROOT / "base" / "countries.csv"


def fail(message: str) -> None:
    raise SystemExit(f"manifest validation failed: {message}")


def read_json(path: Path) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError:
        fail(f"missing file {path}")
    except json.JSONDecodeError as error:
        fail(f"invalid JSON {path}: {error}")


def ensure_base() -> set[str]:
    if not BASE_PATH.exists():
        fail("missing base dataset countries.csv")
    with BASE_PATH.open(newline="", encoding="utf-8") as f:
        rows = list(csv.DictReader(f))
    expected = {"country_id", "name", "lat", "lon"}
    if not rows:
        fail("base dataset has no rows")
    if set(rows[0].keys()) != expected:
        fail("base dataset headers must be country_id,name,lat,lon")
    codes = set()
    for row in rows:
        code = (row.get("country_id") or "").strip()
        if not code:
            fail("base dataset contains empty country_id")
        codes.add(code)
    return codes


def validate_manifest_structure(manifest: dict) -> list[dict]:
    for key in ["schema_version", "data_version", "generated_at", "fingerprint", "base", "clues"]:
        if key not in manifest:
            fail(f"missing top-level key: {key}")
    if not isinstance(manifest["clues"], list):
        fail("clues must be a list")
    return manifest["clues"]


def validate_clues(clues: list[dict], playable_codes: set[str]) -> None:
    seen_ids: set[str] = set()
    for clue in clues:
        clue_id = clue.get("id")
        if not clue_id or not isinstance(clue_id, str):
            fail("clue missing valid id")
        if clue_id in seen_ids:
            fail(f"duplicate clue id: {clue_id}")
        seen_ids.add(clue_id)

        metadata_path = clue.get("metadata_path")
        if not metadata_path:
            fail(f"clue {clue_id} missing metadata_path")
        metadata_abs = PUBLIC_ROOT / metadata_path.lstrip("/")
        metadata = read_json(metadata_abs)
        if metadata.get("id") != clue_id:
            fail(f"metadata id mismatch for clue {clue_id}")

        is_computed = bool(clue.get("computed"))
        if not is_computed:
            data_path = clue.get("data_path")
            if not data_path:
                fail(f"non-computed clue {clue_id} missing data_path")
            data_abs = PUBLIC_ROOT / data_path.lstrip("/")
            if not data_abs.exists():
                fail(f"data file missing for clue {clue_id}")
            with data_abs.open(newline="", encoding="utf-8") as f:
                rows = list(csv.DictReader(f))
            if rows:
                headers = set(rows[0].keys())
                if headers != {"country_id", "value"}:
                    fail(f"invalid headers for clue {clue_id}, expected country_id,value")
            for row in rows:
                code = (row.get("country_id") or "").strip()
                if code and code not in playable_codes:
                    fail(f"clue {clue_id} has unknown country_id {code}")


def main() -> None:
    playable_codes = ensure_base()
    manifest = read_json(MANIFEST_PATH)
    clues = validate_manifest_structure(manifest)
    validate_clues(clues, playable_codes)
    print(f"Manifest validation passed ({len(clues)} clues)")


if __name__ == "__main__":
    main()
