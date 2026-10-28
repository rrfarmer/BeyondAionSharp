"""Deterministic, source-bound retail NPC profile extraction (no runtime activation).

The pinned binaries select seven divided NPC files. The aggregate is a full-record
crosscheck. This tool preserves source presence/ordering and refuses ambiguous
identities; it does not implement the permissive retail XML reader or combat.

Run with --help. All generated files go into a NEW explicit --output directory,
published by one directory rename after every source has parsed and been rehashed.
"""
from __future__ import annotations

import argparse
import collections
import csv
import hashlib
import json
import math
import os
from pathlib import Path, PurePosixPath
import re
import struct
import sys
import tempfile
from typing import Callable
from xml.parsers import expat

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
SCHEMA_VERSION = 1
PILOT_IDS = frozenset(range(212761, 212783)) | {212833, 212835, 212876}


class ExtractionError(ValueError):
    """Invalid, conflicting or unverified input; no output publication is allowed."""


def canonical(value) -> bytes:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False).encode("utf-8")


def sha_file(path: Path) -> str:
    with path.open("rb") as handle:
        return hashlib.file_digest(handle, "sha256").hexdigest()


def receipt(path: Path, **extra) -> dict:
    return {"path": str(path.resolve()), "bytes": path.stat().st_size, "sha256": sha_file(path), **extra}


def write_json(path: Path, value):
    path.write_bytes(canonical(value) + b"\n")


def children(node, tag=None):
    return [x for x in node[2] if isinstance(x, list) and not x[0].startswith("#") and (tag is None or x[0] == tag)]


def node_text(node) -> str:
    return "".join(x for x in node[2] if isinstance(x, str))


def one_text(node, tag: str, required=False):
    nodes = children(node, tag)
    if len(nodes) > 1:
        raise ExtractionError(f"Duplicate identity field <{tag}>")
    if not nodes:
        if required:
            raise ExtractionError(f"Missing identity field <{tag}>")
        return None
    if children(nodes[0]):
        raise ExtractionError(f"Nested identity field <{tag}>")
    return node_text(nodes[0]).strip()


def record_identity(node):
    raw_id = one_text(node, "id", True)
    name = one_text(node, "name", True)
    if not re.fullmatch(r"[0-9]+", raw_id or "") or not name:
        raise ExtractionError("Missing/non-numeric ID or empty devname")
    return int(raw_id), name


def semantic_tree(node):
    """Ignore formatting between child elements, retain leaf text and list order."""
    has_children = bool(children(node))
    content = []
    for item in node[2]:
        if isinstance(item, str):
            if has_children and not item.strip():
                continue
            if content and isinstance(content[-1], str):
                content[-1] += item
            else:
                content.append(item)
        elif not item[0].startswith("#"):
            content.append(semantic_tree(item))
    return [node[0], sorted(node[1]), content]


def semantic_sha(node):
    return hashlib.sha256(canonical(semantic_tree(node))).hexdigest()


