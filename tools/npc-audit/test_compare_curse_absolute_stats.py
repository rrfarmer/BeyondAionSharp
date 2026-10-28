import tempfile
import unittest
from pathlib import Path
from compare_curse_absolute_stats import compare

class AbsoluteCurseComparisonTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.retail = Path(self.tmp.name) / "retail.xml"
        self.host = Path(self.tmp.name) / "host.xml"

    def test_missing_and_zero_remain_distinct(self):
        self.retail.write_text("<absolute_stat_to_pcs><absolute_stat_to_pc><id>1</id><ar_curse>0</ar_curse></absolute_stat_to_pc><absolute_stat_to_pc><id>2</id></absolute_stat_to_pc></absolute_stat_to_pcs>")
        self.host.write_text('<absolute_stats><stats_set id="1"><modifiers><abs name="CURSE_RESISTANCE" value="0"/><abs name="CURSE_RESISTANCE_PENETRATION" value="0"/></modifiers></stats_set></absolute_stats>')
        result = compare(self.retail, self.host)
        self.assertEqual([{"id": 1, "field": "curse_arp", "retail": None, "host": 0}], result["differences"])
        self.assertEqual([2], result["retail_only_ids"])

    def test_duplicate_host_field_is_rejected(self):
        self.retail.write_text("<absolute_stat_to_pcs><absolute_stat_to_pc><id>1</id></absolute_stat_to_pc></absolute_stat_to_pcs>")
        self.host.write_text('<absolute_stats><stats_set id="1"><modifiers><abs name="CURSE_RESISTANCE" value="1"/><abs name="CURSE_RESISTANCE" value="2"/></modifiers></stats_set></absolute_stats>')
        with self.assertRaises(ValueError):
            compare(self.retail, self.host)

if __name__ == "__main__":
    unittest.main()
