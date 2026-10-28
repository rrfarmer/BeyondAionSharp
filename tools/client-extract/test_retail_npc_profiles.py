"""Meaningful source/ordering/admission/publication tests; standard library only."""
import hashlib
import json
import os
from pathlib import Path
import tempfile
import unittest

import extract_retail_npc_profiles as E
import bxml


def node(tag, text=None, kids=None, attrs=None):
    return [tag, attrs or [], ([text] if text is not None else []) + (kids or [])]


def npc(number, name, hp="0", extra=""):
    return f'<npc><id>{number}</id><name>{name}</name><max_hp>{hp}</max_hp>{extra}</npc>'


REGISTRY = {"root_integer_lexemes": ["max_hp", "parry"], "root_string_fields": ["name"], "attack_fields": {"attack_rate": {"projection": "integer_lexeme", "static_default": "1000"}, "attack_range": {"projection": "float32", "static_default": "1.0"}}}


def varint(number):
    encoded = bytearray()
    while number >= 128:
        encoded.append((number & 127) | 128)
        number >>= 7
    encoded.append(number)
    return bytes(encoded)


def binary_xml(table, nodes):
    return b"\x80" + varint(len(table)) + table + nodes


class ClientAdmissionTests(unittest.TestCase):
    def test_identity_requires_one_scalar_id_and_name(self):
        for record in [
            '<npc><id>1</id><id>1</id><name>One</name></npc>',
            '<npc><id>1</id><name>One</name><name>Two</name></npc>',
            '<npc><id>1</id></npc>',
            '<npc><id><nested>1</nested></id><name>One</name></npc>',
            '<npc><id>1</id><name>O\ufffdne</name></npc>',
        ]:
            with self.subTest(record=record), self.assertRaises(E.ExtractionError):
                E.client_identity_records(('<npcs>' + record + '</npcs>').encode(), bxml)

    def test_valid_binary_client_identity(self):
        strings = ["", "npcs", "npc", "id", "name", "1", "水😀"]
        offsets, table = {}, b""
        for string in strings:
            offsets[string] = len(table) // 2
            table += string.encode("utf-16-le") + b"\0\0"
        def n(name, value=None, kids=()):
            return varint(offsets[name]) + bytes([(1 if value is not None else 0) | (4 if kids else 0)]) + (varint(offsets[value]) if value is not None else b"") + (varint(len(kids)) + b"".join(kids) if kids else b"")
        data = binary_xml(table, n("npcs", kids=[n("npc", kids=[n("id", "1"), n("name", "水😀")])]))
        self.assertEqual(E.client_identity_records(data, bxml), [(1, "水😀")])

    def test_strict_binary_rejects_malformed_used_strings_and_structure(self):
        table = "\0npc\0x\0a\0b\0".encode("utf-16-le")
        malformed = {
            "invalid_surrogate": binary_xml(b"\0\0\0\xd8\0\0", b"\x01\0"),
            "out_of_range_string": binary_xml(b"\0\0", b"\x20\0"),
            "unterminated_used_string": binary_xml("npc".encode("utf-16-le"), b"\0\0"),
            "odd_table": binary_xml(b"\0", b"\0\0"),
            "table_past_eof": b"\x80\x7f\0\0",
            "duplicate_attributes": binary_xml(table, b"\x01\x02\x02\x05\x07\x05\x09"),
            "trailing_bytes": binary_xml(table, b"\x01\0\xff"),
            "truncated_node": binary_xml(table, b"\x01"),
            "unknown_node_flags": binary_xml(table, b"\x01\x80"),
        }
        for name, data in malformed.items():
            with self.subTest(name=name), self.assertRaises(bxml.BinaryXmlError):
                bxml.decode(data, strict=True)


