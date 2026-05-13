from __future__ import annotations

import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = ROOT.parent
DATASETS_ROOT = REPO_ROOT / "server" / "datasets"
BASE_PATH = DATASETS_ROOT / "base" / "countries.csv"
CLUES_ROOT = DATASETS_ROOT / "clues"
MANIFEST_PATH = DATASETS_ROOT / "manifest.json"


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as file:
        while True:
            chunk = file.read(65536)
            if not chunk:
                break
            digest.update(chunk)
    return digest.hexdigest()


def web_path(path: Path) -> str:
    relative = path.relative_to(DATASETS_ROOT)
    return "/datasets/" + relative.as_posix()


def parse_existing_manifest() -> dict | None:
    if not MANIFEST_PATH.exists():
        return None
    try:
        return json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return None


def normalize_fingerprint(value: str | None) -> str | None:
    if not value:
        return None
    if value.startswith("sha256:"):
        return value.split(":", 1)[1]
    return value


def next_patch_version(previous: str | None) -> str:
    if not previous:
        return "0.0.1"
    parts = previous.split(".")
    if len(parts) != 3 or not all(part.isdigit() for part in parts):
        return "0.0.1"
    major, minor, patch = (int(part) for part in parts)
    return f"{major}.{minor}.{patch + 1}"


def build_clue_entries() -> list[dict]:
    entries: list[dict] = []

    if not CLUES_ROOT.exists():
        return entries

    for clue_dir in sorted([path for path in CLUES_ROOT.iterdir() if path.is_dir()], key=lambda path: path.name):
        metadata_path = clue_dir / "metadata.json"
        if not metadata_path.exists():
            continue

        metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
        clue_id = metadata.get("id", clue_dir.name)
        clue_type = metadata.get("type", "numeric")
        source = metadata.get("source", "builtin")
        is_computed = clue_type == "computed" or bool(metadata.get("compute"))

        entry: dict = {
            "id": clue_id,
            "dataset_id": metadata.get("dataset_id", clue_id),
            "source": source,
            "type": clue_type,
            "computed": is_computed,
            "comparator": metadata.get("comparator"),
            "icon": metadata.get("icon"),
            "label": metadata.get("label"),
            "description": metadata.get("description"),
            "unit_symbol": metadata.get("unit", {}).get("symbol") if "unit" in metadata else None,
            "metadata_path": web_path(metadata_path),
            "metadata_checksum": f"sha256:{sha256_file(metadata_path)}",
        }

        if clue_id.startswith("temperature_avg_c_m"):
            maybe_month = clue_id[-2:]
            if maybe_month.isdigit():
                entry["group"] = "temperature_avg_c"
                entry["month"] = int(maybe_month)

        if not is_computed:
            data_path = clue_dir / "data.csv"
            if not data_path.exists():
                raise FileNotFoundError(f"Missing data.csv for non-computed clue: {clue_id}")
            entry["format"] = "csv"
            entry["data_path"] = web_path(data_path)
            entry["data_checksum"] = f"sha256:{sha256_file(data_path)}"

        entries.append(entry)

    return entries


def build_fingerprint(base_checksum: str, clue_entries: list[dict]) -> str:
    payload = {
        "base": base_checksum,
        "clues": [
            {
                "id": clue["id"],
                "meta": clue.get("metadata_checksum", ""),
                "data": clue.get("data_checksum", ""),
            }
            for clue in sorted(clue_entries, key=lambda item: item["id"])
        ],
    }
    encoded = json.dumps(payload, ensure_ascii=True, sort_keys=True).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def main() -> None:
    if not BASE_PATH.exists():
        raise FileNotFoundError("Missing base dataset: server/datasets/base/countries.csv")

    base_checksum = sha256_file(BASE_PATH)
    clue_entries = build_clue_entries()
    fingerprint = build_fingerprint(base_checksum, clue_entries)

    previous = parse_existing_manifest()
    previous_fingerprint = normalize_fingerprint(previous.get("fingerprint") if previous else None)
    previous_version = previous.get("data_version") if previous else None

    if previous_fingerprint == fingerprint and previous_version:
        data_version = previous_version
    else:
        data_version = next_patch_version(previous_version)

    generated_at = datetime.now(timezone.utc).isoformat()
    manifest = {
        "schema_version": "1.0.0",
        "data_version": data_version,
        "generated_at": generated_at,
        "fingerprint": f"sha256:{fingerprint}",
        "base": {
            "dataset_id": "countries_base",
            "format": "csv",
            "path": web_path(BASE_PATH),
            "checksum": f"sha256:{base_checksum}",
        },
        "clues": sorted(clue_entries, key=lambda item: item["id"]),
    }

    if previous:
        prev_norm = dict(previous)
        new_norm = dict(manifest)
        prev_norm.pop("generated_at", None)
        new_norm.pop("generated_at", None)
        if prev_norm == new_norm:
            print(f"Manifest unchanged: version={data_version}, clues={len(clue_entries)}")
            return

    MANIFEST_PATH.write_text(json.dumps(manifest, ensure_ascii=True, indent=2) + "\n", encoding="utf-8")
    print(f"Manifest updated: version={data_version}, clues={len(clue_entries)}")


if __name__ == "__main__":
    main()