def parse_records(path: Path, root_tag: str, record_tag: str, callback: Callable, chunk_size=1024 * 1024):
    """Strict streaming XML with an ordered infoset and exact raw-record spans.

    Adjacent text callbacks are coalesced, so chunk size cannot change the tree.
    External entities are refused. Malformed XML is a visible admission blocker,
    never replacement-decoded or silently skipped. Only one record is retained.
    """
    parser = expat.ParserCreate()
    parser.ordered_attributes = True
    stack = []
    depth = 0
    root = None
    current_start = None
    count = 0
    others = collections.Counter()
    digest = hashlib.sha256()
    with path.open("rb") as raw_handle, path.open("rb") as handle:
        prefix = handle.read(4)
        handle.seek(0)
        codec = "utf-16-le" if prefix.startswith(b"\xff\xfe") or prefix.startswith(b"<\0") else "utf-16-be" if prefix.startswith(b"\xfe\xff") or prefix.startswith(b"\0<") else "utf-8"

        def start(name, attrs):
            nonlocal depth, root, current_start
            depth += 1
            if depth > 128:
                raise ExtractionError(f"XML depth limit: {path}:{parser.CurrentLineNumber}")
            if depth == 1:
                root = name
                if name != root_tag:
                    raise ExtractionError(f"Expected <{root_tag}>, got <{name}> in {path}")
            if depth == 2 and name == record_tag:
                stack.append([name, list(zip(attrs[::2], attrs[1::2])), []])
                current_start = (parser.CurrentByteIndex, parser.CurrentLineNumber)
            elif stack:
                node = [name, list(zip(attrs[::2], attrs[1::2])), []]
                stack[-1][2].append(node)
                stack.append(node)
            elif depth == 2:
                others[name] += 1

        def text(value):
            if stack:
                content = stack[-1][2]
                if content and isinstance(content[-1], str):
                    content[-1] += value
                else:
                    content.append(value)

        def end(name):
            nonlocal depth, count, current_start
            if stack:
                node = stack.pop()
                if depth == 2:
                    byte_start, line = current_start
                    closing_start = parser.CurrentByteIndex
                    raw_handle.seek(closing_start)
                    tail = raw_handle.read(1024)
                    marker = ">".encode(codec)
                    stop = tail.find(marker)
                    if stop >= 0 and tail.startswith(("</" + record_tag).encode(codec)):
                        byte_end = closing_start + stop + len(marker)
                    else:
                        # Expat reports an empty element's end after its '/>'.
                        # Verify the original bytes rather than guessing a span.
                        raw_handle.seek(byte_start)
                        opening = raw_handle.read(closing_start - byte_start)
                        if not opening.decode(codec, "strict").rstrip().endswith("/>"):
                            raise ExtractionError(f"Cannot establish raw record end in {path}:{line}")
                        byte_end = closing_start
                    if byte_end - byte_start > 8 * 1024 * 1024:
                        raise ExtractionError(f"Record exceeds bounded8MiB admission: {path}:{line}")
                    raw_handle.seek(byte_start)
                    raw = raw_handle.read(byte_end - byte_start)
                    callback(node, {"path": str(path.resolve()), "record_index": count, "line": line, "byte_start": byte_start, "byte_end_exclusive": byte_end, "raw_sha256": hashlib.sha256(raw).hexdigest(), "semantic_sha256": semantic_sha(node)})
                    count += 1
                    current_start = None
            depth -= 1

        def comment(value):
            if stack:
                stack[-1][2].append(["#comment", [], [value]])

        def pi(target, value):
            if stack:
                stack[-1][2].append(["#pi", [["target", target]], [value]])

        parser.StartElementHandler, parser.EndElementHandler = start, end
        parser.CharacterDataHandler, parser.CommentHandler, parser.ProcessingInstructionHandler = text, comment, pi
        parser.ExternalEntityRefHandler = lambda *_: 0
        try:
            while block := handle.read(chunk_size):
                digest.update(block)
                parser.Parse(block, False)
            parser.Parse(b"", True)
        except expat.ExpatError as error:
            raise ExtractionError(f"Strict XML admission failed {path}:{error.lineno}:{error.offset}: {error}; no retail permissive-reader equivalence assumed") from error
    return {"path": str(path.resolve()), "bytes": path.stat().st_size, "sha256": digest.hexdigest(), "strict_xml_complete": True, "record_tag": record_tag, "record_count": count, "other_root_children": dict(sorted(others.items()))}


def project_field(parent, tag: str, projection="string", consumer_ref=None, default=None):
    values = [node_text(n) for n in children(parent, tag)]
    field = {"presence": "absent" if not values else "ambiguous_duplicate" if len(values) > 1 else "explicit_value", "raw_values": values, "projection": projection, "default_applied": False}
    if consumer_ref:
        field["consumer_ref"] = consumer_ref
    if default is not None:
        field["static_default_evidence_not_applied"] = default
    if len(values) == 1 and projection != "string":
        raw = values[0].strip()
        if re.fullmatch(r"[+-]?0+(?:\.0*)?(?:[eE][+-]?[0-9]+)?", raw):
            field["presence"] = "explicit_zero"
        try:
            if projection == "integer_lexeme":
                if not re.fullmatch(r"[+-]?[0-9]+", raw):
                    raise ValueError("not an integer lexeme")
                field["parsed_unbounded_integer"] = int(raw)
            elif projection == "float32":
                number = float(raw)
                packed = struct.pack(">f", number)
                if not math.isfinite(number) or not math.isfinite(struct.unpack(">f", packed)[0]):
                    raise ValueError("non-finite float32")
                field["float32_bits_hex"] = packed.hex()
            elif projection == "decimal_lexeme":
                if not re.fullmatch(r"[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?", raw):
                    raise ValueError("not a decimal lexeme")
                # No guessed native double/float width, units or conversion.
        except (ValueError, OverflowError) as error:
            field["parse_error"] = str(error)
    return field


def select_sources(xml_root: Path, filenames, region="USA"):
    if region != "USA":
        raise ExtractionError("Schema1 supports the explicitly evidenced USA selection only")
    plan = []
    for filename in filenames:
        regional, base = xml_root / region / filename, xml_root / filename
        selected = regional if regional.is_file() else base
        if not selected.is_file():
            raise ExtractionError(f"Neither selected regional nor base file exists: {filename}")
        plan.append({"filename": filename, "region": region, "regional_candidate": str(regional.resolve()), "regional_exists": regional.is_file(), "selected": str(selected.resolve()), "reason": "regional_file_present_requires_complete_parse" if regional.is_file() else "regional_missing_base_fallback"})
    return plan


