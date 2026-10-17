"""Regression checks for data semantics across the Spine runtime versions."""
import importlib.util
import pathlib
import unittest

spec = importlib.util.spec_from_file_location('adapter', pathlib.Path(__file__).parents[1]/'tools/import-epicseven-spine.py')
adapter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(adapter)

class ScaleDefaults(unittest.TestCase):
    def test_omitted_identity_scale_does_not_collapse_geometry(self):
        source = {'skeleton':{'spine':'3.8.99'}, 'bones':[{'name':'root'}], 'slots':[], 'skins':[],
                  'animations':{'idle':{'bones':{'root':{'scale':[{'y':-1}, {}, {'x':0}]}}}}}
        frames = adapter.adapt(source)['animations']['idle']['bones']['root']['scale']
        self.assertEqual([(f['x'],f['y']) for f in frames], [(1,-1),(1,1),(0,1)])
        self.assertNotIn('x', source['animations']['idle']['bones']['root']['scale'][0])

if __name__ == '__main__':
    unittest.main()
