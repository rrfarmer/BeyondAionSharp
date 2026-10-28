"""Exact reviewed same-ID Abyss name drift, not a general NPC alias mechanism.

The original4.8 client and supplied5.8 source were independently decoded/reviewed.
Only that complete pinned90-row review is admitted. Paths are labels, never opened;
this module performs no extraction, output publication, stat change or activation.
Java NpcData/NpcTemplate and the existing C# reader were read before implementation.
"""
from __future__ import annotations

from dataclasses import dataclass
import hashlib
import json
from pathlib import PureWindowsPath
from types import MappingProxyType
from typing import Mapping

REVIEW_SHA256 = "f90650eda2f22bf9e04d81a02a716fc992cbad3a186edfcefc793b54aecd3ecf"
CLIENT48_SHA256 = "6934fae2ba6edefe14659de13add33101e6f0231634fd553d359e1f6fabfce43"
RETAIL_SOURCE_SHA256 = "e51dc42fd01b8f0cb2b2f1cd03fa15ec473202efb8079e7006f60119a68faee1"
FIELDS = ("desc", "race_type", "tribe", "npc_type", "hpgauge_level", "ai_name")
IDS = frozenset(range(260000, 260015)) | frozenset(range(260901, 260916)) | frozenset(range(261501, 261516)) | frozenset(range(261801, 261816)) | frozenset(range(262101, 262116)) | frozenset(range(262401, 262416))
_ADMISSION = object()


class IdentityBindingError(ValueError):
    """The explicitly reviewed source/content identity cannot be established."""


def _need(condition, message):
    if not condition:
        raise IdentityBindingError(message)


def _unique(pairs):
    out = {}
    for key, value in pairs:
        _need(key not in out, "Duplicate review JSON property")
        out[key] = value
    return out


@dataclass(frozen=True, slots=True)
class ReviewedNpcIdentity:
    npc_id: int
    client48_devname: str
    retail58_devname: str
    raw_record_sha256: str
    byte_start: int
    byte_end_exclusive: int
    identity_fields: tuple[tuple[str, str], ...]
    previous_level48: str
    retail_level58: str


@dataclass(frozen=True, slots=True)
class IdentityBinding:
    npc_id: int
    client48_devname: str
    retail58_devname: str
    original_source_sha256: str
    original_record_sha256: str
    review_sha256: str
    previous_level48: str
    retail_level58: str

    def metadata(self):
        """A fresh provenance object; it does not attest an effective actor or skill."""
        return {
            "schema": "reviewed_same_id_abyss_reference_drift_v1",
            "npc_id": self.npc_id,
            "client48_devname": self.client48_devname,
            "retail58_devname": self.retail58_devname,
            "source_sha256": self.original_source_sha256,
            "raw_record_sha256": self.original_record_sha256,
            "identity_review_sha256": self.review_sha256,
            "client48_archive_sha256": CLIENT48_SHA256,
            "previous_level48": self.previous_level48,
            "retail_level58": self.retail_level58,
            "authority": "maintainer_supplied58_mob_profiles_2026-10-21",
            "effective_profile_verified": False,
            "skill_compatibility_verified": False,
            "activation_ready": False,
        }


class ReviewedAbyssBindings:
    # The admitted wrapper deliberately has no dataclass replace constructor.
    # Immutable entry records are exposed only through this reviewed mapping.
    __slots__ = ("_entries",)

    def __init__(self, entries: Mapping[int, ReviewedNpcIdentity], _admission: object):
        _need(_admission is _ADMISSION and isinstance(entries, MappingProxyType),
              "Use the complete pinned-review admission factory")
        object.__setattr__(self, "_entries", entries)

    @property
    def entries(self):
        return self._entries

    def __setattr__(self, name, value):
        raise AttributeError("Admitted identity bindings are immutable")

    def admit(self, *, npc_id: int, client48_devnames, current_level48: str,
              source_file_sha256: str, source: Mapping, raw_fields: Mapping) -> IdentityBinding:
        """Bind a parsed selected record, retaining both devnames and exact source.

        raw_fields maps each consumed scalar to its ordered occurrence tuple.
        Source bytes/hash/span must come from the extractor's actual strict parse;
        this pure identity unit does not discover or bless a caller's source bytes.
        """
        _need(type(npc_id) is int and npc_id in self.entries, "Unreviewed NPC ID")
        r = self.entries[npc_id]
        _need(isinstance(client48_devnames, (tuple, list)) and list(client48_devnames) == [r.client48_devname], "Original independent4.8 devname differs")
        _need(current_level48 == r.previous_level48, "Current4.8 template level drift requires a new review")
        _need(source_file_sha256 == RETAIL_SOURCE_SHA256, "Selected source file hash differs")
        _need(isinstance(source, Mapping) and PureWindowsPath(str(source.get("path", ""))).name == "npcs_abyss_monsters.xml", "Selected divided source label differs")
        _need(source.get("raw_sha256") == r.raw_record_sha256 and type(source.get("byte_start")) is int
              and type(source.get("byte_end_exclusive")) is int and source["byte_start"] == r.byte_start
              and source["byte_end_exclusive"] == r.byte_end_exclusive, "Exact original record hash/span differs")
        _need(isinstance(raw_fields, Mapping), "Ordered parsed identity fields required")
        expected = (("id", str(npc_id)), ("name", r.retail58_devname), ("level", r.retail_level58)) + r.identity_fields
        for tag, value in expected:
            occurrences = raw_fields.get(tag)
            _need(isinstance(occurrences, (tuple, list)) and len(occurrences) == 1
                  and isinstance(occurrences[0], str) and occurrences[0].strip() == value,
                  "Missing, duplicated or changed selected identity field: " + tag)
        return IdentityBinding(npc_id, r.client48_devname, r.retail58_devname,
                               RETAIL_SOURCE_SHA256, r.raw_record_sha256, REVIEW_SHA256,
                               r.previous_level48, r.retail_level58)