def pe_array(path: Path, va: int, expected_sha: str):
    data = path.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if digest != expected_sha:
        raise ExtractionError(f"Pinned binary identity mismatch: {path}")
    pe = struct.unpack_from("<I", data, 0x3c)[0]
    if data[pe:pe + 4] != b"PE\0\0":
        raise ExtractionError("Not a PE")
    count = struct.unpack_from("<H", data, pe + 6)[0]
    opt = struct.unpack_from("<H", data, pe + 20)[0]
    image = struct.unpack_from("<Q", data, pe + 48)[0]
    sections = [struct.unpack_from("<IIII", data, pe + 24 + opt + i * 40 + 8) for i in range(count)]
    def offset(address):
        rva = address - image
        for _, virtual, raw_size, raw_ptr in sections:
            if virtual <= rva < virtual + raw_size:
                return raw_ptr + rva - virtual
        raise ExtractionError(f"No file-backed PE mapping: {address:x}")
    raw = data[offset(va):offset(va) + 56]
    entries = []
    for pointer in struct.unpack("<7Q", raw):
        start = offset(pointer)
        end = data.index(0, start)
        entries.append(data[start:end].decode("ascii", "strict"))
    return {"path": str(path.resolve()), "sha256": digest, "bytes": len(data), "array_va": hex(va), "array_file_offset": offset(va), "array_bytes_hex": raw.hex(), "filenames": entries}


def native_skill_name_key(name: str) -> str:
    # Actual Main NPC XML parser -> registry lookup/insert uses _wcsicmp.
    # All 14,457 names in this pinned source are ASCII. Do not substitute
    # Unicode casefold/whitespace trimming for an unproved locale contract.
    # Evidence: stats-only-20261025-01/skill-name-case-review-01/REPORT.md.
    return name.lower() if name.isascii() else name


class RetailSkillNameIndex:
    """Prepared native ASCII name buckets; retain all IDs to expose collisions."""
    def __init__(self, names):
        buckets = collections.defaultdict(set)
        for name, ids in names.items():
            buckets[native_skill_name_key(name)].update(ids)
        self.buckets = {key: frozenset(ids) for key, ids in buckets.items()}

    def get(self, name, default=None):
        return self.buckets.get(native_skill_name_key(name), default)


def prepare_skill_name_index(names):
    return names if isinstance(names, RetailSkillNameIndex) else RetailSkillNameIndex(names)


def resolve_skill(name: str, retail_names, supported_ids, stack_names):
    retail = sorted(prepare_skill_name_index(retail_names).get(name, set()))
    candidates = sorted(stack_names.get(name.upper(), set()))
    if len(retail) > 1 or len(candidates) > 1:
        status, chosen = "ambiguous_name_binding_blocked", None
    elif not retail:
        status, chosen = "retail_skill_name_absent_blocked", None
    elif retail[0] not in supported_ids:
        status, chosen = "unsupported_48_skill_id_blocked", None
    elif not candidates:
        status, chosen = "id_only_no_independent_stack_match_review", None
    elif retail != candidates:
        status, chosen = "retail_id_stack_id_disagreement_blocked", None
    else:
        status, chosen = "dual_join_agrees_static_only", retail[0]
    return {"name": name, "retail_name_id_candidates": retail, "supported48_stack_id_candidates": candidates, "supported48_id": chosen, "status": status, "effect_semantics_verified": False}


def client_identity_records(data: bytes, decoder):
    """Admit exactly one scalar ID/name per record, with strict string decoding."""
    import xml.etree.ElementTree as ET
    root = decoder.decode(data, strict=True) if decoder.is_binary_xml(data) else ET.fromstring(data)
    identities = []
    for node in root:
        values = {}
        for tag in ["id", "name"]:
            fields = node.findall(tag)
            if len(fields) != 1 or len(fields[0]):
                raise ExtractionError(f"Client identity requires exactly one scalar <{tag}>")
            values[tag] = (fields[0].text or "").strip()
        if not re.fullmatch(r"[0-9]+", values["id"]) or not values["name"] or "\ufffd" in values["name"]:
            raise ExtractionError("Unusable client NPC identity; no replacement text admitted")
        identities.append((int(values["id"]), values["name"]))
    return identities


