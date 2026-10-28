"""Inventory candidate 5.8 skill identities for 4.8 absolute-stat effects.

Name matches are candidates, not proof that an effect or parameter is equivalent.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def host_bindings(path):
    rows = []
    for event, skill in ET.iterparse(path, events=("end",)):
        if skill.tag != "skill_template":
            continue
        for effect in skill.iter():
            if effect.tag in ("absstatbuff", "absstatdebuff"):
                rows.append({"id": int(skill.attrib["skill_id"]), "level": int(skill.get("lvl", "1")),
                             "stack": skill.get("stack", ""), "name": skill.get("name", ""),
                             "effect": effect.tag, "position": effect.get("e"),
                             "stat_set": int(effect.attrib["statsetid"])})
        skill.clear()
    return rows

def retail_candidates(path, bindings):
    wanted = {row["stack"].upper() for row in bindings if row["stack"]}
    result = {name: [] for name in wanted}
    for _, record in ET.iterparse(path, events=("end",)):
        if record.tag != "skill_base":
            continue
        name = record.findtext("name", "")
        normalized = re.sub(r"_G[1-9][0-9]*$", "", name.upper())
        if normalized in wanted:
            effects = [{"position": i, "type": record.findtext(f"effect{i}_type"),
                        "reserved1": record.findtext(f"effect{i}_reserved1"),
                        "reserved4": record.findtext(f"effect{i}_reserved4")}
                       for i in range(1, 5) if record.findtext(f"effect{i}_type")]
            result[normalized].append({"id": int(record.findtext("id")), "name": name, "effects": effects})
        record.clear()
    return result

def absolute_names(path):
    root = ET.parse(path).getroot()
    if root.tag != "absolute_stat_to_pcs":
        raise ValueError("Unexpected retail absolute-stat root")
    names = {}
    for record in root.findall("absolute_stat_to_pc"):
        name, raw_id = record.findtext("name"), record.findtext("id")
        if not name or not raw_id or name.casefold() in names:
            raise ValueError("Missing or duplicate retail absolute-stat name")
        names[name.casefold()] = int(raw_id)
    return names

def compare(retail_path, host_path, absolute_path):
    bindings = host_bindings(host_path)
    candidates = retail_candidates(retail_path, bindings)
    names = absolute_names(absolute_path)
    rows = []
    for row in bindings:
        found = candidates.get(row["stack"].upper(), [])
        for candidate in found:
            for effect in candidate["effects"]:
                if effect["type"] in ("AbsoluteStatToPCBuff", "AbsoluteStatToPCDebuff"):
                    effect["resolved_stat_set"] = names.get((effect["reserved1"] or "").casefold())
        rows.append({**row, "candidates": found})
    return {"retail_sha256": digest(retail_path), "host_sha256": digest(host_path),
            "retail_absolute_sha256": digest(absolute_path), "bindings": rows,
            "scope": "Candidates matched by 4.8 stack and 5.8 raw name after optional _G<number> suffix removal. Not verified identities, effect equivalence, region selection, effect lifetime or gameplay admission."}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--retail", type=Path, required=True)
    parser.add_argument("--host", type=Path, required=True)
    parser.add_argument("--retail-absolute", type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(compare(args.retail, args.host, args.retail_absolute), indent=2))

if __name__ == "__main__":
    main()