class SourceFixture(unittest.TestCase):
    def setUp(self):
        parent = os.environ.get("AION_NPC_TEST_TMPDIR")
        if parent:
            Path(parent).mkdir(parents=True, exist_ok=True)
        self.tmp = tempfile.TemporaryDirectory(dir=parent)
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.xml_root = self.root / "xml"
        self.xml_root.mkdir()

    def xml(self, filename, body, encoding="utf-8"):
        path = self.xml_root / filename
        path.parent.mkdir(parents=True, exist_ok=True)
        declaration = 'UTF-16' if encoding.startswith("utf-16") else 'UTF-8'
        path.write_bytes((f'<?xml version="1.0" encoding="{declaration}"?><npcs>' + body + '</npcs>').encode(encoding))
        return path

    def parse(self, path, chunk_size=1024):
        found = []
        meta = E.parse_records(path, "npcs", "npc", lambda n, s: found.append((n, s)), chunk_size)
        return found, meta

    def generate(self, body=None, targets=None, output="out", **kw):
        if body is not None:
            self.xml("part.xml", body)
            self.xml("npcs.xml", body)
        return E.atomic_generate(output=self.root / output, xml_root=self.xml_root, registry=REGISTRY, targets=targets or {1: {"devnames48": ["One"], "family": "fixture"}}, filenames=["part.xml"], source_receipts=kw.pop("source_receipts", []), pilot_ids={1}, **kw)


class XmlAdmissionTests(SourceFixture):
    def test_utf16_raw_spans_and_unicode_without_replacement(self):
        path = self.xml("part.xml", npc(1, "水😀"), "utf-16")
        records, meta = self.parse(path)
        source = records[0][1]
        raw = path.read_bytes()[source["byte_start"]:source["byte_end_exclusive"]]
        self.assertTrue(raw.decode("utf-16-le").startswith("<npc>"))
        self.assertTrue(raw.decode("utf-16-le").endswith("</npc>"))
        self.assertEqual(hashlib.sha256(raw).hexdigest(), source["raw_sha256"])
        self.assertEqual(E.record_identity(records[0][0]), (1, "水😀"))
        self.assertTrue(meta["strict_xml_complete"])

    def test_chunking_does_not_change_text_or_order(self):
        path = self.xml("part.xml", npc(1, "A<!-- comment -->B", extra='<unknown x="a" y="b"> x<![CDATA[<text>]]>y</unknown>'))
        small, _ = self.parse(path, 1)
        large, _ = self.parse(path, 8192)
        self.assertEqual(small, large)
        unknown = E.children(small[0][0], "unknown")[0]
        self.assertEqual(unknown[1], [("x", "a"), ("y", "b")])
        self.assertEqual(E.node_text(unknown), " x<text>y")

    def test_duplicate_attributes_fail_instead_of_overwrite(self):
        path = self.xml("part.xml", '<npc x="1" x="2"><id>1</id><name>One</name></npc>')
        with self.assertRaisesRegex(E.ExtractionError, "Strict XML admission"):
            self.parse(path)

    def test_duplicate_identity_fields_fail(self):
        record = node("npc", kids=[node("id", "1"), node("id", "2"), node("name", "One")])
        with self.assertRaisesRegex(E.ExtractionError, "Duplicate identity"):
            E.record_identity(record)

    def test_invalid_utf8_is_not_replacement_decoded(self):
        path = self.root / "bad.xml"
        path.write_bytes(b'<npcs><npc><id>1</id><name>\xff</name></npc></npcs>')
        with self.assertRaises(E.ExtractionError):
            self.parse(path)

    def test_external_entity_is_not_fetched(self):
        path = self.root / "bad.xml"
        path.write_text('<!DOCTYPE npcs [<!ENTITY x SYSTEM "file:///definitely-not-a-source">]><npcs><npc><id>1</id><name>&x;</name></npc></npcs>', encoding="utf-8")
        with self.assertRaises(E.ExtractionError):
            self.parse(path)

    def test_self_closing_attribute_records_have_exact_span(self):
        path = self.root / "skills.xml"
        raw = b'<skill_data><skill_template skill_id="1"/></skill_data>'
        path.write_bytes(raw)
        found = []
        E.parse_records(path, "skill_data", "skill_template", lambda n, s: found.append((n, s)))
        s = found[0][1]
        self.assertEqual(raw[s["byte_start"]:s["byte_end_exclusive"]], b'<skill_template skill_id="1"/>')