def profile_record(node, source, target, registry, retail_skills, supported_skills, stack_names):
    npc, name = record_identity(node)
    scalar = {}
    for category, projection in [("root_integer_lexemes", "integer_lexeme"), ("root_decimal_lexemes", "decimal_lexeme"), ("root_string_fields", "string")]:
        for tag in registry.get(category, []):
            scalar[tag] = project_field(node, tag, projection, "root/" + tag)
    lists, bindings = {}, []
    for group in ["attacks", "skills", "abnormal_resists", "abnormal_penetrations", "first_level_attr", "bound_radius", "items_info"]:
        groups = children(node, group)
        entries = []
        for group_index, g in enumerate(groups):
            for index, entry in enumerate(children(g)):
                fields = {}
                if group == "attacks":
                    for tag, evidence in registry.get("attack_fields", {}).items():
                        fields[tag] = project_field(entry, tag, evidence["projection"], "attacks/" + tag, evidence.get("static_default"))
                elif group == "skills":
                    for tag in ["skill_name", "skill_level", "skill_rate", "skill_count"]:
                        fields[tag] = project_field(entry, tag, "string" if tag == "skill_name" else "integer_lexeme", "skills/" + tag)
                    names = children(entry, "skill_name")
                    binding = resolve_skill(node_text(names[0]).strip(), retail_skills, supported_skills, stack_names) if len(names) == 1 else {"status": "missing_or_duplicate_skill_name_blocked", "supported48_id": None, "effect_semantics_verified": False}
                    bindings.append({"source_group_index": group_index, "source_entry_index": index, **binding})
                entries.append({"source_group_index": group_index, "source_entry_index": index, "tag": entry[0], "projected_fields": fields})
        lists[group] = {"presence": "absent" if not groups else "explicit_value" if len(groups) == 1 else "ambiguous_duplicate", "entries": entries}
    blockers = ["effective_runtime_profile_not_verified", "field_units_defaults_and_scaling_not_fully_recovered", "skill_effect_semantics_not_verified"]
    for name_, field in scalar.items():
        if field["presence"] == "ambiguous_duplicate" or "parse_error" in field:
            blockers.append("unresolved_scalar:" + name_)
    for group, data in lists.items():
        if data["presence"] == "ambiguous_duplicate":
            blockers.append("duplicate_list_group:" + group)
        for entry in data["entries"]:
            for field_name, field in entry["projected_fields"].items():
                if field["presence"] == "ambiguous_duplicate" or "parse_error" in field:
                    blockers.append("unresolved_list_field:" + group + "/" + str(entry["source_entry_index"]) + "/" + field_name)
    if any(b["status"] != "dual_join_agrees_static_only" for b in bindings):
        blockers.append("unresolved_skill_identity_binding")
    return {"schema_version": SCHEMA_VERSION, "npc_id": npc, "devname": name, "binding48": target, "source": source, "raw_record": node, "scalar_fields": scalar, "ordered_lists": lists, "skill_bindings": bindings, "blockers": sorted(set(blockers)), "verification": {"stage": "static_source_extracted", "fixture_verified": False, "activation_ready": False}}


