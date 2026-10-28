import tempfile
import unittest
from pathlib import Path
from compare_curse_absolute_bindings import compare

class AbsoluteBindingComparisonTests(unittest.TestCase):
    def test_candidates_do_not_assume_matching_numeric_id(self):
        with tempfile.TemporaryDirectory() as tmp:
            retail, host, absolute = Path(tmp)/"r.xml", Path(tmp)/"h.xml", Path(tmp)/"a.xml"
            retail.write_text("<skill_bases><skill_base><id>999</id><name>EXAMPLE_G1</name><effect1_type>AbsoluteStatToPCBuff</effect1_type><effect1_reserved1>TEST</effect1_reserved1><effect1_reserved4>47</effect1_reserved4></skill_base><skill_base><id>8</id><name>OTHER</name></skill_base></skill_bases>")
            host.write_text('<skill_data><skill_template skill_id="8" stack="EXAMPLE" lvl="1" name="Example"><effects><absstatbuff statsetid="36" e="1"/></effects></skill_template></skill_data>')
            absolute.write_text("<absolute_stat_to_pcs><absolute_stat_to_pc><id>36</id><name>Test</name></absolute_stat_to_pc></absolute_stat_to_pcs>")
            row = compare(retail, host, absolute)["bindings"][0]
            self.assertEqual(8, row["id"])
            self.assertEqual(999, row["candidates"][0]["id"])
            self.assertEqual("47", row["candidates"][0]["effects"][0]["reserved4"])
            self.assertEqual(36, row["stat_set"])
            self.assertEqual(36, row["candidates"][0]["effects"][0]["resolved_stat_set"])

if __name__ == "__main__":
    unittest.main()
