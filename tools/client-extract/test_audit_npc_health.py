"""Regression for an old comparison crossing NPC boundaries on absent HP."""
from pathlib import Path
import tempfile
import unittest
import xml.etree.ElementTree as ET

from audit_npc_health import retail_health


class RetailHealthTests(unittest.TestCase):
    def read(self, xml):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "npcs.xml"
            path.write_text(xml, encoding="utf-16")
            return retail_health(path)

    def test_missing_shield_hp_cannot_borrow_next_statue_hp(self):
        xml = """<npcs><npc><id>282123</id><name>shield</name>
          <unattackable>1</unattackable></npc>
          <npc><id>282124</id><name>statue</name><max_hp>2921</max_hp></npc></npcs>"""
        self.assertEqual(self.read(xml), {"statue": 2921})

    def test_nested_data_and_names_do_not_break_record_or_supply_root_fields(self):
        xml = """<npcs><npc><skills><data><name>skill</name><max_hp>999</max_hp></data></skills>
          <name>mob</name><max_hp>42</max_hp><attacks><data/><data/></attacks></npc>
          <npc><name>missing</name><skills><data><max_hp>100</max_hp></data></skills></npc></npcs>"""
        self.assertEqual(self.read(xml), {"mob": 42})

    def test_explicit_zero_is_kept(self):
        self.assertEqual(self.read("<npcs><npc><name>zero</name><max_hp>0</max_hp></npc></npcs>"), {"zero": 0})

    def test_ambiguous_or_truncated_source_fails_before_apply(self):
        for xml in [
            "<npcs><npc><name>x</name><max_hp>1</max_hp><max_hp>2</max_hp></npc></npcs>",
            "<npcs><npc><name>x</name><max_hp>1</max_hp></npc><npc><name>x</name><max_hp>2</max_hp></npc></npcs>",
            "<npcs><npc><name>x</name><max_hp>-1</max_hp></npc></npcs>",
            "<npcs><npc><name>x</name><max_hp>1</max_hp></npc>",
        ]:
            with self.subTest(xml=xml), self.assertRaises((ValueError, ET.ParseError)):
                self.read(xml)


if __name__ == "__main__":
    unittest.main()
