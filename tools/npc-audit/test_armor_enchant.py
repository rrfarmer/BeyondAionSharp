import tempfile
import unittest
from pathlib import Path

import armor_enchant as armor


class ArmorEnchantSelectionTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix='aion-armor-audit-')
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        (self.root / 'item_armors.xml').write_text(
            '<items><item><id>110100420</id><name>robe_one</name></item></items>', encoding='utf-8')
        (self.root / 'item_enchanttable.xml').write_text('''<item_enchanttables><item_enchanttable>
            <name>rb_torso</name><start_limitless>16</start_limitless><enchant_attr_list>
            <data><level>1</level><attr1>physical_defend 3</attr1><attr2>max_hp 24</attr2></data>
            <data><level>15</level><attr1>physical_defend 45</attr1><attr2>max_hp 360</attr2><attr3>max_hp_O 999</attr3></data>
            <data><level>16</level><attr1>physical_defend 48</attr1><attr2>max_hp 384</attr2></data>
            </enchant_attr_list><limitless_attr_list><data><attr_name>physical_defend</attr_name>
            <attr_value>6</attr_value></data><data><attr_name>max_hp</attr_name><attr_value>48</attr_value></data>
            </limitless_attr_list></item_enchanttable></item_enchanttables>''', encoding='utf-8')
        self.host_items = self.root / 'host-items.xml'
        self.host_items.write_text('<items><item_template id="110100420" cName="robe_one" item_group="RB_TORSO" /></items>', encoding='utf-8')
        self.host_tables = self.root / 'host-tables.xml'
        self.host_tables.write_text('''<enchant_templates><enchant_list item_group="RB_TORSO">
            <enchant_data level="1"><enchant_stat stat="PHYSICAL_DEFENSE" value="3" /><enchant_stat stat="MAXHP" value="14" /></enchant_data>
            <enchant_data level="15"><enchant_stat stat="PHYSICAL_DEFENSE" value="45" /><enchant_stat stat="MAXHP" value="210" /></enchant_data>
            <enchant_data level="16"><enchant_stat stat="PHYSICAL_DEFENSE" value="48" /><enchant_stat stat="MAXHP" value="224" /></enchant_data>
            <enchant_data level="20"><enchant_stat stat="PHYSICAL_DEFENSE" value="60" /></enchant_data>
            <enchant_data level="21"><enchant_stat stat="PHYSICAL_DEFENSE" value="3" /></enchant_data>
            </enchant_list></enchant_templates>''', encoding='utf-8')

    def prepare(self, level):
        return armor.prepare(110100420, level, self.root, self.host_items, self.host_tables)

    def test_default_selection_retains_distinct_threshold_operands(self):
        row = self.prepare(16)
        self.assertEqual('rb_torso', row['retail_table'])
        self.assertEqual('RB_TORSO', row['host_table'])
        self.assertEqual([16, 16, 45, 6, 0, 21, 0, 0, 48], row['inputs'])
        self.assertEqual([30, 16, 45, 6, 0, 21, 60, 3, 0], self.prepare(30)['inputs'])

    def test_hp_uses_selected_attribute_and_does_not_merge_bonus_channels(self):
        row = armor.prepare(110100420, 15, self.root, self.host_items, self.host_tables, 'max-hp')
        self.assertEqual('max-hp', row['stat'])
        self.assertEqual([15, 16, 0, 48, 360, 21, 0, 0, 210], row['inputs'])
        row = armor.prepare(110100420, 16, self.root, self.host_items, self.host_tables, 'max-hp')
        self.assertEqual([16, 16, 360, 48, 0, 21, 0, 0, 224], row['inputs'])

    def test_duplicate_hp_attribute_is_rejected(self):
        path = self.root / 'item_enchanttable.xml'
        text = path.read_text(encoding='utf-8').replace('max_hp_O 999', 'MAX_HP 999')
        path.write_text(text, encoding='utf-8')
        with self.assertRaisesRegex(ValueError, 'Duplicate retail enchant attribute'):
            armor.prepare(110100420, 15, self.root, self.host_items, self.host_tables, 'max-hp')

    def test_region_override_is_selected_and_item_identity_is_checked(self):
        usa = self.root / 'USA'
        usa.mkdir()
        (usa / 'item_armors.xml').write_text('<items><item><id>110100420</id><name>different</name></item></items>', encoding='utf-8')
        with self.assertRaisesRegex(ValueError, 'names do not agree'):
            self.prepare(1)

    def test_explicit_retail_and_host_table_names_are_kept_separate(self):
        item = self.root / 'item_armors.xml'
        item.write_text('<items><item><id>110100420</id><name>robe_one</name>'
                        '<enchant_name>rb_override</enchant_name></item></items>', encoding='utf-8')
        table = self.root / 'item_enchanttable.xml'
        table.write_text(table.read_text(encoding='utf-8').replace('<name>rb_torso</name>', '<name>rb_override</name>'), encoding='utf-8')
        self.host_items.write_text('<items><item_template id="110100420" cName="robe_one" '
                                   'item_group="OTHER" enchant_name="RB_TORSO" /></items>', encoding='utf-8')
        row = self.prepare(1)
        self.assertEqual(('rb_override', 'RB_TORSO'), (row['retail_table'], row['host_table']))
        self.assertEqual(('explicit', 'explicit'), (row['retail_selection'], row['host_selection']))

    def test_duplicate_item_is_not_silently_selected(self):
        p = self.root / 'item_armors.xml'
        p.write_text('<items><item><id>110100420</id><name>robe_one</name></item>'
                     '<item><id>110100420</id><name>robe_one</name></item></items>', encoding='utf-8')
        with self.assertRaisesRegex(ValueError, 'Duplicate item ID'):
            self.prepare(1)

    def test_missing_limitless_list_is_visible_even_for_a_low_level(self):
        path = self.root / 'item_enchanttable.xml'
        text = path.read_text(encoding='utf-8')
        text = text.replace('<limitless_attr_list>', '<ignored>')
        text = text.replace('</limitless_attr_list>', '</ignored>')
        path.write_text(text, encoding='utf-8')
        with self.assertRaisesRegex(ValueError, 'no unique limitless attribute list'):
            self.prepare(1)


if __name__ == '__main__':
    unittest.main()
