"""Pinned retail selection for the separate local test hosts, never the live stack."""
from pathlib import Path
import hashlib
import json
import os


def selected_package(repo):
    repo = Path(repo).resolve()
    selection_path = repo / "scripts/private-test/retail-package.json"
    selection = json.loads(selection_path.read_text(encoding="utf-8"))
    if selection["version"] != 1 or selection["mode"] != "statistics-only-v1":
        raise ValueError("Unsupported private-test package selection")
    manifest = (repo / selection["manifest"]).resolve()
    if not 0 < manifest.stat().st_size <= 64 * 1024 * 1024:
        raise ValueError("Missing/oversized private-test manifest")
    raw = manifest.read_bytes()
    pin = hashlib.sha256(raw).hexdigest()
    if pin != selection["sha256"]:
        raise ValueError("Private-test manifest SHA256 differs from selection")
    if json.loads(raw)["mode"] != selection["mode"]:
        raise ValueError("Private-test manifest mode differs from selection")
    return {"path": str(manifest), "sha256": pin, "mode": selection["mode"]}


def host_environment(repo, kind, inherited=None):
    if kind not in ("login", "chat", "game"):
        raise ValueError("Choose login, chat or game")
    env = dict(os.environ if inherited is None else inherited)
    # The old pilot enabled navigation and experimental skills. This selection
    # uses the ordinary production statistics loader only, in the child process.
    prefixes = ("AION_RETAIL_", "AION_SKYTEMPLE_", "AION_THREE_KEYS_", "AION_SIM_")
    for key in list(env):
        if key.startswith(prefixes):
            del env[key]
    package = None
    if kind == "game":
        package = selected_package(repo)
        env["AION_RETAIL_STATS_MANIFEST"] = package["path"]
        env["AION_RETAIL_STATS_MANIFEST_SHA256"] = package["sha256"]
    return env, package


if __name__ == "__main__":
    print(json.dumps(selected_package(Path(__file__).resolve().parents[2]), indent=2))