def atomic_generate(*, output: Path, xml_root: Path, registry: dict, targets: dict, filenames: list, source_receipts: list, retail_skills=None, supported_skills=None, stack_names=None, pilot_ids=PILOT_IDS, expected_exact=None, reviewed_identity_bindings=None, log=lambda _: None):
    """Core generator, also used by small source fixtures in the stdlib tests."""
    retail_skills = prepare_skill_name_index(retail_skills or {})
    output, xml_root = output.resolve(), xml_root.resolve()
    if output.exists():
        raise ExtractionError(f"Refusing to overwrite output: {output}")
    if output == xml_root or xml_root in output.parents:
        raise ExtractionError("Output must be outside source XML tree")
    if reviewed_identity_bindings is not None:
        from retail_npc_identity_bindings import ReviewedAbyssBindings
        if type(reviewed_identity_bindings) is not ReviewedAbyssBindings:
            raise ExtractionError("A complete admitted Abyss identity review is required; no alias callback")
        for npc, binding in reviewed_identity_bindings.entries.items():
            target = targets.get(npc)
            if target is None or target.get("devnames48") != [binding.client48_devname] or target.get("attrs48", {}).get("level") != binding.previous_level48:
                raise ExtractionError(f"Reviewed original4.8 cohort identity/level differs: {npc}")
        from retail_npc_identity_bindings import CLIENT48_SHA256
        archives = [row for row in source_receipts if row.get("role") == "independent48_identity_archive"]
        if len(archives) != 1 or archives[0].get("sha256") != CLIENT48_SHA256 or type(archives[0].get("bytes")) is not int or archives[0]["bytes"] != 3005028:
            raise ExtractionError("Reviewed original4.8 client archive identity changed or is missing; a new review is required")
    output.parent.mkdir(parents=True, exist_ok=True)
    plan = select_sources(xml_root, filenames)
    stage = Path(tempfile.mkdtemp(prefix="." + output.name + ".staging-", dir=output.parent)).resolve()
    assert stage.parent == output.parent
    selected, offsets, aggregate_seen, diffs = {}, {}, set(), []
    manifests = list(source_receipts)
    unsupported_rows = []
    exact_ids = set()
    reviewed_rows = {}
    try:
        with (stage / "_profiles-spool.jsonl").open("w+b") as spool:
            for selected_file in plan:
                path = Path(selected_file["selected"])
                log("parse selected " + path.name)
                reviewed_file_hash = sha_file(path) if reviewed_identity_bindings is not None and path.name == "npcs_abyss_monsters.xml" else None
                def consume(node, location):
                    npc, name = record_identity(node)
                    if npc in selected:
                        raise ExtractionError(f"Duplicate selected NPC ID {npc}: {selected[npc]['source']} and {location}; no overwrite")
                    selected[npc] = {"name": name, "source": location, "semantic_sha256": location["semantic_sha256"]}
                    target = targets.get(npc)
                    if target is None:
                        return
                    if reviewed_identity_bindings is not None and npc in reviewed_identity_bindings.entries:
                        from retail_npc_identity_bindings import IdentityBindingError
                        fields = {tag: tuple(node_text(child) for child in children(node, tag)) for tag in
                                  ("id", "name", "level", "desc", "race_type", "tribe", "npc_type", "hpgauge_level", "ai_name")}
                        try:
                            binding = reviewed_identity_bindings.admit(npc_id=npc, client48_devnames=target["devnames48"],
                                current_level48=target["attrs48"]["level"], source_file_sha256=reviewed_file_hash,
                                source=location, raw_fields=fields)
                        except IdentityBindingError as error:
                            raise ExtractionError(f"Reviewed same-ID identity admission failed for {npc}: {error}") from error
                        reviewed_rows[npc] = binding.metadata()
                        target = {**target, "reviewed_reference_drift": reviewed_rows[npc]}
                    elif target["devnames48"] != [name]:
                        return
                    profile = profile_record(node, location, target, registry, retail_skills or {}, supported_skills or set(), stack_names or {})
                    raw = canonical(profile) + b"\n"
                    offsets[npc] = (spool.tell(), len(raw))
                    spool.write(raw)
                    exact_ids.add(npc)
                try:
                    parsed = parse_records(path, "npcs", "npc", consume)
                    if reviewed_file_hash is not None and parsed["sha256"] != reviewed_file_hash:
                        raise ExtractionError("Reviewed divided source changed between prehash and parse")
                    manifests.append(parsed)
                except ExtractionError as error:
                    if selected_file["regional_exists"]:
                        raise ExtractionError(f"Present regional candidate failed; native partial-load ambiguity blocked (base is not silently substituted): {error}") from error
                    raise
            spool.flush()
            if reviewed_identity_bindings is not None and set(reviewed_rows) != set(reviewed_identity_bindings.entries):
                raise ExtractionError("The complete90 reviewed original IDs were not source-admitted")
            def load_profile(npc):
                offset, size = offsets[npc]
                spool.seek(offset)
                return json.loads(spool.read(size))
            aggregate = xml_root / "npcs.xml"
            log("parse aggregate crosscheck npcs.xml")
            def check(node, location):
                npc, name = record_identity(node)
                if npc in aggregate_seen:
                    raise ExtractionError(f"Duplicate aggregate NPC ID {npc}; crosscheck cannot pick a winner")
                aggregate_seen.add(npc)
                original = selected.get(npc)
                if original is None or original["name"] != name or original["semantic_sha256"] != location["semantic_sha256"]:
                    diffs.append({"npc_id": npc, "status": "aggregate_only" if original is None else "semantic_record_mismatch", "selected": original, "aggregate": {"name": name, "source": location, "semantic_sha256": location["semantic_sha256"]}})
            manifests.append(parse_records(aggregate, "npcs", "npc", check))
            for npc in sorted(set(selected) - aggregate_seen):
                diffs.append({"npc_id": npc, "status": "aggregate_record_absent", "selected": selected[npc]})
            diff_by_id = {d["npc_id"]: d for d in diffs}
            if expected_exact is not None and len(exact_ids) != expected_exact:
                raise ExtractionError(f"Expected {expected_exact} source-admitted bindings, found {len(exact_ids)}")
            for row in manifests:
                if not Path(row["path"]).is_file():
                    raise ExtractionError("Input disappeared: " + row["path"])
            dataset_id = hashlib.sha256(canonical({"schema_version": SCHEMA_VERSION, "source_manifests": manifests, "selection": plan, "target_ids": sorted(targets), "registry": registry})).hexdigest()
            coverage = []
            with (stage / "profiles.jsonl").open("wb") as profiles, (stage / "pilot-profiles.jsonl").open("wb") as pilot:
                for npc, target in sorted(targets.items()):
                    source = selected.get(npc)
                    status = "source_absent_blocked" if source is None else "exported_static_source_only" if npc in exact_ids else "devname_changed_or_ambiguous_blocked"
                    row = {"npc_id": npc, "devnames48": ";".join(target["devnames48"]), "retail_devname": source["name"] if source else "", "status": status, "family": target.get("family", "unclassified"), "source_path": source["source"]["path"] if source else "", "source_line": source["source"]["line"] if source else "", "aggregate_crosscheck": diff_by_id[npc]["status"] if npc in diff_by_id else "equal" if source else "not_applicable", "fixture_verified": False, "activation_ready": False}
                    coverage.append(row)
                    if npc not in exact_ids:
                        continue
                    profile = load_profile(npc)
                    profile["dataset_id"] = dataset_id
                    profile["aggregate_crosscheck"] = {"status": row["aggregate_crosscheck"], "authority": "crosscheck_only;selected_divided_record_retained"}
                    if npc in diff_by_id:
                        profile["blockers"].append("aggregate_semantic_conflict_requires_review")
                        profile["blockers"] = sorted(set(profile["blockers"]))
                    raw = canonical(profile) + b"\n"
                    profiles.write(raw)
                    if npc in pilot_ids:
                        pilot.write(raw)
                    for binding in profile["skill_bindings"]:
                        if binding["status"] != "dual_join_agrees_static_only":
                            unsupported_rows.append({"npc_id": npc, "index": binding["source_entry_index"], "name": binding.get("name", ""), "status": binding["status"], "retail_id_candidates": ";".join(map(str, binding.get("retail_name_id_candidates", []))), "stack_id_candidates": ";".join(map(str, binding.get("supported48_stack_id_candidates", [])))})
        (stage / "_profiles-spool.jsonl").unlink()
        def csv_file(name, rows, fields):
            with (stage / name).open("w", encoding="utf-8", newline="") as handle:
                writer = csv.DictWriter(handle, fields, lineterminator="\n")
                writer.writeheader()
                writer.writerows(rows)
        csv_file("coverage.csv", coverage, list(coverage[0]) if coverage else ["npc_id", "status"])
        csv_file("identity-blockers.csv", [r for r in coverage if r["status"] != "exported_static_source_only"], list(coverage[0]) if coverage else ["npc_id", "status"])
        csv_file("unsupported-skill-bindings.csv", unsupported_rows, ["npc_id", "index", "name", "status", "retail_id_candidates", "stack_id_candidates"])
        write_json(stage / "semantic-diff.json", {"policy": "complete ordered-record hashes; formatting between elements ignored; leaf text retained", "compared_selected_records": len(selected), "aggregate_records": len(aggregate_seen), "differences": sorted(diffs, key=lambda d: d["npc_id"])})
        write_json(stage / "source-manifest.json", manifests)
        provenance = {"schema_version": SCHEMA_VERSION, "dataset_id": dataset_id, "selection": plan, "consumer_registry": registry, "raw_record_contract": "ordered infoset plus original byte span/SHA; original source files remain unmodified", "field_presence_contract": "absent/explicit_zero/explicit_value/ambiguous_duplicate; no default substitution", "source_authority": "pinned consumer-selected seven divided files;aggregate crosscheck only", "activation_ready": False}
        if reviewed_identity_bindings is not None:
            provenance["reviewed_same_id_identity_policy"] = "Explicit exception to the copied registry's default changed-name rejection: only the complete independently reviewed90 retained IDs, exact original4.8 archive and selected5.8 source/raw records; no name-only or suffix matching; source identity is not runtime approval"
        write_json(stage / "provenance.json", provenance)
        summary = {"schema_version": SCHEMA_VERSION, "dataset_id": dataset_id, "target_count": len(targets), "selected_retail_records": len(selected), "aggregate_records": len(aggregate_seen), "exported_profiles": len(exact_ids), "identity_blockers": len(targets) - len(exact_ids), "pilot_profiles": len(exact_ids & set(pilot_ids)), "coverage_status_counts": dict(sorted(collections.Counter(r["status"] for r in coverage).items())), "semantic_differences": len(diffs), "unsupported_skill_binding_rows": len(unsupported_rows), "fixture_verified": False, "activation_ready": False}
        if reviewed_identity_bindings is not None:
            summary["reviewed_same_id_reference_drift_bindings"] = len(reviewed_rows)
        write_json(stage / "summary.json", summary)
        validation = {"every_target_has_coverage_row": {r["npc_id"] for r in coverage} == set(targets), "profile_ids_exactly_supported_and_identity_matched": set(offsets) == exact_ids, "every_source_completed_strict_parse_or_was_hashed_input": True, "duplicates_overwritten": 0, "defaults_substituted": 0, "replacement_decoding_used": False, "fixture_verified": False, "activation_ready": False}
        if not all(validation[k] for k in ["every_target_has_coverage_row", "profile_ids_exactly_supported_and_identity_matched"]):
            raise ExtractionError("Coverage integrity failure")
        write_json(stage / "validation.json", validation)
        # Finish publication only after a second whole-input hash check. Existence
        # of regional candidates is also part of the selected-input contract.
        for row in manifests:
            if sha_file(Path(row["path"])) != row["sha256"]:
                raise ExtractionError("Input changed during generation: " + row["path"])
        for entry in plan:
            if Path(entry["regional_candidate"]).is_file() != entry["regional_exists"]:
                raise ExtractionError("Regional source selection changed during generation")
        output_receipts = [{"file": p.name, "bytes": p.stat().st_size, "sha256": sha_file(p)} for p in sorted(stage.iterdir()) if p.is_file()]
        write_json(stage / "output-manifest.json", output_receipts)
        if output.exists():
            raise ExtractionError("Output appeared while generation was running; refusing overwrite")
        os.rename(stage, output)
        return summary
    except BaseException as error:
        # A failed staging directory remains clearly marked for audit, never at
        # the requested output path. No source or existing output is removed.
        write_json(stage / "FAILURE.json", {"error_type": type(error).__name__, "message": str(error), "published": False})
        raise