class PresenceAndOrderingTests(unittest.TestCase):
    def test_absent_explicit_zero_and_duplicate_are_distinct(self):
        absent = E.project_field(node("npc"), "parry", "integer_lexeme", default="17")
        zero = E.project_field(node("npc", kids=[node("parry", "0")]), "parry", "integer_lexeme")
        duplicate = E.project_field(node("npc", kids=[node("parry", "0"), node("parry", "20")]), "parry", "integer_lexeme")
        self.assertEqual(absent["presence"], "absent")
        self.assertNotIn("parsed_unbounded_integer", absent)
        self.assertFalse(absent["default_applied"])
        self.assertEqual(zero["presence"], "explicit_zero")
        self.assertEqual(zero["parsed_unbounded_integer"], 0)
        self.assertEqual(duplicate["presence"], "ambiguous_duplicate")
        self.assertEqual(duplicate["raw_values"], ["0", "20"])

    def test_known_float32_bits_and_unknown_decimal_width(self):
        parent = node("data", kids=[node("range", "0.1")])
        self.assertEqual(E.project_field(parent, "range", "float32")["float32_bits_hex"], "3dcccccd")
        self.assertNotIn("float32_bits_hex", E.project_field(parent, "range", "decimal_lexeme"))
        invalid = E.project_field(node("data", kids=[node("range", "NaN")]), "range", "float32")
        self.assertIn("parse_error", invalid)

    def test_semantic_hash_preserves_skill_order_but_ignores_indentation(self):
        first = node("skills", kids=[node("data", "A"), node("data", "B")])
        reversed_ = node("skills", kids=[node("data", "B"), node("data", "A")])
        spaced = ["skills", [], ["\n ", node("data", "A"), "\n ", node("data", "B"), "\n"]]
        self.assertNotEqual(E.semantic_sha(first), E.semantic_sha(reversed_))
        self.assertEqual(E.semantic_sha(first), E.semantic_sha(spaced))

    def test_skill_binding_ambiguity_never_selects_first(self):
        result = E.resolve_skill("Spell", {"Spell": {10}}, {10, 11}, {"SPELL": {10, 11}})
        self.assertIsNone(result["supported48_id"])
        self.assertEqual(result["status"], "ambiguous_name_binding_blocked")
        disagree = E.resolve_skill("Spell", {"Spell": {10}}, {10, 11}, {"SPELL": {11}})
        self.assertEqual(disagree["status"], "retail_id_stack_id_disagreement_blocked")

    def test_unsupported_skill_not_added_as_content(self):
        result = E.resolve_skill("New58Spell", {"New58Spell": {90000}}, {10}, {})
        self.assertIsNone(result["supported48_id"])
        self.assertEqual(result["status"], "unsupported_48_skill_id_blocked")

    def test_native_ascii_skill_lookup_preserves_raw_spelling_and_independent_id(self):
        raw = {"BN_Charmed_IDNovice": {19226}}
        index = E.prepare_skill_name_index(raw)
        self.assertIs(E.prepare_skill_name_index(index), index)
        result = E.resolve_skill("BN_Charmed_idnovice", index, {19226}, {"BN_CHARMED_IDNOVICE": {19226}})
        self.assertEqual(result["name"], "BN_Charmed_idnovice")
        self.assertEqual(result["supported48_id"], 19226)
        self.assertEqual(result["status"], "dual_join_agrees_static_only")
        self.assertEqual(raw, {"BN_Charmed_IDNovice": {19226}})
        self.assertFalse(result["effect_semantics_verified"])

    def test_native_case_collision_is_visible_even_when_an_exact_key_exists(self):
        result = E.resolve_skill("Spell", {"Spell": {10}, "SPELL": {11}}, {10, 11}, {"SPELL": {10}})
        self.assertEqual(result["retail_name_id_candidates"], [10, 11])
        self.assertEqual(result["status"], "ambiguous_name_binding_blocked")
        self.assertIsNone(result["supported48_id"])

    def test_native_name_lookup_does_not_trim_or_invent_unicode_aliases(self):
        for query in [" Spell", "Spell ", "STRASSE"]:
            result = E.resolve_skill(query, {"Spell": {10}, "Straße": {11}}, {10, 11}, {query.upper(): {10}})
            self.assertEqual(result["status"], "retail_skill_name_absent_blocked")

    def test_projected_lists_keep_source_order_and_raw_unknown_fields(self):
        attacks = node("attacks", kids=[node("data", kids=[node("attack_range", "9"), node("future_field", "opaque")]), node("data", kids=[node("attack_range", "2")])])
        skills = node("skills", kids=[node("data", kids=[node("skill_name", "B")]), node("data", kids=[node("skill_name", "A")])])
        record = node("npc", kids=[node("id", "1"), node("name", "One"), attacks, skills])
        profile = E.profile_record(record, {}, {}, REGISTRY, {"A": {10}, "B": {11}}, {10, 11}, {"A": {10}, "B": {11}})
        self.assertEqual([row["name"] for row in profile["skill_bindings"]], ["B", "A"])
        self.assertEqual([row["source_entry_index"] for row in profile["ordered_lists"]["attacks"]["entries"]], [0, 1])
        self.assertEqual(E.one_text(E.children(E.children(profile["raw_record"], "attacks")[0])[0], "future_field"), "opaque")


