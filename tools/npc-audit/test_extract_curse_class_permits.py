import unittest
import xml.etree.ElementTree as ET

import extract_curse_class_permits as extractor


class ClassPermitExtractionTests(unittest.TestCase):
    def test_preserves_absent_and_explicit_zero(self):
        node = ET.fromstring('<item><fighter>0</fighter><knight>33</knight></item>')
        values, present = extractor.permits(node)
        self.assertEqual(17, len(values))
        self.assertEqual([0, 0, 33], values[:3])
        self.assertEqual(6, present)

    def test_rejects_duplicate_and_out_of_range_values(self):
        for xml in ('<item><fighter>1</fighter><fighter>2</fighter></item>',
                    '<item><fighter>100</fighter></item>',
                    '<item><fighter>-1</fighter></item>',
                    '<item><fighter><x/></fighter></item>'):
            with self.assertRaises(ValueError):
                extractor.permits(ET.fromstring(xml))

    def test_file_selection_does_not_guess_unknown_family(self):
        self.assertEqual('item_weapons.xml', extractor.selected_file({'kind': 'weapon', 'host_group': 'SWORD'}))
        self.assertEqual('item_armors.xml', extractor.selected_file({'kind': 'other', 'host_group': 'SHIELD'}))
        self.assertEqual('item_accessories.xml', extractor.selected_file({'kind': 'other', 'host_group': 'RING'}))
        with self.assertRaises(ValueError):
            extractor.selected_file({'kind': 'other', 'host_group': 'UNKNOWN'})


if __name__ == '__main__':
    unittest.main()
