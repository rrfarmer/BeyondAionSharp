"""Read-only comparison of the curse fields in retail and emulator absolute-stat sets.

Absent fields are deliberately distinct from explicit zero: retail uses a presence
bit to decide whether an absolute value overrides the ordinary resistance channel.
"""
import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET

FIELDS = (("ar_curse", "CURSE_RESISTANCE"), ("curse_arp", "CURSE_RESISTANCE_PENETRATION"))

def _digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def _retail(path):
    root = ET.parse(path).getroot()
    if root.tag != "absolute_stat_to_pcs":
        raise ValueError("Unexpected retail absolute-stat root")
    rows = {}
    for record in root.findall("absolute_stat_to_pc"):
        values = record.findall("id")
        if len(values) != 1 or not values[0].text:
            raise ValueError("Missing or duplicate retail set ID")
        key = int(values[0].text)
        if key in rows:
            raise ValueError(f"Duplicate retail set ID {key}")
        fields = {}
        for retail, _ in FIELDS:
            nodes = record.findall(retail)
            if len(nodes) > 1:
                raise ValueError(f"Duplicate retail {retail} in set {key}")
            fields[retail] = int(nodes[0].text) if nodes and nodes[0].text is not None else None
        rows[key] = fields
    return rows

def _host(path):
    root = ET.parse(path).getroot()
    if root.tag != "absolute_stats":
        raise ValueError("Unexpected host absolute-stat root")
    rows = {}
    for record in root.findall("stats_set"):
        key = int(record.attrib["id"])
        if key in rows:
            raise ValueError(f"Duplicate host set ID {key}")
        fields = {}
        for retail, host in FIELDS:
            nodes = [n for n in record.iter("abs") if n.get("name") == host]
            if len(nodes) > 1 or any(n.get("bonus", "false").lower() not in ("false", "0") for n in nodes):
                raise ValueError(f"Ambiguous host {host} in set {key}")
            fields[retail] = int(nodes[0].attrib["value"]) if nodes else None
        rows[key] = fields
    return rows

def compare(retail_path, host_path):
    retail, host = _retail(retail_path), _host(host_path)
    shared = sorted(retail.keys() & host.keys())
    differences = [{"id": i, "field": name, "retail": retail[i][name], "host": host[i][name]}
                   for i in shared for name, _ in FIELDS if retail[i][name] != host[i][name]]
    return {"retail_sha256": _digest(retail_path), "host_sha256": _digest(host_path),
            "retail_sets": len(retail), "host_sets": len(host), "shared_sets": len(shared),
            "differences": differences, "retail_only_ids": sorted(retail.keys()-host.keys()),
            "host_only_ids": sorted(host.keys()-retail.keys()),
            "scope": "Raw absolute curse resistance and penetration fields only; not source selection, active effect precedence, or gameplay admission."}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--retail", type=Path, required=True)
    parser.add_argument("--host", type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(compare(args.retail, args.host), indent=2))

if __name__ == "__main__":
    main()
