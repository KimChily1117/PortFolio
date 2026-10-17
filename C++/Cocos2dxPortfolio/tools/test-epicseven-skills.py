"""Regression checks for source-relative choreography, without converting assets."""
import importlib.util
from pathlib import Path
import unittest

spec=importlib.util.spec_from_file_location('skill_import',Path(__file__).with_name('import-epicseven-skills.py'))
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
class CompilerTests(unittest.TestCase):
    def compiler(self):
        return m.Compiler({'animations':{'skill3':{'bones':{'root':{'rotate':[{'time':2}]}},'events':[{'time':.25,'name':'jump'},{'time':1.25,'name':'landing'},{'time':.5,'name':'cutin'}]}}},m.Assets(),'actual.webp')
    def test_sequence_parallel_and_same_time_order(self):
        c=self.compiler();span=c.walk({'etty':'SEQ','nodes':[{'etty':'SPAWN','nodes':[{'etty':'DELAY','time':100},{'etty':'DELAY','time':200}]},{'etty':'FIRE'},{'etty':'STOP','targetId':'d'},{'etty':'DELAY','time':100}]},0)
        self.assertAlmostEqual(span,.3);self.assertEqual([e['type'] for e in c.events],['FIRE','STOP']);self.assertEqual([e['time'] for e in c.events],[.2,.2])
    def test_animation_positions_and_marker_scope(self):
        c=self.compiler();c.walk({'etty':'ANI','ani':'skill3','nodes':[{'etty':'ANI_POS','delay':800,'nodes':[{'etty':'FIRE'}]},{'etty':'ANI_SCOPE','from':'jump','to':'landing','nodes':[{'etty':'MOVE'}]}]},3)
        fire=next(e for e in c.events if e['type']=='FIRE');move=next(e for e in c.events if e['type']=='MOVE')
        self.assertEqual(fire['time'],3.8);self.assertEqual((move['time'],move['duration']),(3.25,1));self.assertFalse(c.errors)
    def test_stale_animation_position_is_rejected(self):
        c=self.compiler();c.walk({'etty':'ANI','ani':'skill3','nodes':[{'etty':'ANI_POS','delay':5000,'nodes':[{'etty':'FIRE'}]}]},0)
        self.assertTrue(any('beyond currentclip' in e for e in c.errors))
    def test_onevent_and_explicit_cutin(self):
        c=self.compiler();result=c.compile({'start':{'action':{'etty':'ROOT','nodes':[{'etty':'ANI','ani':'skill3'},{'etty':'CUTIN','delay':600}]},'events':{'nodes':[{'etty':'ONEVENT','ani':'skill3','name':'landing','nodes':[{'etty':'FIRE'}]}]}}})
        self.assertEqual([e['time'] for e in result['events'] if e['type']=='CUTIN'],[.6]);self.assertEqual([e['time'] for e in result['events'] if e['type']=='FIRE'],[1.25])
    def test_unknown_action_and_missing_animation_reported(self):
        c=self.compiler();c.walk({'etty':'NOT_IMPLEMENTED'},0);c.walk({'etty':'ANI','ani':'missing'},0)
        self.assertEqual(c.events[0]['type'],'UNSUPPORTED');self.assertTrue(c.errors)
    def test_nonblocking_delay_does_not_shift_next_event(self):
        c=self.compiler();c.walk({'etty':'SEQ','nodes':[{'etty':'DELAY','time':1000,'without':True},{'etty':'FIRE'}]},0)
        self.assertEqual(c.events[0]['time'],0)
if __name__=='__main__':unittest.main()