def load_reviewed_abyss_bindings(review_bytes: bytes, *, expected_sha256: str) -> ReviewedAbyssBindings:
    """The pinned accepted review is an independent trust anchor, not self-hashing input."""
    _need(type(review_bytes) is bytes and 0 < len(review_bytes) <= 1024 * 1024, "Bounded original review bytes required")
    _need(expected_sha256 == REVIEW_SHA256 and hashlib.sha256(review_bytes).hexdigest() == REVIEW_SHA256,
          "Unreviewed identity evidence; arbitrary alias lists are unsupported")
    try:
        root = json.loads(review_bytes.decode("utf-8", "strict"), object_pairs_hook=_unique,
                          parse_constant=lambda value: (_ for _ in ()).throw(IdentityBindingError("Nonfinite JSON")))
        _need(root["classification"] == "STATIC_IDENTITY_DRIFT_REVIEW_NOT_PROFILE_OR_ACTIVATION"
              and root["missing27_unchanged"] is True and root["automatic_alias_imported"] is False
              and root["original_phase0_or_dataset_changed"] is False, "Review scope differs")
        _need(root["summary"]["reviewed"] == 90 and root["summary"]["identity_pass"] == 90
              and root["summary"]["identity_mismatch"] == 0 and root["mismatches"] == [], "Incomplete identity review")
        sources = root["source_receipts"]
        _need(any(x["sha256"] == CLIENT48_SHA256 and x["bytes"] == 3005028 for x in sources)
              and any(x["sha256"] == RETAIL_SOURCE_SHA256 and x["bytes"] == 246594362 for x in sources), "Independent source pins differ")
        result = {}
        for row in root["rows"]:
            i = row["npc_id"]
            _need(type(i) is int and i in IDS and i not in result and row["static_identity_review_pass"] is True
                  and row["mismatches"] == [] and row["activation_ready"] is False
                  and row["effective_profile_verified"] is False and row["skills_compatible"] is False, "Unexpected/duplicate/unverified review row")
            old, new = row["client48_name"], row["retail58_same_id_name"]
            _need(new == old + "_No", "Reviewed exact retained-ID rename differs")
            fields = []
            for key in FIELDS:
                c = row["identity_field_comparisons"][key]
                _need(c["both_present_equal"] is True and isinstance(c["client48"], str)
                      and c["client48"] and c["client48"] == c["retail58"], "Reviewed identity field differs")
                fields.append((key, c["retail58"]))
            levels = row["levels"]
            _need(levels["csharp48"] == levels["java48"] and levels["retail58"] == "65", "Reviewed profile level facts differ")
            alternatives = row["same_old_name_other_retail_records"]
            _need(len(alternatives) == 1 and alternatives[0]["id"] != i
                  and alternatives[0]["name"] == old and alternatives[0]["level"] == "75", "Different-ID name fork evidence is incomplete")
            raw = row["retail_raw_record"]
            start, end = raw["raw_record_start"], raw["raw_record_end"]
            _need(type(start) is int and type(end) is int and 0 <= start < end <= 246594362
                  and start % 2 == end % 2 == 0, "Original UTF16 record span differs")
            result[i] = ReviewedNpcIdentity(i, old, new, raw["raw_record_sha256"], start, end,
                                           tuple(fields), levels["csharp48"], levels["retail58"])
        _need(set(result) == IDS, "All90 reviewed IDs are required")
        return ReviewedAbyssBindings(MappingProxyType(result), _ADMISSION)
    except (KeyError, TypeError, UnicodeError, json.JSONDecodeError) as error:
        raise IdentityBindingError("Invalid original identity review") from error