def load_production_inputs(args, registry):
    manifests = []
    # Hash inputs before decoding/importing them, then rehash all of them before
    # publication. A post-decode-only receipt could bless a changed input.
    for path in [Path(__file__), HERE / "retail_npc_consumers.json", HERE / "retail_npc_profiles.schema.json", HERE / "aionpak.py", HERE / "bxml.py", HERE / "keys/pak_table_v1.bin", HERE / "keys/pak_table_v2.bin"]:
        manifests.append(receipt(path, role="extractor_schema_registry_or_read_only_decoder"))
    registry_receipt = next(row for row in manifests if Path(row["path"]).name == "retail_npc_consumers.json")
    if canonical(json.loads(Path(registry_receipt["path"]).read_bytes())) != canonical(registry):
        raise ExtractionError("Consumer registry changed before input admission")
    for key in ["main", "npc", "script"]:
        path = getattr(args, key + "_binary").resolve()
        expected = registry["builds"][key]
        if "array_va" in expected:
            evidence = pe_array(path, int(expected["array_va"], 16), expected["sha256"])
            if evidence["filenames"] != registry["divided_files"]:
                raise ExtractionError("Binary divided-file array disagrees with registry")
            manifests.append({"path": str(path), "sha256": evidence["sha256"], "bytes": evidence["bytes"], "role": "pinned_binary_static_read_only", "filename_array_evidence": evidence})
        else:
            row = receipt(path, role="build_identity_only")
            if row["sha256"] != expected["sha256"]:
                raise ExtractionError("Script build hash mismatch")
            manifests.append(row)
    # Read the existing Java/C# integration before this tool is implemented or
    # changed. Production execution below only reads their data, never loads CLR.
    templates = {}
    def template(node, source):
        attrs = dict(node[1])
        npc = int(attrs["npc_id"])
        if npc in templates:
            raise ExtractionError(f"Duplicate supported4.8 NPC template {npc}")
        templates[npc] = {"attrs48": attrs, "template_source": source}
    template_path = args.port_root / "game-server/data/static_data/npcs/npc_templates.xml"
    manifests.append(parse_records(template_path, "npc_templates", "npc_template", template))
    manifests.append(receipt(args.inventory, role="phase0_inventory"))
    with args.inventory.open(encoding="utf-8-sig", newline="") as handle:
        inventory = {}
        for row in csv.DictReader(handle):
            npc = int(row["npc_id"])
            if npc in inventory:
                raise ExtractionError(f"Duplicate inventory NPC ID {npc}")
            inventory[npc] = row
    if set(inventory) != set(templates):
        raise ExtractionError("Current template IDs and Phase0 inventory disagree; rebaseline first")
    # Existing decoder modules perform CRC/size checks and write nothing when
    # imported. Restrict record identity scope to the actual NPC tables.
    import aionpak
    import bxml
    client_names = collections.defaultdict(set)
    member_receipts = []
    admitted = set(registry["client_identity_members"])
    archive_receipt = receipt(args.client48_pak, role="independent48_identity_archive", decoder_mode="strict_utf16_used_strings;duplicate_attributes_rejected;exactly_one_scalar_id_and_name")
    client_seen = set()
    admitted_members = set()
    for name, data in aionpak.read_pak(args.client48_pak):
        basename = PurePosixPath(name.replace("\\", "/")).name.lower()
        member_receipts.append({"member": name, "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest(), "crc_size_verified": True, "identity_table": basename in admitted})
        if not member_receipts[-1]["identity_table"]:
            continue
        if basename in admitted_members:
            raise ExtractionError(f"Duplicate client identity archive member: {basename}")
        admitted_members.add(basename)
        identities = client_identity_records(data, bxml)
        member_receipts[-1]["identity_record_count"] = len(identities)
        for npc, devname in identities:
            if npc in client_seen:
                raise ExtractionError(f"Duplicate independent4.8 client NPC ID: {npc}")
            client_seen.add(npc)
            client_names[npc].add(devname)
    if admitted_members != admitted:
        raise ExtractionError("Required independent4.8 client NPC identity table absent")
    archive_receipt["members"] = member_receipts
    manifests.append(archive_receipt)
    targets = {}
    for npc, row in templates.items():
        names = sorted(client_names.get(npc, set()))
        if names != sorted(set(filter(None, inventory[npc]["client48_devnames"].split(";")))):
            raise ExtractionError(f"Independent client identity/inventory drift: {npc}")
        targets[npc] = {**row, "devnames48": names, "family": inventory[npc]["family"], "rank_rating_policy": "compatibility_metadata_only;not_retail_stat_synthesis"}
    retail_skills = collections.defaultdict(set)
    def skill_base(node, source):
        number, name = record_identity(node)
        retail_skills[name].add(number)
    manifests.append(parse_records(args.xml_root / "skill_base.xml", "skill_bases", "skill_base", skill_base))
    supported_skills, stacks = set(), collections.defaultdict(set)
    def skill48(node, source):
        attrs = dict(node[1])
        number = int(attrs["skill_id"])
        if number in supported_skills:
            raise ExtractionError(f"Duplicate4.8 skill template ID {number}")
        supported_skills.add(number)
        if attrs.get("stack"):
            stacks[attrs["stack"].upper()].add(number)
    manifests.append(parse_records(args.port_root / "game-server/data/static_data/skills/skill_templates.xml", "skill_data", "skill_template", skill48))
    return targets, manifests, prepare_skill_name_index(retail_skills), supported_skills, stacks


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--xml-root", type=Path, required=True)
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--client48-pak", type=Path, required=True)
    parser.add_argument("--main-binary", type=Path, required=True)
    parser.add_argument("--npc-binary", type=Path, required=True)
    parser.add_argument("--script-binary", type=Path, required=True)
    parser.add_argument("--port-root", type=Path, default=REPO)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--reviewed-abyss-bindings", type=Path,
                        help="Opt in to the exact pinned90-row same-ID review; does not accept arbitrary aliases or activate NPCs")
    parser.add_argument("--expect-covered", type=int)
    parser.add_argument("--expect-exact", type=int)
    args = parser.parse_args(argv)
    args.port_root, args.xml_root = args.port_root.resolve(), args.xml_root.resolve()
    output = args.output.resolve()
    if output.exists() or args.port_root == output or args.port_root in output.parents:
        raise ExtractionError("Output must be a new directory outside the source checkout")
    registry = json.loads((HERE / "retail_npc_consumers.json").read_text(encoding="utf-8"))
    targets, manifests, retail_skills, supported, stacks = load_production_inputs(args, registry)
    reviewed = None
    if args.reviewed_abyss_bindings is not None:
        review_path = args.reviewed_abyss_bindings.resolve()
        helper_receipt = receipt(HERE / "retail_npc_identity_bindings.py", role="reviewed_same_id_identity_admission_code")
        review_receipt = receipt(review_path, role="exact_pinned90_same_id_reference_drift_review")
        if review_receipt["bytes"] > 1024 * 1024:
            raise ExtractionError("Reviewed identity evidence exceeds its bounded input")
        from retail_npc_identity_bindings import load_reviewed_abyss_bindings, REVIEW_SHA256, IdentityBindingError
        with review_path.open("rb") as handle:
            review_bytes = handle.read(1024 * 1024 + 1)
        try:
            reviewed = load_reviewed_abyss_bindings(review_bytes, expected_sha256=REVIEW_SHA256)
        except IdentityBindingError as error:
            raise ExtractionError("Reviewed same-ID evidence admission failed: " + str(error)) from error
        if review_receipt["sha256"] != REVIEW_SHA256:
            raise ExtractionError("Reviewed identity evidence changed before admission")
        manifests.extend([helper_receipt, review_receipt])
    if args.expect_covered is not None and len(targets) != args.expect_covered:
        raise ExtractionError("Expected target coverage count differs from current inputs")
    summary = atomic_generate(output=output, xml_root=args.xml_root, registry=registry, targets=targets, filenames=registry["divided_files"], source_receipts=manifests, retail_skills=retail_skills, supported_skills=supported, stack_names=stacks, expected_exact=args.expect_exact, reviewed_identity_bindings=reviewed, log=lambda message: print(message, flush=True))
    print(json.dumps(summary, indent=2), flush=True)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except ExtractionError as error:
        print("Extraction blocked: " + str(error), file=sys.stderr)
        raise SystemExit(2)
