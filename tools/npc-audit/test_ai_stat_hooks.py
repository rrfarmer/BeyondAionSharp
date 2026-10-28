import tempfile
import unittest
from pathlib import Path

import ai_stat_hooks


class AiStatHookTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='aion-ai-hook-test-')
        self.addCleanup(self.temp.cleanup)
        self.repo = Path(self.temp.name)
        self.ai = self.repo / 'src/Aion.GameServer/Handlers/AI'
        self.ai.mkdir(parents=True)

    def test_direct_hook_is_a_candidate_and_comments_do_not_invent_stats(self):
        (self.ai / 'Boss.cs').write_text('''
// [AIName("not_real")] class Fake { override void ModifyOwnerStat(S s) {} }
[AIName("Boss")]
public class BossAI : Parent {
    public override void ModifyOwnerStat(Stat2 stat) {
        if (stat.GetStat() == StatEnum.MAXHP) stat.SetBase(123);
        // StatEnum.PHYSICAL_ATTACK is not code.
        var label = "StatEnum.MAGICAL_ATTACK { ignored }";
    }
    private class Nested { public override void ModifyOwnerStat(Stat2 stat) { stat.SetBase(2); } }
}
''', encoding='utf-8')
        hooks = ai_stat_hooks.scan(self.repo)
        self.assertEqual(['boss'], list(hooks))
        result = ai_stat_hooks.describe('BOSS', hooks)
        self.assertEqual('direct_override_found', result['status'])
        self.assertEqual(['MAXHP'], result['handlers'][0]['referenced_stats'])
        self.assertIn('runtime conditions', result['scope'])
        self.assertEqual('no_direct_override_found', ai_stat_hooks.describe('other', hooks)['status'])

    def test_duplicate_names_are_retained_and_nested_methods_do_not_count(self):
        for file in ('A.cs', 'B.cs'):
            (self.ai / file).write_text('''[AIName("same")] public class Test {
                public override void ModifyOwnerStat(Stat2 s) { s.SetBase(1); }
            }''', encoding='utf-8')
        (self.ai / 'Nested.cs').write_text('''[AIName("nested")] public class Outer {
            class Inner { public override void ModifyOwnerStat(Stat2 s) { s.SetBase(1); } }
        }''', encoding='utf-8')
        hooks = ai_stat_hooks.scan(self.repo)
        self.assertEqual(2, len(hooks['same']))
        self.assertNotIn('nested', hooks)
        self.assertEqual([], hooks['same'][0]['referenced_stats'])

    def test_unbalanced_ai_class_is_visible(self):
        (self.ai / 'Broken.cs').write_text('[AIName("bad")] public class Bad {', encoding='utf-8')
        with self.assertRaisesRegex(ValueError, 'Unbalanced'):
            ai_stat_hooks.scan(self.repo)


if __name__ == '__main__':
    unittest.main()