class PublicationTests(SourceFixture):
    def test_reviewed_name_drift_rejects_arbitrary_alias_objects(self):
        with self.assertRaisesRegex(E.ExtractionError, "no alias callback"):
            self.generate(npc(1, "One"), reviewed_identity_bindings=object())
        self.assertFalse((self.root / "out").exists())

    def test_reviewed_name_drift_rejects_a_subclass_without_factory_admission(self):
        import retail_npc_identity_bindings as B
        class UnadmittedSubclass(B.ReviewedAbyssBindings):
            def __init__(self):
                pass
        with self.assertRaisesRegex(E.ExtractionError, "no alias callback"):
            self.generate(npc(1, "One"), reviewed_identity_bindings=UnadmittedSubclass())
        self.assertFalse((self.root / "out").exists())

    def test_reviewed_name_drift_requires_all_original_supported_identities(self):
        import retail_npc_identity_bindings as B
        from test_retail_npc_identity_bindings import read_private_review
        admitted = B.load_reviewed_abyss_bindings(read_private_review(), expected_sha256=B.REVIEW_SHA256)
        with self.assertRaisesRegex(E.ExtractionError, "cohort identity/level differs"):
            self.generate(npc(1, "One"), reviewed_identity_bindings=admitted)
        self.assertFalse((self.root / "out").exists())

    def test_reviewed_name_drift_cannot_bless_similar_fixture_source(self):
        import retail_npc_identity_bindings as B
        from test_retail_npc_identity_bindings import read_private_review
        admitted = B.load_reviewed_abyss_bindings(read_private_review(), expected_sha256=B.REVIEW_SHA256)
        targets = {i: {"devnames48": [r.client48_devname], "attrs48": {"level": r.previous_level48}} for i, r in admitted.entries.items()}
        r = admitted.entries[260000]
        extra = "<level>65</level>" + "".join("<" + k + ">" + v + "</" + k + ">" for k, v in r.identity_fields)
        with self.assertRaisesRegex(E.ExtractionError, "Selected source file hash differs"):
            self.generate(npc(260000, r.retail58_devname, extra=extra), targets=targets, reviewed_identity_bindings=admitted,
                          source_receipts=[{"role": "independent48_identity_archive", "sha256": B.CLIENT48_SHA256, "bytes": 3005028}])
        self.assertFalse((self.root / "out").exists())

    def test_reviewed_name_drift_rejects_new_missing_or_ambiguous_client_archive_context(self):
        import retail_npc_identity_bindings as B
        from test_retail_npc_identity_bindings import read_private_review
        admitted = B.load_reviewed_abyss_bindings(read_private_review(), expected_sha256=B.REVIEW_SHA256)
        targets = {i: {"devnames48": [r.client48_devname], "attrs48": {"level": r.previous_level48}} for i, r in admitted.entries.items()}
        original = {"role": "independent48_identity_archive", "sha256": B.CLIENT48_SHA256, "bytes": 3005028}
        contexts = [[], [original, original], [{**original, "sha256": "0" * 64}],
                    [{**original, "bytes": 3005029}], [{**original, "bytes": "3005028"}],
                    [{**original, "role": "similar_client"}]]
        for index, receipts in enumerate(contexts):
            with self.subTest(index=index), self.assertRaisesRegex(E.ExtractionError, "client archive identity changed or is missing"):
                self.generate(npc(1, "One"), targets=targets, reviewed_identity_bindings=admitted,
                              source_receipts=receipts, output="context-" + str(index))
            self.assertFalse((self.root / ("context-" + str(index))).exists())

    def test_output_inside_sources_is_refused(self):
        self.xml("part.xml", npc(1, "One"))
        self.xml("npcs.xml", npc(1, "One"))
        with self.assertRaisesRegex(E.ExtractionError, "outside source XML"):
            E.atomic_generate(output=self.xml_root / "out", xml_root=self.xml_root, registry=REGISTRY, targets={}, filenames=["part.xml"], source_receipts=[])

    def test_duplicate_selected_id_leaves_no_published_output(self):
        with self.assertRaisesRegex(E.ExtractionError, "Duplicate selected NPC"):
            self.generate(npc(1, "One") + npc(1, "Other"))
        self.assertFalse((self.root / "out").exists())

    def test_present_malformed_region_is_not_silently_replaced_by_base(self):
        self.xml("part.xml", npc(1, "One"))
        self.xml("npcs.xml", npc(1, "One"))
        regional = self.xml_root / "USA/part.xml"
        regional.parent.mkdir()
        regional.write_text('<npcs><npc><id>1</id>', encoding="utf-8")
        with self.assertRaisesRegex(E.ExtractionError, "partial-load ambiguity"):
            self.generate()
        self.assertFalse((self.root / "out").exists())

    def test_missing_region_uses_base_and_all_identity_issues_have_rows(self):
        summary = self.generate(npc(1, "One") + npc(2, "Renamed"), targets={1: {"devnames48": ["One"]}, 2: {"devnames48": ["Old"]}, 3: {"devnames48": ["Absent"]}})
        self.assertEqual(summary["target_count"], 3)
        self.assertEqual(summary["exported_profiles"], 1)
        self.assertEqual(summary["identity_blockers"], 2)
        profile = json.loads((self.root / "out/profiles.jsonl").read_text())
        self.assertFalse(profile["verification"]["activation_ready"])
        provenance = json.loads((self.root / "out/provenance.json").read_text())
        self.assertEqual(provenance["selection"][0]["reason"], "regional_missing_base_fallback")

    def test_aggregate_is_crosscheck_not_value_replacement(self):
        self.xml("part.xml", npc(1, "One", "10"))
        self.xml("npcs.xml", npc(1, "One", "99"))
        self.generate()
        profile = json.loads((self.root / "out/profiles.jsonl").read_text())
        self.assertEqual(profile["scalar_fields"]["max_hp"]["raw_values"], ["10"])
        self.assertEqual(profile["aggregate_crosscheck"]["status"], "semantic_record_mismatch")
        self.assertIn("aggregate_semantic_conflict_requires_review", profile["blockers"])

    def test_late_source_corruption_does_not_publish_earlier_records(self):
        self.xml("part.xml", npc(1, "One"))
        (self.xml_root / "npcs.xml").write_text('<npcs>' + npc(1, "One") + '<npc>', encoding="utf-8")
        with self.assertRaises(E.ExtractionError):
            self.generate()
        self.assertFalse((self.root / "out").exists())

    def test_existing_output_not_touched(self):
        old = self.root / "out"
        old.mkdir()
        (old / "keep").write_bytes(b"unchanged")
        with self.assertRaisesRegex(E.ExtractionError, "overwrite"):
            self.generate(npc(1, "One"))
        self.assertEqual((old / "keep").read_bytes(), b"unchanged")

    def test_input_changed_after_parse_blocks_publication(self):
        source = self.xml("part.xml", npc(1, "One"))
        self.xml("npcs.xml", npc(1, "One"))
        def log(message):
            if message.startswith("parse aggregate"):
                source.write_bytes(source.read_bytes() + b" ")
        with self.assertRaisesRegex(E.ExtractionError, "Input changed"):
            self.generate(log=log)
        self.assertFalse((self.root / "out").exists())

    def test_two_generations_are_byte_identical(self):
        self.generate(npc(1, "One"), output="a")
        self.generate(output="b")
        a, b = self.root / "a", self.root / "b"
        self.assertEqual(sorted(p.name for p in a.iterdir()), sorted(p.name for p in b.iterdir()))
        for path in a.iterdir():
            self.assertEqual(path.read_bytes(), (b / path.name).read_bytes(), path.name)


if __name__ == "__main__":
    unittest.main()
